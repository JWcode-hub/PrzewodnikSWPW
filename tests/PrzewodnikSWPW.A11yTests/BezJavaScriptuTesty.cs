using Microsoft.Playwright;

namespace PrzewodnikSWPW.A11yTests;

/// <summary>
/// Progressive enhancement (WN-26, CLAUDE.md zasada 7): przeglądarka z wyłączonym JavaScriptem przechodzi
/// spacer, wyszukiwanie i wyznaczenie trasy na zwykłych linkach i formularzach.
/// </summary>
[Collection(AplikacjaKolekcja.Nazwa)]
[Trait("Kategoria", "Dostepnosc")]
public class BezJavaScriptuTesty(AplikacjaFixture app)
{
    [Fact]
    public async Task Spacer_TrzyKroki_KazdyZmieniaMiejsce_ListaMaZawszeCzteryPozycje()
    {
        var strona = await app.NowaStrona(javaScript: false);
        await strona.GotoAsync("/");
        await strona.GetByRole(AriaRole.Link, new() { Name = "Rozpocznij spacer po budynkach" }).ClickAsync();
        await strona.Locator("main ul a").First.ClickAsync(); // jedyny budynek w danych początkowych
        await strona.GetByRole(AriaRole.Link, new() { NameRegex = new("^Rozpocznij spacer od miejsca") }).ClickAsync();

        var odwiedzone = new List<string> { await strona.Locator("h1").InnerTextAsync() };
        for (var krok = 0; krok < 3; krok++)
        {
            // Stała kolejność (D-01): prosto, w lewo, w prawo, do tyłu — także dla pozycji bez przejścia.
            Assert.Equal(["prosto", "lewo", "prawo", "tyl"],
                await strona.Locator(".lista-kierunkow > li").EvaluateAllAsync<string[]>("lista => lista.map(li => li.dataset.kierunek)"));

            await strona.Locator(".lista-kierunkow a").First.ClickAsync();
            odwiedzone.Add(await strona.Locator("h1").InnerTextAsync());
        }

        Assert.All(odwiedzone.Zip(odwiedzone.Skip(1)), para => Assert.NotEqual(para.First, para.Second));
        Assert.StartsWith(odwiedzone[^1], await strona.TitleAsync());
    }

    [Fact]
    public async Task Wyszukiwanie_FormularzPost_WynikWTytuleINaglowku()
    {
        var strona = await app.NowaStrona(javaScript: false);
        await strona.GotoAsync("/");
        await strona.GetByLabel("Numer lub nazwa sali").FillAsync("A15");
        await strona.GetByRole(AriaRole.Button, new() { Name = "Szukaj sali" }).ClickAsync();

        var naglowek = await strona.Locator("h1").InnerTextAsync();
        Assert.StartsWith("Znaleziono 1 salę", naglowek);
        Assert.StartsWith(naglowek, await strona.TitleAsync());
        // Bez JavaScriptu pole jest zwykłym polem tekstowym — combobox dokłada dopiero szukaj.js.
        Assert.Null(await strona.Locator("#pole-szukaj").GetAttributeAsync("role"));
    }

    [Fact]
    public async Task Trasa_OdWynikuWyszukiwaniaDoListyKrokow()
    {
        var strona = await app.NowaStrona(javaScript: false);
        await strona.GotoAsync("/szukaj?q=A15");
        await strona.GetByRole(AriaRole.Link, new() { Name = "Wyznacz trasę do sali A15" }).ClickAsync();

        // Sala docelowa przyszła z linku; wybieramy tylko początkową.
        await strona.GetByLabel("Sala początkowa").SelectOptionAsync(new SelectOptionValue { Label = await OpcjaZaczynajacaSieOd(strona, "#pole-Z", "A12") });
        await strona.GetByRole(AriaRole.Button, new() { Name = "Wyznacz trasę" }).ClickAsync();

        Assert.Equal("Trasa z sali A12 do sali A15", await strona.Locator("h1").InnerTextAsync());
        Assert.True(await strona.Locator("ol.trasa > li").CountAsync() >= 2);
    }

    [Fact]
    public async Task Trasa_PustyFormularz_BladWTytule_IAutofocusNaPierwszymBlednymPolu()
    {
        var strona = await app.NowaStrona(javaScript: false);
        await strona.GotoAsync("/trasa");
        await strona.GetByRole(AriaRole.Button, new() { Name = "Wyznacz trasę" }).ClickAsync();

        Assert.StartsWith("Błąd w formularzu – Wyznacz trasę", await strona.TitleAsync());
        // Fokus na błędnym polu daje sam atrybut autofocus — bez skryptu.
        Assert.NotNull(await strona.Locator("#pole-Z").GetAttributeAsync("autofocus"));
        Assert.Equal("true", await strona.Locator("#pole-Z").GetAttributeAsync("aria-invalid"));
    }

    [Fact]
    public async Task Ustawienia_ZmianaMotywu_ZapisujeSieBezSkryptu()
    {
        var strona = await app.NowaStrona(javaScript: false);
        await strona.GotoAsync("/ustawienia");
        await strona.Locator("#motyw-WysokiKontrast").CheckAsync();
        await strona.GetByRole(AriaRole.Button, new() { Name = "Zapisz ustawienia" }).ClickAsync();

        Assert.Equal("wysoki-kontrast", await strona.Locator("html").GetAttributeAsync("data-motyw"));
        Assert.StartsWith("Zapisano – Ustawienia dostępności", await strona.TitleAsync());
    }

    private static async Task<string> OpcjaZaczynajacaSieOd(IPage strona, string selektor, string poczatek)
    {
        var opcje = await strona.Locator($"{selektor} option").AllInnerTextsAsync();
        return opcje.Single(o => o.TrimStart().StartsWith(poczatek + " ", StringComparison.Ordinal));
    }
}
