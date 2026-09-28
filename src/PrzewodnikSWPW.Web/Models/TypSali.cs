using System.ComponentModel.DataAnnotations;

namespace PrzewodnikSWPW.Web.Models;

public class TypSali : ISlownik
{
    public int Id { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(50, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Nazwa typu sali")]
    public string Nazwa { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Opis typu sali")]
    public string? Opis { get; set; }

    public ICollection<Sala> Sale { get; set; } = [];
}
