using System.ComponentModel.DataAnnotations;

namespace PrzewodnikSWPW.Web.Models;

/// <summary>
/// Zdjęcie punktu ruchu albo sali. Tekst alternatywny jest wymagany przez schemat bazy
/// (NOT NULL + CK_Zdjecie_Alt) — dostępność wymuszona schematem, nie regulaminem (WCAG 1.1.1).
/// </summary>
public class Zdjecie : IValidatableObject
{
    public const int MinDlugoscTekstuAlternatywnego = 5;

    public int Id { get; set; }

    [Display(Name = "Punkt ruchu")]
    public int? PunktRuchuId { get; set; }
    public PunktRuchu? PunktRuchu { get; set; }

    [Display(Name = "Sala")]
    public int? SalaId { get; set; }
    public Sala? Sala { get; set; }

    /// <summary>
    /// Nazwa nadana przez serwer, względem katalogu /media, np. „zdjecia/3f2a….jpg” (D-10) — jeden plik,
    /// rozszerzenie z rozpoznanej zawartości, metadane usunięte przy zapisie.
    /// </summary>
    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(400, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Ścieżka pliku")]
    public string SciezkaPliku { get; set; } = string.Empty;

    /// <summary>
    /// Pusty wyłącznie dla zdjęcia dekoracyjnego (wtedy w widoku <c>alt=""</c>);
    /// dla informacyjnego — co najmniej 5 znaków.
    /// </summary>
    [Required(AllowEmptyStrings = true, ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(300, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Tekst alternatywny (opis zdjęcia)")]
    public string TekstAlternatywny { get; set; } = string.Empty;

    [Display(Name = "Opis rozszerzony")]
    public string? OpisRozszerzony { get; set; }

    /// <summary>Kto zrobił zdjęcie albo skąd pochodzi — wymagane przy każdym pliku (WN-39, P-06, D-09).</summary>
    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(200, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Źródło zdjęcia")]
    public string Zrodlo { get; set; } = string.Empty;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(200, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Licencja")]
    public string Licencja { get; set; } = string.Empty;

    /// <summary>Wymiary obrazu w orientacji wyświetlanej — atrybuty width/height i układ współrzędnych obszarów aktywnych (D-09, D-10).</summary>
    public int? Szerokosc { get; set; }

    public int? Wysokosc { get; set; }

    [Range(0, 1000, ErrorMessage = Komunikaty.Zakres)]
    [Display(Name = "Kolejność wyświetlania")]
    public int Kolejnosc { get; set; }

    [Display(Name = "Zdjęcie dekoracyjne (bez treści informacyjnej)")]
    public bool CzyDekoracyjne { get; set; }

    public ICollection<ObszarAktywny> ObszaryAktywne { get; set; } = [];

    /// <summary>Odpowiedniki ograniczeń CK_Zdjecie_Wlasciciel i CK_Zdjecie_Alt po stronie serwera.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PunktRuchuId.HasValue == SalaId.HasValue)
        {
            yield return new ValidationResult(
                "Zdjęcie musi należeć do punktu ruchu albo do sali — wybierz dokładnie jedno.",
                [nameof(PunktRuchuId), nameof(SalaId)]);
        }

        if (!CzyDekoracyjne && (TekstAlternatywny ?? string.Empty).Trim().Length < MinDlugoscTekstuAlternatywnego)
        {
            yield return new ValidationResult(
                $"Opisz, co widać na zdjęciu — tekst alternatywny musi mieć co najmniej {MinDlugoscTekstuAlternatywnego} znaków. " +
                "Jeśli zdjęcie jest wyłącznie dekoracyjne, zaznacz pole „Zdjęcie dekoracyjne”.",
                [nameof(TekstAlternatywny)]);
        }
    }
}
