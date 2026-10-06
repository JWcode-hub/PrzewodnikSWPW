using System.ComponentModel.DataAnnotations;

namespace PrzewodnikSWPW.Web.Models;

public class Udogodnienie : ISlownik
{
    public int Id { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(100, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Nazwa udogodnienia")]
    public string Nazwa { get; set; } = string.Empty;

    [StringLength(400, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Opis udogodnienia")]
    public string? Opis { get; set; }

    [StringLength(50, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Ikona")]
    public string? Ikona { get; set; }

    public ICollection<PunktRuchu> PunktyRuchu { get; set; } = [];
    public ICollection<Sala> Sale { get; set; } = [];
    public ICollection<PunktUdogodnienie> PunktUdogodnienia { get; set; } = [];
    public ICollection<SalaUdogodnienie> SalaUdogodnienia { get; set; } = [];
}
