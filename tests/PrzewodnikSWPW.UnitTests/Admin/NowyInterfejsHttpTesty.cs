using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace PrzewodnikSWPW.UnitTests.Admin;

/// <summary>
/// Układ ekranu miejsca i osi trasy z docs/prototyp-interfejsu.html — to, co widać w HTML:
/// dwa widoczne wiersze przejścia, brak &lt;figure&gt; bez zdjęcia, oś czasu z n+1 wierszami.
/// </summary>
[Collection(AplikacjaKolekcja.Nazwa)]
[Trait("Kategoria", "Integracyjne")]
public class NowyInterfejsHttpTesty(AplikacjaFixture app)
{
    private static string Tekst(string html) => Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), "\\s+", " ").Trim();

    private async Task<string> AdresTrasy(string z, string @do)
    {
        await using var db = app.Baza.UtworzKontekst();
        var salaZ = await db.Sale.SingleAsync(s => s.Symbol == z);
        var salaDo = await db.Sale.SingleAsync(s => s.Symbol == @do);
        return $"/trasa/wynik?z={salaZ.Id}&do={salaDo.Id}";
    }

    private static async Task WlaczMowe(HttpClient klient)
    {
        var formularz = await klient.GetStringAsync("/ustawienia");
        await klient.PostAsync("/ustawienia", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AplikacjaFixture.Token(formularz),
            ["Motyw"] = "Systemowy", ["RozmiarTekstu"] = "100", ["TempoMowy"] = "100",
            ["MowaWlaczona"] = "true", ["SkrotyWlaczone"] = "true", ["OgraniczAnimacje"] = "false",
        }));
    }

    [Fact]
    public async Task EkranMiejsca_KazdePrzejscieMaDwaWidoczneWiersze_ABrakPrzejsciaZostajeTekstem()
    {
        // P04 ze zwrotem 270: prosto korytarz, w lewo ściana z gablotą, w prawo drzwi do A12, do tyłu skrzyżowanie.
        var html = await app.Klient().GetStringAsync("/spacer/A/0/P04?zwrot=270");
        var pozycje = Regex.Matches(html, "<li data-kierunek=\"([a-z]+)\">(.*?)</li>", RegexOptions.Singleline);

        Assert.Equal(["prosto", "lewo", "prawo", "tyl"], pozycje.Select(p => p.Groups[1].Value)); // D-01

        var prosto = pozycje[0].Groups[2].Value;
        Assert.Matches("^\\s*<a href=\"[^\"]+\" class=\"kierunek\">", prosto);
        Assert.Equal("Idź prosto — korytarz zachodni przy salach A14 i A15", Tekst(Regex.Match(prosto, "<span class=\"kierunek__nazwa\">(.*?)</span>").Groups[1].Value));
        Assert.StartsWith("Korytarz, 6 metrów.", Tekst(Regex.Match(prosto, "<span class=\"kierunek__szczegol\">(.*?)</span>").Groups[1].Value));
        // Oba wiersze są zwykłymi, widocznymi elementami — nie tekstem tylko dla czytnika.
        Assert.DoesNotContain("visually-hidden", prosto);
        Assert.DoesNotContain("aria-hidden", prosto);

        var lewo = pozycje[1].Groups[2].Value;
        Assert.DoesNotContain("<a ", lewo);
        Assert.DoesNotContain("<button", lewo);
        Assert.Matches("^\\s*<span class=\"kierunek kierunek--brak\">", lewo);
        Assert.StartsWith("W lewo — brak przejścia.", Tekst(lewo));
        Assert.Contains("ściana z gablotą informacyjną", Tekst(lewo));

        Assert.StartsWith("Drzwi, 1 metr.", Tekst(Regex.Match(pozycje[2].Groups[2].Value, "<span class=\"kierunek__szczegol\">(.*?)</span>").Groups[1].Value));
    }

    [Fact]
    public async Task EkranMiejsca_BezZdjecia_NieMaFigure_ANaglowkiSaWStalejKolejnosci()
    {
        await using var db = app.Baza.UtworzKontekst();
        var bezZdjec = await db.PunktyRuchu.Include(p => p.Pietro).ThenInclude(p => p.Budynek)
            .FirstAsync(p => p.CzyAktywny && p.Kod == "A-0-P03");
        Assert.False(await db.Zdjecia.AnyAsync(z => z.PunktRuchuId == bezZdjec.Id));

        var html = await app.Klient().GetStringAsync(
            $"/spacer/{bezZdjec.Pietro.Budynek.Kod}/{bezZdjec.Pietro.Numer}/{bezZdjec.Kod.Split('-')[^1]}?zwrot={bezZdjec.AzymutDomyslny}");
        var tresc = Regex.Match(html, "<main\\b.*?</main>", RegexOptions.Singleline).Value;

        Assert.DoesNotContain("<figure", tresc);
        Assert.DoesNotContain("<img", tresc);
        Assert.Equal(
            ["h1", "h2 Opis miejsca", "h2 Dostępne przejścia"],
            Regex.Matches(tresc, "<(h[1-6])[^>]*>(.*?)</h[1-6]>", RegexOptions.Singleline)
                .Select(n => n.Groups[1].Value == "h1" ? "h1" : $"{n.Groups[1].Value} {Tekst(n.Groups[2].Value)}"));
    }

    [Fact]
    public async Task Trasa_OsCzasu_PoczatekICel_PodsumowanieJakoDl_BezPrzyciskowMowyDomyslnie()
    {
        var html = await app.Klient().GetStringAsync(await AdresTrasy("A12", "A15"));

        var podsumowanie = Regex.Match(html, "<div class=\"podsumowanie-trasy\">\\s*<dl>(.*?)</dl>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Equal(["Długość:", "Kroki:", "Czas:", "Tryb windy:", "Schody:"],
            Regex.Matches(podsumowanie, "<dt>(.*?)</dt>").Select(d => d.Groups[1].Value));

        var kroki = Regex.Matches(Regex.Match(html, "<ol class=\"trasa\">(.*?)</ol>", RegexOptions.Singleline).Groups[1].Value,
            "<li>(.*?)</li>", RegexOptions.Singleline).Select(k => k.Groups[1].Value).ToList();
        Assert.True(kroki.Count >= 3);
        Assert.Contains($"<dt>Kroki:</dt><dd>{kroki.Count}</dd>", podsumowanie);

        // Każdy wiersz: nazwa punktu i instrukcja; pierwszy to początek bez dystansu, ostatni to cel.
        Assert.All(kroki, k => Assert.Matches("<span class=\"krok__punkt\">\\s*\\S", k));
        Assert.All(kroki, k => Assert.Matches("<p class=\"krok__instrukcja\">\\s*\\S", k));
        Assert.Contains("<span class=\"krok__rola\">Początek trasy</span>", kroki[0]);
        Assert.DoesNotContain("krok__dystans", kroki[0]);
        Assert.StartsWith("Wyjdź z sali A12 na korytarz.", Tekst(Regex.Match(kroki[0], "<p class=\"krok__instrukcja\">(.*?)</p>", RegexOptions.Singleline).Groups[1].Value));
        Assert.Contains("<span class=\"krok__rola\">Punkt docelowy</span>", kroki[^1]);
        Assert.StartsWith("Sala A15 — ", Tekst(Regex.Match(kroki[^1], "<span class=\"krok__punkt\">(.*?)<span", RegexOptions.Singleline).Groups[1].Value));
        Assert.All(kroki.Skip(1), k => Assert.Matches("<span class=\"krok__dystans\">(Korytarz|Drzwi|Schody|Winda|Podjazd), \\d", k));
        Assert.All(kroki.Skip(1).SkipLast(1), k => Assert.DoesNotContain("krok__rola", k));

        // Kropki i linia są w CSS — wiersz nie zawiera pustych ani ukrytych elementów.
        Assert.All(kroki, k => Assert.DoesNotContain("aria-hidden", k));
        // Mowa domyślnie wyłączona: żadnych przycisków.
        Assert.DoesNotContain("Przeczytaj ten krok", html);
        Assert.DoesNotContain("panel-mowy", html);
    }

    [Fact]
    public async Task Trasa_PoWlaczeniuMowy_PrzyciskPrzyKazdymKroku_IPrzyciskZatrzymania()
    {
        var klient = app.Klient();
        await WlaczMowe(klient);

        var html = await klient.GetStringAsync(await AdresTrasy("A12", "A15"));

        var liczbaKrokow = Regex.Matches(html, "<div class=\"krok\">").Count;
        var przyciski = Regex.Matches(html, "<button type=\"button\" class=\"btn btn-secondary btn-mowa\" data-czytaj=\"([^\"]+)\" hidden>Przeczytaj ten krok<span class=\"visually-hidden\">: ([^<]+)</span></button>");
        Assert.Equal(liczbaKrokow, przyciski.Count);
        // Tekst do przeczytania to nazwa punktu i instrukcja kroku; nazwa przycisku rozróżnia kroki.
        Assert.StartsWith(WebUtility.HtmlDecode(przyciski[0].Groups[2].Value) + ". Wyjdź z sali A12", WebUtility.HtmlDecode(przyciski[0].Groups[1].Value));
        Assert.Equal(przyciski.Count, przyciski.Select(p => p.Groups[2].Value).Distinct().Count());
        Assert.Contains("<button type=\"button\" id=\"btn-zatrzymaj\" class=\"btn btn-secondary\">Zatrzymaj czytanie</button>", html);
        Assert.Matches("<script src=\"/js/mowa\\.js\\?v=[^\"]+\"></script>", html);
    }
}
