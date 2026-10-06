using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.ViewModels;

namespace PrzewodnikSWPW.Web.Controllers;

/// <summary>
/// Logowanie, wylogowanie i zmiana hasła (UC-15, WF-22). Własne widoki zamiast domyślnego UI Identity,
/// aby spełnić te same zasady dostępności co reszta aplikacji.
/// </summary>
[Route("konto")]
public class KontoController(SignInManager<IdentityUser> logowanie, UserManager<IdentityUser> uzytkownicy) : Controller
{
    private const string KluczKomunikatu = "KomunikatKonta";

    [AllowAnonymous]
    [HttpGet("logowanie")]
    public IActionResult Logowanie(string? returnUrl) => View(new LogowanieViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous]
    [HttpPost("logowanie")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logowanie(LogowanieViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // lockoutOnFailure: true — 5 nieudanych prób blokuje konto na 15 minut (Program.cs, P-19).
        var wynik = await logowanie.PasswordSignInAsync(model.Email, model.Haslo, isPersistent: false, lockoutOnFailure: true);
        if (wynik.Succeeded)
        {
            return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : "/admin");
        }

        model.Haslo = string.Empty; // hasła nigdy nie odsyłamy z powrotem do formularza
        ModelState.Remove(nameof(model.Haslo));
        // Ten sam komunikat dla złego e-maila i złego hasła — nie zdradzamy, które konta istnieją.
        ModelState.AddModelError(nameof(model.Email), wynik.IsLockedOut
            ? "Konto jest zablokowane na 15 minut po 5 nieudanych próbach logowania. Spróbuj ponownie później."
            : "Nieprawidłowy adres e-mail lub hasło.");
        return View(model);
    }

    [Authorize]
    [HttpPost("wylogowanie")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Wylogowanie()
    {
        await logowanie.SignOutAsync();
        return LocalRedirect("/");
    }

    [Authorize]
    [HttpGet("zmiana-hasla")]
    public IActionResult ZmianaHasla()
    {
        ViewData["Komunikat"] = TempData[KluczKomunikatu] as string;
        return View(new ZmianaHaslaViewModel());
    }

    [Authorize]
    [HttpPost("zmiana-hasla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ZmianaHasla(ZmianaHaslaViewModel model)
    {
        if (ModelState.IsValid && await uzytkownicy.GetUserAsync(User) is { } uzytkownik)
        {
            var wynik = await uzytkownicy.ChangePasswordAsync(uzytkownik, model.ObecneHaslo, model.NoweHaslo);
            if (wynik.Succeeded)
            {
                await logowanie.RefreshSignInAsync(uzytkownik);
                TempData[KluczKomunikatu] = "Hasło zostało zmienione.";
                return RedirectToAction(nameof(ZmianaHasla));
            }

            foreach (var blad in wynik.Errors)
            {
                ModelState.AddModelError(blad.Code == "PasswordMismatch" ? nameof(model.ObecneHaslo) : nameof(model.NoweHaslo), blad.Description);
            }
        }

        // Pól z hasłami nie wypełniamy ponownie po błędzie.
        foreach (var pole in new[] { nameof(model.ObecneHaslo), nameof(model.NoweHaslo), nameof(model.PowtorzHaslo) })
        {
            if (ModelState.TryGetValue(pole, out var stan)) stan.RawValue = null;
        }
        return View(new ZmianaHaslaViewModel());
    }

    [AllowAnonymous]
    [HttpGet("brak-dostepu")]
    public IActionResult BrakDostepu() => View();
}
