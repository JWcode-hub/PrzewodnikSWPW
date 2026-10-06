using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels.Admin;

namespace PrzewodnikSWPW.Web.Areas.Admin.Controllers;

/// <summary>Piętra (UC-16). Usuwamy tylko piętro puste — bez sal i punktów ruchu.</summary>
[Route("admin/pietra")]
public class PietraController(AdministracjaService admin) : AdminKontroler
{
    [Authorize(Roles = Role.Administrator)]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        PrzekazKomunikat();
        return View((await admin.Pietra(ct)).Select(PietroWiersz.Z).ToList());
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("nowe")]
    public async Task<IActionResult> Nowe(CancellationToken ct) => View("Formularz", await Uzupelnij(new PietroFormularz(), ct));

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("nowe")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nowe(PietroFormularz model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var wynik = await admin.ZapiszPietro(model.NaEncje(new Pietro()), ct);
            if (wynik.Sukces)
            {
                Komunikat($"Dodano piętro „{model.Nazwa}”.");
                return RedirectToAction(nameof(Index));
            }
            DodajBledy(wynik);
        }
        return View("Formularz", await Uzupelnij(model, ct));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("{id:int}/edytuj")]
    public async Task<IActionResult> Edytuj(int id, CancellationToken ct) =>
        await admin.PobierzPietro(id, ct) is { } p ? View("Formularz", await Uzupelnij(PietroFormularz.Z(p), ct)) : NotFound();

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/edytuj")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edytuj(int id, PietroFormularz model, CancellationToken ct)
    {
        if (await admin.PobierzPietro(id, ct) is not { } p) return NotFound();
        model.Id = id;
        if (ModelState.IsValid)
        {
            var wynik = await admin.ZapiszPietro(model.NaEncje(p), ct);
            if (wynik.Sukces)
            {
                Komunikat($"Zapisano zmiany w piętrze „{model.Nazwa}”.");
                return RedirectToAction(nameof(Index));
            }
            DodajBledy(wynik);
        }
        return View("Formularz", await Uzupelnij(model, ct));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("{id:int}/usun")]
    public async Task<IActionResult> Usun(int id, CancellationToken ct)
    {
        var p = (await admin.Pietra(ct)).FirstOrDefault(x => x.Id == id);
        if (p is null) return NotFound();
        var przeszkoda = p.Sale.Count > 0 || p.PunktyRuchu.Count > 0
            ? $"Tego piętra nie można usunąć: ma {Odmiana.Sale(p.Sale.Count)} i {p.PunktyRuchu.Count} punktów ruchu. Najpierw przenieś albo dezaktywuj sale i punkty."
            : null;
        return View("Potwierdzenie", new PotwierdzenieViewModel(
            $"Usunięcie piętra {p.Nazwa}",
            $"Czy na pewno usunąć piętro „{p.Nazwa}” w budynku {p.Budynek.Kod}?",
            "Usunięcia nie można cofnąć.",
            przeszkoda,
            $"Tak, usuń piętro {p.Nazwa}",
            Url.Action(nameof(Usun), new { id })!,
            Url.Action(nameof(Index))!,
            "Anuluj i wróć do listy pięter"));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/usun")]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Usun))]
    public async Task<IActionResult> UsunPotwierdzone(int id, CancellationToken ct)
    {
        var wynik = await admin.UsunPietro(id, ct);
        Komunikat(wynik.Sukces ? "Piętro usunięto." : wynik.Bledy[0].Komunikat);
        return RedirectToAction(nameof(Index));
    }

    private async Task<PietroFormularz> Uzupelnij(PietroFormularz model, CancellationToken ct)
    {
        model.Budynki = Lista(await admin.Budynki(ct), b => b.Id, b => $"{b.Kod} — {b.Nazwa}");
        return model;
    }
}
