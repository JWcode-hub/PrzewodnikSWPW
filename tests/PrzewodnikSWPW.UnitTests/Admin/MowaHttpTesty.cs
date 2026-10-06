using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace PrzewodnikSWPW.UnitTests.Admin;

/// <summary>
/// Odczyt opisu mową syntetyczną (WF-35, 06 §4, D-11, ryzyko P-05) — to, co da się sprawdzić po stronie serwera:
/// panel z przyciskami istnieje wyłącznie po włączeniu mowy w ustawieniach i niesie OpisGlosowy.
/// </summary>
[Collection(AplikacjaKolekcja.Nazwa)]
[Trait("Kategoria", "Integracyjne")]
public class MowaHttpTesty(AplikacjaFixture app)
{
    private const string Miejsce = "/spacer/A/0/P01?zwrot=0";

    private static async Task ZapiszUstawienia(HttpClient klient, bool mowa, int tempo = 100)
    {
        var formularz = await klient.GetStringAsync("/ustawienia");
        var odpowiedz = await klient.PostAsync("/ustawienia", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AplikacjaFixture.Token(formularz),
            ["Motyw"] = "Systemowy", ["RozmiarTekstu"] = "100", ["TempoMowy"] = tempo.ToString(),
            ["MowaWlaczona"] = mowa ? "true" : "false", ["SkrotyWlaczone"] = "true", ["OgraniczAnimacje"] = "false",
        }));
        Assert.Equal(HttpStatusCode.Redirect, odpowiedz.StatusCode);
    }

    [Fact]
    public async Task Domyslnie_MowaWylaczona_BezPrzyciskowNaEkranieMiejsca()
    {
        var html = await app.Klient().GetStringAsync(Miejsce);

        Assert.Contains("data-mowa=\"wylaczona\"", html);
        Assert.DoesNotContain("panel-mowy", html);
        Assert.DoesNotContain("Przeczytaj opis", html);
        Assert.DoesNotContain("Zatrzymaj czytanie", html);
    }

    [Fact]
    public async Task PoWlaczeniu_PanelZOpisemGlosowymIObomaPrzyciskami_UkrytyDoCzasuUruchomieniaSkryptu()
    {
        await using var db = app.Baza.UtworzKontekst();
        var punkt = await db.PunktyRuchu.SingleAsync(p => p.Kod == "A-0-P01");
        Assert.False(string.IsNullOrWhiteSpace(punkt.OpisGlosowy));

        var klient = app.Klient();
        await ZapiszUstawienia(klient, mowa: true, tempo: 150);
        var html = await klient.GetStringAsync(Miejsce);

        Assert.Contains("data-mowa=\"wlaczona\"", html);
        Assert.Contains("data-tempo-mowy=\"150\"", html);
        // Czytany jest OpisGlosowy; panel ma atrybut hidden, który zdejmuje dopiero mowa.js.
        var panel = WebUtility.HtmlDecode(Regex.Match(html, "<div id=\"panel-mowy\"[^>]*>").Value);
        Assert.Contains($"data-tekst=\"{punkt.OpisGlosowy}\"", panel);
        Assert.EndsWith(" hidden>", panel);
        Assert.Contains("<button type=\"button\" id=\"btn-czytaj\" class=\"btn btn-primary\">Przeczytaj opis</button>", html);
        Assert.Contains("<button type=\"button\" id=\"btn-zatrzymaj\" class=\"btn btn-secondary\">Zatrzymaj czytanie</button>", html);
        Assert.Matches("<script src=\"/js/mowa\\.js\\?v=[^\"]+\"></script>", html);
        // Tekst na ekranie zostaje — mowa go nie zastępuje.
        Assert.Matches("<p id=\"opis-miejsca\">[^<]+</p>", html);
        // Panel stoi tuż po opisie, przed listą kierunków (UI-02).
        Assert.True(html.IndexOf("id=\"opis-miejsca\"", StringComparison.Ordinal) < html.IndexOf("id=\"panel-mowy\"", StringComparison.Ordinal));
        Assert.True(html.IndexOf("id=\"panel-mowy\"", StringComparison.Ordinal) < html.IndexOf("class=\"lista-kierunkow\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PoWlaczeniu_KartaSaliTezMaPanel_APoWylaczeniuPanelZnika()
    {
        await using var db = app.Baza.UtworzKontekst();
        var sala = await db.Sale.FirstAsync(s => s.Symbol == "A15");
        var klient = app.Klient();

        await ZapiszUstawienia(klient, mowa: true);
        Assert.Contains("id=\"panel-mowy\"", await klient.GetStringAsync($"/sala/{sala.Id}"));

        await ZapiszUstawienia(klient, mowa: false);
        Assert.DoesNotContain("panel-mowy", await klient.GetStringAsync($"/sala/{sala.Id}"));
        Assert.DoesNotContain("panel-mowy", await klient.GetStringAsync(Miejsce));
    }

    [Fact]
    public async Task Ustawienia_TekstPrzyPrzelacznikuMowiDlaKogoJestOpcja_IDomyslnieJestWylaczona()
    {
        var html = await app.Klient().GetStringAsync("/ustawienia");

        var opis = Regex.Replace(Regex.Match(html, "<p id=\"mowa-opis\">(.*?)</p>", RegexOptions.Singleline).Groups[1].Value, "\\s+", " ").Trim();
        Assert.StartsWith("Ta opcja czyta opis miejsca głosem przeglądarki. Jeśli korzystasz z czytnika ekranu (NVDA, JAWS, VoiceOver), "
            + "zostaw ją wyłączoną — Twój czytnik już czyta całą stronę, a dwa głosy naraz nakładają się na siebie.", WebUtility.HtmlDecode(opis));
        var przelacznik = Regex.Match(html, "<input[^>]*id=\"pole-MowaWlaczona\"[^>]*>").Value;
        Assert.Contains("aria-describedby=\"mowa-opis\"", przelacznik);
        Assert.DoesNotContain("checked", przelacznik);
    }
}
