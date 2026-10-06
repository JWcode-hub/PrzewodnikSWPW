using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.UnitTests.Admin;

/// <summary>Zdjęcie i aktywne obszary na ekranie spaceru (WF-27, P-13, D-10) — przez HTTP, bez JavaScriptu.</summary>
[Collection(AplikacjaKolekcja.Nazwa)]
[Trait("Kategoria", "Integracyjne")]
public class SpacerZdjeciaHttpTesty(AplikacjaFixture app)
{
    [Fact]
    public async Task EkranMiejsca_ZdjeciePrzedOpisem_WymiaryIMapaObszarow_ObszarTylkoDlaMozliwegoKierunku()
    {
        await using var db = app.Baza.UtworzKontekst();
        // Punkt z kierunkiem nieaktywnym (ściana z gablotą) i co najmniej jednym aktywnym.
        var nieaktywny = await db.Kierunki.Include(k => k.PunktZrodlowy).ThenInclude(p => p.Pietro).ThenInclude(p => p.Budynek)
            .FirstAsync(k => !k.CzyAktywny);
        var punkt = nieaktywny.PunktZrodlowy;
        var aktywny = await db.Kierunki.FirstAsync(k => k.PunktZrodlowyId == punkt.Id && k.CzyAktywny);

        var zdjecie = new Zdjecie
        {
            PunktRuchuId = punkt.Id, SciezkaPliku = "zdjecia/test-spacer.jpg", TekstAlternatywny = "Korytarz z gablotą po lewej stronie kadru.",
            Zrodlo = "Zespół projektu", Licencja = "Własne", Szerokosc = 640, Wysokosc = 480,
            ObszaryAktywne =
            [
                new ObszarAktywny { KierunekId = aktywny.Id, Ksztalt = "rect", Wspolrzedne = "100,50,300,400", Etykieta = "Przejście korytarzem" },
                new ObszarAktywny { KierunekId = nieaktywny.Id, Ksztalt = "rect", Wspolrzedne = "400,50,600,400", Etykieta = "Gablota — obszar bez przejścia" },
            ],
        };
        db.Zdjecia.Add(zdjecie);
        await db.SaveChangesAsync();
        try
        {
            var adres = $"/spacer/{punkt.Pietro.Budynek.Kod}/{punkt.Pietro.Numer}/{punkt.Kod.Split('-')[^1]}?zwrot={punkt.AzymutDomyslny}";
            var odpowiedz = await app.Klient().GetAsync(adres);
            Assert.Equal(HttpStatusCode.OK, odpowiedz.StatusCode);
            var html = await odpowiedz.Content.ReadAsStringAsync();

            // Kolejność w kodzie: nagłówek → zdjęcie → opis → lista przejść (UI-02, D-13).
            Assert.DoesNotContain("brak-zdjecia", html); // ramka zastępcza tylko wtedy, gdy zdjęcia nie ma
            var naglowek = html.IndexOf("id=\"naglowek-miejsca\"");
            var figura = html.IndexOf("<figure>");
            var opis = html.IndexOf("id=\"opis-miejsca\"");
            var lista = html.IndexOf("class=\"lista-kierunkow\"");
            Assert.True(naglowek >= 0 && naglowek < figura && figura < opis && opis < lista,
                "Kolejność w kodzie: nagłówek, zdjęcie, opis, lista przejść.");

            var img = Regex.Match(html, "<img [^>]*test-spacer\\.jpg[^>]*>").Value;
            Assert.Contains("width=\"640\"", img);
            Assert.Contains("height=\"480\"", img);
            Assert.Contains("alt=\"Korytarz z gablotą po lewej stronie kadru.\"", img);
            // Inne testy mogą dodać zdjęcia do tego samego punktu — numer mapy bierzemy z atrybutu usemap.
            var mapa = Regex.Match(img, "usemap=\"#(obszary-zdjecia-\\d+)\"").Groups[1].Value;
            Assert.NotEmpty(mapa);

            // Obszar prowadzi tam, gdzie link z listy kierunków; obszar kierunku bez przejścia nie jest renderowany.
            var area = Regex.Match(html, $"<map name=\"{mapa}\" id=\"{mapa}\">\\s*(<area [^>]*>)\\s*</map>").Groups[1].Value;
            Assert.Contains("alt=\"Przejście korytarzem\"", area);
            Assert.Contains("coords=\"100,50,300,400\"", area);
            var href = WebUtility.HtmlDecode(Regex.Match(area, "href=\"([^\"]+)\"").Groups[1].Value);
            var linkiKierunkow = Regex.Matches(html, "<a href=\"([^\"]+)\" class=\"kierunek\">").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value));
            Assert.Contains(href, linkiKierunkow);
            Assert.DoesNotContain("Gablota — obszar bez przejścia", html);
        }
        finally
        {
            db.Zdjecia.Remove(zdjecie); // obszary znikają kaskadowo
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task EkranMiejscaBezObszarow_BezAtrybutuUsemapIBezMapy()
    {
        await using var db = app.Baza.UtworzKontekst();
        var punkt = await db.PunktyRuchu.Include(p => p.Pietro).ThenInclude(p => p.Budynek).FirstAsync(p => p.Kod == "A-0-P02");
        var zdjecie = new Zdjecie
        {
            PunktRuchuId = punkt.Id, SciezkaPliku = "zdjecia/test-bez-obszarow.jpg", TekstAlternatywny = "", CzyDekoracyjne = true,
            Zrodlo = "Zespół projektu", Licencja = "Własne", Szerokosc = 320, Wysokosc = 240,
        };
        db.Zdjecia.Add(zdjecie);
        await db.SaveChangesAsync();
        try
        {
            var html = await app.Klient().GetStringAsync($"/spacer/{punkt.Pietro.Budynek.Kod}/{punkt.Pietro.Numer}/P02?zwrot={punkt.AzymutDomyslny}");

            var img = Regex.Match(html, "<img [^>]*test-bez-obszarow\\.jpg[^>]*>").Value;
            Assert.Contains("alt=\"\"", img); // dekoracyjne — czytnik ekranu pomija
            Assert.Contains("width=\"320\"", img);
            Assert.DoesNotContain("usemap", img);
            Assert.DoesNotContain("<map", html);
        }
        finally
        {
            db.Zdjecia.Remove(zdjecie);
            await db.SaveChangesAsync();
        }
    }
}
