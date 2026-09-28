using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Services;

/// <summary>Błąd przypisany do pola formularza (<see cref="Pole"/> = "" — błąd całego formularza).</summary>
public sealed record BladPola(string Pole, string Komunikat);

public sealed record WynikOperacji(IReadOnlyList<BladPola> Bledy, string? Informacja = null)
{
    public bool Sukces => Bledy.Count == 0;
    public static WynikOperacji Ok(string? informacja = null) => new([], informacja);
    public static WynikOperacji Blad(string pole, string komunikat) => new([new BladPola(pole, komunikat)]);
}

public sealed record WpisAudytuWidok(DateTime Data, string Uzytkownik, string Encja, string Klucz, string Operacja, string? Stare, string? Nowe);

/// <summary>
/// Reguły panelu administratora (UC-16, UC-17, WF-23, WF-24). Unikalność i zależności są sprawdzane
/// przed zapisem, aby użytkownik dostał zrozumiały komunikat przy polu formularza, a nie błąd bazy.
/// Punktów, kierunków, sal i budynków nie usuwamy fizycznie — tylko CzyAktywny = false (D-04, P-09).
/// </summary>
public sealed class AdministracjaService(IAdministracjaRepozytorium repo)
{
    // --- Budynki -------------------------------------------------------------------------------------

    public Task<List<Budynek>> Budynki(CancellationToken ct = default) =>
        repo.Zapytanie<Budynek>().AsNoTracking().Include(b => b.Pietra).OrderBy(b => b.Kod).ToListAsync(ct);

    public Task<Budynek?> PobierzBudynek(int id, CancellationToken ct = default) => repo.ZnajdzAsync<Budynek>(id, ct);

    public async Task<WynikOperacji> ZapiszBudynek(Budynek b, CancellationToken ct = default)
    {
        if (await repo.Zapytanie<Budynek>().AnyAsync(x => x.Kod == b.Kod && x.Id != b.Id, ct))
        {
            return WynikOperacji.Blad(nameof(Budynek.Kod), $"Kod „{b.Kod}” ma już inny budynek. Wybierz inny kod.");
        }
        return await Zapisz(b, ct);
    }

    public Task<WynikOperacji> UstawAktywnoscBudynku(int id, bool aktywny, CancellationToken ct = default) =>
        UstawAktywnosc<Budynek>(id, b => b.CzyAktywny = aktywny, ct);

    // --- Piętra ----------------------------------------------------------------------------------------

    public Task<List<Pietro>> Pietra(CancellationToken ct = default) =>
        repo.Zapytanie<Pietro>().AsNoTracking()
            .Include(p => p.Budynek).Include(p => p.Sale).Include(p => p.PunktyRuchu)
            .OrderBy(p => p.Budynek.Kod).ThenBy(p => p.Numer).ToListAsync(ct);

    public Task<Pietro?> PobierzPietro(int id, CancellationToken ct = default) =>
        repo.Zapytanie<Pietro>().Include(p => p.Budynek).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<WynikOperacji> ZapiszPietro(Pietro p, CancellationToken ct = default)
    {
        if (!await repo.Zapytanie<Budynek>().AnyAsync(b => b.Id == p.BudynekId, ct))
        {
            return WynikOperacji.Blad(nameof(Pietro.BudynekId), "Wybierz budynek z listy.");
        }
        if (await repo.Zapytanie<Pietro>().AnyAsync(x => x.BudynekId == p.BudynekId && x.Numer == p.Numer && x.Id != p.Id, ct))
        {
            return WynikOperacji.Blad(nameof(Pietro.Numer), $"Ten budynek ma już piętro numer {p.Numer}.");
        }
        return await Zapisz(p, ct);
    }

    /// <summary>Piętro usuwamy tylko puste — z salami lub punktami ruchu usunięcie skasowałoby część grafu.</summary>
    public async Task<WynikOperacji> UsunPietro(int id, CancellationToken ct = default)
    {
        var p = await repo.Zapytanie<Pietro>().Include(x => x.Sale).Include(x => x.PunktyRuchu).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return WynikOperacji.Blad("", "Nie znaleziono piętra.");
        if (p.Sale.Count > 0 || p.PunktyRuchu.Count > 0)
        {
            return WynikOperacji.Blad("", $"Nie można usunąć piętra „{p.Nazwa}”: ma {Odmiana.Sale(p.Sale.Count)} i {p.PunktyRuchu.Count} punktów ruchu. " +
                "Najpierw przenieś albo dezaktywuj sale i punkty.");
        }
        repo.Usun(p);
        await repo.ZapiszAsync(ct);
        return WynikOperacji.Ok();
    }

    // --- Sale ------------------------------------------------------------------------------------------

    public Task<List<Sala>> Sale(CancellationToken ct = default) =>
        repo.Zapytanie<Sala>().AsNoTracking()
            .Include(s => s.Pietro).ThenInclude(p => p.Budynek).Include(s => s.TypSali).Include(s => s.PunktWejsciowy)
            .OrderBy(s => s.Pietro.Budynek.Kod).ThenBy(s => s.Pietro.Numer).ThenBy(s => s.Symbol).ToListAsync(ct);

    public Task<Sala?> PobierzSale(int id, CancellationToken ct = default) =>
        repo.Zapytanie<Sala>().Include(s => s.Pietro).ThenInclude(p => p.Budynek).FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<WynikOperacji> ZapiszSale(Sala s, CancellationToken ct = default)
    {
        var pietro = await repo.Zapytanie<Pietro>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == s.PietroId, ct);
        if (pietro is null) return WynikOperacji.Blad(nameof(Sala.PietroId), "Wybierz piętro z listy.");
        if (!await repo.Zapytanie<TypSali>().AnyAsync(t => t.Id == s.TypSaliId, ct))
        {
            return WynikOperacji.Blad(nameof(Sala.TypSaliId), "Wybierz typ sali z listy.");
        }
        if (await repo.Zapytanie<Sala>().AnyAsync(x => x.PietroId == s.PietroId && x.Symbol == s.Symbol && x.Id != s.Id, ct))
        {
            return WynikOperacji.Blad(nameof(Sala.Symbol), $"Na tym piętrze jest już sala o symbolu „{s.Symbol}”.");
        }
        if (s.PunktWejsciowyId is int punktId
            && !await repo.Zapytanie<PunktRuchu>().AnyAsync(p => p.Id == punktId && p.Pietro.BudynekId == pietro.BudynekId, ct))
        {
            return WynikOperacji.Blad(nameof(Sala.PunktWejsciowyId), "Punkt wejściowy musi leżeć w tym samym budynku co sala.");
        }
        return await Zapisz(s, ct);
    }

    public Task<WynikOperacji> UstawAktywnoscSali(int id, bool aktywna, CancellationToken ct = default) =>
        UstawAktywnosc<Sala>(id, s => s.CzyAktywna = aktywna, ct);

    // --- Słowniki: TypSali, TypPunktu, Udogodnienie --------------------------------------------------

    public Task<List<T>> Slownik<T>(CancellationToken ct = default) where T : class, ISlownik =>
        repo.Zapytanie<T>().AsNoTracking().OrderBy(x => x.Nazwa).ToListAsync(ct);

    public Task<T?> PozycjaSlownika<T>(int id, CancellationToken ct = default) where T : class, ISlownik => repo.ZnajdzAsync<T>(id, ct);

    public async Task<WynikOperacji> ZapiszSlownik<T>(T pozycja, CancellationToken ct = default) where T : class, ISlownik
    {
        if (await repo.Zapytanie<T>().AnyAsync(x => x.Nazwa == pozycja.Nazwa && x.Id != pozycja.Id, ct))
        {
            return WynikOperacji.Blad(nameof(ISlownik.Nazwa), $"Pozycja o nazwie „{pozycja.Nazwa}” już istnieje.");
        }
        return await Zapisz(pozycja, ct);
    }

    /// <summary>Liczba rekordów korzystających z pozycji słownika — pozycji w użyciu nie usuwamy.</summary>
    public async Task<int> UzyciaSlownika<T>(int id, CancellationToken ct = default) where T : class, ISlownik => typeof(T).Name switch
    {
        nameof(TypSali) => await repo.Zapytanie<Sala>().CountAsync(s => s.TypSaliId == id, ct),
        nameof(TypPunktu) => await repo.Zapytanie<PunktRuchu>().CountAsync(p => p.TypPunktuId == id, ct),
        nameof(Udogodnienie) => await repo.Zapytanie<PunktUdogodnienie>().CountAsync(u => u.UdogodnienieId == id, ct)
                              + await repo.Zapytanie<SalaUdogodnienie>().CountAsync(u => u.UdogodnienieId == id, ct),
        _ => throw new NotSupportedException(typeof(T).Name),
    };

    public async Task<WynikOperacji> UsunSlownik<T>(int id, CancellationToken ct = default) where T : class, ISlownik
    {
        var pozycja = await repo.ZnajdzAsync<T>(id, ct);
        if (pozycja is null) return WynikOperacji.Blad("", "Nie znaleziono pozycji.");
        var uzycia = await UzyciaSlownika<T>(id, ct);
        if (uzycia > 0)
        {
            return WynikOperacji.Blad("", $"Nie można usunąć „{pozycja.Nazwa}”: używa jej {uzycia} {Odmiana.PoLiczbie(uzycia, "rekord", "rekordy", "rekordów")}.");
        }
        repo.Usun(pozycja);
        await repo.ZapiszAsync(ct);
        return WynikOperacji.Ok();
    }

    // --- Punkty ruchu ----------------------------------------------------------------------------------

    public Task<List<PunktRuchu>> Punkty(CancellationToken ct = default) =>
        repo.Zapytanie<PunktRuchu>().AsNoTracking()
            .Include(p => p.Pietro).ThenInclude(p => p.Budynek).Include(p => p.TypPunktu).Include(p => p.KierunkiWychodzace)
            .OrderBy(p => p.Kod).ToListAsync(ct);

    public Task<PunktRuchu?> PobierzPunkt(int id, CancellationToken ct = default) =>
        repo.Zapytanie<PunktRuchu>().Include(p => p.Pietro).ThenInclude(p => p.Budynek).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<WynikOperacji> ZapiszPunkt(PunktRuchu p, CancellationToken ct = default)
    {
        if (!await repo.Zapytanie<Pietro>().AnyAsync(x => x.Id == p.PietroId, ct))
            return WynikOperacji.Blad(nameof(PunktRuchu.PietroId), "Wybierz piętro z listy.");
        if (!await repo.Zapytanie<TypPunktu>().AnyAsync(x => x.Id == p.TypPunktuId, ct))
            return WynikOperacji.Blad(nameof(PunktRuchu.TypPunktuId), "Wybierz typ punktu z listy.");
        if (await repo.Zapytanie<PunktRuchu>().AnyAsync(x => x.Kod == p.Kod && x.Id != p.Id, ct))
            return WynikOperacji.Blad(nameof(PunktRuchu.Kod), $"Kod „{p.Kod}” ma już inny punkt ruchu.");
        return await Zapisz(p, ct);
    }

    public Task<WynikOperacji> UstawAktywnoscPunktu(int id, bool aktywny, CancellationToken ct = default) =>
        UstawAktywnosc<PunktRuchu>(id, p => p.CzyAktywny = aktywny, ct);

    // --- Kierunki (krawędzie grafu) --------------------------------------------------------------------

    public Task<List<Kierunek>> KierunkiPunktu(int punktId, CancellationToken ct = default) =>
        repo.Zapytanie<Kierunek>().AsNoTracking()
            .Include(k => k.PunktDocelowy).Include(k => k.SalaDocelowa)
            .Where(k => k.PunktZrodlowyId == punktId).OrderBy(k => k.Azymut).ToListAsync(ct);

    public Task<Kierunek?> PobierzKierunek(int id, CancellationToken ct = default) =>
        repo.Zapytanie<Kierunek>().Include(k => k.PunktZrodlowy).Include(k => k.KierunekPowrotny).FirstOrDefaultAsync(k => k.Id == id, ct);

    /// <summary>
    /// Dodaje kierunek A→B i — domyślnie — powrotny B→A z tą samą wagą i azymutem odwróconym o 180°
    /// (WF-24; P-03: połowa pracy mniej, P-07: brak krawędzi bez pary). Oba w jednej transakcji,
    /// wzajemnie powiązane przez KierunekPowrotnyId. Gdy B→A już istnieje, zostaje tylko powiązany.
    /// </summary>
    public async Task<WynikOperacji> DodajKierunek(Kierunek k, bool utworzPowrotny, string? opisPowrotny, CancellationToken ct = default)
    {
        var bledy = await SprawdzKierunek(k, ct);
        var azymutPowrotny = Azymuty.Normalizuj(k.Azymut + 180);
        Kierunek? istniejacyPowrotny = null;

        if (k.PunktDocelowyId is int b)
        {
            istniejacyPowrotny = await repo.Zapytanie<Kierunek>()
                .FirstOrDefaultAsync(x => x.PunktZrodlowyId == b && x.Azymut == azymutPowrotny, ct);

            if (utworzPowrotny && istniejacyPowrotny is not null && istniejacyPowrotny.PunktDocelowyId != k.PunktZrodlowyId)
            {
                bledy.Add(new BladPola("UtworzPowrotny",
                    $"Punkt docelowy ma już kierunek o azymucie {azymutPowrotny}°, prowadzący gdzie indziej — kierunek powrotny nie może powstać. " +
                    "Sprawdź azymut albo odznacz tworzenie kierunku powrotnego."));
            }
        }
        if (bledy.Count > 0) return new WynikOperacji(bledy);

        await using var transakcja = await repo.RozpocznijTransakcjeAsync(ct);
        repo.Dodaj(k);
        await repo.ZapiszAsync(ct);

        string? informacja = null;
        if (k.PunktDocelowyId is int docelowy)
        {
            var powrotny = istniejacyPowrotny;
            if (powrotny is null && utworzPowrotny)
            {
                powrotny = new Kierunek
                {
                    PunktZrodlowyId = docelowy,
                    PunktDocelowyId = k.PunktZrodlowyId,
                    Azymut = azymutPowrotny,
                    Waga = k.Waga,
                    RodzajPrzejscia = k.RodzajPrzejscia,
                    CzyAktywny = k.CzyAktywny,
                    CzyDostepnyBezSchodow = k.CzyDostepnyBezSchodow,
                    OpisPrzejscia = string.IsNullOrWhiteSpace(opisPowrotny) ? null : opisPowrotny.Trim(),
                };
                repo.Dodaj(powrotny);
                await repo.ZapiszAsync(ct);
                informacja = $"Utworzono też kierunek powrotny o azymucie {azymutPowrotny}°.";
            }
            else if (powrotny is not null)
            {
                informacja = "Kierunek powiązano z istniejącym kierunkiem powrotnym.";
            }

            if (powrotny is not null)
            {
                k.KierunekPowrotnyId = powrotny.Id;
                powrotny.KierunekPowrotnyId = k.Id;
                await repo.ZapiszAsync(ct);
            }
        }
        else
        {
            informacja = "Kierunek prowadzi do sali — kierunku powrotnego z sali nie tworzymy.";
        }

        await transakcja.CommitAsync(ct);
        return WynikOperacji.Ok(informacja);
    }

    public async Task<WynikOperacji> ZapiszKierunek(Kierunek k, CancellationToken ct = default)
    {
        var bledy = await SprawdzKierunek(k, ct);
        return bledy.Count > 0 ? new WynikOperacji(bledy) : await Zapisz(k, ct);
    }

    /// <summary>Kierunku nie usuwamy — ustawiamy CzyAktywny (brak przejścia z opisem orientacyjnym, WF-04).</summary>
    public async Task<WynikOperacji> UstawAktywnoscKierunku(int id, bool aktywny, bool takzePowrotny, CancellationToken ct = default)
    {
        var k = await repo.ZnajdzAsync<Kierunek>(id, ct);
        if (k is null) return WynikOperacji.Blad("", "Nie znaleziono kierunku.");
        k.CzyAktywny = aktywny;
        if (takzePowrotny && k.KierunekPowrotnyId is int p && await repo.ZnajdzAsync<Kierunek>(p, ct) is { } powrotny)
        {
            powrotny.CzyAktywny = aktywny;
        }
        await repo.ZapiszAsync(ct);
        return WynikOperacji.Ok();
    }

    private async Task<List<BladPola>> SprawdzKierunek(Kierunek k, CancellationToken ct)
    {
        var bledy = new List<BladPola>();
        if (!await repo.Zapytanie<PunktRuchu>().AnyAsync(p => p.Id == k.PunktZrodlowyId, ct))
            bledy.Add(new BladPola(nameof(Kierunek.PunktZrodlowyId), "Wybierz punkt źródłowy z listy."));
        if (await repo.Zapytanie<Kierunek>().AnyAsync(x => x.PunktZrodlowyId == k.PunktZrodlowyId && x.Azymut == k.Azymut && x.Id != k.Id, ct))
            bledy.Add(new BladPola(nameof(Kierunek.Azymut),
                $"Z tego punktu wychodzi już kierunek o azymucie {k.Azymut}°. Z jednego punktu w danym azymucie może prowadzić tylko jeden kierunek."));
        if (k.PunktDocelowyId is int b && !await repo.Zapytanie<PunktRuchu>().AnyAsync(p => p.Id == b, ct))
            bledy.Add(new BladPola(nameof(Kierunek.PunktDocelowyId), "Wybierz punkt docelowy z listy."));
        if (k.SalaDocelowaId is int s && !await repo.Zapytanie<Sala>().AnyAsync(x => x.Id == s, ct))
            bledy.Add(new BladPola(nameof(Kierunek.SalaDocelowaId), "Wybierz salę docelową z listy."));
        return bledy;
    }

    // --- Rejestr zmian -----------------------------------------------------------------------------

    public async Task<List<WpisAudytuWidok>> OstatnieWpisyAudytu(int ile, CancellationToken ct = default)
    {
        var wpisy = await repo.Zapytanie<WpisAudytu>().AsNoTracking().OrderByDescending(w => w.Id).Take(ile).ToListAsync(ct);
        var ids = wpisy.Where(w => w.UzytkownikId != null).Select(w => w.UzytkownikId!).Distinct().ToList();
        var nazwy = await repo.Zapytanie<IdentityUser>().AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id, ct);
        return wpisy.Select(w => new WpisAudytuWidok(w.DataOperacji,
            w.UzytkownikId is null ? "system" : nazwy.GetValueOrDefault(w.UzytkownikId, w.UzytkownikId),
            w.Encja, w.KluczEncji, w.Operacja, w.WartosciStare, w.WartosciNowe)).ToList();
    }

    // --- wspólne ---------------------------------------------------------------------------------------

    private async Task<WynikOperacji> Zapisz<T>(T encja, CancellationToken ct) where T : class
    {
        if (EncjaJestNowa(encja))
        {
            repo.Dodaj(encja);
        }
        await repo.ZapiszAsync(ct);
        return WynikOperacji.Ok();
    }

    private static bool EncjaJestNowa(object encja) => encja.GetType().GetProperty("Id")?.GetValue(encja) is 0;

    private async Task<WynikOperacji> UstawAktywnosc<T>(int id, Action<T> ustaw, CancellationToken ct) where T : class
    {
        var encja = await repo.ZnajdzAsync<T>(id, ct);
        if (encja is null) return WynikOperacji.Blad("", "Nie znaleziono rekordu.");
        ustaw(encja);
        await repo.ZapiszAsync(ct);
        return WynikOperacji.Ok();
    }
}
