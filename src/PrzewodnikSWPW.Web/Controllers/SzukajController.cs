using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels;

namespace PrzewodnikSWPW.Web.Controllers;

/// <summary>
/// Wyszukiwanie sal (UC-06, WF-10 … WF-13). Formularz wysyła zwykły POST, który przekierowuje (PRG)
/// na /szukaj?q=… — wyniki mają własny adres (mapa aplikacji 02 §6, P-12): działa przycisk Wstecz,
/// zakładka i wysłanie linku, a odświeżenie nie ponawia wysyłki formularza.
/// </summary>
[Route("szukaj")]
public class SzukajController(WyszukiwarkaSalService wyszukiwarka) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return View(new SzukajViewModel(null, null));
        }

        var wynik = await wyszukiwarka.Szukaj(q, ct);
        return View(new SzukajViewModel(wynik.Fraza, wynik));
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    [ActionName("Index")]
    public IActionResult Wyslij([FromForm] string? q) =>
        string.IsNullOrWhiteSpace(q)
            ? RedirectToAction(nameof(Index))
            : RedirectToAction(nameof(Index), new { q = q.Trim() });

    /// <summary>Podpowiedzi dla pola wyszukiwania (ulepszenie JavaScriptowe — combobox ARIA).</summary>
    [HttpGet("podpowiedzi")]
    public async Task<IActionResult> Podpowiedzi(string? q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length > WyszukiwarkaSalService.MaksDlugoscFrazy)
        {
            return Json(Array.Empty<PodpowiedzDto>());
        }

        var sale = await wyszukiwarka.Podpowiedzi(q, ct: ct);
        return Json(sale.Select(s => new PodpowiedzDto(
            s.Id,
            $"{s.Symbol} — {s.Nazwa}, {PierwszaMala(s.NazwaPietra)}",
            Url.Action(nameof(SalaController.Index), "Sala", new { id = s.Id })!)));
    }

    /// <summary>Lista wszystkich sal budynku — cel linku przy braku wyników (WCAG 3.3.3).</summary>
    [HttpGet("sale/{kodBudynku}")]
    public async Task<IActionResult> Sale(string kodBudynku, CancellationToken ct)
    {
        var wynik = await wyszukiwarka.SaleBudynku(kodBudynku, ct);
        return wynik is { } w ? View(new SaleBudynkuViewModel(w.Budynek, w.Sale)) : NotFound();
    }

    private static string PierwszaMala(string tekst) =>
        tekst.Length == 0 ? tekst : char.ToLowerInvariant(tekst[0]) + tekst[1..];
}
