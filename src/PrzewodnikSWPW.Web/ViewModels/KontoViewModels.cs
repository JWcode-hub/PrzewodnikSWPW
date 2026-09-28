using System.ComponentModel.DataAnnotations;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.ViewModels;

public sealed class LogowanieViewModel
{
    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [EmailAddress(ErrorMessage = "Wpisz adres e-mail w formacie nazwa@domena.pl.")]
    [Display(Name = "Adres e-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [DataType(DataType.Password)]
    [Display(Name = "Hasło")]
    public string Haslo { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed class ZmianaHaslaViewModel
{
    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [DataType(DataType.Password)]
    [Display(Name = "Obecne hasło")]
    public string ObecneHaslo { get; set; } = string.Empty;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "Nowe hasło musi mieć od {2} do {1} znaków.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nowe hasło")]
    public string NoweHaslo { get; set; } = string.Empty;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Compare(nameof(NoweHaslo), ErrorMessage = "Oba nowe hasła muszą być takie same.")]
    [DataType(DataType.Password)]
    [Display(Name = "Powtórz nowe hasło")]
    public string PowtorzHaslo { get; set; } = string.Empty;
}
