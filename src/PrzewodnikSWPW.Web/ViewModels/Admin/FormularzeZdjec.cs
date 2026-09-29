using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.Web.ViewModels.Admin;

// Zdjęcia (WF-26). Jak pozostałe formularze panelu: tylko pola edytowalne
// i jawne przepisanie na encję (NaEncje). Nazwy pliku ani wymiarów formularz nie przyjmuje — ustala je serwer.

public sealed class ZdjecieFormularz : IValidatableObject
{
    public int? Id { get; set; }

    /// <summary>Właściciel — dokładnie jedno z dwóch; pola ukryte, sprawdzane w serwisie.</summary>
    public int? PunktRuchuId { get; set; }
    public int? SalaId { get; set; }

    [Display(Name = "Plik zdjęcia")]
    public IFormFile? Plik { get; set; }

    /// <summary>
    /// Wymagany, chyba że zdjęcie jest dekoracyjne — wtedy zapisujemy pusty tekst i renderujemy alt="" (WN-06).
    /// Dlatego nie [Required], tylko reguła w <see cref="Validate"/>; baza i tak odrzuci brak (CK_Zdjecie_Alt).
    /// </summary>
    [StringLength(300, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Tekst alternatywny (opis zdjęcia)")]
    public string? TekstAlternatywny { get; set; }

    [Display(Name = "Zdjęcie dekoracyjne (nie wnosi informacji — czytnik ekranu je pominie)")]
    public bool CzyDekoracyjne { get; set; }

    [Display(Name = "Opis rozszerzony (opcjonalny, dłuższy niż tekst alternatywny)")]
    public string? OpisRozszerzony { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(200, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Źródło zdjęcia")]
    public string? Zrodlo { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(200, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Licencja albo zgoda na wykorzystanie")]
    public string? Licencja { get; set; }

    [Range(0, 1000, ErrorMessage = Komunikaty.Zakres)]
    [Display(Name = "Kolejność wyświetlania")]
    public int Kolejnosc { get; set; }

    // Tylko do wyświetlenia — nie są wiązane z formularza (kontroler wypełnia je z bazy).
    public string NazwaWlasciciela { get; set; } = string.Empty;
    public string? SciezkaPliku { get; set; }
    public int? Szerokosc { get; set; }
    public int? Wysokosc { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CzyDekoracyjne) yield break;

        var dlugosc = (TekstAlternatywny ?? "").Trim().Length;
        if (dlugosc == 0)
        {
            yield return new ValidationResult(
                "Pole „Tekst alternatywny (opis zdjęcia)” jest wymagane. Opisz, co widać na zdjęciu, a jeśli zdjęcie " +
                "niczego nie wnosi — zaznacz „Zdjęcie dekoracyjne”.",
                [nameof(TekstAlternatywny)]);
        }
        else if (dlugosc < Zdjecie.MinDlugoscTekstuAlternatywnego)
        {
            yield return new ValidationResult(
                $"Tekst alternatywny musi mieć co najmniej {Zdjecie.MinDlugoscTekstuAlternatywnego} znaków — opisz, co widać na zdjęciu.",
                [nameof(TekstAlternatywny)]);
        }
    }

    public static ZdjecieFormularz Z(Zdjecie z, string nazwaWlasciciela) => new()
    {
        Id = z.Id, PunktRuchuId = z.PunktRuchuId, SalaId = z.SalaId, TekstAlternatywny = z.TekstAlternatywny,
        CzyDekoracyjne = z.CzyDekoracyjne, OpisRozszerzony = z.OpisRozszerzony, Zrodlo = z.Zrodlo, Licencja = z.Licencja,
        Kolejnosc = z.Kolejnosc, NazwaWlasciciela = nazwaWlasciciela, SciezkaPliku = z.SciezkaPliku,
        Szerokosc = z.Szerokosc, Wysokosc = z.Wysokosc,
    };

    /// <summary>Właściciela przepisujemy tylko przy nowym zdjęciu — edycja nie przenosi zdjęcia do innego miejsca.</summary>
    public Zdjecie NaEncje(Zdjecie z)
    {
        if (z.Id == 0)
        {
            z.PunktRuchuId = PunktRuchuId;
            z.SalaId = PunktRuchuId is null ? SalaId : null;
        }
        z.CzyDekoracyjne = CzyDekoracyjne;
        z.TekstAlternatywny = CzyDekoracyjne ? string.Empty : TekstAlternatywny!.Trim();
        z.OpisRozszerzony = string.IsNullOrWhiteSpace(OpisRozszerzony) ? null : OpisRozszerzony.Trim();
        z.Zrodlo = Zrodlo!.Trim();
        z.Licencja = Licencja!.Trim();
        z.Kolejnosc = Kolejnosc;
        return z;
    }
}

public sealed record ZdjecieWiersz(int Id, string Nazwa, string? TekstAlternatywny, bool CzyDekoracyjne, string Zrodlo, string Licencja,
    int? Szerokosc, int? Wysokosc, int LiczbaObszarow)
{
    public static ZdjecieWiersz Z(Zdjecie z) => new(z.Id, NazwyZdjec.Nazwa(z), z.TekstAlternatywny, z.CzyDekoracyjne, z.Zrodlo, z.Licencja,
        z.Szerokosc, z.Wysokosc, z.ObszaryAktywne.Count);
}

public sealed record ZdjeciaViewModel(string NazwaWlasciciela, int? PunktRuchuId, int? SalaId, IReadOnlyList<ZdjecieWiersz> Zdjecia,
    string AdresPowrotu, string TekstPowrotu);

public static class NazwyZdjec
{
    /// <summary>Nazwa zdjęcia w linkach i nagłówkach panelu — stała i jednoznaczna (numer rekordu).</summary>
    public static string Nazwa(Zdjecie z) => $"zdjęcie nr {z.Id}";
}
