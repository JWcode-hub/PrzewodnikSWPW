using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels;

namespace PrzewodnikSWPW.Web.Controllers;

/// <summary>
/// Moduł „Trasa” (UC-07, UC-08). Formularz wysyła POST (antiforgery, walidacja ModelState — seq-trasa.mmd),
/// a poprawne dane przekierowują (PRG) na /trasa/wynik?z=…&amp;do=…&amp;winda=… — wynik ma własny adres
/// (zakładka, Wstecz, wysłanie linku) i działa bez JavaScriptu.
/// </summary>
[Route("trasa")]
public class TrasaController(WyszukiwarkaTrasService trasy, TimeProvider czas) : Controller
{
    /// <summary>Formularz. Parametry z adresu wypełniają go wstępnie (np. link „Wyznacz trasę do sali A15” z wyszukiwarki).</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(int? z, int? @do, bool winda, CancellationToken ct) =>
        View(new TrasaFormularzViewModel { Z = z, Do = @do, TrybWindy = winda, Sale = await trasy.SaleDoWyboru(ct) });

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(TrasaFormularzViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            model.Sale = await trasy.SaleDoWyboru(ct);
            return View(model);
        }

        return RedirectToAction(nameof(Wynik), new { z = model.Z, @do = model.Do, winda = model.TrybWindy });
    }

    [HttpGet("wynik")]
    public async Task<IActionResult> Wynik(int? z, int? @do, bool winda, CancellationToken ct)
    {
        if (z is null || @do is null)
        {
            return RedirectToAction(nameof(Index), new { z, @do, winda });
        }

        var wynik = await trasy.Wyznacz(z.Value, @do.Value, winda, czas.GetLocalNow().DateTime, ct);

        return View(new WynikTrasyViewModel(
            wynik,
            Url.Action(nameof(Index), new { z, @do, winda })!,
            wynik.SalaDo is { } sala ? Url.Action(nameof(SalaController.Index), "Sala", new { id = sala.Id }) : null));
    }
}
