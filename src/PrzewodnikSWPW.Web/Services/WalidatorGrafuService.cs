using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Services;

public enum PoziomUwagi
{
    /// <summary>Dane są błędne — przewodnik poprowadzi źle albo wcale.</summary>
    Blad,
    /// <summary>Podejrzane — redaktor decyduje (np. strona względem nazwanego obiektu jest poprawna).</summary>
    Ostrzezenie,
}

public enum RodzajRekordu { Budynek, PunktRuchu, Kierunek }

/// <summary>Jedna uwaga walidatora z podpowiedzią, jak ją naprawić, i wskazaniem rekordu do edycji.</summary>
public sealed record UwagaWalidatora(
    PoziomUwagi Poziom,
    string Regula,
    string Tresc,
    string Podpowiedz,
    RodzajRekordu Rekord,
    int RekordId,
    string OpisRekordu);

/// <summary>
/// Walidator grafu nawigacji (UC-19, WF-25). Reguły są czystymi funkcjami na danych w pamięci —
/// testowalne bez bazy. Zakres: słowa stronne w opisach punktów (D-06), spójność kierunków
/// powrotnych (D-07), wejście główne i osiągalność punktów z niego (D-08).
/// </summary>
public sealed partial class WalidatorGrafuService(IAdministracjaRepozytorium repo)
{
    /// <summary>Różnica wag pary tam–z powrotem, powyżej której zgłaszamy ostrzeżenie (D-07 pkt 3).</summary>
    public const decimal ProgRoznicyWag = 0.5m;

    public const string RegulaSlowaStronne = "D-06 słowa stronne";
    public const string RegulaPowrotWzajemny = "D-07 powrót wzajemny";
    public const string RegulaPowrotKonce = "D-07 końce powrotu";
    public const string RegulaPowrotAzymut = "D-07 azymut powrotu";
    public const string RegulaPowrotWaga = "D-07 waga powrotu";
    public const string RegulaWejscieBrak = "D-08 brak wejścia głównego";
    public const string RegulaWejscieInnyBudynek = "D-08 wejście w innym budynku";
    public const string RegulaNieosiagalny = "D-08 punkt nieosiągalny";

    /// <summary>
    /// Słowa stronne z D-06. Dodatkowo „po twojej lewej/prawej” — wariant, który występował
    /// w danych początkowych i jest najjaskrawszym przypadkiem odniesienia do użytkownika.
    /// </summary>
    [GeneratedRegex(@"\b(po (twojej )?lewej|po (twojej )?prawej|z lewej|z prawej|na lewo|na prawo)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    public static partial Regex SlowaStronne();

    public async Task<IReadOnlyList<UwagaWalidatora>> WalidujAsync(CancellationToken ct = default)
    {
        var budynki = await repo.Zapytanie<Budynek>().AsNoTracking().ToListAsync(ct);
        var punkty = await repo.Zapytanie<PunktRuchu>().AsNoTracking().Include(p => p.Pietro).ToListAsync(ct);
        var kierunki = await repo.Zapytanie<Kierunek>().AsNoTracking().ToListAsync(ct);
        return Waliduj(budynki, punkty, kierunki);
    }

    /// <summary>Wszystkie reguły; błędy przed ostrzeżeniami.</summary>
    public static IReadOnlyList<UwagaWalidatora> Waliduj(IReadOnlyList<Budynek> budynki, IReadOnlyList<PunktRuchu> punkty, IReadOnlyList<Kierunek> kierunki) =>
        SprawdzSlowaStronne(punkty)
            .Concat(SprawdzPowroty(punkty, kierunki))
            .Concat(SprawdzWejsciaIOsiagalnosc(budynki, punkty, kierunki))
            .OrderBy(u => u.Poziom)
            .ThenBy(u => u.Regula)
            .ThenBy(u => u.OpisRekordu)
            .ToList();

    // --- D-06 --------------------------------------------------------------------------------------------

    public static IEnumerable<UwagaWalidatora> SprawdzSlowaStronne(IEnumerable<PunktRuchu> punkty)
    {
        foreach (var p in punkty)
        {
            foreach (var (pole, tekst) in new[] { ("Opis", p.Opis), ("Opis głosowy", p.OpisGlosowy) })
            {
                if (tekst is null || SlowaStronne().Match(tekst) is not { Success: true } trafienie) continue;

                yield return new UwagaWalidatora(PoziomUwagi.Ostrzezenie, RegulaSlowaStronne,
                    $"{pole} zawiera „{trafienie.Value}”: „{Skrot(tekst)}”.",
                    "Do punktu wchodzi się z kilku stron, więc strona względem idącego jest fałszem przy części dojść. " +
                    "Wymień punkty orientacyjne bez stron („Są tu drzwi do sali A14 i do sali A15”) albo nazwij obiekt " +
                    "odniesienia („po prawej stronie drzwi windy” — takie zdanie jest poprawne i może zostać).",
                    RodzajRekordu.PunktRuchu, p.Id, $"punkt {p.Kod}");
            }
        }
    }

    // --- D-07 --------------------------------------------------------------------------------------------

    public static IEnumerable<UwagaWalidatora> SprawdzPowroty(IEnumerable<PunktRuchu> punkty, IReadOnlyList<Kierunek> kierunki)
    {
        var kody = punkty.ToDictionary(p => p.Id, p => p.Kod);
        var poId = kierunki.ToDictionary(k => k.Id);
        string Opis(Kierunek k) => $"kierunek {k.Azymut}° z punktu {kody.GetValueOrDefault(k.PunktZrodlowyId, $"#{k.PunktZrodlowyId}")}";

        foreach (var k in kierunki.Where(k => k.KierunekPowrotnyId is not null))
        {
            if (!poId.TryGetValue(k.KierunekPowrotnyId!.Value, out var p)) continue; // klucz obcy gwarantuje istnienie

            if (p.KierunekPowrotnyId != k.Id)
            {
                yield return new UwagaWalidatora(PoziomUwagi.Blad, RegulaPowrotWzajemny,
                    $"Wskazuje jako powrót {Opis(p)}, ale tamten wskazuje {(p.KierunekPowrotnyId is null ? "brak powrotu" : "inny kierunek")}.",
                    "Powiązanie musi być wzajemne: A→B wskazuje B→A i odwrotnie. Popraw kierunek powrotny w obu krawędziach.",
                    RodzajRekordu.Kierunek, k.Id, Opis(k));
            }

            if (p.PunktZrodlowyId != k.PunktDocelowyId || p.PunktDocelowyId != k.PunktZrodlowyId)
            {
                yield return new UwagaWalidatora(PoziomUwagi.Blad, RegulaPowrotKonce,
                    $"Kierunek powrotny ({Opis(p)}) nie prowadzi z punktu docelowego z powrotem do punktu źródłowego.",
                    "Kierunek powrotny krawędzi A→B musi być krawędzią B→A. Wskaż właściwy kierunek albo usuń powiązanie.",
                    RodzajRekordu.Kierunek, k.Id, Opis(k));
            }

            if (p.Azymut != Azymuty.Normalizuj(k.Azymut + 180))
            {
                yield return new UwagaWalidatora(PoziomUwagi.Blad, RegulaPowrotAzymut,
                    $"Azymut powrotu to {p.Azymut}°, a powinien być {Azymuty.Normalizuj(k.Azymut + 180)}° (azymut + 180°).",
                    "Jeśli idzie się tam na wschód (90°), wraca się na zachód (270°). Błędny azymut da użytkownikowi złą instrukcję „w lewo / w prawo”.",
                    RodzajRekordu.Kierunek, k.Id, Opis(k));
            }

            // Wag nie porównujemy na równość — schody w górę mogą „kosztować” więcej niż w dół (D-07 pkt 3).
            // Każdą parę zgłaszamy raz: od krawędzi o mniejszym Id.
            if (k.Id < p.Id && Math.Min(k.Waga, p.Waga) > 0
                && Math.Max(k.Waga, p.Waga) / Math.Min(k.Waga, p.Waga) - 1 > ProgRoznicyWag)
            {
                yield return new UwagaWalidatora(PoziomUwagi.Ostrzezenie, RegulaPowrotWaga,
                    $"Odległość tam: {NawigacjaService.Metry(k.Waga)}, z powrotem: {NawigacjaService.Metry(p.Waga)} — różnica ponad {ProgRoznicyWag:P0}.",
                    "Różne wagi są dopuszczalne (np. schody w górę), ale tak duża różnica to częściej pomyłka przy wpisywaniu.",
                    RodzajRekordu.Kierunek, k.Id, Opis(k));
            }
        }
    }

    // --- D-08 --------------------------------------------------------------------------------------------

    public static IEnumerable<UwagaWalidatora> SprawdzWejsciaIOsiagalnosc(IEnumerable<Budynek> budynki, IReadOnlyList<PunktRuchu> punkty, IReadOnlyList<Kierunek> kierunki)
    {
        foreach (var b in budynki.Where(b => b.CzyAktywny))
        {
            var punktyBudynku = punkty.Where(p => p.Pietro.BudynekId == b.Id).ToDictionary(p => p.Id);

            if (b.PunktWejsciaGlownegoId is not int wejscie)
            {
                yield return new UwagaWalidatora(PoziomUwagi.Blad, RegulaWejscieBrak,
                    "Budynek nie ma ustawionego wejścia głównego.",
                    "Ustaw wejście główne w edycji budynku. Bez niego spacer nie ma punktu startowego, a walidator nie sprawdzi, czy każde miejsce jest osiągalne.",
                    RodzajRekordu.Budynek, b.Id, $"budynek {b.Kod}");
                continue;
            }

            if (!punktyBudynku.ContainsKey(wejscie))
            {
                yield return new UwagaWalidatora(PoziomUwagi.Blad, RegulaWejscieInnyBudynek,
                    "Wskazane wejście główne leży w innym budynku.",
                    "Wybierz jako wejście główne punkt ruchu tego budynku.",
                    RodzajRekordu.Budynek, b.Id, $"budynek {b.Kod}");
                continue;
            }

            // Przeszukiwanie wszerz od wejścia po aktywnych krawędziach do aktywnych punktów.
            var sasiedzi = kierunki
                .Where(k => k.CzyAktywny && k.PunktDocelowyId is int d && punktyBudynku.TryGetValue(d, out var cel) && cel.CzyAktywny)
                .ToLookup(k => k.PunktZrodlowyId, k => k.PunktDocelowyId!.Value);
            var odwiedzone = new HashSet<int> { wejscie };
            var kolejka = new Queue<int>([wejscie]);
            while (kolejka.TryDequeue(out var u))
            {
                foreach (var v in sasiedzi[u])
                {
                    if (odwiedzone.Add(v)) kolejka.Enqueue(v);
                }
            }

            foreach (var p in punktyBudynku.Values.Where(p => p.CzyAktywny && !odwiedzone.Contains(p.Id)))
            {
                yield return new UwagaWalidatora(PoziomUwagi.Blad, RegulaNieosiagalny,
                    "Z wejścia głównego nie da się dojść do tego punktu.",
                    "Punkt leży w odciętej części grafu. Dodaj brakujący kierunek (najczęściej brakuje kierunku powrotnego) albo dezaktywuj punkt.",
                    RodzajRekordu.PunktRuchu, p.Id, $"punkt {p.Kod}");
            }
        }
    }

    private static string Skrot(string tekst) => tekst.Length <= 120 ? tekst : tekst[..117] + "…";
}
