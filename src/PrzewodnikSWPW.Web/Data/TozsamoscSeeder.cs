using Microsoft.AspNetCore.Identity;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.Web.Data;

/// <summary>Role i — wyłącznie w środowisku deweloperskim — konto administratora.</summary>
public static class TozsamoscSeeder
{
    public const string SekcjaAdministratoraDev = "Przewodnik:AdministratorDev";

    /// <summary>Tworzy brakujące role. Idempotentne — bezpieczne przy każdym starcie aplikacji.</summary>
    public static async Task ZasiejRoleAsync(RoleManager<IdentityRole> role)
    {
        foreach (var nazwa in Role.Wszystkie)
        {
            if (!await role.RoleExistsAsync(nazwa))
            {
                await role.CreateAsync(new IdentityRole(nazwa));
            }
        }
    }

    /// <summary>
    /// Konto administratora dla środowiska deweloperskiego. E-mail i hasło pochodzą z konfiguracji
    /// (user-secrets albo appsettings.Development.json w .gitignore) — nigdy z kodu.
    /// Bez konfiguracji konto nie powstaje.
    /// </summary>
    public static async Task ZasiejAdministratoraDevAsync(UserManager<IdentityUser> uzytkownicy, IConfiguration konfiguracja, ILogger log)
    {
        var email = konfiguracja[$"{SekcjaAdministratoraDev}:Email"];
        var haslo = konfiguracja[$"{SekcjaAdministratoraDev}:Haslo"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(haslo))
        {
            log.LogWarning("Brak {Sekcja}:Email / :Haslo w konfiguracji — konto administratora deweloperskiego nie zostało utworzone. Instrukcja: README.md.",
                SekcjaAdministratoraDev);
            return;
        }

        var uzytkownik = await uzytkownicy.FindByEmailAsync(email);
        if (uzytkownik is null)
        {
            uzytkownik = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var wynik = await uzytkownicy.CreateAsync(uzytkownik, haslo);
            if (!wynik.Succeeded)
            {
                log.LogError("Nie udało się utworzyć konta administratora deweloperskiego: {Bledy}",
                    string.Join(" ", wynik.Errors.Select(b => b.Description)));
                return;
            }
        }

        foreach (var rola in new[] { Role.Administrator, Role.Superadministrator })
        {
            if (!await uzytkownicy.IsInRoleAsync(uzytkownik, rola))
            {
                await uzytkownicy.AddToRoleAsync(uzytkownik, rola);
            }
        }
    }
}
