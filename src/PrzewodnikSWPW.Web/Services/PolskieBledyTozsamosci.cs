using Microsoft.AspNetCore.Identity;

namespace PrzewodnikSWPW.Web.Services;

/// <summary>Komunikaty ASP.NET Core Identity po polsku — widzi je użytkownik przy zmianie hasła i zakładaniu konta.</summary>
public sealed class PolskieBledyTozsamosci : IdentityErrorDescriber
{
    private static IdentityError Blad(string kod, string opis) => new() { Code = kod, Description = opis };

    public override IdentityError PasswordTooShort(int length) =>
        Blad(nameof(PasswordTooShort), $"Hasło musi mieć co najmniej {length} znaków.");
    public override IdentityError PasswordRequiresDigit() =>
        Blad(nameof(PasswordRequiresDigit), "Hasło musi zawierać co najmniej jedną cyfrę.");
    public override IdentityError PasswordRequiresLower() =>
        Blad(nameof(PasswordRequiresLower), "Hasło musi zawierać co najmniej jedną małą literę.");
    public override IdentityError PasswordRequiresUpper() =>
        Blad(nameof(PasswordRequiresUpper), "Hasło musi zawierać co najmniej jedną wielką literę.");
    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Blad(nameof(PasswordRequiresNonAlphanumeric), "Hasło musi zawierać co najmniej jeden znak specjalny, np. ! albo #.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Blad(nameof(PasswordRequiresUniqueChars), $"Hasło musi zawierać co najmniej {uniqueChars} różnych znaków.");
    public override IdentityError PasswordMismatch() =>
        Blad(nameof(PasswordMismatch), "Obecne hasło jest nieprawidłowe.");
    public override IdentityError DuplicateEmail(string email) =>
        Blad(nameof(DuplicateEmail), $"Adres {email} jest już używany przez inne konto.");
    public override IdentityError DuplicateUserName(string userName) =>
        Blad(nameof(DuplicateUserName), $"Nazwa użytkownika {userName} jest już zajęta.");
    public override IdentityError InvalidEmail(string? email) =>
        Blad(nameof(InvalidEmail), $"Adres e-mail „{email}” jest nieprawidłowy.");
    public override IdentityError DefaultError() =>
        Blad(nameof(DefaultError), "Wystąpił błąd. Spróbuj ponownie.");
}
