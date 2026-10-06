using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace PrzewodnikSWPW.Web.Models;

/// <summary>
/// Krawędź grafu skierowanego z wagą: możliwość przejścia z punktu ruchu w określoną stronę.
/// Przechowuje azymut BEZWZGLĘDNY — etykiety „prosto / w lewo / w prawo / do tyłu” są
/// wyliczane względem zwrotu użytkownika (docs/04_BAZA_DANYCH.md rozdz. 5, ryzyko P-01).
/// Nie dodawaj tu kolumn typu WLewoId / DoPrzoduId.
/// </summary>
public class Kierunek : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Display(Name = "Punkt źródłowy")]
    public int PunktZrodlowyId { get; set; }
    public PunktRuchu PunktZrodlowy { get; set; } = null!;

    /// <summary>Cel: punkt ruchu. Wypełnione dokładnie jedno z <see cref="PunktDocelowyId"/> i <see cref="SalaDocelowaId"/>.</summary>
    [Display(Name = "Punkt docelowy")]
    public int? PunktDocelowyId { get; set; }
    public PunktRuchu? PunktDocelowy { get; set; }

    /// <summary>Cel: sala. Wypełnione dokładnie jedno z <see cref="PunktDocelowyId"/> i <see cref="SalaDocelowaId"/>.</summary>
    [Display(Name = "Sala docelowa")]
    public int? SalaDocelowaId { get; set; }
    public Sala? SalaDocelowa { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Azymut]
    [Display(Name = "Azymut (kierunek bezwzględny)")]
    public int Azymut { get; set; }

    /// <summary>Odległość w metrach — algorytm wyznacza trasę najkrótszą, nie o najmniejszej liczbie punktów.</summary>
    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Precision(6, 2)]
    [Range(typeof(decimal), "0.01", "200", ParseLimitsInInvariantCulture = true, ErrorMessage = Komunikaty.Zakres)]
    [Display(Name = "Odległość w metrach")]
    public decimal Waga { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [EnumDataType(typeof(RodzajPrzejscia), ErrorMessage = "Wybierz rodzaj przejścia z listy.")]
    [Display(Name = "Rodzaj przejścia")]
    public RodzajPrzejscia RodzajPrzejscia { get; set; } = RodzajPrzejscia.Korytarz;

    [StringLength(600, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Opis przejścia (czytany użytkownikowi)")]
    public string? OpisPrzejscia { get; set; }

    /// <summary><c>false</c> = brak przejścia; kierunek nadal jest opisany jako punkt orientacyjny.</summary>
    [Display(Name = "Przejście istnieje")]
    public bool CzyAktywny { get; set; } = true;

    [Display(Name = "Dostępny bez schodów")]
    public bool CzyDostepnyBezSchodow { get; set; } = true;

    [Display(Name = "Kierunek powrotny")]
    public int? KierunekPowrotnyId { get; set; }
    public Kierunek? KierunekPowrotny { get; set; }

    public ICollection<Utrudnienie> Utrudnienia { get; set; } = [];
    public ICollection<ObszarAktywny> ObszaryAktywne { get; set; } = [];

    /// <summary>Odpowiedniki ograniczeń CK_Kierunek_JedenCel i CK_Kierunek_BezPetli po stronie serwera.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PunktDocelowyId.HasValue == SalaDocelowaId.HasValue)
        {
            yield return new ValidationResult(
                "Kierunek musi prowadzić do punktu ruchu albo do sali — wybierz dokładnie jeden cel.",
                [nameof(PunktDocelowyId), nameof(SalaDocelowaId)]);
        }

        if (PunktDocelowyId == PunktZrodlowyId)
        {
            yield return new ValidationResult(
                "Kierunek nie może prowadzić do tego samego punktu, z którego wychodzi.",
                [nameof(PunktDocelowyId)]);
        }
    }
}
