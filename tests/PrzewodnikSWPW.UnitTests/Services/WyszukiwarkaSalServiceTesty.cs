using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PrzewodnikSWPW.UnitTests.Data;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.UnitTests.Services;

/// <summary>Wyszukiwanie sal na danych z seedera (UC-06). Wymaga SQL Server — patrz <see cref="BazaTestowaFixture"/>.</summary>
[Collection(BazaTestowaKolekcja.Nazwa)]
[Trait("Kategoria", "Integracyjne")]
public class WyszukiwarkaSalServiceTesty(BazaTestowaFixture baza)
{
    private static WyszukiwarkaSalService Serwis(PrzewodnikDbContext db) =>
        new(new WyszukiwarkaRepozytorium(db), NullLogger<WyszukiwarkaSalService>.Instance);

    [Theory]
    [InlineData("15", "A15")]
    [InlineData("A15", "A15")]
    [InlineData("a 15", "A15")]
    [InlineData("A-15", "A15")]
    [InlineData("sala 15", "A15")]
    [InlineData("pokój 15", "A15")]
    [InlineData("dziekanat", "A16")]
    [InlineData("Sekretariat Wydziału Informatyki", "A16")]
    [InlineData("sekretariat wydzialu", "A16")]
    [InlineData("łazienka", "WC-0-Z")]
    [InlineData("lazienka", "WC-0-Z")]
    [InlineData("pracownia sieci", "A15")]
    public async Task Szukaj_WariantZapisu_ZnajdujeSaleNaPierwszymMiejscu(string fraza, string symbol)
    {
        await using var db = baza.UtworzKontekst();

        var wynik = await Serwis(db).Szukaj(fraza);

        Assert.NotEmpty(wynik.Wyniki);
        Assert.Equal(symbol, wynik.Wyniki[0].Symbol);
    }

    [Fact]
    public async Task Szukaj_15_NieZwracaInnychSal()
    {
        await using var db = baza.UtworzKontekst();

        var wynik = await Serwis(db).Szukaj("15");

        Assert.Equal(["A15"], wynik.Wyniki.Select(s => s.Symbol));
    }

    [Fact]
    public async Task BrakWyniku_ProponujeNajblizszaSaleIListeSalBudynku()
    {
        await using var db = baza.UtworzKontekst();

        var wynik = await Serwis(db).Szukaj("A51");

        Assert.Empty(wynik.Wyniki);
        Assert.Contains(wynik.Propozycje, s => s.Symbol == "A15");
        Assert.Equal(["A"], wynik.Budynki.Select(b => b.Kod));
    }

    [Fact]
    public async Task BrakWyniku_NiePodobnaFraza_BezPropozycjiAleZListaSal()
    {
        await using var db = baza.UtworzKontekst();

        var wynik = await Serwis(db).Szukaj("A99");

        Assert.Empty(wynik.Wyniki);
        Assert.Empty(wynik.Propozycje);
        Assert.NotEmpty(wynik.Budynki);
    }

    [Fact]
    public async Task Szukaj_ZapisujeZapytanieZWynikiemIBezWyniku()
    {
        await using var db = baza.UtworzKontekst();
        var znacznik = $"test-{Guid.NewGuid():N}"[..20];

        await Serwis(db).Szukaj($"dziekanat {znacznik}"); // bez wyniku — znacznika nie ma w danych
        await Serwis(db).Szukaj("dziekanat");

        var bezWyniku = await db.ZapytaniaTrasy.SingleAsync(z => z.FrazaZ == $"dziekanat {znacznik}");
        Assert.False(bezWyniku.CzySukces);
        Assert.Null(bezWyniku.SalaDoId);
        Assert.Contains(await db.ZapytaniaTrasy.Where(z => z.FrazaZ == "dziekanat").ToListAsync(), z => z.CzySukces);
    }

    [Fact]
    public async Task Podpowiedzi_NieSaZapisywane()
    {
        await using var db = baza.UtworzKontekst();
        var przed = await db.ZapytaniaTrasy.CountAsync();

        var podpowiedzi = await Serwis(db).Podpowiedzi("a1");

        Assert.Equal(["A12", "A14", "A15", "A16"], podpowiedzi.Select(s => s.Symbol));
        Assert.Equal(przed, await db.ZapytaniaTrasy.CountAsync());
    }

    [Fact]
    public async Task SaleBudynku_ZwracaWszystkieSale()
    {
        await using var db = baza.UtworzKontekst();

        var wynik = await Serwis(db).SaleBudynku("a");

        Assert.NotNull(wynik);
        Assert.Equal(5, wynik.Value.Sale.Count);
    }
}
