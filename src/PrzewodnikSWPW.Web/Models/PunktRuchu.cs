using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace PrzewodnikSWPW.Web.Models;

/// <summary>Wierzchołek grafu nawigacji — miejsce, w którym użytkownik podejmuje decyzję o ruchu.</summary>
public class PunktRuchu
{
    public int Id { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Display(Name = "Piętro")]
    public int PietroId { get; set; }
    public Pietro Pietro { get; set; } = null!;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Display(Name = "Typ punktu")]
    public int TypPunktuId { get; set; }
    public TypPunktu TypPunktu { get; set; } = null!;

    /// <summary>Kod punktu, np. „A-0-P03”.</summary>
    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(30, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Kod punktu")]
    public string Kod { get; set; } = string.Empty;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(150, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Nazwa miejsca")]
    public string Nazwa { get; set; } = string.Empty;

    [Display(Name = "Opis miejsca")]
    public string? Opis { get; set; }

    /// <summary>Krótki tekst dla syntezatora mowy — bez skrótów, liczby słownie. Inny niż <see cref="Opis"/>.</summary>
    [StringLength(600, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Opis głosowy (czytany użytkownikowi)")]
    public string? OpisGlosowy { get; set; }

    /// <summary>Zwrot użytkownika po wejściu do punktu z zewnątrz (np. z linku bezpośredniego).</summary>
    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Azymut]
    [Display(Name = "Domyślny zwrot (azymut)")]
    public int AzymutDomyslny { get; set; }

    /// <summary>Współrzędna na planie piętra — wyłącznie do rysowania planu, nie do wyznaczania trasy.</summary>
    [Precision(9, 2)]
    [Range(-9999999.99, 9999999.99, ErrorMessage = Komunikaty.Zakres)]
    [Display(Name = "Współrzędna X na planie")]
    public decimal? X { get; set; }

    [Precision(9, 2)]
    [Range(-9999999.99, 9999999.99, ErrorMessage = Komunikaty.Zakres)]
    [Display(Name = "Współrzędna Y na planie")]
    public decimal? Y { get; set; }

    [Display(Name = "Aktywny")]
    public bool CzyAktywny { get; set; } = true;

    public ICollection<Kierunek> KierunkiWychodzace { get; set; } = [];
    public ICollection<Kierunek> KierunkiPrzychodzace { get; set; } = [];
    public ICollection<Zdjecie> Zdjecia { get; set; } = [];
    public ICollection<Utrudnienie> Utrudnienia { get; set; } = [];
    public ICollection<Udogodnienie> Udogodnienia { get; set; } = [];
    public ICollection<PunktUdogodnienie> PunktUdogodnienia { get; set; } = [];
}
