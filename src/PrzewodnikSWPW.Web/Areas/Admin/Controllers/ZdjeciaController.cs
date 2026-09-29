using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels.Admin;

namespace PrzewodnikSWPW.Web.Areas.Admin.Controllers;

/// <summary>
/// Zdjęcia punktów i sal (WF-26, D-09, D-10). Zdjęć nie usuwamy (D-04).
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

    // --- pomocnicze --------------------------------------------------------------------------------------

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
