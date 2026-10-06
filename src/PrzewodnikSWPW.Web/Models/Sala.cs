using System.ComponentModel.DataAnnotations;

namespace PrzewodnikSWPW.Web.Models;

/// <summary>Pomieszczenie docelowe — cel wyszukiwania i wyznaczania trasy.</summary>
public class Sala
{
    public int Id { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Display(Name = "Piętro")]
    public int PietroId { get; set; }
    public Pietro Pietro { get; set; } = null!;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Display(Name = "Typ sali")]
    public int TypSaliId { get; set; }
    public TypSali TypSali { get; set; } = null!;

    /// <summary>Punkt ruchu na korytarzu przed drzwiami — wierzchołek startowy/końcowy trasy.</summary>
    [Display(Name = "Punkt wejściowy (przed drzwiami)")]
    public int? PunktWejsciowyId { get; set; }
    public PunktRuchu? PunktWejsciowy { get; set; }

    /// <summary>Oznaczenie widoczne na drzwiach, np. „A15”.</summary>
    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(20, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Symbol sali")]
    public string Symbol { get; set; } = string.Empty;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(200, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Nazwa sali")]
    public string Nazwa { get; set; } = string.Empty;

    /// <summary>Dodatkowe nazwy rozdzielone średnikiem, np. „dziekanat;sekretariat WI” (WF-11).</summary>
    [StringLength(400, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Inne nazwy (rozdzielone średnikiem)")]
    public string? Aliasy { get; set; }

    [Display(Name = "Opis sali")]
    public string? Opis { get; set; }

    [StringLength(600, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Opis głosowy (czytany użytkownikowi)")]
    public string? OpisGlosowy { get; set; }

    [Range(0, 2000, ErrorMessage = Komunikaty.Zakres)]
    [Display(Name = "Liczba miejsc")]
    public int? LiczbaMiejsc { get; set; }

    [Display(Name = "Dostępna dla osób na wózkach")]
    public bool CzyDostepnaDlaWozkow { get; set; }

    [Display(Name = "Aktywna")]
    public bool CzyAktywna { get; set; } = true;

    public ICollection<Kierunek> KierunkiDoSali { get; set; } = [];
    public ICollection<Zdjecie> Zdjecia { get; set; } = [];
    public ICollection<Udogodnienie> Udogodnienia { get; set; } = [];
    public ICollection<SalaUdogodnienie> SalaUdogodnienia { get; set; } = [];
}
