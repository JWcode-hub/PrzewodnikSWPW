using System.ComponentModel.DataAnnotations;

namespace PrzewodnikSWPW.Web.Models;

/// <summary>
/// Azymut BEZWZGLĘDNY w stopniach: 0 = północ budynku, 90 = wschód, 180 = południe, 270 = zachód.
/// Kierunki względne (prosto / w lewo / w prawo / do tyłu) są wyliczane, nigdy zapisywane
/// — docs/04_BAZA_DANYCH.md rozdz. 5, ryzyko P-01.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AzymutAttribute : ValidationAttribute
{
    public static readonly int[] DozwoloneWartosci = [0, 90, 180, 270];

    public AzymutAttribute()
        : base("Pole „{0}” musi mieć jedną z wartości: 0, 90, 180 albo 270 stopni.")
    {
    }

    /// <summary>Brak wartości przepuszczamy — to zadanie atrybutu [Required] (inaczej pole dostałoby dwa błędy).</summary>
    public override bool IsValid(object? value) =>
        value is null || (value is int azymut && DozwoloneWartosci.Contains(azymut));
}
