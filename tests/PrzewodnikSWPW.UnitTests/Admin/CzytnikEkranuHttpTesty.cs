using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace PrzewodnikSWPW.UnitTests.Admin;

/// <summary>
/// Ścieżka czytnika ekranu (etap 13, docs/RAPORT_CZYTNIK.md) — przez HTTP, czyli z definicji bez JavaScriptu:
/// struktura nagłówków, jedna zasada ogłaszania (pusty aria-live po przeładowaniu), cel fokusu
/// oraz pełne przejście spacer → wyszukiwanie → trasa → zmiana motywu (WN-26).
/// </summary>
[Collection(AplikacjaKolekcja.Nazwa)]
[Trait("Kategoria", "Integracyjne")]
public partial class CzytnikEkranuHttpTesty(AplikacjaFixture app)
{
    private const string PustyObszarLive = "<div id=\"komunikaty\" role=\"status\" aria-live=\"polite\" aria-atomic=\"true\"></div>";

    /// <summary>GET z ręcznym podążaniem za przekierowaniami — klient testowy ich nie wykonuje sam.</summary>
    private static async Task<(string Adres, string Html)> Otworz(HttpClient klient, string adres)
    {
        for (var i = 0; i < 5; i++)
        {
            var odpowiedz = await klient.GetAsync(adres);
            if ((int)odpowiedz.StatusCode is >= 300 and < 400)
            {
                adres = odpowiedz.Headers.Location!.OriginalString;
                continue;
            }

            Assert.Equal(HttpStatusCode.OK, odpowiedz.StatusCode);
            return (adres, await odpowiedz.Content.ReadAsStringAsync());
        }

        throw new InvalidOperationException($"Za dużo przekierowań: {adres}");
    }

    private static async Task<(string Adres, string Html)> Wyslij(HttpClient klient, string adres, string htmlZTokenem, Dictionary<string, string> pola)
    {
        pola["__RequestVerificationToken"] = AplikacjaFixture.Token(htmlZTokenem);
        var odpowiedz = await klient.PostAsync(adres, new FormUrlEncodedContent(pola));
        if (odpowiedz.StatusCode == HttpStatusCode.OK)
        {
            return (adres, await odpowiedz.Content.ReadAsStringAsync());
        }

        Assert.Equal(HttpStatusCode.Redirect, odpowiedz.StatusCode);
        return await Otworz(klient, odpowiedz.Headers.Location!.OriginalString);
    }

    private static string Tresc(string html) => MainRegex().Match(html).Value;

    private static string Tytul(string html) => WebUtility.HtmlDecode(TytulRegex().Match(html).Groups[1].Value);

    /// <summary>Wspólne wymagania struktury dla każdej strony: jeden h1, brak przeskoków, jeden pusty obszar live.</summary>
    private static void SprawdzStrukture(string html, string adres)
    {
        var poziomy = NaglowekRegex().Matches(html).Select(m => int.Parse(m.Groups[1].Value)).ToList();
        Assert.True(poziomy.Count(p => p == 1) == 1, $"{adres}: liczba <h1> = {poziomy.Count(p => p == 1)}");
        Assert.Equal(1, poziomy[0]);
        Assert.All(poziomy.Zip(poziomy.Skip(1)), para =>
            Assert.True(para.Second <= para.First + 1, $"{adres}: przeskok nagłówków h{para.First} → h{para.Second}"));

        // Zasada ogłaszania: po pełnym przeładowaniu obszar live jest pusty i jest jedyny.
        Assert.Contains(PustyObszarLive, html);
        Assert.Single(Regex.Matches(html, "aria-live="));
        Assert.DoesNotContain("role=\"alert\"", html);
        Assert.DoesNotContain("data-oglos", html);

        Assert.Contains("<html lang=\"pl\"", html);
        Assert.EndsWith(" – Multimedialny przewodnik po Uczelni – SWPW", Tytul(html));
        Assert.All(ImgRegex().Matches(html), img => Assert.Contains(" alt=\"", img.Value));
        Assert.DoesNotContain("placeholder=", html);
        Assert.DoesNotContain("onclick=", html);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/spacer")]
    [InlineData("/spacer/A")]
    [InlineData("/szukaj")]
    [InlineData("/szukaj?q=A15")]
    [InlineData("/szukaj?q=nie-ma-takiej-sali")]
    [InlineData("/szukaj/sale/A")]
    [InlineData("/trasa")]
    [InlineData("/ustawienia")]
    [InlineData("/konto/logowanie")]
    [InlineData("/deklaracja-dostepnosci")]
    [InlineData("/skroty-klawiszowe")]
    [InlineData("/zglos-problem")]
    [InlineData("/polityka-prywatnosci")]
    public async Task StronaPubliczna_JedenH1_BezPrzeskokowNaglowkow_PustyObszarLive(string adres)
    {
        var (_, html) = await Otworz(app.Klient(), adres);

        SprawdzStrukture(html, adres);
    }

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/budynki")]
    [InlineData("/admin/budynki/nowy")]
    [InlineData("/admin/pietra")]
    [InlineData("/admin/sale")]
    [InlineData("/admin/punkty")]
    [InlineData("/admin/kierunki?punkt=1")]
    [InlineData("/admin/kierunki/nowy?punkt=1&azymut=90")]
    [InlineData("/admin/walidacja-grafu")]
    [InlineData("/admin/rejestr-zmian")]
    [InlineData("/konto/zmiana-hasla")]
    public async Task StronaPanelu_JedenH1_BezPrzeskokowNaglowkow_PustyObszarLive(string adres)
    {
        var klient = app.Klient();
        await app.Zaloguj(klient, AplikacjaFixture.EmailAdministratora, app.Haslo);

        var (_, html) = await Otworz(klient, adres);

        SprawdzStrukture(html, adres);
    }

    /// <summary>Zadanie z pkt 5 etapu 13 — klient HTTP nie wykonuje JavaScriptu, więc każdy krok to zwykłe żądanie.</summary>
    [Fact]
    public async Task BezJavaScriptu_WyborBudynku_TrzyKrokiSpaceru_Wyszukanie_Trasa_ZmianaMotywu()
    {
        var klient = app.Klient();

        // 1. Wybór budynku: strona główna → spacer → budynek A → miejsce startowe.
        var (_, glowna) = await Otworz(klient, "/");
        Assert.Contains("href=\"/spacer\"", glowna);
        var (_, budynki) = await Otworz(klient, "/spacer");
        Assert.Contains("href=\"/spacer/A\"", budynki);
        var (_, budynek) = await Otworz(klient, "/spacer/A");
        var start = WebUtility.HtmlDecode(Regex.Match(budynek, "<a href=\"([^\"]+)\" class=\"btn btn-primary\">Rozpocznij spacer od miejsca").Groups[1].Value);
        Assert.NotEmpty(start);

        // 2. Trzy kroki spaceru: za każdym razem pierwszy kierunek, który jest linkiem.
        var (adres, miejsce) = await Otworz(klient, start);
        var odwiedzone = new List<string> { Tytul(miejsce) };
        for (var krok = 0; krok < 3; krok++)
        {
            SprawdzStrukture(miejsce, adres);
            // Nowe miejsce: fokus na nazwie miejsca; cztery pozycje w stałej kolejności (D-01), także te bez przejścia.
            Assert.Matches("<h1 id=\"naglowek-miejsca\" tabindex=\"-1\" data-fokus-po-zaladowaniu(=\"\")?>", miejsce);
            Assert.Equal(["prosto", "lewo", "prawo", "tyl"],
                Regex.Matches(miejsce, "<li data-kierunek=\"([a-z]+)\">").Select(m => m.Groups[1].Value));

            var dalej = WebUtility.HtmlDecode(Regex.Match(miejsce, "<a href=\"([^\"]+)\" class=\"kierunek\">").Groups[1].Value);
            Assert.NotEmpty(dalej);
            (adres, miejsce) = await Otworz(klient, dalej);
            odwiedzone.Add(Tytul(miejsce));
        }

        Assert.Equal(4, odwiedzone.Count);
        Assert.All(odwiedzone.Zip(odwiedzone.Skip(1)), para => Assert.NotEqual(para.First, para.Second)); // każdy krok zmienia tytuł

        // 3. Wyszukanie sali: POST → przekierowanie → wynik w tytule i w nagłówku z fokusem.
        var (_, formularzSzukania) = await Otworz(klient, "/szukaj");
        var (adresWynikow, wyniki) = await Wyslij(klient, "/szukaj", formularzSzukania, new() { ["q"] = "A15" });
        Assert.StartsWith("/szukaj?q=A15", adresWynikow);
        SprawdzStrukture(wyniki, adresWynikow);
        var naglowek = WebUtility.HtmlDecode(Regex.Match(wyniki, "<h1 id=\"naglowek-wynikow\" tabindex=\"-1\" data-fokus-po-zaladowaniu(?:=\"\")?>([^<]+)</h1>").Groups[1].Value);
        Assert.StartsWith("Znaleziono ", naglowek);
        Assert.StartsWith(naglowek, Tytul(wyniki));
        var linkTrasy = WebUtility.HtmlDecode(Regex.Match(wyniki, "<a href=\"(/trasa\\?do=\\d+)\">Wyznacz trasę do sali A15</a>").Groups[1].Value);
        Assert.NotEmpty(linkTrasy);

        // 4. Wyznaczenie trasy: sala docelowa z linku wyniku, początkowa — pierwsza inna z listy.
        var (_, formularzTrasy) = await Otworz(klient, linkTrasy);
        var doSali = Regex.Match(linkTrasy, "\\d+").Value;
        var zSali = Regex.Matches(Regex.Match(formularzTrasy, "<select id=\"pole-Z\".*?</select>", RegexOptions.Singleline).Value, "<option value=\"(\\d+)\"")
            .Select(m => m.Groups[1].Value).First(id => id != doSali);
        var (adresTrasy, trasa) = await Wyslij(klient, "/trasa", formularzTrasy, new() { ["Z"] = zSali, ["Do"] = doSali, ["TrybWindy"] = "false" });
        Assert.StartsWith("/trasa/wynik", adresTrasy);
        SprawdzStrukture(trasa, adresTrasy);
        Assert.Matches("<h1 id=\"naglowek-trasy\" tabindex=\"-1\" data-fokus-po-zaladowaniu(=\"\")?>Trasa z sali \\S+ do sali A15</h1>", trasa);
        Assert.Matches("<ol class=\"lista-krokow\">\\s*<li>", trasa);

        // 5. Zmiana motywu w ustawieniach: POST → przekierowanie → motyw w <html>, wynik w tytule i w komunikacie z fokusem.
        var (_, ustawienia) = await Otworz(klient, "/ustawienia");
        var (adresUstawien, zapisane) = await Wyslij(klient, "/ustawienia", ustawienia, new()
        {
            ["Motyw"] = "WysokiKontrast", ["RozmiarTekstu"] = "100", ["TempoMowy"] = "100",
            ["MowaWlaczona"] = "false", ["SkrotyWlaczone"] = "true", ["OgraniczAnimacje"] = "false",
        });
        Assert.Equal("/ustawienia", adresUstawien);
        SprawdzStrukture(zapisane, adresUstawien);
        Assert.Contains("data-motyw=\"wysoki-kontrast\"", zapisane);
        Assert.StartsWith("Zapisano – Ustawienia dostępności", Tytul(zapisane));
        Assert.Contains("<p class=\"komunikat komunikat--sukces\" tabindex=\"-1\" data-fokus-po-zaladowaniu>Ustawienia zostały zapisane.</p>", zapisane);

        // Szybki przełącznik w stopce wraca na stronę, z której go użyto.
        var (adresPoStopce, poStopce) = await Wyslij(klient, "/ustawienia/motyw", zapisane, new() { ["motyw"] = "Ciemny", ["powrot"] = "/spacer/A" });
        Assert.Equal("/spacer/A", adresPoStopce);
        Assert.Contains("data-motyw=\"ciemny\"", poStopce);
    }

    [Fact]
    public async Task Trasa_BladWalidacji_FokusNaPierwszymBlednymPolu_BezRoliAlert()
    {
        var klient = app.Klient();
        var (_, formularz) = await Otworz(klient, "/trasa");

        var (_, html) = await Wyslij(klient, "/trasa", formularz, new() { ["Z"] = "", ["Do"] = "", ["TrybWindy"] = "false" });

        SprawdzStrukture(html, "/trasa (błąd)");
        Assert.StartsWith("Błąd w formularzu – Wyznacz trasę", Tytul(html));
        var poleZ = Regex.Match(html, "<select id=\"pole-Z\"[^>]*>").Value;
        var poleDo = Regex.Match(html, "<select id=\"pole-Do\"[^>]*>").Value;
        Assert.Contains("autofocus=\"autofocus\"", poleZ);
        Assert.Contains("aria-invalid=\"true\"", poleZ);
        Assert.Contains("aria-describedby=\"blad-Z\"", poleZ);
        Assert.Contains("aria-invalid=\"true\"", poleDo);
        Assert.DoesNotContain("autofocus", poleDo);
        // Nagłówek nie konkuruje o fokus z błędnym polem.
        Assert.Contains("<h1 id=\"naglowek-strony\" tabindex=\"-1\">Wyznacz trasę</h1>", html);
    }

    [Fact]
    public async Task Ustawienia_BladWalidacji_FokusNaPierwszejOpcjiBlednejGrupy()
    {
        var klient = app.Klient();
        var (_, formularz) = await Otworz(klient, "/ustawienia");

        var (_, html) = await Wyslij(klient, "/ustawienia", formularz, new()
        {
            ["Motyw"] = "Rozowy", ["RozmiarTekstu"] = "100", ["TempoMowy"] = "100",
            ["MowaWlaczona"] = "false", ["SkrotyWlaczone"] = "true", ["OgraniczAnimacje"] = "false",
        });

        SprawdzStrukture(html, "/ustawienia (błąd)");
        Assert.StartsWith("Błąd w formularzu – Ustawienia dostępności", Tytul(html));
        Assert.Contains("id=\"blad-Motyw\"", html);
        Assert.Single(Regex.Matches(html, "autofocus="));
        Assert.Contains("autofocus=\"autofocus\"", Regex.Match(html, "<input[^>]*id=\"motyw-Systemowy\"[^>]*>").Value);
    }

    [Fact]
    public async Task WynikiWyszukiwania_BrakWynikow_NaglowekZFokusemIDrogaNaprawy()
    {
        var (_, html) = await Otworz(app.Klient(), "/szukaj?q=nie-ma-takiej-sali");

        Assert.Matches("<h1 id=\"naglowek-wynikow\" tabindex=\"-1\" data-fokus-po-zaladowaniu(=\"\")?>Nie znaleziono sali", html);
        Assert.StartsWith("Nie znaleziono sali", Tytul(html));
        Assert.Contains("Lista wszystkich sal:", Tresc(html));
    }

    [Fact]
    public async Task FormularzWyszukiwania_PrzedWyszukaniem_NaglowekBezFokusu()
    {
        var (_, html) = await Otworz(app.Klient(), "/szukaj");

        // Zwykłe wejście na stronę: fokus zostaje na początku dokumentu, czytnik zaczyna od tytułu.
        Assert.Matches("<h1 id=\"naglowek-wynikow\" tabindex=\"-1\" ?>Wyszukaj salę</h1>", html);
    }

    [Fact]
    public async Task Spacer_BrakPrzejscia_FokusNaKomunikacieNieNaNazwieMiejsca()
    {
        await using var db = app.Baza.UtworzKontekst();
        var sciana = await db.Kierunki.FirstAsync(k => !k.CzyAktywny);

        // Zwrot równy azymutowi krawędzi → kierunek względny „prosto” (zasada 2 z CLAUDE.md).
        var (adres, html) = await Otworz(app.Klient(), $"/spacer/idz?zPunktu={sciana.PunktZrodlowyId}&kierunek=prosto&zwrot={sciana.Azymut}");

        SprawdzStrukture(html, adres);
        Assert.StartsWith("Brak przejścia – ", Tytul(html));
        // Użytkownik został w tym samym miejscu: fokus dostaje komunikat, a nagłówek miejsca — nie.
        Assert.Matches("<h1 id=\"naglowek-miejsca\" tabindex=\"-1\" ?>", html);
        Assert.Matches("<p class=\"komunikat komunikat--blad\" tabindex=\"-1\" data-fokus-po-zaladowaniu(=\"\")?>[^<]+</p>", html);
        Assert.Single(Regex.Matches(html, "data-fokus-po-zaladowaniu"));
    }

    [GeneratedRegex("<h([1-6])[\\s>]")]
    private static partial Regex NaglowekRegex();

    [GeneratedRegex("<img\\b[^>]*>")]
    private static partial Regex ImgRegex();

    [GeneratedRegex("<title>(.*?)</title>", RegexOptions.Singleline)]
    private static partial Regex TytulRegex();

    [GeneratedRegex("<main\\b.*?</main>", RegexOptions.Singleline)]
    private static partial Regex MainRegex();
}
