using Microsoft.Playwright;

namespace PrzewodnikSWPW.A11yTests;

/// <summary>Automatyczne sprawdzenia każdej kluczowej strony w prawdziwej przeglądarce (06 §6.1, etap 16 pkt 2).</summary>
[Collection(AplikacjaKolekcja.Nazwa)]
[Trait("Kategoria", "Dostepnosc")]
public class StronyKluczoweTesty(AplikacjaFixture app)
{
    private const string KoniecTytulu = " – Multimedialny przewodnik po Uczelni – SWPW";

    public static TheoryData<string> Nazwy => new(Strony.Kluczowe);

    [Theory]
    [MemberData(nameof(Nazwy))]
    public async Task Strona_BezNaruszenAxeCriticalISerious(string nazwa)
    {
        var strona = await Strony.Otworz(app, nazwa);

        var naruszenia = await Strony.PowazneNaruszeniaAxe(strona);

        Assert.True(naruszenia.Count == 0, $"{nazwa}:\n{string.Join("\n", naruszenia)}");
    }

    [Theory]
    [MemberData(nameof(Nazwy))]
    public async Task Strona_JedenH1_BezPrzeskokowNaglowkow_AltIEtykiety_JezykITytul(string nazwa)
    {
        var strona = await Strony.Otworz(app, nazwa);

        var s = await Strony.OdczytajStrukture(strona);

        // Dokładnie jeden <h1> — regresja, która już raz wystąpiła (Views/Szukaj/Index.cshtml, etap 13).
        Assert.True(s.Naglowki.Count(p => p == 1) == 1, $"{nazwa}: liczba <h1> = {s.Naglowki.Count(p => p == 1)}");
        Assert.Equal(1, s.Naglowki[0]);
        Assert.All(s.Naglowki.Zip(s.Naglowki.Skip(1)), para =>
            Assert.True(para.Second <= para.First + 1, $"{nazwa}: przeskok nagłówków h{para.First} → h{para.Second}"));

        Assert.True(s.ObrazyBezAlt.Length == 0, $"{nazwa}: <img> bez alt: {string.Join(" | ", s.ObrazyBezAlt)}");
        Assert.True(s.PolaBezEtykiety.Length == 0, $"{nazwa}: pola bez etykiety: {string.Join(" | ", s.PolaBezEtykiety)}");

        Assert.Equal("pl", s.Jezyk);
        Assert.EndsWith(KoniecTytulu, s.Tytul);
        Assert.True(s.Tytul.Length > KoniecTytulu.Length, $"{nazwa}: tytuł bez nazwy strony");
    }

    [Fact]
    public async Task KazdaKluczowaStrona_MaInnyTytul()
    {
        var tytuly = new Dictionary<string, string>();
        foreach (var nazwa in Strony.Kluczowe)
        {
            var strona = await Strony.Otworz(app, nazwa);
            tytuly[nazwa] = await strona.TitleAsync();
        }

        var powtorzone = tytuly.GroupBy(t => t.Value).Where(g => g.Count() > 1)
            .Select(g => $"„{g.Key}”: {string.Join(", ", g.Select(t => t.Key))}");
        Assert.Empty(powtorzone);
    }

    /// <summary>
    /// Test kontrolny narzędzia: celowo zepsuta strona MUSI dać naruszenia. Bez niego źle skonfigurowany
    /// axe (albo nasz filtr wagi) dawałby wieczną zieleń, której nikt by nie zauważył.
    /// </summary>
    [Fact]
    public async Task Kontrolny_CelowoZepsutaStrona_AxeISprawdzenieStrukturyJeWykrywaja()
    {
        var strona = await app.NowaStrona();
        await strona.GotoAsync("/");
        await strona.EvaluateAsync(
            """
            () => document.querySelector('main').insertAdjacentHTML('beforeend',
              '<h1>Drugi nagłówek pierwszego poziomu</h1><h4>Przeskok poziomu</h4>' +
              '<img src="/favicon.ico"><input type="text" id="bez-etykiety">' +
              '<p style="color:#bbb;background:#fff">Tekst o zbyt niskim kontraście</p>')
            """);

        var naruszenia = await Strony.PowazneNaruszeniaAxe(strona);
        var s = await Strony.OdczytajStrukture(strona);

        Assert.Contains(naruszenia, n => n.Contains("image-alt"));
        Assert.Contains(naruszenia, n => n.Contains("color-contrast"));
        Assert.Contains(naruszenia, n => n.Contains("label"));
        Assert.Equal(2, s.Naglowki.Count(p => p == 1));
        Assert.Contains(4, s.Naglowki);
        Assert.Single(s.ObrazyBezAlt);
        Assert.Single(s.PolaBezEtykiety);
    }

    /// <summary>Kontrast liczy się osobno w każdym motywie — axe sprawdza kolory faktycznie wyrenderowane.</summary>
    [Theory]
    [InlineData("Podstawowy")]
    [InlineData("WysokiKontrast")]
    [InlineData("Ciemny")]
    public async Task EkranMiejsca_WKazdymMotywie_BezNaruszenAxe(string motyw)
    {
        var strona = await app.NowaStrona();
        await strona.GotoAsync("/ustawienia");
        await strona.Locator($"#motyw-{motyw}").CheckAsync();
        await strona.GetByRole(AriaRole.Button, new() { Name = "Zapisz ustawienia" }).ClickAsync();
        await strona.WaitForURLAsync("**/ustawienia");

        // Także strona ustawień — ma komunikat o zapisie, pola wyboru i listę.
        var naUstawieniach = await Strony.PowazneNaruszeniaAxe(strona);
        await strona.GotoAsync(Strony.Miejsce);
        var naMiejscu = await Strony.PowazneNaruszeniaAxe(strona);

        Assert.True(naUstawieniach.Count == 0, $"ustawienia, motyw {motyw}:\n{string.Join("\n", naUstawieniach)}");
        Assert.True(naMiejscu.Count == 0, $"ekran miejsca, motyw {motyw}:\n{string.Join("\n", naMiejscu)}");
    }

    /// <summary>Panel mowy pojawia się dopiero po włączeniu w ustawieniach — sprawdzamy także ten stan strony.</summary>
    [Fact]
    public async Task EkranMiejsca_ZWlaczonaMowa_BezNaruszenAxe_IPrzyciskiSaWidoczne()
    {
        var strona = await app.NowaStrona();
        await strona.GotoAsync("/ustawienia");
        await strona.Locator("#pole-MowaWlaczona").CheckAsync();
        await strona.GetByRole(AriaRole.Button, new() { Name = "Zapisz ustawienia" }).ClickAsync();
        await strona.WaitForURLAsync("**/ustawienia");
        await strona.GotoAsync(Strony.Miejsce);

        Assert.True(await strona.GetByRole(AriaRole.Button, new() { Name = "Przeczytaj opis" }).IsVisibleAsync());
        Assert.True(await strona.GetByRole(AriaRole.Button, new() { Name = "Zatrzymaj czytanie" }).IsVisibleAsync());
        var naruszenia = await Strony.PowazneNaruszeniaAxe(strona);
        Assert.True(naruszenia.Count == 0, string.Join("\n", naruszenia));
    }
}
