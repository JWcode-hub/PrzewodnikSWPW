using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels.Admin;

namespace PrzewodnikSWPW.Web.Areas.Admin.Controllers;

/// <summary>
/// Kierunki (krawędzie grafu) jednego punktu — UC-17, WF-24. Ekran pokazuje zawsze cztery azymuty
/// (0/90/180/270): istniejący kierunek albo link do dodania. Kierunków nie usuwamy — dezaktywujemy.
/// </summary>
[Route("admin/kierunki")]
public class KierunkiController(AdministracjaService admin) : AdminKontroler
{
    [Authorize(Roles = Role.Administrator)]
    [HttpGet("")]
    public async Task<IActionResult> Index(int punkt, CancellationToken ct)
    {
        var p = (await admin.Punkty(ct)).FirstOrDefault(x => x.Id == punkt);
        if (p is null) return NotFound();
        PrzekazKomunikat();
        var kierunki = await admin.KierunkiPunktu(punkt, ct);
        return View(new KierunkiPunktuViewModel(PunktWiersz.Z(p), kierunki.ToDictionary(k => k.Azymut, KierunekWiersz.Z)));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("nowy")]
    public async Task<IActionResult> Nowy(int punkt, int? azymut, CancellationToken ct) =>
        View("Formularz", await Uzupelnij(new KierunekFormularz { PunktZrodlowyId = punkt, Azymut = azymut }, ct));

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("nowy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nowy(KierunekFormularz model, CancellationToken ct)
    {
        WalidujRegulyMiedzyPolami(model);
        if (ModelState.IsValid)
        {
            var wynik = await admin.DodajKierunek(model.NaEncje(new Kierunek()), model.UtworzPowrotny, model.OpisPrzejsciaPowrotnego, ct);
            if (wynik.Sukces)
            {
                Komunikat($"Dodano kierunek o azymucie {model.Azymut}°. {wynik.Informacja}".Trim());
                return RedirectToAction(nameof(Index), new { punkt = model.PunktZrodlowyId });
            }
            DodajBledy(wynik);
        }
        return View("Formularz", await Uzupelnij(model, ct));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("{id:int}/edytuj")]
    public async Task<IActionResult> Edytuj(int id, CancellationToken ct) =>
        await admin.PobierzKierunek(id, ct) is { } k ? View("Formularz", await Uzupelnij(KierunekFormularz.Z(k), ct)) : NotFound();

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/edytuj")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edytuj(int id, KierunekFormularz model, CancellationToken ct)
    {
        if (await admin.PobierzKierunek(id, ct) is not { } k) return NotFound();
        model.Id = id;
        model.PunktZrodlowyId = k.PunktZrodlowyId; // źródła nie zmieniamy — kierunek należy do punktu
        WalidujRegulyMiedzyPolami(model);
        if (ModelState.IsValid)
        {
            var wynik = await admin.ZapiszKierunek(model.NaEncje(k), ct);
            if (wynik.Sukces)
            {
                Komunikat($"Zapisano kierunek o azymucie {model.Azymut}°.");
                return RedirectToAction(nameof(Index), new { punkt = k.PunktZrodlowyId });
            }
            DodajBledy(wynik);
        }
        return View("Formularz", await Uzupelnij(model, ct));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("{id:int}/dezaktywuj")]
    public async Task<IActionResult> Dezaktywuj(int id, CancellationToken ct)
    {
        if (await admin.PobierzKierunek(id, ct) is not { } k) return NotFound();
        return View("Potwierdzenie", new PotwierdzenieViewModel(
            $"Dezaktywacja kierunku {k.Azymut}° z punktu {k.PunktZrodlowy.Kod}",
            $"Czy na pewno oznaczyć kierunek {k.Azymut}° z punktu {k.PunktZrodlowy.Kod} jako „brak przejścia”?",
            "Kierunek zostanie w bazie jako nieaktywny: użytkownik usłyszy jego opis jako punkt orientacyjny, ale nie przejdzie tędy. Możesz go później przywrócić.",
            null,
            $"Tak, dezaktywuj kierunek {k.Azymut}°",
            Url.Action(nameof(Dezaktywuj), new { id })!,
            Url.Action(nameof(Index), new { punkt = k.PunktZrodlowyId })!,
            $"Anuluj i wróć do kierunków punktu {k.PunktZrodlowy.Kod}",
            PokazOpcjePowrotnego: k.KierunekPowrotnyId is not null));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/dezaktywuj")]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Dezaktywuj))]
    public async Task<IActionResult> DezaktywujPotwierdzone(int id, bool takzePowrotny, CancellationToken ct)
    {
        if (await admin.PobierzKierunek(id, ct) is not { } k) return NotFound();
        var wynik = await admin.UstawAktywnoscKierunku(id, false, takzePowrotny, ct);
        Komunikat(wynik.Sukces ? (takzePowrotny ? "Kierunek i kierunek powrotny dezaktywowano." : "Kierunek dezaktywowano.") : wynik.Bledy[0].Komunikat);
        return RedirectToAction(nameof(Index), new { punkt = k.PunktZrodlowyId });
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/aktywuj")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aktywuj(int id, CancellationToken ct)
    {
        if (await admin.PobierzKierunek(id, ct) is not { } k) return NotFound();
        var wynik = await admin.UstawAktywnoscKierunku(id, true, takzePowrotny: false, ct);
        Komunikat(wynik.Sukces ? "Kierunek przywrócono." : wynik.Bledy[0].Komunikat);
        return RedirectToAction(nameof(Index), new { punkt = k.PunktZrodlowyId });
    }

    private async Task<KierunekFormularz> Uzupelnij(KierunekFormularz model, CancellationToken ct)
    {
        model.Punkty = Lista(await admin.Punkty(ct), p => p.Id, p => $"{p.Kod} — {p.Nazwa}{(p.CzyAktywny ? "" : " (nieaktywny)")}");
        model.Sale = Lista(await admin.Sale(ct), s => s.Id, s => $"{s.Symbol} — {s.Nazwa}");
        return model;
    }
}
