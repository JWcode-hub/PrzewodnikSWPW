using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.Web.Areas.Admin.Controllers;

/// <summary>
/// Baza kontrolerów panelu. Autoryzacja ma trzy niezależne warstwy (05 §8, P-19):
/// konwencja obszaru (AutoryzacjaObszaruAdmin), ten atrybut na klasie oraz [Authorize] na KAŻDEJ akcji
/// w klasach pochodnych — test AutoryzacjaPaneluTesty pilnuje, aby żadnej nie zabrakło.
/// </summary>
[Area(AutoryzacjaObszaruAdmin.Obszar)]
[Authorize(Roles = Role.Administrator)]
public abstract class AdminKontroler : Controller
{
    private const string KluczKomunikatu = "KomunikatAdmina";

    /// <summary>Komunikat po udanej operacji — wyświetlany i ogłaszany na stronie docelowej przekierowania (PRG).</summary>
    protected void Komunikat(string tekst) => TempData[KluczKomunikatu] = tekst;

    protected void PrzekazKomunikat() => ViewData["Komunikat"] = TempData[KluczKomunikatu] as string;

    protected void DodajBledy(WynikOperacji wynik)
    {
        foreach (var blad in wynik.Bledy)
        {
            ModelState.AddModelError(blad.Pole, blad.Komunikat);
        }
    }

    /// <summary>
    /// Reguły między polami (IValidatableObject) MVC uruchamia dopiero, gdy atrybuty pól przeszły.
    /// Uruchamiamy je także przy błędach atrybutów, aby użytkownik zobaczył WSZYSTKIE błędy za pierwszym razem,
    /// a nie odkrywał je po jednym przy każdym wysłaniu formularza.
    /// </summary>
    protected void WalidujRegulyMiedzyPolami(IValidatableObject model)
    {
        if (ModelState.IsValid)
        {
            return; // MVC już je sprawdziło
        }

        foreach (var wynik in model.Validate(new ValidationContext(model)))
        {
            foreach (var pole in wynik.MemberNames.DefaultIfEmpty(string.Empty))
            {
                if (!ModelState.TryGetValue(pole, out var stan) || stan.Errors.Count == 0)
                {
                    ModelState.AddModelError(pole, wynik.ErrorMessage ?? "Nieprawidłowa wartość.");
                }
            }
        }
    }

    protected static IEnumerable<SelectListItem> Lista<T>(IEnumerable<T> elementy, Func<T, int> id, Func<T, string> tekst) =>
        elementy.Select(e => new SelectListItem(tekst(e), id(e).ToString()));
}
