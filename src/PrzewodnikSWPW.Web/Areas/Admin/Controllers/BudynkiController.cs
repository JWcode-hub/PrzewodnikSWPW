using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels.Admin;

namespace PrzewodnikSWPW.Web.Areas.Admin.Controllers;

/// <summary>Budynki (UC-16). Budynku nie usuwamy — dezaktywujemy (skasowanie wzięłoby ze sobą piętra i graf).</summary>
[Route("admin/budynki")]
public class BudynkiController(AdministracjaService admin) : AdminKontroler
{
    [Authorize(Roles = Role.Administrator)]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        PrzekazKomunikat();
        return View((await admin.Budynki(ct)).Select(BudynekWiersz.Z).ToList());
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("nowy")]
    public IActionResult Nowy() => View("Formularz", new BudynekFormularz());  // nowy budynek nie ma jeszcze punktów

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("nowy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nowy(BudynekFormularz model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var wynik = await admin.ZapiszBudynek(model.NaEncje(new Budynek()), ct);
            if (wynik.Sukces)
            {
                Komunikat($"Dodano budynek {model.Kod}.");
                return RedirectToAction(nameof(Index));
            }
            DodajBledy(wynik);
        }
        return View("Formularz", model);
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("{id:int}/edytuj")]
    public async Task<IActionResult> Edytuj(int id, CancellationToken ct) =>
        await admin.PobierzBudynek(id, ct) is { } b ? View("Formularz", await Uzupelnij(BudynekFormularz.Z(b), ct)) : NotFound();

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/edytuj")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edytuj(int id, BudynekFormularz model, CancellationToken ct)
    {
        if (await admin.PobierzBudynek(id, ct) is not { } b) return NotFound();
        model.Id = id;
        if (ModelState.IsValid)
        {
            var wynik = await admin.ZapiszBudynek(model.NaEncje(b), ct);
            if (wynik.Sukces)
            {
                Komunikat($"Zapisano zmiany w budynku {model.Kod}.");
                return RedirectToAction(nameof(Index));
            }
            DodajBledy(wynik);
        }
        return View("Formularz", await Uzupelnij(model, ct));
    }

    private async Task<BudynekFormularz> Uzupelnij(BudynekFormularz model, CancellationToken ct)
    {
        model.PunktyBudynku = model.Id is int id
            ? Lista(await admin.PunktyBudynku(id, ct), p => p.Id, p => $"{p.Kod} — {p.Nazwa}{(p.CzyAktywny ? "" : " (nieaktywny)")}")
            : [];
        return model;
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("{id:int}/dezaktywuj")]
    public async Task<IActionResult> Dezaktywuj(int id, CancellationToken ct)
    {
        if (await admin.PobierzBudynek(id, ct) is not { } b) return NotFound();
        return View("Potwierdzenie", new PotwierdzenieViewModel(
            $"Dezaktywacja budynku {b.Kod}",
            $"Czy na pewno dezaktywować budynek {b.Kod} — {b.Nazwa}?",
            "Budynek zniknie z przewodnika, ale jego piętra, sale i punkty zostaną w bazie. Możesz go później przywrócić.",
            null,
            $"Tak, dezaktywuj budynek {b.Kod}",
            Url.Action(nameof(Dezaktywuj), new { id })!,
            Url.Action(nameof(Index))!,
            "Anuluj i wróć do listy budynków"));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/dezaktywuj")]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Dezaktywuj))]
    public async Task<IActionResult> DezaktywujPotwierdzone(int id, CancellationToken ct)
    {
        var wynik = await admin.UstawAktywnoscBudynku(id, false, ct);
        Komunikat(wynik.Sukces ? "Budynek dezaktywowano." : wynik.Bledy[0].Komunikat);
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/aktywuj")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aktywuj(int id, CancellationToken ct)
    {
        var wynik = await admin.UstawAktywnoscBudynku(id, true, ct);
        Komunikat(wynik.Sukces ? "Budynek przywrócono." : wynik.Bledy[0].Komunikat);
        return RedirectToAction(nameof(Index));
    }
}
