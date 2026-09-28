using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels;

namespace PrzewodnikSWPW.Web.Controllers;

/// <summary>
/// Panel ustawień dostępności (WF-34, UC-10). Działa bez JavaScriptu: zwykły POST,
/// zapis do ciasteczka i przekierowanie (Post/Redirect/Get).
/// </summary>
[Route("ustawienia")]
public class UstawieniaController(UstawieniaDostepnosciService ustawienia) : Controller
{
    private const string KluczKomunikatu = "KomunikatUstawien";

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Komunikat"] = TempData[KluczKomunikatu] as string;
        return View(UstawieniaViewModel.Z(ustawienia.Pobierz()));
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public IActionResult Index(UstawieniaViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        ustawienia.Zapisz(model.NaUstawienia());
        TempData[KluczKomunikatu] = "Ustawienia zostały zapisane.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Szybka zmiana motywu z formularza w stopce każdej strony (06 §5.1).</summary>
    [HttpPost("motyw")]
    [ValidateAntiForgeryToken]
    public IActionResult Motyw(Motyw motyw, string? powrot)
    {
        if (Enum.IsDefined(motyw))
        {
            ustawienia.Zapisz(ustawienia.Pobierz() with { Motyw = motyw });
        }

        return LocalRedirect(!string.IsNullOrEmpty(powrot) && Url.IsLocalUrl(powrot) ? powrot : "/");
    }
}
