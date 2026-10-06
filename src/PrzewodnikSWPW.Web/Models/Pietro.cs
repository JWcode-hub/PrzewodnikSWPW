using System.ComponentModel.DataAnnotations;

namespace PrzewodnikSWPW.Web.Models;

public class Pietro
{
    public int Id { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Display(Name = "Budynek")]
    public int BudynekId { get; set; }
    public Budynek Budynek { get; set; } = null!;

    /// <summary>−1 = piwnica, 0 = parter, 1, 2, … — liczba ze znakiem, aby sortować kondygnacje.</summary>
    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Range(-5, 20, ErrorMessage = Komunikaty.Zakres)]
    [Display(Name = "Numer piętra")]
    public int Numer { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(100, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Nazwa piętra")]
    public string Nazwa { get; set; } = string.Empty;

    [Display(Name = "Opis piętra")]
    public string? Opis { get; set; }

    public ICollection<Sala> Sale { get; set; } = [];
    public ICollection<PunktRuchu> PunktyRuchu { get; set; } = [];
}
