using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels;

namespace PrzewodnikSWPW.Web.Controllers;

/// <summary>
/// Moduł „Spacer” (UC-01 … UC-04). Cały stan — punkt i zwrot — jest w adresie URL (ryzyko P-12),
/// dzięki czemu działają przycisk Wstecz, zakładki i wysłanie linku (US-09). Działa bez JavaScriptu:
/// kierunki to zwykłe linki, przejście to GET z przekierowaniem (PRG).
/// </summary>
[Route("spacer")]
public class SpacerController(NawigacjaService nawigacja) : Controller
{
    private const string KluczKomunikatu = "KomunikatSpaceru";

    /// <summary>Wartości parametru „kierunek” w adresie /spacer/idz.</summary>
    public static readonly IReadOnlyDictionary<KierunekWzgledny, string> ParametrKierunku =
        new Dictionary<KierunekWzgledny, string>
        {
            [KierunekWzgledny.Prosto] = "prosto",
            [KierunekWzgledny.WLewo] = "lewo",
            [KierunekWzgledny.WPrawo] = "prawo",
            [KierunekWzgledny.DoTylu] = "tyl",
        };

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var budynki = await nawigacja.PobierzBudynki(ct);
        return View(new BudynkiViewModel(budynki));
    }

    /// <summary>
    /// Przejście jedną krawędzią i przekierowanie na adres nowego miejsca (PRG).
    /// Zadeklarowane przed „{kodBudynku}”, choć i tak segment stały ma pierwszeństwo przed parametrem.
    /// </summary>
    [HttpGet("idz")]
    public async Task<IActionResult> Idz(int zPunktu, string? kierunek, int zwrot, CancellationToken ct)
    {
        var wzgledny = ParametrKierunku
            .Where(p => string.Equals(p.Value, kierunek, StringComparison.OrdinalIgnoreCase))
            .Select(p => (KierunekWzgledny?)p.Key)
            .SingleOrDefault();
        if (wzgledny is null || !CzyPoprawnyZwrot(zwrot))
        {
            return BadRequest();
        }

        var wynik = await nawigacja.Przejdz(zPunktu, wzgledny.Value, zwrot, ct);
        if (wynik is null)
        {
            return NotFound();
        }

        switch (wynik.Rodzaj)
        {
            case RodzajWynikuPrzejscia.DoSali:
                return RedirectToAction(nameof(SalaController.Index), "Sala",
                    new { id = wynik.SalaId, zPunktu = wynik.PunktId, zwrot = wynik.Zwrot });

            case RodzajWynikuPrzejscia.BrakPrzejscia:
                TempData[KluczKomunikatu] = wynik.Opis;
                break;
        }

        return Redirect(AdresMiejsca(wynik.Adres, wynik.Zwrot));
    }

    [HttpGet("{kodBudynku}")]
    public async Task<IActionResult> Budynek(string kodBudynku, CancellationToken ct)
    {
        var budynek = await nawigacja.PobierzBudynek(kodBudynku, ct);
        if (budynek is null)
        {
            return NotFound();
        }

        return View(new BudynekViewModel(budynek,
            budynek.PunktStartowy is { } start ? AdresMiejsca(start.Adres, start.AzymutDomyslny) : null,
            budynek.Pietra.ToDictionary(p => p.Numer, p => (IReadOnlyList<(PunktNaLiscie, string)>)p.Punkty
                .Select(pr => (pr, AdresMiejsca(pr.Adres, pr.AzymutDomyslny)))
                .ToList())));
    }

    /// <summary>Ekran miejsca (UC-02). Adres bez poprawnego zwrotu jest przekierowywany na adres z domyślnym zwrotem punktu.</summary>
    [HttpGet("{kodBudynku}/{pietro:int}/{kodPunktu}")]
    public async Task<IActionResult> Miejsce(string kodBudynku, int pietro, string kodPunktu, string? zwrot, CancellationToken ct)
    {
        var punktId = await nawigacja.ZnajdzPunkt(kodBudynku, pietro, kodPunktu, ct);
        if (punktId is null)
        {
            return NotFound();
        }

        if (!int.TryParse(zwrot, out var zwrotLiczba) || !CzyPoprawnyZwrot(zwrotLiczba))
        {
            var domyslny = await nawigacja.DomyslnyZwrot(punktId.Value, ct);
            return domyslny is null ? NotFound() : RedirectToAction(nameof(Miejsce), new { kodBudynku, pietro, kodPunktu, zwrot = domyslny });
        }

        var widok = await nawigacja.PobierzMiejsce(punktId.Value, zwrotLiczba, ct);
        if (widok is null)
        {
            return NotFound();
        }

        var kierunki = widok.Kierunki
            .Select(k => new KierunekNaEkranie(
                ParametrKierunku[k.Kierunek],
                k.Tekst,
                k.CzyMozliwy
                    ? Url.Action(nameof(Idz), new { zPunktu = widok.PunktId, kierunek = ParametrKierunku[k.Kierunek], zwrot = widok.Zwrot })
                    : null))
            .ToList();

        return View(new MiejsceViewModel(widok, kierunki, TempData[KluczKomunikatu] as string,
            Url.Action(nameof(Budynek), new { kodBudynku = widok.Adres.KodBudynku })!));
    }

    private static bool CzyPoprawnyZwrot(int zwrot) => zwrot % 90 == 0;

    private string AdresMiejsca(AdresMiejsca adres, int zwrot) =>
        Url.Action(nameof(Miejsce), "Spacer", new
        {
            kodBudynku = adres.KodBudynku,
            pietro = adres.NumerPietra,
            kodPunktu = adres.SegmentPunktu,
            zwrot = Azymuty.Normalizuj(zwrot),
        })!;
}
