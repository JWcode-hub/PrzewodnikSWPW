using System.Net;
using Microsoft.EntityFrameworkCore;

namespace PrzewodnikSWPW.UnitTests.Admin;

/// <summary>Panel administratora przez HTTP — autoryzacja, blokada konta, antiforgery, formularze.</summary>
[Collection(AplikacjaKolekcja.Nazwa)]
[Trait("Kategoria", "Integracyjne")]
public class PanelHttpTesty(AplikacjaFixture app)
{
    /// <summary>Znacznik otwierający elementu o danym id — asercje nie zależą od kolejności atrybutów.</summary>
    private static string Znacznik(string html, string id) =>
        System.Text.RegularExpressions.Regex.Match(html, $"<(input|select|textarea)[^>]*\\bid=\"{id}\"[^>]*>").Value;

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/budynki")]
    [InlineData("/admin/kierunki?punkt=1")]
    [InlineData("/admin/rejestr-zmian")]
    public async Task Anonim_PrzekierowanyNaLogowanie(string adres)
    {
        var odpowiedz = await app.Klient().GetAsync(adres);

        Assert.Equal(HttpStatusCode.Redirect, odpowiedz.StatusCode);
        Assert.StartsWith("/konto/logowanie", odpowiedz.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Redaktor_NieMaDostepuDoPanelu()
    {
        var klient = app.Klient();
        Assert.Equal(HttpStatusCode.Redirect, (await app.Zaloguj(klient, AplikacjaFixture.EmailRedaktora, app.Haslo)).StatusCode);

        var odpowiedz = await klient.GetAsync("/admin/budynki");

        Assert.Equal(HttpStatusCode.Redirect, odpowiedz.StatusCode);
        Assert.StartsWith("/konto/brak-dostepu", odpowiedz.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task PiecNieudanychProb_BlokujaKonto_NawetPoprawneHasloNieWpuszcza()
    {
        var klient = app.Klient();
        for (var i = 0; i < 5; i++)
        {
            var zle = await app.Zaloguj(klient, AplikacjaFixture.EmailDoBlokady, "zle-haslo-" + i);
            Assert.Equal(HttpStatusCode.OK, zle.StatusCode); // formularz z błędem, bez przekierowania
        }

        var poprawne = await app.Zaloguj(klient, AplikacjaFixture.EmailDoBlokady, app.Haslo);
        var html = await poprawne.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, poprawne.StatusCode);
        Assert.Contains("zablokowane na 15 minut", html);
    }

    [Fact]
    public async Task ZleHaslo_KomunikatNieZdradzaCzyKontoIstnieje()
    {
        var klient = app.Klient();

        var istniejace = await (await app.Zaloguj(klient, AplikacjaFixture.EmailRedaktora, "zle-haslo")).Content.ReadAsStringAsync();
        var nieistniejace = await (await app.Zaloguj(klient, "nikt@testy.test", "zle-haslo")).Content.ReadAsStringAsync();

        Assert.Contains("Nieprawidłowy adres e-mail lub hasło.", istniejace);
        Assert.Contains("Nieprawidłowy adres e-mail lub hasło.", nieistniejace);
    }

    [Fact]
    public async Task PostBezTokenuAntiforgery_Odrzucony()
    {
        var klient = app.Klient();
        await app.Zaloguj(klient, AplikacjaFixture.EmailAdministratora, app.Haslo);

        var odpowiedz = await klient.PostAsync("/admin/budynki/nowy",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Kod"] = "X", ["Nazwa"] = "Bez tokenu" }));

        Assert.Equal(HttpStatusCode.BadRequest, odpowiedz.StatusCode);
    }

    [Fact]
    public async Task BladWalidacji_FokusNaPierwszymBlednymPolu_WpisaneDaneZostaja()
    {
        var klient = app.Klient();
        await app.Zaloguj(klient, AplikacjaFixture.EmailAdministratora, app.Haslo);
        var formularz = await klient.GetStringAsync("/admin/budynki/nowy");

        var odpowiedz = await klient.PostAsync("/admin/budynki/nowy", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AplikacjaFixture.Token(formularz),
            ["Kod"] = "",
            ["Nazwa"] = "Budynek wpisany przed błędem",
            ["Adres"] = "ul. Testowa 1",
        }));
        var html = await odpowiedz.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, odpowiedz.StatusCode);
        // O błędzie mówi tytuł i pole z fokusem; podsumowanie nie jest drugim obszarem live (zasada ogłaszania, site.js).
        Assert.Contains("<title>Błąd w formularzu – Nowy budynek – ", html);
        Assert.DoesNotContain("role=\"alert\"", html);
        // Błąd tekstem „Błąd: …”, powiązany przez aria-describedby, z aria-invalid i autofocus na polu Kod.
        var poleKod = Znacznik(html, "Kod");
        Assert.Contains("aria-describedby=\"Kod-wskazowka Kod-blad\"", poleKod);
        Assert.Contains("aria-invalid=\"true\"", poleKod);
        Assert.Contains("autofocus=\"autofocus\"", poleKod);
        Assert.Contains("<p id=\"Kod-blad\" class=\"komunikat-bledu\">Błąd: Pole „Kod budynku” jest wymagane.</p>", html);
        Assert.Matches("<label[^>]*\\bfor=\"Kod\"[^>]*>Kod budynku \\(wymagane\\)</label>", html);
        // Dane nie znikają, a autofocus jest tylko jeden.
        Assert.Contains("value=\"Budynek wpisany przed błędem\"", html);
        Assert.Contains("value=\"ul. Testowa 1\"", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "autofocus="));
        Assert.DoesNotContain("placeholder=", html);
    }

    [Fact]
    public async Task FormularzKierunku_WszystkieBledyNaRaz_FokusNaPierwszymWKolejnosciPol()
    {
        var klient = app.Klient();
        await app.Zaloguj(klient, AplikacjaFixture.EmailAdministratora, app.Haslo);
        var formularz = await klient.GetStringAsync("/admin/kierunki/nowy?punkt=1&azymut=90");
        Assert.Contains("checked=\"checked\"", Znacznik(formularz, "UtworzPowrotny")); // WF-24: domyślnie zaznaczone

        var html = await (await klient.PostAsync("/admin/kierunki/nowy", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AplikacjaFixture.Token(formularz),
            ["PunktZrodlowyId"] = "1",
            ["Azymut"] = "90",
            ["RodzajCelu"] = "punkt",
            ["RodzajPrzejscia"] = "Korytarz",
            ["UtworzPowrotny"] = "true",
            ["OpisPrzejscia"] = "Wzdłuż ściany z tablicą ogłoszeń.",
        }))).Content.ReadAsStringAsync();

        Assert.Contains("Wybierz punkt docelowy z listy.", html);           // reguła między polami
        Assert.Contains("Pole „Odległość w metrach” jest wymagane.", html); // atrybut pola
        Assert.Contains("autofocus=\"autofocus\"", Znacznik(html, "PunktDocelowyId"));
        Assert.DoesNotContain("autofocus", Znacznik(html, "Waga"));
        Assert.Contains("Wzdłuż ściany z tablicą ogłoszeń.", html);
    }

    [Fact]
    public async Task TypyPunktow_BezStronyWPanelu_AlePoleWFormularzuPunktuZostaje()
    {
        var klient = app.Klient();
        await app.Zaloguj(klient, AplikacjaFixture.EmailAdministratora, app.Haslo);

        var panel = await klient.GetStringAsync("/admin");
        Assert.DoesNotContain("Typy punktów", panel);
        Assert.Contains(">Typy sal</a>", panel);
        Assert.Equal(HttpStatusCode.NotFound, (await klient.GetAsync("/admin/typy-punktow")).StatusCode);

        // Typ punktu nadal wybiera się przy punkcie ruchu — z pięciu typów z migracji.
        var formularz = await klient.GetStringAsync("/admin/punkty/nowy");
        var lista = System.Text.RegularExpressions.Regex.Match(formularz, "<select[^>]*id=\"TypPunktuId\".*?</select>",
            System.Text.RegularExpressions.RegexOptions.Singleline).Value;
        Assert.Contains(">Korytarz</option>", lista);
        Assert.Contains(">Przed windą</option>", lista);
    }

    [Fact]
    public async Task FormularzKierunku_BladGrupyOpcji_FokusNaPierwszejOpcjiGrupy()
    {
        var klient = app.Klient();
        await app.Zaloguj(klient, AplikacjaFixture.EmailAdministratora, app.Haslo);
        var formularz = await klient.GetStringAsync("/admin/kierunki/nowy?punkt=1&azymut=90");

        // Nieznany RodzajCelu: grupa przycisków opcji jest pierwszym błędnym polem w kolejności kodu strony.
        var html = await (await klient.PostAsync("/admin/kierunki/nowy", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AplikacjaFixture.Token(formularz),
            ["PunktZrodlowyId"] = "1",
            ["Azymut"] = "90",
            ["RodzajCelu"] = "winda",
            ["RodzajPrzejscia"] = "Korytarz",
        }))).Content.ReadAsStringAsync();

        Assert.Contains("id=\"RodzajCelu-blad\"", html);
        Assert.Contains("autofocus=\"autofocus\"", Znacznik(html, "cel-punkt"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "autofocus="));
    }

    [Fact]
    public async Task Administrator_DodajeBudynek_ZmianaTrafiaDoRejestru()
    {
        var klient = app.Klient();
        await app.Zaloguj(klient, AplikacjaFixture.EmailAdministratora, app.Haslo);
        var formularz = await klient.GetStringAsync("/admin/budynki/nowy");
        var kod = $"T{Random.Shared.Next(100, 999)}";

        var odpowiedz = await klient.PostAsync("/admin/budynki/nowy", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AplikacjaFixture.Token(formularz),
            ["Kod"] = kod,
            ["Nazwa"] = "Budynek testowy",
            ["CzyAktywny"] = "true",
        }));

        Assert.Equal(HttpStatusCode.Redirect, odpowiedz.StatusCode);
        var lista = await klient.GetStringAsync(odpowiedz.Headers.Location!.ToString());
        // Wynik operacji: w tytule (bez JavaScriptu) i w komunikacie, na który trafia fokus — nie w aria-live.
        Assert.Contains("<title>Zapisano – Budynki – ", lista);
        Assert.Contains($"<p class=\"komunikat komunikat--sukces\" tabindex=\"-1\" data-fokus-po-zaladowaniu>Dodano budynek {kod}.</p>", lista);
        Assert.DoesNotContain("data-oglos", lista);
        // Menu panelu stoi przed <main>, więc link „Przejdź do treści głównej” je omija.
        Assert.True(lista.IndexOf("Menu panelu administratora", StringComparison.Ordinal) < lista.IndexOf("<main", StringComparison.Ordinal));

        await using var db = app.Baza.UtworzKontekst();
        var admin = await db.Users.SingleAsync(u => u.Email == AplikacjaFixture.EmailAdministratora);
        var budynek = await db.Budynki.SingleAsync(b => b.Kod == kod);
        var wpis = await db.WpisyAudytu.SingleAsync(w => w.Encja == "Budynek" && w.KluczEncji == budynek.Id.ToString() && w.Operacja == "INSERT");
        Assert.Equal(admin.Id, wpis.UzytkownikId);
        Assert.Contains("\"Nazwa\":\"Budynek testowy\"", wpis.WartosciNowe);
    }

    [Fact]
    public async Task EdycjaBudynku_PoleWejsciaGlownego_ZPunktamiTegoBudynku()
    {
        var klient = app.Klient();
        await app.Zaloguj(klient, AplikacjaFixture.EmailAdministratora, app.Haslo);

        var html = await klient.GetStringAsync("/admin/budynki/1/edytuj");

        Assert.Matches("<label[^>]*\\bfor=\"PunktWejsciaGlownegoId\"[^>]*>Punkt wejścia głównego</label>", html);
        Assert.Matches("<option selected=\"selected\" value=\"\\d+\">A-0-P01 — Wejście główne</option>", html);
    }

    [Fact]
    public async Task WalidacjaGrafu_RaportZPodsumowaniemIDostepemTylkoDlaAdministratora()
    {
        Assert.Equal(HttpStatusCode.Redirect, (await app.Klient().GetAsync("/admin/walidacja-grafu")).StatusCode);

        var klient = app.Klient();
        await app.Zaloguj(klient, AplikacjaFixture.EmailAdministratora, app.Haslo);
        var html = await klient.GetStringAsync("/admin/walidacja-grafu");

        Assert.Contains("<h1>Walidacja grafu</h1>", html);
        Assert.Matches("tabindex=\"-1\" data-fokus-po-zaladowaniu>\\s*<strong>Walidacja zakończona\\. Znaleziono problemów: \\d+\\. Błędy krytyczne: \\d+\\. Ostrzeżenia: \\d+\\.</strong>", html);
        Assert.DoesNotContain("data-oglos", html);
        Assert.Contains("<th scope=\"col\">Jak naprawić</th>", html);
        // „Popraw” z ukrytym dopowiedzeniem — nazwa linku zrozumiała poza tabelą.
        Assert.Matches("<a href=\"/admin/punkty/\\d+/edytuj\">Popraw<span class=\"visually-hidden\"> punkt A-0-P08</span></a>", html);
        Assert.Matches("<a[^>]*aria-current=\"page\"[^>]*>Walidacja grafu</a>", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "aria-current=\"page\"")); // „Panel” nie jest drugą bieżącą stroną
    }

    [Fact]
    public async Task PotwierdzenieUsuniecia_ToOsobnaStronaBezJavaScriptu()
    {
        var klient = app.Klient();
        await app.Zaloguj(klient, AplikacjaFixture.EmailAdministratora, app.Haslo);

        var html = await klient.GetStringAsync("/admin/budynki/1/dezaktywuj");

        Assert.Contains("<form method=\"post\" action=\"/admin/budynki/1/dezaktywuj\">", html);
        Assert.Contains("Tak, dezaktywuj budynek A", html);
        Assert.Contains("Anuluj i wróć do listy budynków", html);
        Assert.DoesNotContain("confirm(", html);
    }
}
