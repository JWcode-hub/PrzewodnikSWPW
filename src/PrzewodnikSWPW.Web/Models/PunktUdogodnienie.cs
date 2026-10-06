using System.ComponentModel.DataAnnotations;

namespace PrzewodnikSWPW.Web.Models;

/// <summary>Tabela łącznikowa N:M między punktem ruchu a udogodnieniem.</summary>
public class PunktUdogodnienie
{
    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Display(Name = "Punkt ruchu")]
    public int PunktRuchuId { get; set; }
    public PunktRuchu PunktRuchu { get; set; } = null!;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Display(Name = "Udogodnienie")]
    public int UdogodnienieId { get; set; }
    public Udogodnienie Udogodnienie { get; set; } = null!;

    [StringLength(300, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Uwagi")]
    public string? Uwagi { get; set; }
}
