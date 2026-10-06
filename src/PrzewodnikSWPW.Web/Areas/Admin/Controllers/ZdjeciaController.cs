using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels.Admin;

namespace PrzewodnikSWPW.Web.Areas.Admin.Controllers;

/// <summary>
/// Zdjęcia punktów i sal oraz ich aktywne obszary (WF-26, WF-27, D-09, D-10). Zdjęć nie usuwamy (D-04);
/// aktywne obszary tak — to fragment zdjęcia, nie rekord grafu.
/// </summary>
[Route("admin/zdjecia")]
public class ZdjeciaController(ZdjeciaService zdjecia) : AdminKontroler
{
    /// <summary>Twardy limit żądania — ponad limitem z konfiguracji, żeby za duży plik dostał komunikat przy polu, a nie błąd 413.</summary>
    private const long LimitZadania = 16 * 1024 * 1024;

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("")]
    public async Task<IActionResult> Index(int? punkt, int? sala, CancellationToken ct)
    {
        if (await zdjecia.NazwaWlasciciela(punkt, sala, ct) is not { } wlasciciel) return NotFound();
        PrzekazKomunikat();
        var lista = await zdjecia.ZdjeciaWlasciciela(punkt, sala, ct);
        var (adresPowrotu, tekstPowrotu) = Powrot(punkt, sala, wlasciciel);
        return View(new ZdjeciaViewModel(wlasciciel, punkt, punkt is null ? sala : null, lista.Select(ZdjecieWiersz.Z).ToList(), adresPowrotu, tekstPowrotu));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("nowe")]
    public async Task<IActionResult> Nowe(int? punkt, int? sala, CancellationToken ct) =>
        await zdjecia.NazwaWlasciciela(punkt, sala, ct) is { } wlasciciel
            ? View("Formularz", new ZdjecieFormularz { PunktRuchuId = punkt, SalaId = punkt is null ? sala : null, NazwaWlasciciela = wlasciciel })
            : NotFound();

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("nowe")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(LimitZadania)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimitZadania)]
    public async Task<IActionResult> Nowe(ZdjecieFormularz model, CancellationToken ct)
    {
        if (await zdjecia.NazwaWlasciciela(model.PunktRuchuId, model.SalaId, ct) is not { } wlasciciel) return NotFound();
        model.NazwaWlasciciela = wlasciciel;
        WalidujRegulyMiedzyPolami(model);

        // Plik sprawdzamy także przy błędach innych pól — użytkownik ma zobaczyć wszystkie błędy naraz.
        // Za dużego pliku nie wczytujemy do pamięci — do komunikatu wystarczy jego rozmiar.
        WgrywanyPlik? plik = null;
        var bladPliku = model.Plik is { Length: var rozmiar } && rozmiar > zdjecia.MaksRozmiarBajtow
            ? ZdjeciaService.KomunikatZaDuzyPlik(rozmiar, zdjecia.MaksRozmiarBajtow)
            : ZdjeciaService.SprawdzPlik(plik = await OdczytajPlik(model.Plik, ct), zdjecia.MaksRozmiarBajtow).Blad;
        if (bladPliku is not null) ModelState.AddModelError(nameof(model.Plik), bladPliku);

        if (ModelState.IsValid)
        {
            var wynik = await zdjecia.DodajZdjecie(model.NaEncje(new Zdjecie()), plik, ct);
            if (wynik.Sukces)
            {
                Komunikat($"Dodano zdjęcie ({wlasciciel}).");
                return RedirectToAction(nameof(Index), new { punkt = model.PunktRuchuId, sala = model.SalaId });
            }
            DodajBledy(wynik);
        }
        return View("Formularz", model);
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("{id:int}/edytuj")]
    public async Task<IActionResult> Edytuj(int id, CancellationToken ct)
    {
        if (await zdjecia.PobierzZdjecie(id, ct) is not { } z) return NotFound();
        return View("Formularz", ZdjecieFormularz.Z(z, await zdjecia.NazwaWlasciciela(z.PunktRuchuId, z.SalaId, ct) ?? ""));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/edytuj")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edytuj(int id, ZdjecieFormularz model, CancellationToken ct)
    {
        if (await zdjecia.PobierzZdjecie(id, ct) is not { } z) return NotFound();
        ModelState.Remove(nameof(model.Plik)); // edycja nie wymienia pliku
        WalidujRegulyMiedzyPolami(model);
        if (ModelState.IsValid)
        {
            var wynik = await zdjecia.ZapiszZdjecie(model.NaEncje(z), ct);
            if (wynik.Sukces)
            {
                Komunikat($"Zapisano zmiany w: {NazwyZdjec.Nazwa(z)}.");
                return RedirectToAction(nameof(Index), new { punkt = z.PunktRuchuId, sala = z.SalaId });
            }
            DodajBledy(wynik);
        }
        var wypelniony = ZdjecieFormularz.Z(z, await zdjecia.NazwaWlasciciela(z.PunktRuchuId, z.SalaId, ct) ?? "");
        model.Id = id;
        model.PunktRuchuId = z.PunktRuchuId;
        model.SalaId = z.SalaId;
        model.NazwaWlasciciela = wypelniony.NazwaWlasciciela;
        model.SciezkaPliku = z.SciezkaPliku;
        model.Szerokosc = z.Szerokosc;
        model.Wysokosc = z.Wysokosc;
        return View("Formularz", model);
    }

    // --- Aktywne obszary (WF-27) -------------------------------------------------------------------------

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("{id:int}/obszary")]
    public async Task<IActionResult> Obszary(int id, CancellationToken ct)
    {
        if (await zdjecia.PobierzZdjecie(id, ct) is not { PunktRuchu: { } punkt } z) return NotFound();
        PrzekazKomunikat();
        var obszary = z.ObszaryAktywne.OrderBy(o => o.Id)
            .Select(o => new ObszarWiersz(o.Id, o.Etykieta, $"{o.Kierunek.Azymut}°", WspolrzedneObszaru.Ksztalty.GetValueOrDefault(o.Ksztalt, o.Ksztalt), o.Wspolrzedne))
            .ToList();
        return View(new ObszaryViewModel(id, NazwyZdjec.Nazwa(z), $"punkt {punkt.Kod}", punkt.Id, obszary));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("{id:int}/obszary/nowy")]
    public async Task<IActionResult> NowyObszar(int id, CancellationToken ct) =>
        await zdjecia.PobierzZdjecie(id, ct) is { PunktRuchuId: not null } z
            ? View("FormularzObszaru", await Uzupelnij(new ObszarFormularz { ZdjecieId = id }, z, ct))
            : NotFound();

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/obszary/nowy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NowyObszar(int id, ObszarFormularz model, CancellationToken ct)
    {
        if (await zdjecia.PobierzZdjecie(id, ct) is not { PunktRuchuId: not null } z) return NotFound();
        model.ZdjecieId = id;
        if (ModelState.IsValid)
        {
            var wynik = await zdjecia.ZapiszObszar(model.NaEncje(new ObszarAktywny()), ct);
            if (wynik.Sukces)
            {
                Komunikat($"Dodano obszar „{model.Etykieta!.Trim()}”.");
                return RedirectToAction(nameof(Obszary), new { id });
            }
            DodajBledy(wynik);
        }
        return View("FormularzObszaru", await Uzupelnij(model, z, ct));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("obszary/{id:int}/edytuj")]
    public async Task<IActionResult> EdytujObszar(int id, CancellationToken ct) =>
        await zdjecia.PobierzObszar(id, ct) is { } o
            ? View("FormularzObszaru", await Uzupelnij(ObszarFormularz.Z(o), o.Zdjecie, ct))
            : NotFound();

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("obszary/{id:int}/edytuj")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EdytujObszar(int id, ObszarFormularz model, CancellationToken ct)
    {
        if (await zdjecia.PobierzObszar(id, ct) is not { } o) return NotFound();
        model.Id = id;
        model.ZdjecieId = o.ZdjecieId; // obszaru nie przenosimy na inne zdjęcie
        if (ModelState.IsValid)
        {
            var wynik = await zdjecia.ZapiszObszar(model.NaEncje(o), ct);
            if (wynik.Sukces)
            {
                Komunikat($"Zapisano obszar „{o.Etykieta}”.");
                return RedirectToAction(nameof(Obszary), new { id = o.ZdjecieId });
            }
            DodajBledy(wynik);
        }
        return View("FormularzObszaru", await Uzupelnij(model, o.Zdjecie, ct));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("obszary/{id:int}/usun")]
    public async Task<IActionResult> UsunObszar(int id, CancellationToken ct)
    {
        if (await zdjecia.PobierzObszar(id, ct) is not { } o) return NotFound();
        return View("Potwierdzenie", new PotwierdzenieViewModel(
            $"Usunięcie obszaru „{o.Etykieta}”",
            $"Czy na pewno usunąć obszar „{o.Etykieta}” ze zdjęcia nr {o.ZdjecieId}?",
            "Obszar zniknie ze zdjęcia w spacerze. Kierunek, do którego prowadził, zostaje bez zmian na liście kierunków.",
            null,
            $"Tak, usuń obszar „{o.Etykieta}”",
            Url.Action(nameof(UsunObszar), new { id })!,
            Url.Action(nameof(Obszary), new { id = o.ZdjecieId })!,
            "Anuluj i wróć do listy obszarów"));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("obszary/{id:int}/usun")]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(UsunObszar))]
    public async Task<IActionResult> UsunObszarPotwierdzone(int id, CancellationToken ct)
    {
        if (await zdjecia.PobierzObszar(id, ct) is not { } o) return NotFound();
        var zdjecieId = o.ZdjecieId;
        var wynik = await zdjecia.UsunObszar(id, ct);
        Komunikat(wynik.Sukces ? "Obszar usunięto." : wynik.Bledy[0].Komunikat);
        return RedirectToAction(nameof(Obszary), new { id = zdjecieId });
    }

    // --- pomocnicze --------------------------------------------------------------------------------------

    private async Task<ObszarFormularz> Uzupelnij(ObszarFormularz model, Zdjecie z, CancellationToken ct)
    {
        model.NazwaZdjecia = $"{NazwyZdjec.Nazwa(z)} (punkt {z.PunktRuchu?.Kod})";
        model.SciezkaPliku = z.SciezkaPliku;
        model.TekstAlternatywnyZdjecia = z.TekstAlternatywny;
        model.Szerokosc = z.Szerokosc;
        model.Wysokosc = z.Wysokosc;
        model.Kierunki = Lista(await zdjecia.KierunkiDlaObszarow(z.PunktRuchuId!.Value, ct), k => k.Id, NazwyZdjec.OpisKierunku);
        return model;
    }

    private static async Task<WgrywanyPlik?> OdczytajPlik(IFormFile? plik, CancellationToken ct)
    {
        if (plik is null) return null;
        using var pamiec = new MemoryStream((int)plik.Length);
        await plik.CopyToAsync(pamiec, ct);
        return new WgrywanyPlik(plik.FileName, plik.ContentType ?? "", pamiec.ToArray());
    }

    private (string Adres, string Tekst) Powrot(int? punkt, int? sala, string wlasciciel) => punkt is int p
        ? (Url.Action(nameof(KierunkiController.Index), "Kierunki", new { punkt = p })!, $"Wróć do kierunków: {wlasciciel}")
        : (Url.Action(nameof(SaleController.Edytuj), "Sale", new { id = sala })!, $"Wróć do edycji: {wlasciciel}");
}
