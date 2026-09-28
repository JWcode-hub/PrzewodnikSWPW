using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace PrzewodnikSWPW.Web.Models;

/// <summary>Log zapytań o trasę — KPI K4/K5 i wykrywanie zapytań bez wyniku (luk w danych).</summary>
public class ZapytanieTrasy
{
    public long Id { get; set; }

    [Display(Name = "Sala początkowa")]
    public int? SalaZId { get; set; }
    public Sala? SalaZ { get; set; }

    [Display(Name = "Sala docelowa")]
    public int? SalaDoId { get; set; }
    public Sala? SalaDo { get; set; }

    /// <summary>Co wpisał użytkownik, gdy sali nie znaleziono.</summary>
    [StringLength(100, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Wpisana sala początkowa")]
    public string? FrazaZ { get; set; }

    [StringLength(100, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Wpisana sala docelowa")]
    public string? FrazaDo { get; set; }

    [Display(Name = "Tryb windy")]
    public bool TrybWindy { get; set; }

    [Display(Name = "Trasa znaleziona")]
    public bool CzySukces { get; set; }

    [Precision(8, 2)]
    [Range(typeof(decimal), "0", "999999.99", ParseLimitsInInvariantCulture = true, ErrorMessage = Komunikaty.Zakres)]
    [Display(Name = "Długość trasy w metrach")]
    public decimal? DlugoscMetry { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = Komunikaty.Zakres)]
    [Display(Name = "Liczba kroków")]
    public int? LiczbaKrokow { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = Komunikaty.Zakres)]
    [Display(Name = "Czas wyznaczenia (ms)")]
    public int? CzasMs { get; set; }

    [Display(Name = "Data zapytania")]
    public DateTime DataZapytania { get; set; }
}
