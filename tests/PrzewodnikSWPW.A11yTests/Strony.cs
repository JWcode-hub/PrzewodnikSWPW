using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace PrzewodnikSWPW.A11yTests;

/// <summary>Kluczowe strony serwisu i wspólne sprawdzenia używane przez wszystkie klasy testów.</summary>
public static class Strony
{
    public const string Miejsce = "/spacer/A/0/P01?zwrot=0";

    /// <summary>Nazwy kluczowych stron z etapu 16. Adres wylicza <see cref="Otworz"/>, bo część zależy od danych w bazie.</summary>
    public static readonly string[] Kluczowe =
    [
        "strona główna", "spacer", "wynik wyszukiwania", "wynik trasy", "ustawienia", "deklaracja dostępności",
        "skróty klawiszowe", "zgłoszenie problemu", "logowanie", "formularz panelu administratora",
    ];

    public static async Task<IPage> Otworz(AplikacjaFixture app, string nazwa)
    {
        var strona = await app.NowaStrona();
        switch (nazwa)
        {
            case "strona główna": await strona.GotoAsync("/"); break;
            case "spacer": await strona.GotoAsync(Miejsce); break;
            case "wynik wyszukiwania": await strona.GotoAsync("/szukaj?q=A15"); break;
            case "wynik trasy": await strona.GotoAsync(await AdresTrasy(app)); break;
            case "ustawienia": await strona.GotoAsync("/ustawienia"); break;
            case "deklaracja dostępności": await strona.GotoAsync("/deklaracja-dostepnosci"); break;
            case "skróty klawiszowe": await strona.GotoAsync("/skroty-klawiszowe"); break;
            case "zgłoszenie problemu": await strona.GotoAsync("/zglos-problem"); break;
            case "logowanie": await strona.GotoAsync("/konto/logowanie"); break;
            case "formularz panelu administratora":
                await app.Zaloguj(strona);
                await strona.GotoAsync("/admin/kierunki/nowy?punkt=1&azymut=90"); // pola tekstowe, listy i grupa opcji
                break;
            default: throw new ArgumentOutOfRangeException(nameof(nazwa), nazwa, "Nieznana strona.");
        }

        return strona;
    }

    public static async Task<string> AdresTrasy(AplikacjaFixture app)
    {
        await using var db = app.UtworzKontekst();
        var a12 = await db.Sale.SingleAsync(s => s.Symbol == "A12");
        var a15 = await db.Sale.SingleAsync(s => s.Symbol == "A15");
        return $"/trasa/wynik?z={a12.Id}&do={a15.Id}";
    }

    /// <summary>
    /// Naruszenia axe-core o wadze critical i serious dla reguł WCAG 2.x A i AA. Zielony wynik oznacza tylko,
    /// że automat nic nie znalazł — axe wykrywa ok. 30–40% problemów (06 §6.1) i nie zastępuje testu z czytnikiem.
    /// </summary>
    public static async Task<List<string>> PowazneNaruszeniaAxe(IPage strona)
    {
        var wynik = await strona.RunAxe(new AxeRunOptions
        {
            RunOnly = new RunOnlyOptions { Type = "tag", Values = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"] },
        });

        return wynik.Violations
            .Where(n => n.Impact is "critical" or "serious")
            .Select(n => $"[{n.Impact}] {n.Id}: {n.Help} — {string.Join(" | ", n.Nodes.Select(w => w.Html).Take(3))}")
            .ToList();
    }

    /// <summary>Klasa z konstruktorem bezparametrowym — tego wymaga konwersja wyniku EvaluateAsync w Playwright.</summary>
    public sealed class Struktura
    {
        public string? Jezyk { get; set; }
        public string Tytul { get; set; } = "";
        public int[] Naglowki { get; set; } = [];
        public string[] ObrazyBezAlt { get; set; } = [];
        public string[] PolaBezEtykiety { get; set; } = [];
    }

    /// <summary>Struktura odczytana z DOM po wykonaniu skryptów strony — to, co dostaje czytnik ekranu.</summary>
    public static Task<Struktura> OdczytajStrukture(IPage strona) => strona.EvaluateAsync<Struktura>(
        """
        () => {
          const opis = e => e.outerHTML.slice(0, 120);
          const maEtykiete = p =>
            (p.labels && p.labels.length > 0)
            || (p.getAttribute('aria-label') || '').trim() !== ''
            || (p.getAttribute('aria-labelledby') || '').split(/\s+/).some(id => id && document.getElementById(id));
          const pola = [...document.querySelectorAll('input, select, textarea')]
            .filter(p => !['hidden', 'submit', 'button', 'reset', 'image'].includes(p.type));
          return {
            jezyk: document.documentElement.getAttribute('lang'),
            tytul: document.title,
            naglowki: [...document.querySelectorAll('h1, h2, h3, h4, h5, h6')].map(h => Number(h.tagName[1])),
            obrazyBezAlt: [...document.querySelectorAll('img:not([alt])')].map(opis),
            polaBezEtykiety: pola.filter(p => !maEtykiete(p)).map(opis),
          };
        }
        """);
}
