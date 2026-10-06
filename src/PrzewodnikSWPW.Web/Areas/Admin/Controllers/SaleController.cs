using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels.Admin;

namespace PrzewodnikSWPW.Web.Areas.Admin.Controllers;

/// <summary>Sale (UC-16). Sali nie usuwamy — dezaktywujemy (odwołują się do niej kierunki i log zapytań).</summary>
[Route("admin/sale")]
public class SaleController(AdministracjaService admin) : AdminKontroler
{
    [Authorize(Roles = Role.Administrator)]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        PrzekazKomunikat();
        return View((await admin.Sale(ct)).Select(SalaWiersz.Z).ToList());
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("nowa")]
    public async Task<IActionResult> Nowa(CancellationToken ct) => View("Formularz", await Uzupelnij(new SalaFormularz(), ct));

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("nowa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nowa(SalaFormularz model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var wynik = await admin.ZapiszSale(model.NaEncje(new Sala()), ct);
            if (wynik.Sukces)
            {
                Komunikat($"Dodano salę {model.Symbol}.");
                return RedirectToAction(nameof(Index));
            }
            DodajBledy(wynik);
        }
        return View("Formularz", await Uzupelnij(model, ct));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("{id:int}/edytuj")]
    public async Task<IActionResult> Edytuj(int id, CancellationToken ct) =>
        await admin.PobierzSale(id, ct) is { } s ? View("Formularz", await Uzupelnij(SalaFormularz.Z(s), ct)) : NotFound();

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/edytuj")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edytuj(int id, SalaFormularz model, CancellationToken ct)
    {
        if (await admin.PobierzSale(id, ct) is not { } s) return NotFound();
        model.Id = id;
        if (ModelState.IsValid)
        {
            var wynik = await admin.ZapiszSale(model.NaEncje(s), ct);
            if (wynik.Sukces)
            {
                Komunikat($"Zapisano zmiany w sali {model.Symbol}.");
                return RedirectToAction(nameof(Index));
            }
            DodajBledy(wynik);
        }
        return View("Formularz", await Uzupelnij(model, ct));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("{id:int}/dezaktywuj")]
    public async Task<IActionResult> Dezaktywuj(int id, CancellationToken ct)
    {
        if (await admin.PobierzSale(id, ct) is not { } s) return NotFound();
        return View("Potwierdzenie", new PotwierdzenieViewModel(
            $"Dezaktywacja sali {s.Symbol}",
            $"Czy na pewno dezaktywować salę {s.Symbol} — {s.Nazwa}?",
            "Sala zniknie z wyszukiwarki i tras, ale zostanie w bazie. Możesz ją później przywrócić.",
            null,
            $"Tak, dezaktywuj salę {s.Symbol}",
            Url.Action(nameof(Dezaktywuj), new { id })!,
            Url.Action(nameof(Index))!,
            "Anuluj i wróć do listy sal"));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/dezaktywuj")]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Dezaktywuj))]
    public async Task<IActionResult> DezaktywujPotwierdzone(int id, CancellationToken ct)
    {
        var wynik = await admin.UstawAktywnoscSali(id, false, ct);
        Komunikat(wynik.Sukces ? "Salę dezaktywowano." : wynik.Bledy[0].Komunikat);
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/aktywuj")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aktywuj(int id, CancellationToken ct)
    {
        var wynik = await admin.UstawAktywnoscSali(id, true, ct);
        Komunikat(wynik.Sukces ? "Salę przywrócono." : wynik.Bledy[0].Komunikat);
        return RedirectToAction(nameof(Index));
    }

    private async Task<SalaFormularz> Uzupelnij(SalaFormularz model, CancellationToken ct)
    {
        model.Pietra = Lista(await admin.Pietra(ct), p => p.Id, p => $"{p.Budynek.Kod}: {p.Nazwa}");
        model.TypySal = Lista(await admin.Slownik<TypSali>(ct), t => t.Id, t => t.Nazwa);
        model.Punkty = Lista(await admin.Punkty(ct), p => p.Id, p => $"{p.Kod} — {p.Nazwa}{(p.CzyAktywny ? "" : " (nieaktywny)")}");
        return model;
    }
}
