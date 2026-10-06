using System.ComponentModel.DataAnnotations;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.Web.ViewModels;

public sealed record DeklaracjaViewModel(
    DeklaracjaDostepnosciOptions Dane,
    string AdresSerwisu,
    IReadOnlyList<DostepnoscBudynku> Budynki,
    KontaktAlternatywnyOptions KontaktAlternatywny);

/// <summary>
/// Zgłoszenie braku dostępności (WF-32, art. 18 ustawy). Wymagana jest wyłącznie treść — zgłoszenie
/// anonimowe musi być możliwe, więc imię, e-mail i adres strony są opcjonalne.
/// </summary>
public sealed class ZgloszenieFormularz
{
    [Required(ErrorMessage = "Opisz problem — pole „Treść zgłoszenia” jest wymagane.")]
    [StringLength(4000, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Treść zgłoszenia")]
    public string? Tresc { get; set; }

    [StringLength(500, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Adres strony, której dotyczy zgłoszenie (opcjonalnie)")]
    public string? AdresStrony { get; set; }

    [StringLength(200, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Imię i nazwisko (opcjonalnie)")]
    public string? ImieNazwisko { get; set; }

    [EmailAddress(ErrorMessage = "Wpisz adres e-mail w postaci nazwa@domena.pl albo zostaw pole puste.")]
    [StringLength(254, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Adres e-mail do odpowiedzi (opcjonalnie)")]
    public string? Email { get; set; }
}
