using System.ComponentModel.DataAnnotations;

namespace PrzewodnikSWPW.Web.Models;

/// <summary>Tabela łącznikowa N:M między salą a udogodnieniem.</summary>
public class SalaUdogodnienie
{
    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Display(Name = "Sala")]
    public int SalaId { get; set; }
    public Sala Sala { get; set; } = null!;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Display(Name = "Udogodnienie")]
    public int UdogodnienieId { get; set; }
    public Udogodnienie Udogodnienie { get; set; } = null!;

    [StringLength(300, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Uwagi")]
    public string? Uwagi { get; set; }
}
