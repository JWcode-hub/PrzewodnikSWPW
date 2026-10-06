using Microsoft.EntityFrameworkCore;
using PrzewodnikSWPW.UnitTests.Data;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.UnitTests.Services;

/// <summary>
/// NawigacjaService na danych z seedera (zachodnia część parteru budynku A).
/// Wymaga SQL Server — patrz <see cref="BazaTestowaFixture"/>.
/// </summary>
[Collection(BazaTestowaKolekcja.Nazwa)]
[Trait("Kategoria", "Integracyjne")]
public class NawigacjaServiceTesty(BazaTestowaFixture baza)
{
    private static readonly int[] Zwroty = [0, 90, 180, 270];

    private static NawigacjaService Serwis(PrzewodnikDbContext db) =>
        new(new NawigacjaRepozytorium(db), TimeProvider.System);

    private static Task<int> IdPunktu(PrzewodnikDbContext db, string kod) =>
        db.PunktyRuchu.Where(p => p.Kod == kod).Select(p => p.Id).SingleAsync();

    [Fact]
    public async Task TamIZPowrotem_KazdaParaPolaczonychPunktow_WracaDoStartuZeZwrotemOdwroconym()
    {
        await using var db = baza.UtworzKontekst();
        var serwis = Serwis(db);
        var krawedzie = await db.Kierunki.AsNoTracking()
            .Where(k => k.CzyAktywny && k.PunktDocelowyId != null)
            .ToListAsync();
        Assert.NotEmpty(krawedzie);

        foreach (var k in krawedzie)
        {
            foreach (var zwrotStartowy in Zwroty)
            {
                var kontekst = $"krawędź {k.PunktZrodlowyId}->{k.PunktDocelowyId} (azymut {k.Azymut}), zwrot startowy {zwrotStartowy}";

                var kierunek = Azymuty.NaWzgledny(k.Azymut, zwrotStartowy);
                var tam = await serwis.Przejdz(k.PunktZrodlowyId, kierunek, zwrotStartowy);

                Assert.True(tam is { Rodzaj: RodzajWynikuPrzejscia.DoPunktu }, $"Brak przejścia tam: {kontekst}");
                Assert.Equal(k.PunktDocelowyId, tam.PunktId);
                Assert.Equal(k.Azymut, tam.Zwrot);

                var zPowrotem = await serwis.Przejdz(tam.PunktId, KierunekWzgledny.DoTylu, tam.Zwrot);

                Assert.True(zPowrotem is { Rodzaj: RodzajWynikuPrzejscia.DoPunktu }, $"Brak przejścia z powrotem: {kontekst}");
                Assert.Equal(k.PunktZrodlowyId, zPowrotem.PunktId);
                Assert.Equal((k.Azymut + 180) % 360, zPowrotem.Zwrot);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public async Task PrzejscieWKierunkuNieaktywnym_NieZmieniaPolozeniaIZwracaOpisPrzeszkody(int zwrot)
    {
        await using var db = baza.UtworzKontekst();
        var nieaktywny = await db.Kierunki.AsNoTracking().SingleAsync(k => !k.CzyAktywny);
        var kierunek = Azymuty.NaWzgledny(nieaktywny.Azymut, zwrot);

        var wynik = await Serwis(db).Przejdz(nieaktywny.PunktZrodlowyId, kierunek, zwrot);

        Assert.NotNull(wynik);
        Assert.Equal(RodzajWynikuPrzejscia.BrakPrzejscia, wynik.Rodzaj);
        Assert.False(wynik.CzyPrzeszedl);
        Assert.Equal(nieaktywny.PunktZrodlowyId, wynik.PunktId);
        Assert.Equal(zwrot, wynik.Zwrot);
        Assert.Null(wynik.SalaId);
        Assert.Contains("brak przejścia", wynik.Opis);
        Assert.Contains("ściana z gablotą informacyjną", wynik.Opis);
    }

    [Fact]
    public async Task PrzejscieWKierunkuNieistniejacym_NieZmieniaPolozenia()
    {
        await using var db = baza.UtworzKontekst();
        var wejscie = await IdPunktu(db, "A-0-P01"); // z wejścia prowadzi tylko jeden kierunek: azymut 0

        var wynik = await Serwis(db).Przejdz(wejscie, KierunekWzgledny.WLewo, 0);

        Assert.NotNull(wynik);
        Assert.Equal(RodzajWynikuPrzejscia.BrakPrzejscia, wynik.Rodzaj);
        Assert.Equal(wejscie, wynik.PunktId);
        Assert.Equal(0, wynik.Zwrot);
        Assert.Equal("W lewo — brak przejścia.", wynik.Opis);
    }

    [Fact]
    public async Task PrzejscieDoSali_ZwracaSaleIZostawiaPunktPrzedDrzwiami()
    {
        await using var db = baza.UtworzKontekst();
        var p04 = await IdPunktu(db, "A-0-P04");
        var a12 = await db.Sale.Where(s => s.Symbol == "A12").Select(s => s.Id).SingleAsync();

        // Z P04 zwrócony na zachód (270) drzwi A12 (azymut 0) są po prawej.
        var wynik = await Serwis(db).Przejdz(p04, KierunekWzgledny.WPrawo, 270);

        Assert.NotNull(wynik);
        Assert.Equal(RodzajWynikuPrzejscia.DoSali, wynik.Rodzaj);
        Assert.Equal(a12, wynik.SalaId);
        Assert.Equal(p04, wynik.PunktId);
        Assert.Equal(0, wynik.Zwrot);
    }

    [Fact]
    public async Task WidokMiejsca_ZawszeCzteryKierunkiWKolejnosciD01_DlaKazdegoPunktuIZwrotu()
    {
        await using var db = baza.UtworzKontekst();
        var serwis = Serwis(db);
        KierunekWzgledny[] oczekiwana =
            [KierunekWzgledny.Prosto, KierunekWzgledny.WLewo, KierunekWzgledny.WPrawo, KierunekWzgledny.DoTylu];

        foreach (var punktId in await db.PunktyRuchu.Select(p => p.Id).ToListAsync())
        {
            foreach (var zwrot in Zwroty)
            {
                var widok = await serwis.PobierzMiejsce(punktId, zwrot);

                Assert.NotNull(widok);
                Assert.Equal(oczekiwana, widok.Kierunki.Select(k => k.Kierunek));
                Assert.Equal(zwrot, widok.Zwrot);
            }
        }
    }

    [Fact]
    public async Task WidokMiejsca_P04ZwroconyNaZachod_OpisujeWszystkieCzteryStrony()
    {
        await using var db = baza.UtworzKontekst();
        var p04 = await IdPunktu(db, "A-0-P04");
        var p05 = await IdPunktu(db, "A-0-P05");
        var p03 = await IdPunktu(db, "A-0-P03");

        var widok = await Serwis(db).PobierzMiejsce(p04, 270);

        Assert.NotNull(widok);
        var (prosto, lewo, prawo, tyl) = (widok.Kierunki[0], widok.Kierunki[1], widok.Kierunki[2], widok.Kierunki[3]);

        Assert.True(prosto.CzyMozliwy);
        Assert.Equal(p05, prosto.PunktDocelowyId);
        Assert.Equal("Idź prosto — korytarz zachodni przy salach A14 i A15, 6 metrów", prosto.Tekst);

        Assert.False(lewo.CzyMozliwy);
        Assert.StartsWith("W lewo — brak przejścia.", lewo.Tekst);
        Assert.Contains("ściana z gablotą informacyjną", lewo.Tekst);

        Assert.True(prawo.CzyMozliwy);
        Assert.NotNull(prawo.SalaDocelowaId);
        Assert.Equal("Skręć w prawo — drzwi do sali A12, pracownia komputerowa, 1 metr", prawo.Tekst);

        Assert.True(tyl.CzyMozliwy);
        Assert.Equal(p03, tyl.PunktDocelowyId);
        Assert.StartsWith("Zawróć — ", tyl.Tekst);
    }

    [Fact]
    public async Task WidokMiejsca_KierunekNieistniejacy_JestNaLiscieJakoBrakPrzejscia()
    {
        await using var db = baza.UtworzKontekst();
        var wejscie = await IdPunktu(db, "A-0-P01");

        var widok = await Serwis(db).PobierzMiejsce(wejscie, 0);

        Assert.NotNull(widok);
        Assert.Equal(4, widok.Kierunki.Count);
        Assert.Single(widok.Kierunki, k => k.CzyMozliwy);
        Assert.Equal("W lewo — brak przejścia.", widok.Kierunki[1].Tekst);
        Assert.Equal("Do tyłu — brak przejścia.", widok.Kierunki[3].Tekst);
    }

    [Fact]
    public async Task AktywneUtrudnienie_BlokujePrzejscieIPodajePrzyczyne()
    {
        await using var db = baza.UtworzKontekst();
        var p03 = await IdPunktu(db, "A-0-P03");
        var krawedz = await db.Kierunki.SingleAsync(k => k.PunktZrodlowyId == p03 && k.Azymut == 270);
        var utrudnienie = new Utrudnienie
        {
            KierunekId = krawedz.Id,
            Przyczyna = "remont korytarza zachodniego",
            ObowiazujeOd = DateTime.Now.AddHours(-1),
            ObowiazujeDo = DateTime.Now.AddDays(1),
        };
        db.Utrudnienia.Add(utrudnienie);
        await db.SaveChangesAsync();

        try
        {
            var wynik = await Serwis(db).Przejdz(p03, KierunekWzgledny.WLewo, 0);

            Assert.NotNull(wynik);
            Assert.Equal(RodzajWynikuPrzejscia.BrakPrzejscia, wynik.Rodzaj);
            Assert.Equal(p03, wynik.PunktId);
            Assert.Contains("remont korytarza zachodniego", wynik.Opis);
        }
        finally
        {
            db.Utrudnienia.Remove(utrudnienie);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task NieistniejacyPunkt_ZwracaNull()
    {
        await using var db = baza.UtworzKontekst();
        var serwis = Serwis(db);

        Assert.Null(await serwis.PobierzMiejsce(-1, 0));
        Assert.Null(await serwis.Przejdz(-1, KierunekWzgledny.Prosto, 0));
    }
}
