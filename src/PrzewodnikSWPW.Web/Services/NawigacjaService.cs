using System.Globalization;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Services;

/// <summary>
/// „Spacer” po grafie: widok miejsca z czterema kierunkami względnymi i przejście jedną krawędzią.
/// Przeliczanie azymutów wyłącznie przez <see cref="Azymuty"/>.
/// </summary>
public sealed class NawigacjaService(INawigacjaRepozytorium repozytorium, TimeProvider czas)
{
    private static readonly CultureInfo Polski = CultureInfo.GetCultureInfo("pl-PL");

    /// <summary>
    /// Widok punktu dla użytkownika zwróconego w stronę <paramref name="zwrot"/>. Lista kierunków ma
    /// zawsze 4 pozycje w kolejności z D-01 — także nieaktywne i nieistniejące, z opisem.
    /// Zwraca <c>null</c>, gdy punkt nie istnieje albo jest nieaktywny.
    /// </summary>
    public async Task<WidokMiejsca?> PobierzMiejsce(int punktId, int zwrot, CancellationToken ct = default)
    {
        zwrot = Azymuty.Normalizuj(zwrot);
        var punkt = await repozytorium.PobierzPunktZKierunkamiAsync(punktId, ct);
        if (punkt is null || !punkt.CzyAktywny)
        {
            return null;
        }

        var teraz = czas.GetLocalNow().DateTime;
        var kierunki = Azymuty.KolejnoscNaLiscie
            .Select(wzgledny => ZbudujOpcje(punkt, wzgledny, zwrot, teraz))
            .ToList();

        var zdjecia = punkt.Zdjecia
            .OrderBy(z => z.Kolejnosc)
            .Select(ZdjecieMiejsca.Z)
            .ToList();

        return new WidokMiejsca(punkt.Id, punkt.Kod, punkt.Nazwa, punkt.Opis, punkt.OpisGlosowy, zwrot, zdjecia, kierunki,
            AdresMiejsca.Z(punkt), punkt.Pietro.Budynek.Nazwa, punkt.Pietro.Nazwa);
    }

    /// <summary>
    /// Próba przejścia w kierunku względnym. Po przejściu nowy zwrot = azymut przebytej krawędzi.
    /// Przy braku przejścia użytkownik zostaje w miejscu z tym samym zwrotem.
    /// Zwraca <c>null</c>, gdy punkt startowy nie istnieje albo jest nieaktywny.
    /// </summary>
    public async Task<WynikPrzejscia?> Przejdz(int punktId, KierunekWzgledny kierunek, int zwrot, CancellationToken ct = default)
    {
        zwrot = Azymuty.Normalizuj(zwrot);
        var punkt = await repozytorium.PobierzPunktZKierunkamiAsync(punktId, ct);
        if (punkt is null || !punkt.CzyAktywny)
        {
            return null;
        }

        var azymut = Azymuty.NaBezwzgledny(kierunek, zwrot);
        var krawedz = punkt.KierunkiWychodzace.SingleOrDefault(k => k.Azymut == azymut);

        if (krawedz is null)
        {
            return BrakPrzejscia(punkt, zwrot, $"{Azymuty.NazwaStrony(kierunek)} — brak przejścia.");
        }

        var przeszkoda = Przeszkoda(krawedz, czas.GetLocalNow().DateTime);
        if (przeszkoda is not null)
        {
            return BrakPrzejscia(punkt, zwrot, $"{Azymuty.NazwaStrony(kierunek)} — brak przejścia. {przeszkoda}".TrimEnd());
        }

        var opis = krawedz.OpisPrzejscia ?? $"{Azymuty.Etykieta(kierunek)}, {Metry(krawedz.Waga)}.";

        return krawedz.PunktDocelowy is { } docelowy
            ? new WynikPrzejscia(RodzajWynikuPrzejscia.DoPunktu, docelowy.Id, null, krawedz.Azymut, opis, AdresMiejsca.Z(docelowy))
            : new WynikPrzejscia(RodzajWynikuPrzejscia.DoSali, punkt.Id, krawedz.SalaDocelowaId, krawedz.Azymut, opis, AdresMiejsca.Z(punkt));
    }

    private static WynikPrzejscia BrakPrzejscia(PunktRuchu punkt, int zwrot, string opis) =>
        new(RodzajWynikuPrzejscia.BrakPrzejscia, punkt.Id, null, zwrot, opis, AdresMiejsca.Z(punkt));

    public async Task<IReadOnlyList<BudynekNaLiscie>> PobierzBudynki(CancellationToken ct = default) =>
        (await repozytorium.PobierzBudynkiAsync(ct))
            .Select(b => new BudynekNaLiscie(b.Kod, b.Nazwa, b.Adres))
            .ToList();

    /// <summary>Opisy dostępności architektonicznej aktywnych budynków — sekcja a11y-architektura Deklaracji (WF-36).</summary>
    public async Task<IReadOnlyList<DostepnoscBudynku>> PobierzDostepnoscArchitektoniczna(CancellationToken ct = default) =>
        (await repozytorium.PobierzBudynkiAsync(ct))
            .Select(b => new DostepnoscBudynku(b.Nazwa, b.Adres, b.OpisDostepnosciArchitektonicznej))
            .ToList();

    /// <summary>
    /// Budynek z piętrami i punktami do wyboru. Punkt startowy spaceru to wejście główne budynku (D-08);
    /// bez ustawionego wejścia ekran pokazuje tylko listę punktów. <c>null</c>, gdy budynek nie istnieje.
    /// </summary>
    public async Task<WidokBudynku?> PobierzBudynek(string kodBudynku, CancellationToken ct = default)
    {
        var budynek = await repozytorium.PobierzBudynekAsync(kodBudynku, ct);
        if (budynek is null)
        {
            return null;
        }

        var pietra = budynek.Pietra
            .OrderBy(p => p.Numer)
            .Select(p => new PietroNaLiscie(p.Numer, p.Nazwa, p.PunktyRuchu
                .OrderBy(pr => pr.Kod)
                .Select(pr => new PunktNaLiscie(AdresMiejsca.Z(budynek.Kod, p.Numer, pr.Kod), pr.Nazwa, pr.AzymutDomyslny))
                .ToList()))
            .ToList();

        var start = budynek.PunktWejsciaGlownego is { CzyAktywny: true } w
            ? new PunktNaLiscie(AdresMiejsca.Z(budynek.Kod, w.Pietro.Numer, w.Kod), w.Nazwa, w.AzymutDomyslny)
            : null;

        return new WidokBudynku(budynek.Kod, budynek.Nazwa, budynek.Adres,
            budynek.OpisDostepnosciArchitektonicznej, pietra, start);
    }

    public Task<int?> ZnajdzPunkt(string kodBudynku, int numerPietra, string segmentPunktu, CancellationToken ct = default) =>
        repozytorium.ZnajdzPunktIdAsync(kodBudynku, numerPietra, segmentPunktu, ct);

    /// <summary>Domyślny zwrot punktu (np. dla linku bez parametru zwrot) albo <c>null</c>, gdy punkt nie istnieje.</summary>
    public async Task<int?> DomyslnyZwrot(int punktId, CancellationToken ct = default) =>
        (await repozytorium.PobierzPunktAsync(punktId, ct))?.AzymutDomyslny;

    /// <summary>
    /// Karta sali. Powrót prowadzi do punktu <paramref name="zPunktu"/> (albo punktu wejściowego sali)
    /// ze zwrotem odwróconym względem wejścia — użytkownik wychodzi z sali twarzą od drzwi.
    /// </summary>
    public async Task<WidokSali?> PobierzSale(int salaId, int? zPunktu, int? zwrotWejscia, CancellationToken ct = default)
    {
        var sala = await repozytorium.PobierzSaleAsync(salaId, ct);
        if (sala is null || !sala.CzyAktywna)
        {
            return null;
        }

        var punktPowrotu = zPunktu is int id && id != sala.PunktWejsciowyId
            ? await repozytorium.PobierzPunktAsync(id, ct)
            : sala.PunktWejsciowy;

        PunktNaLiscie? powrot = punktPowrotu is { CzyAktywny: true }
            ? new PunktNaLiscie(AdresMiejsca.Z(punktPowrotu), punktPowrotu.Nazwa, punktPowrotu.AzymutDomyslny)
            : null;

        int? zwrotPowrotu = zwrotWejscia is int z && z % 90 == 0
            ? Azymuty.Normalizuj(z + 180)
            : powrot?.AzymutDomyslny;

        return new WidokSali(sala.Id, sala.Symbol, sala.Nazwa, sala.TypSali.Nazwa, sala.Opis, sala.OpisGlosowy,
            sala.Pietro.Budynek.Nazwa, sala.Pietro.Nazwa, sala.LiczbaMiejsc, sala.CzyDostepnaDlaWozkow,
            sala.Zdjecia.OrderBy(z => z.Kolejnosc)
                .Select(ZdjecieMiejsca.Z)
                .ToList(),
            sala.SalaUdogodnienia.Select(su => new UdogodnienieSali(su.Udogodnienie.Nazwa, su.Uwagi)).ToList(),
            powrot, zwrotPowrotu);
    }

    private static OpcjaKierunku ZbudujOpcje(PunktRuchu punkt, KierunekWzgledny wzgledny, int zwrot, DateTime teraz)
    {
        var azymut = Azymuty.NaBezwzgledny(wzgledny, zwrot);
        var krawedz = punkt.KierunkiWychodzace.SingleOrDefault(k => k.Azymut == azymut);
        var strona = Azymuty.NazwaStrony(wzgledny);

        if (krawedz is null)
        {
            return new OpcjaKierunku(wzgledny, false, $"{strona} — brak przejścia.", null, null, null, null);
        }

        var przeszkoda = Przeszkoda(krawedz, teraz);
        if (przeszkoda is not null)
        {
            return new OpcjaKierunku(wzgledny, false, $"{strona} — brak przejścia. {przeszkoda}".TrimEnd(),
                null, null, null, krawedz.OpisPrzejscia);
        }

        var tekst = $"{Azymuty.Etykieta(wzgledny)} — {NazwaCelu(krawedz)}, {Metry(krawedz.Waga)}";
        return new OpcjaKierunku(wzgledny, true, tekst,
            krawedz.PunktDocelowyId, krawedz.SalaDocelowaId, krawedz.Waga, krawedz.OpisPrzejscia, krawedz.Id);
    }

    /// <summary>
    /// Opis przeszkody, gdy krawędzią nie da się teraz przejść; <c>null</c>, gdy przejście jest wolne.
    /// Dla kierunku nieaktywnego zwraca jego opis orientacyjny (może być pusty — wtedy "").
    /// </summary>
    private static string? Przeszkoda(Kierunek krawedz, DateTime teraz)
    {
        if (!krawedz.CzyAktywny)
        {
            return krawedz.OpisPrzejscia ?? string.Empty;
        }

        var utrudnienie = AktywneUtrudnienie(krawedz.Utrudnienia, teraz)
            ?? (krawedz.PunktDocelowy is null ? null : AktywneUtrudnienie(krawedz.PunktDocelowy.Utrudnienia, teraz));
        if (utrudnienie is not null)
        {
            var doKiedy = utrudnienie.ObowiazujeDo is DateTime koniec
                ? $" Przejście będzie otwarte po {koniec.ToString("d MMMM yyyy", Polski)}."
                : string.Empty;
            return $"Przejście czasowo zamknięte: {utrudnienie.Przyczyna}.{doKiedy}";
        }

        if (krawedz.PunktDocelowy is { CzyAktywny: false })
        {
            return "Dalsza część korytarza jest niedostępna.";
        }

        if (krawedz.SalaDocelowa is { CzyAktywna: false })
        {
            return "Sala jest niedostępna.";
        }

        return null;
    }

    private static Utrudnienie? AktywneUtrudnienie(IEnumerable<Utrudnienie> utrudnienia, DateTime teraz) =>
        utrudnienia.FirstOrDefault(u => u.ObowiazujeOd <= teraz && (u.ObowiazujeDo is null || teraz <= u.ObowiazujeDo));

    /// <summary>„korytarz zachodni przy sali A12” albo „drzwi do sali A12, pracownia komputerowa” (06 §2.2).</summary>
    private static string NazwaCelu(Kierunek krawedz) => krawedz switch
    {
        { PunktDocelowy: { } p } => MalaLitera(p.Nazwa),
        { SalaDocelowa: { } s } => $"drzwi do sali {s.Symbol}, {MalaLitera(s.Nazwa)}",
        _ => "dalej",
    };

    private static string MalaLitera(string tekst) =>
        tekst.Length == 0 ? tekst : char.ToLower(tekst[0], Polski) + tekst[1..];

    /// <summary>Odległość z poprawną polską odmianą: 1 metr, 2–4 metry, 5 metrów, 22 metry, 0,5 metra.</summary>
    public static string Metry(decimal metry)
    {
        if (metry != decimal.Truncate(metry))
        {
            return $"{metry.ToString("0.#", Polski)} metra";
        }

        var n = (long)metry;
        var forma = n == 1 ? "metr"
            : n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? "metry"
            : "metrów";
        return $"{n} {forma}";
    }
}
