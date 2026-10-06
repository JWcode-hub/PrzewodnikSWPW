using Microsoft.Playwright;

namespace PrzewodnikSWPW.A11yTests;

/// <summary>Klawiatura i fokus na ekranie spaceru (etap 16 pkt 3–4, WCAG 2.1.1, 2.1.2, 2.4.3; 06 §3.1).</summary>
[Collection(AplikacjaKolekcja.Nazwa)]
[Trait("Kategoria", "Dostepnosc")]
public class KlawiaturaIFokusTesty(AplikacjaFixture app)
{
    /// <summary>Znacznik elementu z fokusem: numer w kolejności kodu strony albo „poza” dla elementów nieinteraktywnych.</summary>
    private const string OznaczElementy =
        """
        () => {
          const widoczny = e => e.getClientRects().length > 0 && getComputedStyle(e).visibility !== 'hidden';
          const lista = [...document.querySelectorAll('a[href], button, input, select, textarea, [tabindex="0"]')]
            .filter(e => !e.disabled && e.type !== 'hidden' && e.tabIndex >= 0 && widoczny(e));
          lista.forEach((e, i) => e.setAttribute('data-test-nr', String(i)));
          return lista.map(e => (e.textContent || e.value || e.getAttribute('aria-label') || e.tagName).trim().slice(0, 50));
        }
        """;

    private const string NumerZFokusem = "() => document.activeElement.getAttribute('data-test-nr')";

    [Fact]
    public async Task EkranMiejsca_Tab_OdwiedzaKazdyElementWKolejnosciKoduStrony_IWracaNaPoczatek()
    {
        var strona = await app.NowaStrona();
        await strona.GotoAsync(Strony.Miejsce);
        var elementy = await strona.EvaluateAsync<string[]>(OznaczElementy);
        Assert.True(elementy.Length >= 10, $"Za mało elementów interaktywnych: {elementy.Length}");

        // Po załadowaniu fokus jest na nazwie miejsca (h1), więc Tab zaczyna od pierwszego elementu po niej.
        // Naciskamy z zapasem: po ostatnim elemencie przeglądarka przechodzi przez początek dokumentu.
        var odwiedzone = new List<int>();
        for (var i = 0; i < elementy.Length * 2 + 4; i++)
        {
            await strona.Keyboard.PressAsync("Tab");
            if (await strona.EvaluateAsync<string?>(NumerZFokusem) is { } nr)
            {
                odwiedzone.Add(int.Parse(nr));
            }
        }

        // Każdy element interaktywny osiągalny.
        var pominiete = Enumerable.Range(0, elementy.Length).Except(odwiedzone).Select(i => elementy[i]).ToList();
        Assert.True(pominiete.Count == 0, $"Tab pomija: {string.Join(" | ", pominiete)}");

        // Kolejność fokusa = kolejność kodu strony; po ostatnim elemencie następuje pierwszy (brak pułapki).
        Assert.All(odwiedzone.Zip(odwiedzone.Skip(1)), para =>
            Assert.True(para.Second == (para.First + 1) % elementy.Length,
                $"Po „{elementy[para.First]}” fokus trafił na „{elementy[para.Second]}”."));
        Assert.Contains(0, odwiedzone);                      // wrócił na początek: link „Przejdź do treści głównej”
        Assert.True(odwiedzone.Count(n => n == odwiedzone[0]) >= 2, "Fokus nie wrócił do elementu, od którego zaczął.");
    }

    [Fact]
    public async Task EkranMiejsca_ShiftTab_WracaDoPoprzedniegoElementu()
    {
        var strona = await app.NowaStrona();
        await strona.GotoAsync(Strony.Miejsce);
        await strona.EvaluateAsync<string[]>(OznaczElementy);

        await strona.Keyboard.PressAsync("Tab");
        var pierwszy = await strona.EvaluateAsync<string?>(NumerZFokusem);
        await strona.Keyboard.PressAsync("Tab");
        await strona.Keyboard.PressAsync("Shift+Tab");

        Assert.NotNull(pierwszy);
        Assert.Equal(pierwszy, await strona.EvaluateAsync<string?>(NumerZFokusem));
    }

    [Fact]
    public async Task LinkPominiecia_JestPierwszymElementem_IPrzenosiDoTresci()
    {
        var strona = await app.NowaStrona();
        await strona.GotoAsync("/spacer"); // zwykła strona: fokus zaczyna od początku dokumentu

        await strona.Keyboard.PressAsync("Tab");
        Assert.Equal("Przejdź do treści głównej", await strona.EvaluateAsync<string>("() => document.activeElement.textContent.trim()"));

        await strona.Keyboard.PressAsync("Enter");
        Assert.Equal("tresc", await strona.EvaluateAsync<string>("() => document.activeElement.id"));
    }

    [Theory]
    [InlineData(false)] // kliknięcie
    [InlineData(true)]  // klawiatura: Tab do pierwszego kierunku i Enter
    public async Task PoPrzejsciuWKierunku_FokusJestNaH1ZNazwaNowegoMiejsca(bool klawiatura)
    {
        var strona = await app.NowaStrona();
        await strona.GotoAsync(Strony.Miejsce);
        var poprzednie = await strona.Locator("h1").InnerTextAsync();

        var kierunek = strona.Locator(".lista-kierunkow a").First;
        if (klawiatura)
        {
            await strona.Keyboard.PressAsync("Tab"); // z h1 na pierwszy kierunek, który jest linkiem
            Assert.True(await kierunek.EvaluateAsync<bool>("e => e === document.activeElement"));
            await strona.Keyboard.PressAsync("Enter");
        }
        else
        {
            await kierunek.ClickAsync();
        }

        await strona.WaitForURLAsync(u => !u.Contains("P01"));
        await strona.WaitForFunctionAsync("() => document.activeElement && document.activeElement.tagName === 'H1'");

        var fokus = await strona.EvaluateAsync<string[]>("() => [document.activeElement.tagName, document.activeElement.id, document.activeElement.textContent.trim()]");
        Assert.Equal(["H1", "naglowek-miejsca"], fokus[..2]);
        Assert.NotEqual(poprzednie, fokus[2]);
        Assert.StartsWith(fokus[2], await strona.TitleAsync());
        // Zasada ogłaszania (RAPORT_CZYTNIK.md rozdz. 2): po przeładowaniu obszar aria-live jest pusty.
        Assert.Equal("", await strona.Locator("#komunikaty").InnerTextAsync());
    }

    [Fact]
    public async Task WynikWyszukiwania_FokusNaNaglowkuZLiczbaWynikow()
    {
        var strona = await app.NowaStrona();
        await strona.GotoAsync("/szukaj");
        await strona.GetByLabel("Numer lub nazwa sali").FillAsync("A15");
        await strona.GetByRole(AriaRole.Button, new() { Name = "Szukaj sali" }).ClickAsync();
        await strona.WaitForURLAsync("**/szukaj?q=A15");

        var fokus = await strona.EvaluateAsync<string[]>("() => [document.activeElement.tagName, document.activeElement.textContent.trim()]");
        Assert.Equal("H1", fokus[0]);
        Assert.StartsWith("Znaleziono 1 salę", fokus[1]);
    }

    [Fact]
    public async Task BladFormularzaTrasy_FokusNaPierwszymBlednymPolu()
    {
        var strona = await app.NowaStrona();
        await strona.GotoAsync("/trasa");
        await strona.GetByRole(AriaRole.Button, new() { Name = "Wyznacz trasę" }).ClickAsync();
        await strona.WaitForFunctionAsync("() => document.title.startsWith('Błąd w formularzu')");

        var fokus = await strona.EvaluateAsync<string[]>("() => [document.activeElement.id, document.activeElement.getAttribute('aria-invalid')]");
        Assert.Equal(["pole-Z", "true"], fokus);
    }
}
