using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PrzewodnikSWPW.Web.Services;
using static PrzewodnikSWPW.UnitTests.Services.ObrazyTestowe;

namespace PrzewodnikSWPW.UnitTests.Admin;

/// <summary>Wgrywanie zdjęć przez HTTP (WF-26, WN-39, D-10).</summary>
[Collection(AplikacjaKolekcja.Nazwa)]
[Trait("Kategoria", "Integracyjne")]
public class ZdjeciaHttpTesty(AplikacjaFixture app)
{
    private const string AltKorytarza = "Korytarz z jasną posadzką. Po prawej drzwi z tabliczką A12.";

    private async Task<HttpClient> Administrator()
    {
        var klient = app.Klient();
        await app.Zaloguj(klient, AplikacjaFixture.EmailAdministratora, app.Haslo);
        return klient;
    }

    private async Task<int> IdPunktu(string kod)
    {
        await using var db = app.Baza.UtworzKontekst();
        return await db.PunktyRuchu.Where(p => p.Kod == kod).Select(p => p.Id).SingleAsync();
    }

    private int LiczbaPlikow() =>
        Directory.Exists(Path.Combine(app.KatalogMedia, "zdjecia")) ? Directory.GetFiles(Path.Combine(app.KatalogMedia, "zdjecia")).Length : 0;

    private static async Task<HttpResponseMessage> Wgraj(HttpClient klient, int punkt, Dictionary<string, string> pola,
        byte[]? plik = null, string nazwa = "IMG_2043 dom Kowalskich.jpg", string mime = "image/jpeg")
    {
        var formularz = await klient.GetStringAsync($"/admin/zdjecia/nowe?punkt={punkt}");
        var tresc = new MultipartFormDataContent
        {
            { new StringContent(AplikacjaFixture.Token(formularz)), "__RequestVerificationToken" },
            { new StringContent(punkt.ToString()), "PunktRuchuId" },
        };
        foreach (var (klucz, wartosc) in pola) tresc.Add(new StringContent(wartosc), klucz);
        if (plik is not null)
        {
            var czesc = new ByteArrayContent(plik);
            czesc.Headers.ContentType = new MediaTypeHeaderValue(mime);
            tresc.Add(czesc, "Plik", nazwa);
        }
        return await klient.PostAsync("/admin/zdjecia/nowe", tresc);
    }

    private static Dictionary<string, string> Pola(string alt = AltKorytarza, string zrodlo = "  Zespół projektu  ", string licencja = "Własne zdjęcie zespołu") =>
        new() { ["TekstAlternatywny"] = alt, ["Zrodlo"] = zrodlo, ["Licencja"] = licencja, ["Kolejnosc"] = "0" };

    [Fact]
    public async Task Formularz_PolePlikuITekstAlternatywnyZInstrukcjaIPrzykladami()
    {
        var klient = await Administrator();

        var html = await klient.GetStringAsync($"/admin/zdjecia/nowe?punkt={await IdPunktu("A-0-P04")}");

        Assert.Contains("enctype=\"multipart/form-data\"", html);
        var polePliku = Regex.Match(html, "<input[^>]*id=\"Plik\"[^>]*>").Value; // kolejność atrybutów bez znaczenia
        Assert.Contains("type=\"file\"", polePliku);
        Assert.Contains($"accept=\"{ZdjeciaService.AtrybutAccept}\"", polePliku);
        Assert.Matches("<label[^>]*for=\"Plik\"[^>]*>Plik zdjęcia \\(wymagane\\)</label>", html);
        Assert.Matches("<label[^>]*for=\"TekstAlternatywny\"[^>]*>Tekst alternatywny \\(opis zdjęcia\\) \\(wymagane\\)</label>", html);
        Assert.Matches("<label[^>]*for=\"Zrodlo\"[^>]*>Źródło zdjęcia \\(wymagane\\)</label>", html);
        Assert.Matches("<label[^>]*for=\"Licencja\"[^>]*>Licencja albo zgoda na wykorzystanie \\(wymagane\\)</label>", html);
        // Instrukcja redakcyjna pod polem, powiązana z nim przez aria-describedby (06 §2.3).
        var wskazowka = Regex.Match(html, "<p id=\"TekstAlternatywny-wskazowka\" class=\"wskazowka\">(.*?)</p>").Groups[1].Value;
        Assert.Contains("Dobrze: „Korytarz z jasną posadzką.", wskazowka);
        Assert.Contains("Źle: „Zdjęcie korytarza”, „IMG_2043.jpg”", wskazowka);
        Assert.Contains("aria-describedby=\"TekstAlternatywny-wskazowka\"", Regex.Match(html, "<textarea[^>]*id=\"TekstAlternatywny\"[^>]*>").Value);
    }

    [Fact]
    public async Task PoprawneZdjecie_ZapisaneZNazwaOdSerwera_BezMetadanych_ZWymiaramiIPrawami()
    {
        var klient = await Administrator();
        var punkt = await IdPunktu("A-0-P04");

        var odpowiedz = await Wgraj(klient, punkt, Pola(), Jpeg(1600, 1200, orientacja: 6));

        Assert.Equal(HttpStatusCode.Redirect, odpowiedz.StatusCode);
        Assert.Equal($"/admin/zdjecia?punkt={punkt}", odpowiedz.Headers.Location!.OriginalString);

        await using var db = app.Baza.UtworzKontekst();
        var z = await db.Zdjecia.OrderByDescending(x => x.Id).FirstAsync(x => x.PunktRuchuId == punkt);
        Assert.Matches("^zdjecia/[0-9a-f]{32}\\.jpg$", z.SciezkaPliku); // nigdy „IMG_2043 dom Kowalskich.jpg”
        Assert.Equal((1200, 1600), (z.Szerokosc, z.Wysokosc));      // orientacja 6 — wymiary wyświetlane
        Assert.Equal((AltKorytarza, "Zespół projektu", "Własne zdjęcie zespołu"), (z.TekstAlternatywny, z.Zrodlo, z.Licencja));

        var zapisany = await File.ReadAllBytesAsync(Path.Combine(app.KatalogMedia, z.SciezkaPliku));
        Assert.False(Zawiera(zapisany, Tajne));
        Assert.False(Zawiera(zapisany, Komentarz));
        Assert.Equal(6, Obrazy.Rozpoznaj(zapisany)!.OrientacjaExif);

        var lista = await klient.GetStringAsync(odpowiedz.Headers.Location.OriginalString);
        Assert.Contains("Dodano zdjęcie (punkt A-0-P04).", lista);
    }

    [Fact]
    public async Task BrakTekstuAlternatywnego_ZapisSieNiePowodzi_PlikNieZapisany()
    {
        var klient = await Administrator();
        var punkt = await IdPunktu("A-0-P04");
        var plikow = LiczbaPlikow();
        await using var db = app.Baza.UtworzKontekst();
        var zdjec = await db.Zdjecia.CountAsync();

        var odpowiedz = await Wgraj(klient, punkt, Pola(alt: "   "), Jpeg(640, 480));
        var html = await odpowiedz.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, odpowiedz.StatusCode);
        Assert.Contains("Błąd: Pole „Tekst alternatywny (opis zdjęcia)” jest wymagane.", html);
        Assert.Contains("autofocus=\"autofocus\"", Regex.Match(html, "<textarea[^>]*id=\"TekstAlternatywny\"[^>]*>").Value);
        Assert.Equal(zdjec, await db.Zdjecia.CountAsync());
        Assert.Equal(plikow, LiczbaPlikow());
    }

    [Fact]
    public async Task BrakPlikuZrodlaILicencji_WszystkieBledyNaRaz()
    {
        var klient = await Administrator();

        var html = await (await Wgraj(klient, await IdPunktu("A-0-P04"), Pola(zrodlo: "", licencja: ""))).Content.ReadAsStringAsync();

        Assert.Contains("Błąd: Wybierz plik zdjęcia.", html);
        Assert.Contains("Błąd: Pole „Źródło zdjęcia” jest wymagane.", html);
        Assert.Contains("Błąd: Pole „Licencja albo zgoda na wykorzystanie” jest wymagane.", html);
        Assert.Contains(AltKorytarza, html); // wpisany tekst nie znika
    }

    [Fact]
    public async Task ZdjecieDekoracyjne_ZapisaneZPustymTekstemAlternatywnym()
    {
        var klient = await Administrator();
        var punkt = await IdPunktu("A-0-P05");
        var pola = Pola(alt: "");
        pola["CzyDekoracyjne"] = "true";

        Assert.Equal(HttpStatusCode.Redirect, (await Wgraj(klient, punkt, pola, Png(300, 200), "ozdobnik.png", "image/png")).StatusCode);

        await using var db = app.Baza.UtworzKontekst();
        var z = await db.Zdjecia.OrderByDescending(x => x.Id).FirstAsync(x => x.PunktRuchuId == punkt);
        Assert.True(z.CzyDekoracyjne);
        Assert.Equal("", z.TekstAlternatywny);
        Assert.EndsWith(".png", z.SciezkaPliku);
    }

    [Theory]
    [InlineData("strona.jpg", "image/jpeg", "Plik nie jest poprawnym zdjęciem")]
    [InlineData("zdjecie.gif", "image/gif", "Pliki .gif nie są przyjmowane")]
    [InlineData("zdjecie.jpg", "text/html", "Typ pliku nie zgadza się")]
    public async Task PlikSpozaBialejListyLubNieObraz_Odrzucony_NicNieZapisane(string nazwa, string mime, string blad)
    {
        var klient = await Administrator();
        var plikow = LiczbaPlikow();
        var dane = nazwa == "strona.jpg" ? "<html><script>alert(1)</script></html>"u8.ToArray() : Jpeg(10, 10);

        var html = await (await Wgraj(klient, await IdPunktu("A-0-P04"), Pola(), dane, nazwa, mime)).Content.ReadAsStringAsync();

        Assert.Contains($"Błąd: {blad}", html);
        Assert.Equal(plikow, LiczbaPlikow());
    }

}
