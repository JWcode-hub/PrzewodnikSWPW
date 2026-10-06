using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace PrzewodnikSWPW.UnitTests.Admin;

/// <summary>Strony wymagane przepisami (WF-31, WF-32, WF-33, WF-37, 06 §8) — przez HTTP, bez JavaScriptu.</summary>
[Collection(AplikacjaKolekcja.Nazwa)]
[Trait("Kategoria", "Integracyjne")]
public class InformacjeHttpTesty(AplikacjaFixture app)
{
    private static readonly string[] StronyWStopce = ["/deklaracja-dostepnosci", "/skroty-klawiszowe", "/zglos-problem", "/polityka-prywatnosci"];

    /// <summary>Kolejność identyfikatorów jak we wzorze Deklaracji (Zarządzenie 14/2021 §3 ust. 5).</summary>
    private static readonly string[] IdentyfikatoryA11y =
    [
        "a11y-wstep", "a11y-podmiot", "a11y-url", "a11y-data-publikacja", "a11y-data-aktualizacja", "a11y-status",
        "a11y-data-sporzadzenie", "a11y-ocena", "a11y-audytor", "a11y-kontakt", "a11y-osoba", "a11y-email", "a11y-telefon",
        "a11y-procedura", "a11y-architektura", "a11y-aplikacje",
    ];

    [Theory]
    [InlineData("/")]
    [InlineData("/spacer")]
    [InlineData("/szukaj")]
    [InlineData("/ustawienia")]
    [InlineData("/konto/logowanie")]
    [InlineData("/deklaracja-dostepnosci")]
    [InlineData("/zglos-problem")]
    public async Task KazdaStrona_StopkaZLinkamiDoCzterechStronInformacyjnych(string adres)
    {
        var html = await app.Klient().GetStringAsync(adres);

        var stopka = Regex.Match(html, "<footer.*?</footer>", RegexOptions.Singleline).Value;
        Assert.All(StronyWStopce, s => Assert.Contains($"href=\"{s}\"", stopka));
    }

    [Theory]
    [InlineData("/deklaracja-dostepnosci", "Deklaracja Dostępności")]
    [InlineData("/skroty-klawiszowe", "Skróty klawiszowe")]
    [InlineData("/zglos-problem", "Zgłoś brak dostępności")]
    [InlineData("/polityka-prywatnosci", "Polityka prywatności")]
    public async Task StronaInformacyjna_DzialaBezLogowania_ZUnikalnymTytulemINaglowkiem(string adres, string tytul)
    {
        var odpowiedz = await app.Klient().GetAsync(adres);
        var html = await odpowiedz.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, odpowiedz.StatusCode);
        Assert.Contains($"<title>{tytul} – Multimedialny przewodnik po Uczelni – SWPW</title>", html);
        Assert.Contains($"<h1>{tytul}</h1>", html);
    }

    [Fact]
    public async Task Deklaracja_WszystkieIdentyfikatoryA11yWKolejnosciWzoru()
    {
        var html = await app.Klient().GetStringAsync("/deklaracja-dostepnosci");

        var pozycje = IdentyfikatoryA11y.Select(id => (id, Pozycja: html.IndexOf($"id=\"{id}\""))).ToList();
        Assert.All(pozycje, p => Assert.True(p.Pozycja >= 0, $"Brak identyfikatora {p.id}."));
        Assert.Equal(pozycje.OrderBy(p => p.Pozycja).Select(p => p.id), pozycje.Select(p => p.id));
        Assert.All(IdentyfikatoryA11y, id => Assert.Single(Regex.Matches(html, $"id=\"{id}\"")));
    }

    [Fact]
    public async Task Deklaracja_StatusUstawaSkrotyZgloszenieIRpo_DaneZKonfiguracji()
    {
        var html = await app.Klient().GetStringAsync("/deklaracja-dostepnosci");

        Assert.Matches("<p id=\"a11y-status\">Strona internetowa jest <strong>częściowo zgodna</strong> z ustawą z dnia 4 kwietnia 2019 r.", html);
        Assert.Contains("<span id=\"a11y-podmiot\">Szkoła Wyższa im. Pawła Włodkowica w Płocku</span>", html);
        Assert.Contains("<time id=\"a11y-data-publikacja\" datetime=\"2026-09-29\">2026-09-29</time>", html); // z appsettings.json
        Assert.Contains("<kbd>Alt + 1</kbd>", WebUtility.HtmlDecode(html)); // „+” bywa zakodowany jako &#x2B;
        Assert.Contains("href=\"/skroty-klawiszowe\"", Regex.Match(html, "<h2>Skróty klawiszowe</h2>.*?<h2", RegexOptions.Singleline).Value);
        Assert.Contains("<a href=\"/zglos-problem\">formularz zgłoszenia braku dostępności</a>", html);
        Assert.Contains("<a href=\"https://bip.brpo.gov.pl/\">strona Rzecznika Praw Obywatelskich</a>", html);
        // Brak danych kontaktowych w konfiguracji (sprawa O-02) — strona mówi to wprost, nie wymyśla adresu.
        Assert.Contains("<span id=\"a11y-email\">do uzupełnienia przed publikacją serwisu</span>", html);
    }

    [Fact]
    public async Task Deklaracja_DostepnoscArchitektonicznaZPolaBudynku()
    {
        const string Opis = "Wejście główne bez progów, drzwi otwierane automatycznie.\n\nWinda z komunikatami głosowymi.";
        await using var db = app.Baza.UtworzKontekst();
        var budynek = await db.Budynki.FirstAsync(b => b.CzyAktywny);
        var poprzedni = budynek.OpisDostepnosciArchitektonicznej;
        budynek.OpisDostepnosciArchitektonicznej = Opis;
        await db.SaveChangesAsync();
        try
        {
            var html = await app.Klient().GetStringAsync("/deklaracja-dostepnosci");

            var sekcja = Regex.Match(html, "<h2 id=\"a11y-architektura\">.*?<h2 id=\"a11y-aplikacje\">", RegexOptions.Singleline).Value;
            Assert.Contains($"<h3>{budynek.Nazwa}", sekcja);
            Assert.Contains("<p>Wejście główne bez progów, drzwi otwierane automatycznie.</p>", sekcja); // akapity z pustych linii
            Assert.Contains("<p>Winda z komunikatami głosowymi.</p>", sekcja);
            Assert.Contains("tłumacza polskiego języka migowego", sekcja);
        }
        finally
        {
            budynek.OpisDostepnosciArchitektonicznej = poprzedni;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Skroty_TabelaSkrotowZD02_InformacjaOWylaczeniu()
    {
        var html = await app.Klient().GetStringAsync("/skroty-klawiszowe");

        Assert.Contains("<th scope=\"col\">Klawisze</th>", html);
        var wiersze = Regex.Matches(html, "<th scope=\"row\"><kbd>(.*?)</kbd></th>\\s*<td>(.*?)</td>").Select(m => (WebUtility.HtmlDecode(m.Groups[1].Value), WebUtility.HtmlDecode(m.Groups[2].Value))).ToList();
        Assert.Equal(["Alt + 1", "Alt + 2", "Alt + 3", "Alt + 4", "Alt + P"], wiersze.Select(w => w.Item1)); // kolejność D-01, skróty D-02
        Assert.StartsWith("Idź prosto", wiersze[0].Item2);
        Assert.DoesNotContain("Alt + S", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("Alt + T", WebUtility.HtmlDecode(html));
        Assert.Matches("<strong>Skróty można wyłączyć</strong> w <a href=\"/ustawienia\">ustawieniach dostępności</a>", html);
    }

    private async Task<HttpResponseMessage> Zglos(HttpClient klient, Dictionary<string, string> pola)
    {
        var formularz = await klient.GetStringAsync("/zglos-problem");
        pola["__RequestVerificationToken"] = AplikacjaFixture.Token(formularz);
        return await klient.PostAsync("/zglos-problem", new FormUrlEncodedContent(pola));
    }

    [Fact]
    public async Task Zgloszenie_Anonimowe_TylkoTresc_PotwierdzenieNaOsobnejStronie()
    {
        var klient = app.Klient();

        var odpowiedz = await Zglos(klient, new() { ["Tresc"] = "Zdjęcie na ekranie sali A15 nie ma opisu." });

        Assert.Equal(HttpStatusCode.Redirect, odpowiedz.StatusCode);
        Assert.Equal("/zglos-problem/wyslano", odpowiedz.Headers.Location!.OriginalString);
        var potwierdzenie = await klient.GetStringAsync("/zglos-problem/wyslano");
        // Wynik niesie tytuł i nagłówek z fokusem — bez powtórki w aria-live.
        Assert.Contains("<h1 tabindex=\"-1\" data-fokus-po-zaladowaniu>Zgłoszenie przyjęte</h1>", potwierdzenie);
        Assert.DoesNotContain("data-oglos", potwierdzenie);
        Assert.Contains("nie zostało zapisane ani wysłane", potwierdzenie); // uczciwie: wersja demonstracyjna
    }

    [Fact]
    public async Task Zgloszenie_BezTresciIZBlednymEmailem_BledyPrzyPolach_DaneZostaja()
    {
        var html = await (await Zglos(app.Klient(), new() { ["Tresc"] = "", ["Email"] = "nie-email", ["ImieNazwisko"] = "Anna Nowak" }))
            .Content.ReadAsStringAsync();

        Assert.Contains("<title>Błąd w formularzu – Zgłoś brak dostępności – Multimedialny przewodnik po Uczelni – SWPW</title>", html);
        Assert.Contains("Błąd: Opisz problem — pole „Treść zgłoszenia” jest wymagane.", html);
        Assert.Contains("Błąd: Wpisz adres e-mail w postaci nazwa@domena.pl albo zostaw pole puste.", html);
        Assert.Contains("value=\"Anna Nowak\"", html);
    }

    [Fact]
    public async Task Zgloszenie_PolaOpcjonalneBezOznaczeniaWymagane_KlauzulaRodo()
    {
        var html = await app.Klient().GetStringAsync("/zglos-problem");

        Assert.Matches("<label[^>]*for=\"Tresc\"[^>]*>Treść zgłoszenia \\(wymagane\\)</label>", html);
        Assert.Matches("<label[^>]*for=\"Email\"[^>]*>Adres e-mail do odpowiedzi \\(opcjonalnie\\)</label>", html);
        Assert.Matches("<label[^>]*for=\"ImieNazwisko\"[^>]*>Imię i nazwisko \\(opcjonalnie\\)</label>", html);
        Assert.Contains("<h2 id=\"klauzula-rodo\" class=\"h5\">Informacja o przetwarzaniu danych osobowych</h2>", html);
        Assert.Contains("art. 6 ust. 1 lit. c RODO", html);
        Assert.Contains("24 miesiące", html);
    }

    [Fact]
    public async Task PolitykaPrywatnosci_RodoCookiesRetencja24Miesiace()
    {
        var html = await app.Klient().GetStringAsync("/polityka-prywatnosci");

        Assert.Contains("<code>przewodnik_ustawienia</code>", html);
        Assert.Contains("<code>przewodnik_sesja</code>", html);
        Assert.Contains("<td>24 miesiące od rozpatrzenia zgłoszenia</td>", html);
        Assert.Contains("<a href=\"https://uodo.gov.pl/\">strona Urzędu Ochrony Danych Osobowych</a>", html);
    }
}
