using System.ComponentModel.DataAnnotations;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.Web.ViewModels;

public sealed class TrasaFormularzViewModel
{
    [Required(ErrorMessage = "Wybierz salę, z której chcesz wyjść.")]
    [Display(Name = "Sala początkowa")]
    public int? Z { get; set; }

    [Required(ErrorMessage = "Wybierz salę, do której chcesz dojść.")]
    [Display(Name = "Sala docelowa")]
    public int? Do { get; set; }

    [Display(Name = "Trasa bez schodów (tryb windy)")]
    public bool TrybWindy { get; set; }

    /// <summary>Sale do list wyboru — wypełniane przez kontroler, nie przez wiązanie modelu.</summary>
    public IReadOnlyList<SalaWyszukana> Sale { get; set; } = [];
}

public sealed record WynikTrasyViewModel(
    WynikTrasy Wynik,
    string AdresFormularza,
    string? AdresSaliDocelowej)
{
    /// <summary>„około 1 minuty”, „około 3 minut” — dopełniacz po „około”.</summary>
    public static string SzacowanyCzas(int sekundy)
    {
        var minuty = Math.Max(1, (int)Math.Ceiling(sekundy / 60.0));
        return $"około {minuty} {Odmiana.PoLiczbie(minuty, "minuty", "minut", "minut")}";
    }
}
