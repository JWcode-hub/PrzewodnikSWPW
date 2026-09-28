using System.ComponentModel.DataAnnotations;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.Web.ViewModels;

public sealed class UstawieniaViewModel : IValidatableObject
{
    public static readonly IReadOnlyList<(Motyw Wartosc, string Nazwa, string Opis)> Motywy =
    [
        (Motyw.Systemowy, "Jak w systemie", "Kolory dopasowane do ustawień twojego urządzenia."),
        (Motyw.Podstawowy, "Podstawowy", "Granatowy tekst na białym tle."),
        (Motyw.WysokiKontrast, "Wysoki kontrast", "Żółty tekst na czarnym tle."),
        (Motyw.Ciemny, "Ciemny", "Jasny tekst na ciemnym tle."),
    ];

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [EnumDataType(typeof(Motyw), ErrorMessage = "Wybierz jeden z motywów z listy.")]
    [Display(Name = "Motyw kolorystyczny")]
    public Motyw Motyw { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Display(Name = "Rozmiar tekstu")]
    public int RozmiarTekstu { get; set; } = 100;

    [Display(Name = "Czytaj opisy miejsc mową syntetyczną")]
    public bool MowaWlaczona { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Display(Name = "Tempo mowy")]
    public int TempoMowy { get; set; } = 100;

    [Display(Name = "Skróty klawiszowe (Alt+1 do Alt+4 i Alt+P)")]
    public bool SkrotyWlaczone { get; set; } = true;

    [Display(Name = "Ogranicz animacje i ruch na stronie")]
    public bool OgraniczAnimacje { get; set; }

    public static UstawieniaViewModel Z(UstawieniaDostepnosci u) => new()
    {
        Motyw = u.Motyw,
        RozmiarTekstu = u.RozmiarTekstu,
        MowaWlaczona = u.MowaWlaczona,
        TempoMowy = u.TempoMowy,
        SkrotyWlaczone = u.SkrotyWlaczone,
        OgraniczAnimacje = u.OgraniczAnimacje,
    };

    public UstawieniaDostepnosci NaUstawienia() => new()
    {
        Motyw = Motyw,
        RozmiarTekstu = RozmiarTekstu,
        MowaWlaczona = MowaWlaczona,
        TempoMowy = TempoMowy,
        SkrotyWlaczone = SkrotyWlaczone,
        OgraniczAnimacje = OgraniczAnimacje,
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!UstawieniaDostepnosci.DozwoloneRozmiaryTekstu.Contains(RozmiarTekstu))
        {
            yield return new ValidationResult("Wybierz rozmiar tekstu: 100, 125, 150 albo 200 procent.", [nameof(RozmiarTekstu)]);
        }

        if (!UstawieniaDostepnosci.DozwoloneTempaMowy.Contains(TempoMowy))
        {
            yield return new ValidationResult("Wybierz tempo mowy z listy.", [nameof(TempoMowy)]);
        }
    }

    /// <summary>Etykieta tempa mowy, np. „0,75 — wolniej”.</summary>
    public static string OpisTempa(int procent) => procent switch
    {
        50 => "0,5 — bardzo wolno",
        75 => "0,75 — wolniej",
        100 => "1 — normalnie",
        125 => "1,25 — szybciej",
        150 => "1,5 — szybko",
        200 => "2 — bardzo szybko",
        _ => $"{procent / 100m}",
    };
}
