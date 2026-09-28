using System.ComponentModel.DataAnnotations;

namespace PrzewodnikSWPW.Web.Models;

public class Budynek
{
    public int Id { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(10, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Kod budynku")]
    public string Kod { get; set; } = string.Empty;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(150, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Nazwa budynku")]
    public string Nazwa { get; set; } = string.Empty;

    [StringLength(250, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Adres")]
    public string? Adres { get; set; }

    [Display(Name = "Opis budynku")]
    public string? Opis { get; set; }

    /// <summary>Obowiązkowy element Deklaracji Dostępności (Zarządzenie 14/2021 §3 ust. 4).</summary>
    [Display(Name = "Opis dostępności architektonicznej")]
    public string? OpisDostepnosciArchitektonicznej { get; set; }

    [Display(Name = "Budynek ma windę")]
    public bool CzyMaWinde { get; set; }

    [Display(Name = "Aktywny")]
    public bool CzyAktywny { get; set; } = true;

    public ICollection<Pietro> Pietra { get; set; } = [];
}
