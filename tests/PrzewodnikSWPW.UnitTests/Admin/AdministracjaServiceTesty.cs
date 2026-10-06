using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.UnitTests.Admin;

/// <summary>Reguły panelu na prawdziwej bazie: kierunki powrotne (WF-24), usuwanie logiczne, rejestr zmian (WF-28).</summary>
[Collection(AplikacjaKolekcja.Nazwa)]
[Trait("Kategoria", "Integracyjne")]
public class AdministracjaServiceTesty(AplikacjaFixture app)
{
    private sealed class Uzytkownik(string? id) : IBiezacyUzytkownik
    {
        public string? Id => id;
    }

    private static AdministracjaService Serwis(PrzewodnikDbContext db) => new(new AdministracjaRepozytorium(db));

    /// <summary>Dwa nowe punkty na parterze budynku A — każdy test pracuje na własnych, by nie kolidować z innymi.</summary>
    private static async Task<(PunktRuchu A, PunktRuchu B)> DwaPunkty(PrzewodnikDbContext db)
    {
        var pietro = await db.Pietra.FirstAsync();
        var typ = await db.TypyPunktow.FirstAsync();
        var znacznik = Guid.NewGuid().ToString("N")[..8];
        var a = new PunktRuchu { PietroId = pietro.Id, TypPunktuId = typ.Id, Kod = $"T-{znacznik}-A", Nazwa = "Testowy A" };
        var b = new PunktRuchu { PietroId = pietro.Id, TypPunktuId = typ.Id, Kod = $"T-{znacznik}-B", Nazwa = "Testowy B" };
        db.PunktyRuchu.AddRange(a, b);
        await db.SaveChangesAsync();
        return (a, b);
    }

    [Fact]
    public async Task DodajKierunek_ZPowrotnym_TworzyParePowiazanaZAzymutemOdwroconym()
    {
        await using var db = app.Baza.UtworzKontekst();
        var (a, b) = await DwaPunkty(db);

        var wynik = await Serwis(db).DodajKierunek(new Kierunek
        {
            PunktZrodlowyId = a.Id, PunktDocelowyId = b.Id, Azymut = 90, Waga = 7.5m, RodzajPrzejscia = RodzajPrzejscia.Korytarz,
            OpisPrzejscia = "Wzdłuż gablot.",
        }, utworzPowrotny: true, opisPowrotny: "Wzdłuż gablot, w stronę holu.");

        Assert.True(wynik.Sukces, string.Join(" ", wynik.Bledy.Select(x => x.Komunikat)));
        var tam = await db.Kierunki.AsNoTracking().SingleAsync(k => k.PunktZrodlowyId == a.Id);
        var zPowrotem = await db.Kierunki.AsNoTracking().SingleAsync(k => k.PunktZrodlowyId == b.Id);
        Assert.Equal((b.Id, 90, 7.5m), (tam.PunktDocelowyId!.Value, tam.Azymut, tam.Waga));
        Assert.Equal((a.Id, 270, 7.5m), (zPowrotem.PunktDocelowyId!.Value, zPowrotem.Azymut, zPowrotem.Waga));
        Assert.Equal("Wzdłuż gablot, w stronę holu.", zPowrotem.OpisPrzejscia);
        Assert.Equal(zPowrotem.Id, tam.KierunekPowrotnyId);
        Assert.Equal(tam.Id, zPowrotem.KierunekPowrotnyId);
    }

    [Fact]
    public async Task DodajKierunek_BezPowrotnego_TworzyTylkoJeden()
    {
        await using var db = app.Baza.UtworzKontekst();
        var (a, b) = await DwaPunkty(db);

        var wynik = await Serwis(db).DodajKierunek(new Kierunek { PunktZrodlowyId = a.Id, PunktDocelowyId = b.Id, Azymut = 0, Waga = 3 },
            utworzPowrotny: false, opisPowrotny: null);

        Assert.True(wynik.Sukces);
        Assert.Equal(0, await db.Kierunki.CountAsync(k => k.PunktZrodlowyId == b.Id));
    }

    [Fact]
    public async Task DodajKierunek_PowrotnyJuzIstnieje_ZostajePowiazanyZamiastDublowany()
    {
        await using var db = app.Baza.UtworzKontekst();
        var (a, b) = await DwaPunkty(db);
        var serwis = Serwis(db);
        await serwis.DodajKierunek(new Kierunek { PunktZrodlowyId = b.Id, PunktDocelowyId = a.Id, Azymut = 180, Waga = 4 }, false, null);

        var wynik = await serwis.DodajKierunek(new Kierunek { PunktZrodlowyId = a.Id, PunktDocelowyId = b.Id, Azymut = 0, Waga = 4 }, true, null);

        Assert.True(wynik.Sukces);
        Assert.Contains("istniejącym kierunkiem powrotnym", wynik.Informacja);
        Assert.Equal(1, await db.Kierunki.CountAsync(k => k.PunktZrodlowyId == b.Id));
        var tam = await db.Kierunki.AsNoTracking().SingleAsync(k => k.PunktZrodlowyId == a.Id);
        Assert.NotNull(tam.KierunekPowrotnyId);
    }

    [Fact]
    public async Task DodajKierunek_AzymutZajety_BladPrzyPoluAzymutBezZapisu()
    {
        await using var db = app.Baza.UtworzKontekst();
        var (a, b) = await DwaPunkty(db);
        var serwis = Serwis(db);
        await serwis.DodajKierunek(new Kierunek { PunktZrodlowyId = a.Id, PunktDocelowyId = b.Id, Azymut = 90, Waga = 2 }, false, null);

        var wynik = await serwis.DodajKierunek(new Kierunek { PunktZrodlowyId = a.Id, PunktDocelowyId = b.Id, Azymut = 90, Waga = 9 }, false, null);

        Assert.False(wynik.Sukces);
        Assert.Equal(nameof(Kierunek.Azymut), Assert.Single(wynik.Bledy).Pole);
        Assert.Equal(1, await db.Kierunki.CountAsync(k => k.PunktZrodlowyId == a.Id));
    }

    [Fact]
    public async Task DodajKierunek_PowrotnyNiemozliwy_BladPrzyOpcjiPowrotnegoINicNieZapisane()
    {
        await using var db = app.Baza.UtworzKontekst();
        var (a, b) = await DwaPunkty(db);
        var (c, _) = await DwaPunkty(db);
        var serwis = Serwis(db);
        // B ma już kierunek 270° — ale prowadzący do C, nie do A.
        await serwis.DodajKierunek(new Kierunek { PunktZrodlowyId = b.Id, PunktDocelowyId = c.Id, Azymut = 270, Waga = 5 }, false, null);

        var wynik = await serwis.DodajKierunek(new Kierunek { PunktZrodlowyId = a.Id, PunktDocelowyId = b.Id, Azymut = 90, Waga = 5 }, true, null);

        Assert.False(wynik.Sukces);
        Assert.Equal("UtworzPowrotny", Assert.Single(wynik.Bledy).Pole);
        Assert.Equal(0, await db.Kierunki.CountAsync(k => k.PunktZrodlowyId == a.Id)); // transakcja: nic połowicznie
    }

    [Fact]
    public async Task IndeksUQ_Kierunek_Powrotny_OdrzucaDwaKierunkiZTymSamymPowrotem()
    {
        await using var db = app.Baza.UtworzKontekst();
        var (a, b) = await DwaPunkty(db);
        await Serwis(db).DodajKierunek(new Kierunek { PunktZrodlowyId = a.Id, PunktDocelowyId = b.Id, Azymut = 90, Waga = 3 }, true, null);
        var powrot = await db.Kierunki.SingleAsync(k => k.PunktZrodlowyId == b.Id);

        // D-07: druga krawędź nie może wskazywać tego samego powrotu — pilnuje tego baza, nie tylko formularz.
        db.Kierunki.Add(new Kierunek { PunktZrodlowyId = a.Id, PunktDocelowyId = b.Id, Azymut = 0, Waga = 3, KierunekPowrotnyId = powrot.Id });
        var wyjatek = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Contains("UQ_Kierunek_Powrotny", wyjatek.InnerException?.Message);
    }

    [Fact]
    public async Task WejscieGlowne_MusiLezecWTymBudynku()
    {
        await using var db = app.Baza.UtworzKontekst();
        var budynek = await db.Budynki.FirstAsync();
        var obcy = new Budynek { Kod = $"W{Random.Shared.Next(100, 999)}", Nazwa = "Obcy" };
        db.Budynki.Add(obcy);
        await db.SaveChangesAsync();
        var punktA = await db.PunktyRuchu.FirstAsync(p => p.Pietro.BudynekId == budynek.Id);

        obcy.PunktWejsciaGlownegoId = punktA.Id;
        var wynik = await Serwis(db).ZapiszBudynek(obcy);

        Assert.False(wynik.Sukces);
        Assert.Equal(nameof(Budynek.PunktWejsciaGlownegoId), Assert.Single(wynik.Bledy).Pole);
    }

    [Fact]
    public async Task DezaktywacjaPunktuIKierunku_NieUsuwaFizycznie()
    {
        await using var db = app.Baza.UtworzKontekst();
        var (a, b) = await DwaPunkty(db);
        var serwis = Serwis(db);
        await serwis.DodajKierunek(new Kierunek { PunktZrodlowyId = a.Id, PunktDocelowyId = b.Id, Azymut = 180, Waga = 2 }, true, null);
        var kierunek = await db.Kierunki.AsNoTracking().SingleAsync(k => k.PunktZrodlowyId == a.Id);

        await serwis.UstawAktywnoscKierunku(kierunek.Id, false, takzePowrotny: true);
        await serwis.UstawAktywnoscPunktu(a.Id, false);

        db.ChangeTracker.Clear();
        Assert.False((await db.PunktyRuchu.SingleAsync(p => p.Id == a.Id)).CzyAktywny);
        Assert.All(await db.Kierunki.Where(k => k.PunktZrodlowyId == a.Id || k.PunktZrodlowyId == b.Id).ToListAsync(), k => Assert.False(k.CzyAktywny));
    }

    [Fact]
    public async Task Audyt_ZapisujeKtoKiedyCoIJak_ZWartosciamiStarymiINowymi()
    {
        var idUzytkownika = Guid.NewGuid().ToString();
        await using var db = app.Baza.UtworzKontekst(new AudytInterceptor(new Uzytkownik(idUzytkownika), TimeProvider.System));
        var typ = await db.TypySal.FirstAsync();
        var pietro = await db.Pietra.FirstAsync();
        var sala = new Sala { PietroId = pietro.Id, TypSaliId = typ.Id, Symbol = $"X{Random.Shared.Next(1000, 9999)}", Nazwa = "Przed zmianą" };

        db.Sale.Add(sala);
        await db.SaveChangesAsync();
        sala.Nazwa = "Po zmianie";
        await db.SaveChangesAsync();

        var wpisy = await db.WpisyAudytu.AsNoTracking()
            .Where(w => w.Encja == nameof(Sala) && w.KluczEncji == sala.Id.ToString()).OrderBy(w => w.Id).ToListAsync();
        Assert.Equal(["INSERT", "UPDATE"], wpisy.Select(w => w.Operacja));
        Assert.All(wpisy, w => Assert.Equal(idUzytkownika, w.UzytkownikId));

        var stare = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(wpisy[1].WartosciStare!)!;
        var nowe = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(wpisy[1].WartosciNowe!)!;
        Assert.Equal("Przed zmianą", stare["Nazwa"].GetString());
        Assert.Equal("Po zmianie", nowe["Nazwa"].GetString());
        Assert.Equal(["Nazwa"], nowe.Keys); // przy zmianie — tylko zmienione pola
        Assert.Equal(sala.Id, JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(wpisy[0].WartosciNowe!)!["Id"].GetInt32());
    }

    [Fact]
    public async Task Audyt_MaskujeDaneOsoboweZeZgloszen()
    {
        await using var db = app.Baza.UtworzKontekst(new AudytInterceptor(new Uzytkownik(null), TimeProvider.System));
        var zgloszenie = new ZgloszenieDostepnosci { Tresc = "Brak opisu zdjęcia", Email = "osoba@przyklad.test", ImieNazwisko = "Jan Testowy" };

        db.ZgloszeniaDostepnosci.Add(zgloszenie);
        await db.SaveChangesAsync();

        var wpis = await db.WpisyAudytu.AsNoTracking().SingleAsync(w => w.Encja == nameof(ZgloszenieDostepnosci) && w.KluczEncji == zgloszenie.Id.ToString());
        Assert.DoesNotContain("osoba@przyklad.test", wpis.WartosciNowe);
        Assert.DoesNotContain("Jan Testowy", wpis.WartosciNowe);
        Assert.Contains("Brak opisu zdjęcia", wpis.WartosciNowe);
        Assert.Null(wpis.UzytkownikId);
    }
}
