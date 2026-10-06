using System.Diagnostics;
using Microsoft.Extensions.Options;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Services;

public enum RodzajWynikuTrasy
{
    /// <summary>Trasa znaleziona w żądanym trybie.</summary>
    Znaleziona,
    /// <summary>Sala początkowa i docelowa to ta sama sala.</summary>
    NaMiejscu,
    /// <summary>Brak trasy bez schodów, ale istnieje trasa ze schodami — podana jako alternatywa.</summary>
    TylkoZeSchodami,
    /// <summary>Brak jakiejkolwiek trasy — przyczyna i alternatywny sposób dostępu.</summary>
    BrakTrasy,
    /// <summary>Braki w danych (np. sala bez punktu wejściowego) — czytelny komunikat zamiast wyjątku.</summary>
    BladDanych,
}

public sealed record OpisSali(int Id, string Symbol, string Nazwa);

public sealed record Trasa(
    IReadOnlyList<Instrukcja> Instrukcje,
    decimal DlugoscMetry,
    int CzasSekundy,
    bool CzyZawieraSchody)
{
    /// <summary>Liczba wierszy osi trasy: punkt startowy i po jednym na każdy odcinek.</summary>
    public int LiczbaKrokow => Instrukcje.Count;
}

/// <summary>
/// Wynik wyznaczania trasy. <see cref="KontaktAlternatywny"/> jest ustawiony zawsze, gdy użytkownik
/// nie dostał trasy w żądanym trybie — art. 7 ustawy o dostępności cyfrowej.
/// </summary>
public sealed record WynikTrasy(
    RodzajWynikuTrasy Rodzaj,
    OpisSali? SalaZ,
    OpisSali? SalaDo,
    bool TrybWindy,
    Trasa? Trasa,
    string? Komunikat,
    KontaktAlternatywnyOptions? KontaktAlternatywny);

/// <summary>
/// Wyznaczanie najkrótszej trasy z sali do sali (UC-07, UC-08, WF-14 … WF-21): Dijkstra na grafie
/// z <see cref="IGrafBudynkuCache"/>, waga = odległość w metrach (trasa najkrótsza, nie „najprostsza”).
/// </summary>
public sealed class WyszukiwarkaTrasService(
    ITrasaRepozytorium repozytorium,
    IGrafBudynkuCache grafy,
    GeneratorOpisuService generator,
    IOptions<KontaktAlternatywnyOptions> kontakt,
    ILogger<WyszukiwarkaTrasService> log)
{
    /// <summary>Tempo marszu do szacowania czasu — ostrożnie, dla osoby z białą laską (ok. 0,8 m/s).</summary>
    public const decimal TempoMetrowNaSekunde = 0.8m;

    public async Task<WynikTrasy> Wyznacz(int salaZId, int salaDoId, bool trybWindy, DateTime data, CancellationToken ct = default)
    {
        var stoper = Stopwatch.StartNew();
        var wynik = await WyznaczBezZapisu(salaZId, salaDoId, trybWindy, data, ct);
        await ZapiszZapytanie(salaZId, salaDoId, trybWindy, wynik, (int)stoper.ElapsedMilliseconds, ct);
        return wynik;
    }

    private async Task<WynikTrasy> WyznaczBezZapisu(int salaZId, int salaDoId, bool trybWindy, DateTime data, CancellationToken ct)
    {
        var sale = await repozytorium.PobierzSaleAsync([salaZId, salaDoId], ct);
        var z = sale.FirstOrDefault(s => s.Id == salaZId && s.CzyAktywna);
        var @do = sale.FirstOrDefault(s => s.Id == salaDoId && s.CzyAktywna);
        var opisZ = z is null ? null : new OpisSali(z.Id, z.Symbol, z.Nazwa);
        var opisDo = @do is null ? null : new OpisSali(@do.Id, @do.Symbol, @do.Nazwa);

        WynikTrasy Blad(RodzajWynikuTrasy rodzaj, string komunikat, Trasa? trasa = null) =>
            new(rodzaj, opisZ, opisDo, trybWindy, trasa, komunikat, kontakt.Value);

        if (z is null || @do is null)
        {
            return Blad(RodzajWynikuTrasy.BladDanych,
                z is null ? "Nie znaleziono sali początkowej. Wybierz salę z listy." : "Nie znaleziono sali docelowej. Wybierz salę z listy.");
        }

        // a) start == cel
        if (z.Id == @do.Id)
        {
            return new WynikTrasy(RodzajWynikuTrasy.NaMiejscu, opisZ, opisDo, trybWindy, null,
                $"Jesteś już na miejscu — sala początkowa i docelowa to ta sama sala {z.Symbol}.", null);
        }

        // d) brak punktu wejściowego — czytelny komunikat, nie wyjątek
        foreach (var s in new[] { z, @do })
        {
            if (s.PunktWejsciowyId is null)
            {
                log.LogWarning("Sala {Symbol} (Id {Id}) nie ma punktu wejściowego — walidator grafu powinien to zgłosić.", s.Symbol, s.Id);
                return Blad(RodzajWynikuTrasy.BladDanych,
                    $"Nie możemy wyznaczyć trasy, bo w danych przewodnika sala {s.Symbol} nie ma jeszcze wyznaczonego wejścia z korytarza. " +
                    "Możesz zgłosić ten brak przez formularz „Zgłoś brak dostępności” w stopce strony.");
            }
        }

        if (z.Pietro.BudynekId != @do.Pietro.BudynekId)
        {
            return Blad(RodzajWynikuTrasy.BrakTrasy,
                $"Sale są w różnych budynkach ({z.Pietro.Budynek.Kod} i {@do.Pietro.Budynek.Kod}). Przewodnik nie wyznacza jeszcze tras między budynkami.");
        }

        var graf = await grafy.PobierzGrafAsync(z.Pietro.BudynekId, ct);
        var start = z.PunktWejsciowyId!.Value;
        var cel = @do.PunktWejsciowyId!.Value;
        var drzwiDoCelu = graf.KrawedzDoSali(cel, @do.Id);

        // Przeszkody w samym punkcie startowym lub docelowym (np. zamknięty odcinek korytarza przed drzwiami).
        foreach (var punkt in new[] { start, cel })
        {
            if (graf.Wezly.TryGetValue(punkt, out var w) && w.Utrudnienia.FirstOrDefault(u => u.Obowiazuje(data)) is { } u)
            {
                return Blad(RodzajWynikuTrasy.BrakTrasy, $"Przejście przy sali jest czasowo zamknięte: {u.Przyczyna}.{DoKiedy(u)}");
            }
        }
        if (drzwiDoCelu is { CzyAktywny: false })
        {
            return Blad(RodzajWynikuTrasy.BrakTrasy, $"Wejście do sali {@do.Symbol} jest niedostępne.");
        }

        var sciezka = Najkrotsza(graf, start, cel, k => Dozwolona(graf, k, data, bezSchodow: trybWindy, uwzgledniajUtrudnienia: true));
        if (sciezka is not null)
        {
            return new WynikTrasy(RodzajWynikuTrasy.Znaleziona, opisZ, opisDo, trybWindy,
                ZbudujTrase(graf, z.Id, start, sciezka, drzwiDoCelu), null, null);
        }

        // b) brak trasy bez schodów — szukamy trasy ze schodami i podajemy ją wyraźnie oznaczoną
        if (trybWindy)
        {
            var zeSchodami = Najkrotsza(graf, start, cel, k => Dozwolona(graf, k, data, bezSchodow: false, uwzgledniajUtrudnienia: true));
            if (zeSchodami is not null)
            {
                return Blad(RodzajWynikuTrasy.TylkoZeSchodami,
                    "Nie ma trasy bez schodów między tymi salami. Poniżej trasa alternatywna — ZAWIERA SCHODY.",
                    ZbudujTrase(graf, z.Id, start, zeSchodami, drzwiDoCelu));
            }
        }

        // c) brak jakiejkolwiek trasy — ustalamy przyczynę
        var bezUtrudnien = Najkrotsza(graf, start, cel, k => Dozwolona(graf, k, data, bezSchodow: false, uwzgledniajUtrudnienia: false));
        if (bezUtrudnien is not null)
        {
            var przyczyny = bezUtrudnien
                .SelectMany(k => k.Utrudnienia.Concat(k.DoPunktu is int p && graf.Wezly.TryGetValue(p, out var w) ? w.Utrudnienia : []))
                .Where(u => u.Obowiazuje(data))
                .Select(u => u.Przyczyna + DoKiedy(u))
                .Distinct()
                .ToList();
            return Blad(RodzajWynikuTrasy.BrakTrasy,
                $"Nie znaleziono trasy, bo na drodze jest czasowo zamknięte przejście: {string.Join("; ", przyczyny)}.");
        }

        return Blad(RodzajWynikuTrasy.BrakTrasy,
            $"Nie znaleziono trasy z sali {z.Symbol} do sali {@do.Symbol}. W danych przewodnika brakuje połączenia między tymi miejscami.");
    }

    /// <summary>
    /// Dijkstra z kopcem (PriorityQueue) na liście sąsiedztwa — 04_BAZA_DANYCH.md rozdz. 8.
    /// Zwraca listę krawędzi od <paramref name="start"/> do <paramref name="cel"/>, pustą, gdy start == cel,
    /// albo <c>null</c>, gdy trasy nie ma.
    /// </summary>
    internal static List<KrawedzGrafu>? Najkrotsza(GrafBudynku graf, int start, int cel, Func<KrawedzGrafu, bool> dozwolona)
    {
        if (start == cel)
        {
            return [];
        }

        var odleglosc = new Dictionary<int, decimal> { [start] = 0 };
        var poprzednik = new Dictionary<int, KrawedzGrafu>();
        var kolejka = new PriorityQueue<int, decimal>();
        kolejka.Enqueue(start, 0);

        while (kolejka.TryDequeue(out var u, out var dystU))
        {
            if (u == cel) break;
            if (dystU > odleglosc[u]) continue; // nieaktualny wpis w kolejce

            foreach (var k in graf.Sasiedzi(u))
            {
                if (k.DoPunktu is not int v || !dozwolona(k)) continue;

                var nowa = dystU + k.Waga;
                if (!odleglosc.TryGetValue(v, out var stara) || nowa < stara)
                {
                    odleglosc[v] = nowa;
                    poprzednik[v] = k;
                    kolejka.Enqueue(v, nowa);
                }
            }
        }

        if (!poprzednik.ContainsKey(cel))
        {
            return null;
        }

        var sciezka = new List<KrawedzGrafu>();
        for (var v = cel; v != start; v = poprzednik[v].Z)
        {
            sciezka.Add(poprzednik[v]);
        }
        sciezka.Reverse();
        return sciezka;
    }

    private static bool Dozwolona(GrafBudynku graf, KrawedzGrafu k, DateTime data, bool bezSchodow, bool uwzgledniajUtrudnienia)
    {
        if (!k.CzyAktywny || k.DoPunktu is not int doPunktu) return false;
        if (!graf.Wezly.TryGetValue(doPunktu, out var cel) || !cel.CzyAktywny) return false;
        if (bezSchodow && k.CzySchody) return false;
        if (uwzgledniajUtrudnienia && (k.Utrudnienia.Any(u => u.Obowiazuje(data)) || cel.Utrudnienia.Any(u => u.Obowiazuje(data)))) return false;
        return true;
    }

    private Trasa ZbudujTrase(GrafBudynku graf, int salaZId, int start, List<KrawedzGrafu> sciezka, KrawedzGrafu? drzwiDoCelu)
    {
        // Użytkownik wychodzi z sali twarzą od drzwi: zwrot = azymut drzwi + 180°.
        var drzwiStartu = graf.KrawedzDoSali(start, salaZId);
        var zwrot = drzwiStartu is not null
            ? Azymuty.Normalizuj(drzwiStartu.Azymut + 180)
            : graf.Wezly.TryGetValue(start, out var w) ? w.AzymutDomyslny : 0;
        var wstep = drzwiStartu is not null && graf.Sale.TryGetValue(salaZId, out var salaZ)
            ? $"Wyjdź z sali {salaZ.Symbol} na korytarz."
            : null;

        var krawedzie = drzwiDoCelu is not null ? [.. sciezka, drzwiDoCelu] : sciezka;
        var instrukcje = generator.Opisz(graf, krawedzie, zwrot, wstep, start);
        var dlugosc = krawedzie.Sum(k => k.Waga);

        return new Trasa(instrukcje, dlugosc, (int)Math.Ceiling(dlugosc / TempoMetrowNaSekunde), krawedzie.Any(k => k.CzySchody));
    }

    /// <summary>Aktywne sale do list wyboru w formularzu trasy.</summary>
    public async Task<IReadOnlyList<SalaWyszukana>> SaleDoWyboru(CancellationToken ct = default) =>
        (await repozytorium.PobierzAktywneSaleAsync(ct))
            .Select(s => new SalaWyszukana(s.Id, s.Symbol, s.Nazwa, s.TypSali.Nazwa, s.Pietro.Nazwa, s.Pietro.Numer,
                s.Pietro.Budynek.Kod, s.Pietro.Budynek.Nazwa))
            .ToList();

    private static string DoKiedy(OkresUtrudnienia u) =>
        u.Do is DateTime koniec ? $" (do {koniec:dd.MM.yyyy})" : string.Empty;

    /// <summary>Zapis w ZapytanieTrasy (K4, K5). Błąd zapisu nie może zepsuć odpowiedzi — tylko go logujemy.</summary>
    private async Task ZapiszZapytanie(int salaZId, int salaDoId, bool trybWindy, WynikTrasy wynik, int czasMs, CancellationToken ct)
    {
        var sukces = wynik.Rodzaj is RodzajWynikuTrasy.Znaleziona or RodzajWynikuTrasy.NaMiejscu;
        try
        {
            await repozytorium.ZapiszZapytanieAsync(new ZapytanieTrasy
            {
                SalaZId = wynik.SalaZ?.Id,
                SalaDoId = wynik.SalaDo?.Id,
                FrazaZ = wynik.SalaZ is null ? $"id {salaZId}" : null,
                FrazaDo = wynik.SalaDo is null ? $"id {salaDoId}" : null,
                TrybWindy = trybWindy,
                CzySukces = sukces,
                DlugoscMetry = sukces ? wynik.Trasa?.DlugoscMetry ?? 0 : null,
                LiczbaKrokow = sukces ? wynik.Trasa?.LiczbaKrokow ?? 0 : null,
                CzasMs = czasMs,
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Nie udało się zapisać zapytania o trasę w ZapytanieTrasy.");
        }
    }
}
