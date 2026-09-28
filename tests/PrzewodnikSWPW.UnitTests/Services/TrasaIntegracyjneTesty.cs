using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PrzewodnikSWPW.UnitTests.Data;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.UnitTests.Services;

/// <summary>Trasa i cache grafu na danych z seedera. Wymaga SQL Server — patrz <see cref="BazaTestowaFixture"/>.</summary>
[Collection(BazaTestowaKolekcja.Nazwa)]
[Trait("Kategoria", "Integracyjne")]
public sealed class TrasaIntegracyjneTesty : IDisposable
{
    private readonly BazaTestowaFixture _baza;
    private readonly ServiceProvider _uslugi;
    private readonly GrafBudynkuCache _cache;

    public TrasaIntegracyjneTesty(BazaTestowaFixture baza)
    {
        _baza = baza;
        _uslugi = new ServiceCollection()
            .AddScoped(_ => baza.UtworzKontekst())
            .AddScoped<ITrasaRepozytorium, TrasaRepozytorium>()
            .BuildServiceProvider();
        _cache = new GrafBudynkuCache(new MemoryCache(new MemoryCacheOptions()), _uslugi.GetRequiredService<IServiceScopeFactory>());
    }

    public void Dispose() => _uslugi.Dispose();

    private WyszukiwarkaTrasService Serwis(PrzewodnikDbContext db) =>
        new(new TrasaRepozytorium(db), _cache, new GeneratorOpisuService(),
            Options.Create(new KontaktAlternatywnyOptions()), NullLogger<WyszukiwarkaTrasService>.Instance);

    private static Task<int> IdSali(PrzewodnikDbContext db, string symbol) =>
        db.Sale.Where(s => s.Symbol == symbol).Select(s => s.Id).SingleAsync();

    [Fact]
    public async Task A16_do_A12_TrasaNajkrotszaZInstrukcjamiIZapisem()
    {
        await using var db = _baza.UtworzKontekst();
        var a16 = await IdSali(db, "A16");
        var a12 = await IdSali(db, "A12");

        var wynik = await Serwis(db).Wyznacz(a16, a12, trybWindy: false, DateTime.Now);

        Assert.Equal(RodzajWynikuTrasy.Znaleziona, wynik.Rodzaj);
        var trasa = wynik.Trasa!;
        Assert.Equal(13m, trasa.DlugoscMetry);                 // P09→P03 7 m + P03→P04 5 m + drzwi 1 m
        Assert.Equal(4, trasa.LiczbaKrokow);
        // Z sekretariatu (drzwi na północ) wychodzi się twarzą na południe; zachód jest wtedy po prawej.
        Assert.StartsWith("Wyjdź z sali A16 na korytarz.", trasa.Instrukcje[0].Tekst);
        Assert.StartsWith("Skręć w prawo i idź 7 metrów.", trasa.Instrukcje[1].Tekst);
        Assert.StartsWith("Idź prosto 5 metrów.", trasa.Instrukcje[2].Tekst);
        Assert.StartsWith("Skręć w prawo i idź 1 metr.", trasa.Instrukcje[3].Tekst);
        Assert.EndsWith("Wejdź do sali A12 — Pracownia komputerowa.", trasa.Instrukcje[3].Tekst);

        var zapis = await db.ZapytaniaTrasy.OrderByDescending(z => z.Id).FirstAsync();
        Assert.Equal((a16, a12, true, 13m, 4), (zapis.SalaZId!.Value, zapis.SalaDoId!.Value, zapis.CzySukces, zapis.DlugoscMetry!.Value, zapis.LiczbaKrokow!.Value));
        Assert.NotNull(zapis.CzasMs);
    }

    [Fact]
    public async Task TrybWindy_NaParterzeBezSchodow_TrasaTaSama()
    {
        await using var db = _baza.UtworzKontekst();

        var wynik = await Serwis(db).Wyznacz(await IdSali(db, "A16"), await IdSali(db, "WC-0-Z"), trybWindy: true, DateTime.Now);

        Assert.Equal(RodzajWynikuTrasy.Znaleziona, wynik.Rodzaj);
        Assert.False(wynik.Trasa!.CzyZawieraSchody);
    }

    [Fact]
    public async Task CacheGrafu_DrugiOdczytZPamieci_PoUniewaznieniuWczytanyOdNowa()
    {
        await using var db = _baza.UtworzKontekst();
        var budynekId = await db.Budynki.Select(b => b.Id).SingleAsync();

        var pierwszy = await _cache.PobierzGrafAsync(budynekId);
        var drugi = await _cache.PobierzGrafAsync(budynekId);
        _cache.Uniewaznij(budynekId);
        var poUniewaznieniu = await _cache.PobierzGrafAsync(budynekId);

        Assert.Equal(9, pierwszy.Wezly.Count);
        Assert.Equal(22, pierwszy.Wychodzace.Values.Sum(k => k.Count));
        Assert.Same(pierwszy, drugi);
        Assert.NotSame(pierwszy, poUniewaznieniu);
    }

    /// <summary>
    /// Sprawa otwarta O-05 (docs/09_DECYZJE.md): opisy przejść w danych zawierają strony względne
    /// („po prawej”), które są prawdziwe tylko przy jednym kierunku dojścia. Test uaktywnić po poprawieniu danych.
    /// </summary>
    [Fact(Skip = "O-05: OpisPrzejscia w 02_dane_poczatkowe.sql zawiera strony względne — czeka na decyzję zespołu")]
    public async Task Instrukcje_NiePrzeczaStronomWOpisachPrzejsc()
    {
        await using var db = _baza.UtworzKontekst();
        var sale = await db.Sale.Select(s => s.Id).ToListAsync();
        var sprzecznosci = new List<string>();

        foreach (var z in sale)
        {
            foreach (var @do in sale.Where(d => d != z))
            {
                var wynik = await Serwis(db).Wyznacz(z, @do, false, DateTime.Now);
                sprzecznosci.AddRange(wynik.Trasa?.Instrukcje
                    .Where(i => (i.Tekst.StartsWith("Skręć w lewo") && i.Tekst.Contains("po prawej"))
                             || (i.Tekst.StartsWith("Skręć w prawo") && i.Tekst.Contains("po lewej"))
                             || (i.Tekst.StartsWith("Idź prosto") && i.Tekst.Contains("Skręć")))
                    .Select(i => i.Tekst) ?? []);
            }
        }

        Assert.Empty(sprzecznosci.Distinct());
    }
}
