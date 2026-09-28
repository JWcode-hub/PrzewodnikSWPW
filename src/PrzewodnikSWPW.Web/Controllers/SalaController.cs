using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels;

namespace PrzewodnikSWPW.Web.Controllers;

/// <summary>Karta sali (WF-12). Wersja minimalna — cel spaceru; wyznaczanie trasy dojdzie z modułem „Trasa”.</summary>
[Route("sala")]
public class SalaController(NawigacjaService nawigacja) : Controller
{
    /// <summary>
    /// <paramref name="zPunktu"/> i <paramref name="zwrot"/> opisują, skąd i w którą stronę użytkownik wszedł —
    /// link powrotu prowadzi na ten punkt ze zwrotem odwróconym (twarzą od drzwi).
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Index(int id, int? zPunktu, int? zwrot, CancellationToken ct)
    {
        var sala = await nawigacja.PobierzSale(id, zPunktu, zwrot, ct);
        if (sala is null)
        {
            return NotFound();
        }

        var adresPowrotu = sala is { Powrot: { } punkt, ZwrotPowrotu: int zwrotPowrotu }
            ? Url.Action(nameof(SpacerController.Miejsce), "Spacer", new
            {
                kodBudynku = punkt.Adres.KodBudynku,
                pietro = punkt.Adres.NumerPietra,
                kodPunktu = punkt.Adres.SegmentPunktu,
                zwrot = zwrotPowrotu,
            })
            : null;

        return View(new SalaViewModel(sala, adresPowrotu));
    }
}
