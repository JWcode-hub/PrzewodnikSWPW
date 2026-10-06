using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels;

namespace PrzewodnikSWPW.Web.Controllers;

/// <summary>
/// Strony wymagane przepisami, podlinkowane w stopce każdej strony (WF-31, WF-32, WF-33, WF-37, 06 §8):
/// Deklaracja Dostępności, wykaz skrótów, zgłoszenie braku dostępności, polityka prywatności.
/// </summary>
public class InformacjeController(
    NawigacjaService nawigacja,
    IOptions<DeklaracjaDostepnosciOptions> deklaracja,
    IOptions<KontaktAlternatywnyOptions> kontakt) : Controller
{
    [HttpGet("/deklaracja-dostepnosci")]
    public async Task<IActionResult> Deklaracja(CancellationToken ct) =>
        View(new DeklaracjaViewModel(
            deklaracja.Value,
            deklaracja.Value.AdresSerwisu ?? $"{Request.Scheme}://{Request.Host}/",
            await nawigacja.PobierzDostepnoscArchitektoniczna(ct),
            kontakt.Value));

    [HttpGet("/skroty-klawiszowe")]
    public IActionResult Skroty() => View();

    [HttpGet("/zglos-problem")]
    public IActionResult ZglosProblem() => View(new ZgloszenieFormularz());

    /// <summary>
    /// Wersja demonstracyjna (projekt dydaktyczny): formularz sprawdza dane i potwierdza przyjęcie,
    /// ale zgłoszenia nie zapisuje ani nie wysyła — mówi o tym wprost strona formularza i potwierdzenia.
    /// </summary>
    [HttpPost("/zglos-problem")]
    [ValidateAntiForgeryToken]
    public IActionResult ZglosProblem(ZgloszenieFormularz model) =>
        ModelState.IsValid ? RedirectToAction(nameof(ZgloszenieWyslane)) : View(model);

    [HttpGet("/zglos-problem/wyslano")]
    public IActionResult ZgloszenieWyslane() => View(deklaracja.Value);

    [HttpGet("/polityka-prywatnosci")]
    public IActionResult PolitykaPrywatnosci() => View(deklaracja.Value);
}
