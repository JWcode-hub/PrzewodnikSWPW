using System.ComponentModel.DataAnnotations;

namespace PrzewodnikSWPW.Web.Models;

/// <summary>
/// Zgłoszenie braku dostępności cyfrowej (art. 18 ustawy, §7 Zarządzenia Rektora).
/// Dane osobowe są opcjonalne — zgłoszenie anonimowe też musi być możliwe (P-20).
/// </summary>
public class ZgloszenieDostepnosci
{
    public int Id { get; set; }

    [StringLength(150, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Imię i nazwisko (opcjonalnie)")]
    public string? ImieNazwisko { get; set; }

    [StringLength(200, ErrorMessage = Komunikaty.MaksDlugosc)]
    [EmailAddress(ErrorMessage = "Wpisz adres e-mail w formacie nazwa@domena.pl.")]
    [Display(Name = "Adres e-mail (opcjonalnie)")]
    public string? Email { get; set; }

    [StringLength(30, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Phone(ErrorMessage = "Wpisz numer telefonu, np. 600 100 200.")]
    [Display(Name = "Telefon (opcjonalnie)")]
    public string? Telefon { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Display(Name = "Opis problemu")]
    public string Tresc { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Adres strony, której dotyczy zgłoszenie")]
    public string? AdresStrony { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [EnumDataType(typeof(StatusZgloszenia), ErrorMessage = "Wybierz status zgłoszenia z listy.")]
    [Display(Name = "Status zgłoszenia")]
    public StatusZgloszenia Status { get; set; } = StatusZgloszenia.Nowe;

    /// <summary>Identyfikator użytkownika ASP.NET Core Identity obsługującego zgłoszenie.</summary>
    [StringLength(450, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Obsługuje")]
    public string? ObslugujeId { get; set; }

    [Display(Name = "Odpowiedź")]
    public string? Odpowiedz { get; set; }

    [Display(Name = "Data zgłoszenia")]
    public DateTime DataZgloszenia { get; set; }

    [Display(Name = "Data odpowiedzi")]
    public DateTime? DataOdpowiedzi { get; set; }
}
