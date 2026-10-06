using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;
using Xunit.Abstractions;

namespace PrzewodnikSWPW.UnitTests.Services;

/// <summary>
/// Wyznaczanie trasy na małych grafach w pamięci — przypadki brzegowe z 04_BAZA_DANYCH.md rozdz. 8
/// i UC-07/UC-08. Bez bazy danych: repozytorium i cache grafu są podstawione.
/// </summary>
public class WyszukiwarkaTrasServiceTesty(ITestOutputHelper wyjscie)
{
    private const int Budynek = 1;
    private static readonly DateTime Dzis = new(2026, 10, 1, 12, 0, 0);

    // --- pomocnicze budowanie grafu -------------------------------------------------------------

    private static WezelGrafu W(int id, params OkresUtrudnienia[] utrudnienia) => new(id, $"Punkt {id}", 0, true, utrudnienia);

    private static KrawedzGrafu K(int id, int z, int doPunktu, int azymut, decimal waga,
        RodzajPrzejscia rodzaj = RodzajPrzejscia.Korytarz, bool aktywny = true, string? opis = null, params OkresUtrudnienia[] utrudnienia) =>
        new(id, z, doPunktu, null, azymut, waga, rodzaj, aktywny, rodzaj != RodzajPrzejscia.Schody, opis, utrudnienia);

    private static KrawedzGrafu Drzwi(int id, int z, int doSali, int azymut) =>
        new(id, z, null, doSali, azymut, 1m, RodzajPrzejscia.Drzwi, true, true, null, []);

    private static Sala Sala(int id, string symbol, int? punktWejsciowy, int budynek = Budynek) => new()
    {
        Id = id,
        Symbol = symbol,
        Nazwa = $"Sala {symbol}",
        CzyAktywna = true,
        PunktWejsciowyId = punktWejsciowy,
        Pietro = new Pietro { BudynekId = budynek, Budynek = new Budynek { Id = budynek, Kod = $"B{budynek}" } },
    };

    private sealed class Repozytorium(params Sala[] sale) : ITrasaRepozytorium
    {
        public List<ZapytanieTrasy> Zapisane { get; } = [];
        public Task<DaneGrafu> PobierzDaneGrafuAsync(int budynekId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<Sala>> PobierzSaleAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
            Task.FromResult(sale.Where(s => ids.Contains(s.Id)).ToList());
        public Task<List<Sala>> PobierzAktywneSaleAsync(CancellationToken ct = default) => Task.FromResult(sale.ToList());
        public Task ZapiszZapytanieAsync(ZapytanieTrasy zapytanie, CancellationToken ct = default)
        {
            Zapisane.Add(zapytanie);
            return Task.CompletedTask;
        }
    }

    private sealed class Cache(GrafBudynku graf) : IGrafBudynkuCache
    {
        public Task<GrafBudynku> PobierzGrafAsync(int budynekId, CancellationToken ct = default) => Task.FromResult(graf);
        public void Uniewaznij(int? budynekId = null) { }
    }

    private static readonly KontaktAlternatywnyOptions Kontakt = new() { Opis = "Zapytaj w portierni.", Telefon = "24 000 00 00" };

    private static WyszukiwarkaTrasService Serwis(GrafBudynku graf, Repozytorium repo) =>
        new(repo, new Cache(graf), new GeneratorOpisuService(), Options.Create(Kontakt), NullLogger<WyszukiwarkaTrasService>.Instance);

    private static GrafBudynku Graf(IEnumerable<WezelGrafu> wezly, IEnumerable<KrawedzGrafu> krawedzie, params Sala[] sale) =>
        new(Budynek, wezly, krawedzie, sale.Select(s => new SalaWGrafie(s.Id, s.Symbol, s.Nazwa)));

    // --- a) start == cel ------------------------------------------------------------------------

    [Fact]
    public async Task A_StartRownyCel_JestesJuzNaMiejscu()
    {
        var sala = Sala(10, "A12", 1);
        var repo = new Repozytorium(sala);

        var wynik = await Serwis(Graf([W(1)], [], sala), repo).Wyznacz(10, 10, trybWindy: false, Dzis);

        Assert.Equal(RodzajWynikuTrasy.NaMiejscu, wynik.Rodzaj);
        Assert.Contains("Jesteś już na miejscu", wynik.Komunikat);
        Assert.Null(wynik.Trasa);
        Assert.True(Assert.Single(repo.Zapisane).CzySukces);
    }

    [Fact]
    public async Task A_DwieSaleZTymSamymPunktemWejsciowym_ToNieJestNaMiejscu()
    {
        // A14 i A15 mają wspólny punkt przed drzwiami (jak P05 w danych budynku A) — trasa to wyjście i wejście naprzeciwko.
        var a14 = Sala(14, "A14", 5);
        var a15 = Sala(15, "A15", 5);
        var graf = Graf([W(5)], [Drzwi(1, 5, 14, 0), Drzwi(2, 5, 15, 180)], a14, a15);

        var wynik = await Serwis(graf, new Repozytorium(a14, a15)).Wyznacz(14, 15, false, Dzis);

        Assert.Equal(RodzajWynikuTrasy.Znaleziona, wynik.Rodzaj);
        Assert.Equal(["Wyjdź z sali A14 na korytarz.", "Idź prosto 1 metr. Wejdź do sali A15 — Sala A15."],
            wynik.Trasa!.Instrukcje.Select(i => i.Tekst));
    }

    // --- b) tryb windy: brak trasy bez schodów, ale ze schodami jest ----------------------------

    [Fact]
    public async Task B_TrybWindy_TylkoZeSchodami_ProponujeTraseAlternatywnaOznaczonaJakoSchody()
    {
        var z = Sala(10, "A12", 1);
        var @do = Sala(20, "B20", 3);
        var graf = Graf([W(1), W(2), W(3)],
            [K(1, 1, 2, 0, 5), K(2, 2, 1, 180, 5), K(3, 2, 3, 0, 4, RodzajPrzejscia.Schody), K(4, 3, 2, 180, 4, RodzajPrzejscia.Schody)],
            z, @do);
        var repo = new Repozytorium(z, @do);

        var wynik = await Serwis(graf, repo).Wyznacz(10, 20, trybWindy: true, Dzis);

        Assert.Equal(RodzajWynikuTrasy.TylkoZeSchodami, wynik.Rodzaj);
        Assert.Contains("Nie ma trasy bez schodów", wynik.Komunikat);
        Assert.Contains("ZAWIERA SCHODY", wynik.Komunikat);
        Assert.NotNull(wynik.Trasa);
        Assert.True(wynik.Trasa.CzyZawieraSchody);
        Assert.Contains(wynik.Trasa.Instrukcje, i => i.CzySchody && i.Tekst.Contains("schody"));
        Assert.NotNull(wynik.KontaktAlternatywny);
        Assert.False(Assert.Single(repo.Zapisane).CzySukces); // tryb windy nie został spełniony — to luka w dostępności
    }

    [Fact]
    public async Task B_TrybWindy_WybieraDluzszaTraseWinda_ZamiastKrotszejSchodami()
    {
        var z = Sala(10, "A", 1);
        var @do = Sala(20, "B", 3);
        var graf = Graf([W(1), W(3), W(4)],
            [K(1, 1, 3, 0, 5, RodzajPrzejscia.Schody), K(2, 1, 4, 90, 20, RodzajPrzejscia.Winda), K(3, 4, 3, 0, 5)],
            z, @do);

        var zwykly = await Serwis(graf, new Repozytorium(z, @do)).Wyznacz(10, 20, trybWindy: false, Dzis);
        var winda = await Serwis(graf, new Repozytorium(z, @do)).Wyznacz(10, 20, trybWindy: true, Dzis);

        Assert.Equal(5m, zwykly.Trasa!.DlugoscMetry);
        Assert.True(zwykly.Trasa.CzyZawieraSchody);
        Assert.Equal(RodzajWynikuTrasy.Znaleziona, winda.Rodzaj);
        Assert.Equal(25m, winda.Trasa!.DlugoscMetry);
        Assert.False(winda.Trasa.CzyZawieraSchody);
        Assert.Contains(winda.Trasa.Instrukcje, i => i.Tekst.Contains("Skorzystaj z windy"));
    }

    // --- c) brak jakiejkolwiek trasy ------------------------------------------------------------

    [Fact]
    public async Task C_GrafRozspojniony_KomunikatZPrzyczynaIKontaktemAlternatywnym()
    {
        var z = Sala(10, "A12", 1);
        var @do = Sala(20, "B20", 2);
        var repo = new Repozytorium(z, @do);

        var wynik = await Serwis(Graf([W(1), W(2)], [], z, @do), repo).Wyznacz(10, 20, false, Dzis);

        Assert.Equal(RodzajWynikuTrasy.BrakTrasy, wynik.Rodzaj);
        Assert.Contains("brakuje połączenia", wynik.Komunikat);
        Assert.NotNull(wynik.KontaktAlternatywny);
        Assert.Equal("24 000 00 00", wynik.KontaktAlternatywny.Telefon);
        Assert.Equal("240000000", wynik.KontaktAlternatywny.TelefonHref); // „24 000 00 00” bez spacji
        var zapis = Assert.Single(repo.Zapisane);
        Assert.False(zapis.CzySukces);
        Assert.Null(zapis.DlugoscMetry);
    }

    [Fact]
    public async Task C_UtrudnienieBlokujeJedynePrzejscie_PrzyczynaWKomunikacie()
    {
        var z = Sala(10, "A12", 1);
        var @do = Sala(20, "B20", 2);
        var remont = new OkresUtrudnienia(Dzis.AddDays(-1), Dzis.AddDays(7), "remont korytarza");
        var graf = Graf([W(1), W(2)], [K(1, 1, 2, 0, 5, utrudnienia: remont)], z, @do);

        var wynik = await Serwis(graf, new Repozytorium(z, @do)).Wyznacz(10, 20, false, Dzis);
        var poRemoncie = await Serwis(graf, new Repozytorium(z, @do)).Wyznacz(10, 20, false, Dzis.AddDays(8));

        Assert.Equal(RodzajWynikuTrasy.BrakTrasy, wynik.Rodzaj);
        Assert.Contains("czasowo zamknięte", wynik.Komunikat);
        Assert.Contains("remont korytarza", wynik.Komunikat);
        Assert.NotNull(wynik.KontaktAlternatywny);
        Assert.Equal(RodzajWynikuTrasy.Znaleziona, poRemoncie.Rodzaj); // utrudnienie liczy się na podaną datę
    }

    [Fact]
    public async Task C_KrawedzNieaktywna_NieJestUzywana()
    {
        var z = Sala(10, "A", 1);
        var @do = Sala(20, "B", 2);
        var graf = Graf([W(1), W(2)], [K(1, 1, 2, 0, 5, aktywny: false)], z, @do);

        var wynik = await Serwis(graf, new Repozytorium(z, @do)).Wyznacz(10, 20, false, Dzis);

        Assert.Equal(RodzajWynikuTrasy.BrakTrasy, wynik.Rodzaj);
    }

    [Fact]
    public async Task C_SaleWRoznychBudynkach_BrakTrasyZPrzyczyna()
    {
        var z = Sala(10, "A12", 1, budynek: 1);
        var @do = Sala(20, "B20", 2, budynek: 2);

        var wynik = await Serwis(Graf([W(1)], [], z), new Repozytorium(z, @do)).Wyznacz(10, 20, false, Dzis);

        Assert.Equal(RodzajWynikuTrasy.BrakTrasy, wynik.Rodzaj);
        Assert.Contains("różnych budynkach", wynik.Komunikat);
        Assert.NotNull(wynik.KontaktAlternatywny);
    }

    // --- d) sala bez punktu wejściowego ---------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task D_SalaBezPunktuWejsciowego_CzytelnyBladZamiastWyjatku(bool brakWSaliDocelowej)
    {
        var z = Sala(10, "A12", brakWSaliDocelowej ? 1 : null);
        var @do = Sala(20, "B20", brakWSaliDocelowej ? null : 2);
        var repo = new Repozytorium(z, @do);

        var wynik = await Serwis(Graf([W(1), W(2)], [], z, @do), repo).Wyznacz(10, 20, false, Dzis);

        Assert.Equal(RodzajWynikuTrasy.BladDanych, wynik.Rodzaj);
        Assert.Contains(brakWSaliDocelowej ? "B20" : "A12", wynik.Komunikat);
        Assert.Contains("wejścia", wynik.Komunikat);
        Assert.NotNull(wynik.KontaktAlternatywny);
        Assert.False(Assert.Single(repo.Zapisane).CzySukces);
    }

    [Fact]
    public async Task D_NieistniejacaSala_CzytelnyBlad()
    {
        var z = Sala(10, "A12", 1);

        var wynik = await Serwis(Graf([W(1)], [], z), new Repozytorium(z)).Wyznacz(10, 999, false, Dzis);

        Assert.Equal(RodzajWynikuTrasy.BladDanych, wynik.Rodzaj);
        Assert.Contains("sali docelowej", wynik.Komunikat);
    }

    // --- algorytm ---------------------------------------------------------------------------------

    [Fact]
    public void Dijkstra_WybieraTraseNajkrotszaMetrycznie_NieONajmniejszejLiczbiePunktow()
    {
        // 1→2 bezpośrednio 10 m; 1→3→4→2 razem 6 m.
        var graf = Graf([W(1), W(2), W(3), W(4)],
            [K(1, 1, 2, 0, 10), K(2, 1, 3, 90, 2), K(3, 3, 4, 0, 2), K(4, 4, 2, 270, 2)]);

        var sciezka = WyszukiwarkaTrasService.Najkrotsza(graf, 1, 2, _ => true);

        Assert.Equal([2, 3, 4], sciezka!.Select(k => k.Id));
    }

    [Fact]
    public async Task Wydajnosc_Graf400Wierzcholkow_TrasaPonizej300ms()
    {
        // Siatka 20×20 = 400 punktów, krawędzie w obie strony do czterech sąsiadów, wagi 1–10 m.
        const int n = 20;
        var losowe = new Random(2026);
        int Id(int x, int y) => y * n + x + 1;
        var wezly = Enumerable.Range(0, n * n).Select(i => W(i + 1)).ToList();
        var krawedzie = new List<KrawedzGrafu>();
        var kolejnyId = 1;
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                if (x + 1 < n)
                {
                    var waga = losowe.Next(1, 11);
                    krawedzie.Add(K(kolejnyId++, Id(x, y), Id(x + 1, y), 90, waga));
                    krawedzie.Add(K(kolejnyId++, Id(x + 1, y), Id(x, y), 270, waga));
                }
                if (y + 1 < n)
                {
                    var waga = losowe.Next(1, 11);
                    krawedzie.Add(K(kolejnyId++, Id(x, y), Id(x, y + 1), 0, waga));
                    krawedzie.Add(K(kolejnyId++, Id(x, y + 1), Id(x, y), 180, waga));
                }
            }
        }

        var start = Sala(10, "START", Id(0, 0));
        var cel = Sala(20, "CEL", Id(n - 1, n - 1));
        var serwis = Serwis(Graf(wezly, krawedzie, start, cel), new Repozytorium(start, cel));

        // Pierwsze wywołanie liczone razem z kompilacją JIT — najgorszy przypadek.
        var stoper = Stopwatch.StartNew();
        var wynik = await serwis.Wyznacz(10, 20, trybWindy: false, Dzis);
        stoper.Stop();

        var kolejne = new List<double>();
        for (var i = 0; i < 50; i++)
        {
            var s = Stopwatch.StartNew();
            await serwis.Wyznacz(10, 20, trybWindy: true, Dzis);
            kolejne.Add(s.Elapsed.TotalMilliseconds);
        }

        wyjscie.WriteLine($"Graf: {wezly.Count} wierzchołków, {krawedzie.Count} krawędzi. " +
            $"Pierwsze wyznaczenie: {stoper.Elapsed.TotalMilliseconds:F1} ms, kolejne (max z 50): {kolejne.Max():F2} ms.");

        Assert.Equal(RodzajWynikuTrasy.Znaleziona, wynik.Rodzaj);
        Assert.True(stoper.Elapsed.TotalMilliseconds < 300, $"Pierwsze wyznaczenie trwało {stoper.Elapsed.TotalMilliseconds:F1} ms.");
        Assert.True(kolejne.Max() < 300, $"Najwolniejsze z kolejnych trwało {kolejne.Max():F1} ms.");
    }
}
