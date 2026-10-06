using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels.Admin;

namespace PrzewodnikSWPW.Web.Areas.Admin.Controllers;

/// <summary>
/// Wspólna obsługa słowników (typy sal, typy punktów, udogodnienia). Pozycję w użyciu blokujemy
/// przed usunięciem — klucze obce mają NO ACTION, więc baza i tak by na to nie pozwoliła.
/// </summary>
public abstract class SlownikController<T>(AdministracjaService admin) : AdminKontroler where T : class, ISlownik, new()
{
    private const string Widoki = "~/Areas/Admin/Views/Slownik/";

    protected abstract string Tytul { get; }
    /// <summary>Nazwa pozycji w bierniku, np. „typ sali”, „udogodnienie”.</summary>
    protected abstract string NazwaPozycji { get; }
    protected abstract int MaksDlugoscNazwy { get; }
    protected abstract int MaksDlugoscOpisu { get; }
    protected virtual bool MaIkone => false;

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        PrzekazKomunikat();
        var pozycje = await admin.Slownik<T>(ct);
        return View(Widoki + "Index.cshtml", new SlownikListaViewModel(Tytul, NazwaPozycji, MaIkone,
            pozycje.Select(p => new SlownikWiersz(p.Id, p.Nazwa, p.Opis, (p as Udogodnienie)?.Ikona)).ToList()));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("nowa")]
    public IActionResult Nowa() => Formularz(new SlownikFormularz());

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("nowa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nowa(SlownikFormularz model, CancellationToken ct) => await Zapisz(new T(), model, ct);

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("{id:int}/edytuj")]
    public async Task<IActionResult> Edytuj(int id, CancellationToken ct)
    {
        if (await admin.PozycjaSlownika<T>(id, ct) is not { } p) return NotFound();
        return Formularz(new SlownikFormularz { Id = p.Id, Nazwa = p.Nazwa, Opis = p.Opis, Ikona = (p as Udogodnienie)?.Ikona });
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/edytuj")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edytuj(int id, SlownikFormularz model, CancellationToken ct)
    {
        if (await admin.PozycjaSlownika<T>(id, ct) is not { } p) return NotFound();
        model.Id = id;
        return await Zapisz(p, model, ct);
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("{id:int}/usun")]
    public async Task<IActionResult> Usun(int id, CancellationToken ct)
    {
        if (await admin.PozycjaSlownika<T>(id, ct) is not { } p) return NotFound();
        var uzycia = await admin.UzyciaSlownika<T>(id, ct);
        return View("Potwierdzenie", new PotwierdzenieViewModel(
            $"Usunięcie: {p.Nazwa}",
            $"Czy na pewno usunąć {NazwaPozycji} „{p.Nazwa}”?",
            "Usunięcia nie można cofnąć.",
            uzycia > 0 ? $"Nie można usunąć — używa tej pozycji {uzycia} {Odmiana.PoLiczbie(uzycia, "rekord", "rekordy", "rekordów")}. Najpierw zmień je na inną pozycję." : null,
            $"Tak, usuń {NazwaPozycji} {p.Nazwa}",
            Url.Action(nameof(Usun), new { id })!,
            Url.Action(nameof(Index))!,
            $"Anuluj i wróć do listy: {Tytul.ToLowerInvariant()}"));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/usun")]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Usun))]
    public async Task<IActionResult> UsunPotwierdzone(int id, CancellationToken ct)
    {
        var wynik = await admin.UsunSlownik<T>(id, ct);
        Komunikat(wynik.Sukces ? "Pozycję usunięto." : wynik.Bledy[0].Komunikat);
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> Zapisz(T pozycja, SlownikFormularz model, CancellationToken ct)
    {
        // Długości kolumn różnią się między słownikami (np. TypSali.Nazwa 50, Udogodnienie.Nazwa 100).
        if (model.Nazwa?.Length > MaksDlugoscNazwy)
            ModelState.AddModelError(nameof(model.Nazwa), $"Nazwa może mieć najwyżej {MaksDlugoscNazwy} znaków.");
        if (model.Opis?.Length > MaksDlugoscOpisu)
            ModelState.AddModelError(nameof(model.Opis), $"Opis może mieć najwyżej {MaksDlugoscOpisu} znaków.");

        if (ModelState.IsValid)
        {
            pozycja.Nazwa = model.Nazwa!.Trim();
            pozycja.Opis = model.Opis;
            if (pozycja is Udogodnienie u) u.Ikona = model.Ikona;

            var wynik = await admin.ZapiszSlownik(pozycja, ct);
            if (wynik.Sukces)
            {
                Komunikat($"Zapisano: {pozycja.Nazwa}.");
                return RedirectToAction(nameof(Index));
            }
            DodajBledy(wynik);
        }
        return Formularz(model);
    }

    private ViewResult Formularz(SlownikFormularz model)
    {
        ViewData["Tytul"] = Tytul;
        ViewData["NazwaPozycji"] = NazwaPozycji;
        ViewData["MaIkone"] = MaIkone;
        return View(Widoki + "Formularz.cshtml", model);
    }
}

[Route("admin/typy-sal")]
public class TypySalController(AdministracjaService admin) : SlownikController<TypSali>(admin)
{
    protected override string Tytul => "Typy sal";
    protected override string NazwaPozycji => "typ sali";
    protected override int MaksDlugoscNazwy => 50;
    protected override int MaksDlugoscOpisu => 200;
}

// Typy punktów ruchu nie mają strony w panelu: pięć typów z migracji (hol, korytarz, skrzyżowanie,
// podest schodów, przed windą) wystarcza, a typ nie wpływa na spacer ani trasę. Redaktor wybiera go
// w formularzu punktu ruchu; nowy typ dodaje się migracją (HasData w PrzewodnikDbContext).

[Route("admin/udogodnienia")]
public class UdogodnieniaController(AdministracjaService admin) : SlownikController<Udogodnienie>(admin)
{
    protected override string Tytul => "Udogodnienia";
    protected override string NazwaPozycji => "udogodnienie";
    protected override int MaksDlugoscNazwy => 100;
    protected override int MaksDlugoscOpisu => 400;
    protected override bool MaIkone => true;
}
