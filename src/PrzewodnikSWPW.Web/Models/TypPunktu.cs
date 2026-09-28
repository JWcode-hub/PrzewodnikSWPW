using System.ComponentModel.DataAnnotations;

namespace PrzewodnikSWPW.Web.Models;

public class TypPunktu : ISlownik
{
    public int Id { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(50, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Nazwa typu punktu")]
    public string Nazwa { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Opis typu punktu")]
    public string? Opis { get; set; }

    public ICollection<PunktRuchu> PunktyRuchu { get; set; } = [];
}
