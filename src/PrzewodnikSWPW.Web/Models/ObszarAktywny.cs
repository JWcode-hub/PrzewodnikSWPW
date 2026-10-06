using System.ComponentModel.DataAnnotations;

namespace PrzewodnikSWPW.Web.Models;

/// <summary>Aktywny obszar na zdjęciu powiązany z kierunkiem (odpowiednik elementu &lt;area&gt;).</summary>
public class ObszarAktywny
{
    public int Id { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Display(Name = "Zdjęcie")]
    public int ZdjecieId { get; set; }
    public Zdjecie Zdjecie { get; set; } = null!;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Display(Name = "Kierunek")]
    public int KierunekId { get; set; }
    public Kierunek Kierunek { get; set; } = null!;

    /// <summary>rect | circle | poly — jak atrybut <c>shape</c> elementu &lt;area&gt;.</summary>
    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(10, ErrorMessage = Komunikaty.MaksDlugosc)]
    [RegularExpression("^(rect|circle|poly)$", ErrorMessage = "Kształt musi mieć wartość rect, circle albo poly.")]
    [Display(Name = "Kształt obszaru")]
    public string Ksztalt { get; set; } = "rect";

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(400, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Współrzędne obszaru")]
    public string Wspolrzedne { get; set; } = string.Empty;

    /// <summary>Tekst odczytywany przez czytnik ekranu — bez niego obszar jest dla niewidomego niewidoczny.</summary>
    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(200, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Etykieta obszaru (czytana użytkownikowi)")]
    public string Etykieta { get; set; } = string.Empty;
}
