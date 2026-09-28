using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.Web.ViewModels.Admin;

// Formularze panelu administratora. Każdy przyjmuje wyłącznie pola, które użytkownik może edytować,
// i przepisuje je na encję jawnie (NaEncje) — ochrona przed over-postingiem (WN-30, CLAUDE.md zasada 11).
// Pola list wyboru (SelectListItem) wypełnia kontroler; nie są wiązane z formularza.

public sealed class BudynekFormularz
{
    public int? Id { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(10, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Kod budynku")]
    public string Kod { get; set; } = string.Empty;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(150, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Nazwa budynku")]
    public string Nazwa { get; set; } = string.Empty;

    [StringLength(250, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Adres")]
    public string? Adres { get; set; }

    [Display(Name = "Opis budynku")]
    public string? Opis { get; set; }

    [Display(Name = "Opis dostępności architektonicznej")]
    public string? OpisDostepnosciArchitektonicznej { get; set; }

    [Display(Name = "Budynek ma windę")]
    public bool CzyMaWinde { get; set; }

    [Display(Name = "Budynek aktywny (widoczny w przewodniku)")]
    public bool CzyAktywny { get; set; } = true;

    public static BudynekFormularz Z(Budynek b) => new()
    {
        Id = b.Id, Kod = b.Kod, Nazwa = b.Nazwa, Adres = b.Adres, Opis = b.Opis,
        OpisDostepnosciArchitektonicznej = b.OpisDostepnosciArchitektonicznej, CzyMaWinde = b.CzyMaWinde, CzyAktywny = b.CzyAktywny,
    };

    public Budynek NaEncje(Budynek b)
    {
        b.Kod = Kod.Trim(); b.Nazwa = Nazwa.Trim(); b.Adres = Adres; b.Opis = Opis;
        b.OpisDostepnosciArchitektonicznej = OpisDostepnosciArchitektonicznej; b.CzyMaWinde = CzyMaWinde; b.CzyAktywny = CzyAktywny;
        return b;
    }
}

public sealed class PietroFormularz
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Wybierz budynek z listy.")]
    [Display(Name = "Budynek")]
    public int? BudynekId { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Range(-5, 20, ErrorMessage = Komunikaty.Zakres)]
    [Display(Name = "Numer piętra (−1 piwnica, 0 parter)")]
    public int? Numer { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(100, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Nazwa piętra")]
    public string Nazwa { get; set; } = string.Empty;

    [Display(Name = "Opis piętra")]
    public string? Opis { get; set; }

    public IEnumerable<SelectListItem> Budynki { get; set; } = [];

    public static PietroFormularz Z(Pietro p) => new() { Id = p.Id, BudynekId = p.BudynekId, Numer = p.Numer, Nazwa = p.Nazwa, Opis = p.Opis };

    public Pietro NaEncje(Pietro p)
    {
        p.BudynekId = BudynekId!.Value; p.Numer = Numer!.Value; p.Nazwa = Nazwa.Trim(); p.Opis = Opis;
        return p;
    }
}

public sealed class SalaFormularz
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Wybierz piętro z listy.")]
    [Display(Name = "Piętro")]
    public int? PietroId { get; set; }

    [Required(ErrorMessage = "Wybierz typ sali z listy.")]
    [Display(Name = "Typ sali")]
    public int? TypSaliId { get; set; }

    [Display(Name = "Punkt wejściowy (punkt ruchu przed drzwiami)")]
    public int? PunktWejsciowyId { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(20, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Symbol sali (jak na drzwiach)")]
    public string Symbol { get; set; } = string.Empty;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(200, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Nazwa sali")]
    public string Nazwa { get; set; } = string.Empty;

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

    [Display(Name = "Sala dostępna dla osób na wózkach")]
    public bool CzyDostepnaDlaWozkow { get; set; }

    [Display(Name = "Sala aktywna (widoczna w przewodniku)")]
    public bool CzyAktywna { get; set; } = true;

    public IEnumerable<SelectListItem> Pietra { get; set; } = [];
    public IEnumerable<SelectListItem> TypySal { get; set; } = [];
    public IEnumerable<SelectListItem> Punkty { get; set; } = [];

    public static SalaFormularz Z(Sala s) => new()
    {
        Id = s.Id, PietroId = s.PietroId, TypSaliId = s.TypSaliId, PunktWejsciowyId = s.PunktWejsciowyId, Symbol = s.Symbol,
        Nazwa = s.Nazwa, Aliasy = s.Aliasy, Opis = s.Opis, OpisGlosowy = s.OpisGlosowy, LiczbaMiejsc = s.LiczbaMiejsc,
        CzyDostepnaDlaWozkow = s.CzyDostepnaDlaWozkow, CzyAktywna = s.CzyAktywna,
    };

    public Sala NaEncje(Sala s)
    {
        s.PietroId = PietroId!.Value; s.TypSaliId = TypSaliId!.Value; s.PunktWejsciowyId = PunktWejsciowyId; s.Symbol = Symbol.Trim();
        s.Nazwa = Nazwa.Trim(); s.Aliasy = Aliasy; s.Opis = Opis; s.OpisGlosowy = OpisGlosowy; s.LiczbaMiejsc = LiczbaMiejsc;
        s.CzyDostepnaDlaWozkow = CzyDostepnaDlaWozkow; s.CzyAktywna = CzyAktywna;
        return s;
    }
}

/// <summary>Wspólny formularz słowników: typ sali, typ punktu, udogodnienie (to ostatnie ma też ikonę).</summary>
public sealed class SlownikFormularz
{
    public int? Id { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(100, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Nazwa")]
    public string Nazwa { get; set; } = string.Empty;

    [StringLength(400, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Opis")]
    public string? Opis { get; set; }

    [StringLength(50, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Nazwa ikony")]
    public string? Ikona { get; set; }
}

public sealed class PunktFormularz
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Wybierz piętro z listy.")]
    [Display(Name = "Piętro")]
    public int? PietroId { get; set; }

    [Required(ErrorMessage = "Wybierz typ punktu z listy.")]
    [Display(Name = "Typ punktu")]
    public int? TypPunktuId { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(30, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Kod punktu, np. A-0-P10")]
    public string Kod { get; set; } = string.Empty;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(150, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Nazwa miejsca")]
    public string Nazwa { get; set; } = string.Empty;

    [Display(Name = "Opis miejsca")]
    public string? Opis { get; set; }

    [StringLength(600, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Opis głosowy (czytany użytkownikowi)")]
    public string? OpisGlosowy { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Azymut]
    [Display(Name = "Domyślny zwrot użytkownika")]
    public int? AzymutDomyslny { get; set; } = 0;

    [Range(-9999999.99, 9999999.99, ErrorMessage = Komunikaty.Zakres)]
    [Display(Name = "Współrzędna X na planie")]
    public decimal? X { get; set; }

    [Range(-9999999.99, 9999999.99, ErrorMessage = Komunikaty.Zakres)]
    [Display(Name = "Współrzędna Y na planie")]
    public decimal? Y { get; set; }

    [Display(Name = "Punkt aktywny")]
    public bool CzyAktywny { get; set; } = true;

    public IEnumerable<SelectListItem> Pietra { get; set; } = [];
    public IEnumerable<SelectListItem> TypyPunktow { get; set; } = [];

    public static PunktFormularz Z(PunktRuchu p) => new()
    {
        Id = p.Id, PietroId = p.PietroId, TypPunktuId = p.TypPunktuId, Kod = p.Kod, Nazwa = p.Nazwa, Opis = p.Opis,
        OpisGlosowy = p.OpisGlosowy, AzymutDomyslny = p.AzymutDomyslny, X = p.X, Y = p.Y, CzyAktywny = p.CzyAktywny,
    };

    public PunktRuchu NaEncje(PunktRuchu p)
    {
        p.PietroId = PietroId!.Value; p.TypPunktuId = TypPunktuId!.Value; p.Kod = Kod.Trim(); p.Nazwa = Nazwa.Trim(); p.Opis = Opis;
        p.OpisGlosowy = OpisGlosowy; p.AzymutDomyslny = AzymutDomyslny!.Value; p.X = X; p.Y = Y; p.CzyAktywny = CzyAktywny;
        return p;
    }
}

public sealed class KierunekFormularz : IValidatableObject
{
    public const string CelPunkt = "punkt";
    public const string CelSala = "sala";

    public int? Id { get; set; }

    [Required(ErrorMessage = "Wybierz punkt źródłowy z listy.")]
    [Display(Name = "Z punktu")]
    public int? PunktZrodlowyId { get; set; }

    [Required(ErrorMessage = "Wybierz azymut z listy.")]
    [Azymut]
    [Display(Name = "Azymut (kierunek bezwzględny)")]
    public int? Azymut { get; set; }

    /// <summary>„punkt” albo „sala” — kierunek prowadzi DOKŁADNIE do jednego celu (CK_Kierunek_JedenCel).</summary>
    [Display(Name = "Kierunek prowadzi do")]
    public string RodzajCelu { get; set; } = CelPunkt;

    [Display(Name = "Punkt docelowy")]
    public int? PunktDocelowyId { get; set; }

    [Display(Name = "Sala docelowa")]
    public int? SalaDocelowaId { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [Range(typeof(decimal), "0.01", "200", ParseLimitsInInvariantCulture = true, ErrorMessage = "Odległość musi wynosić od 0,01 do 200 metrów.")]
    [Display(Name = "Odległość w metrach")]
    public decimal? Waga { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [EnumDataType(typeof(RodzajPrzejscia), ErrorMessage = "Wybierz rodzaj przejścia z listy.")]
    [Display(Name = "Rodzaj przejścia")]
    public RodzajPrzejscia? RodzajPrzejscia { get; set; } = Models.RodzajPrzejscia.Korytarz;

    [StringLength(600, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Opis przejścia (czytany użytkownikowi)")]
    public string? OpisPrzejscia { get; set; }

    [Display(Name = "Przejście istnieje (odznacz, jeśli to tylko punkt orientacyjny, np. ściana z gablotą)")]
    public bool CzyAktywny { get; set; } = true;

    [Display(Name = "Przejście dostępne bez schodów")]
    public bool CzyDostepnyBezSchodow { get; set; } = true;

    /// <summary>Domyślnie zaznaczone — WF-24, P-03, P-07.</summary>
    [Display(Name = "Utwórz też kierunek powrotny (ta sama odległość, azymut odwrócony o 180°)")]
    public bool UtworzPowrotny { get; set; } = true;

    [StringLength(600, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Opis przejścia w drugą stronę")]
    public string? OpisPrzejsciaPowrotnego { get; set; }

    public IEnumerable<SelectListItem> Punkty { get; set; } = [];
    public IEnumerable<SelectListItem> Sale { get; set; } = [];

    public static IEnumerable<SelectListItem> Azymuty =>
    [
        new("0° — w głąb budynku (północ planu)", "0"),
        new("90° — w prawo od wejścia głównego (wschód)", "90"),
        new("180° — w stronę wejścia głównego (południe)", "180"),
        new("270° — w lewo od wejścia głównego (zachód)", "270"),
    ];

    public static IEnumerable<SelectListItem> RodzajePrzejsc =>
    [
        new("korytarz", nameof(Models.RodzajPrzejscia.Korytarz)),
        new("drzwi", nameof(Models.RodzajPrzejscia.Drzwi)),
        new("schody", nameof(Models.RodzajPrzejscia.Schody)),
        new("winda", nameof(Models.RodzajPrzejscia.Winda)),
        new("podjazd", nameof(Models.RodzajPrzejscia.Podjazd)),
    ];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RodzajCelu == CelPunkt && PunktDocelowyId is null)
            yield return new ValidationResult("Wybierz punkt docelowy z listy.", [nameof(PunktDocelowyId)]);
        if (RodzajCelu == CelSala && SalaDocelowaId is null)
            yield return new ValidationResult("Wybierz salę docelową z listy.", [nameof(SalaDocelowaId)]);
        if (RodzajCelu is not (CelPunkt or CelSala))
            yield return new ValidationResult("Wybierz, czy kierunek prowadzi do punktu ruchu, czy do sali.", [nameof(RodzajCelu)]);
        if (RodzajCelu == CelPunkt && PunktDocelowyId is not null && PunktDocelowyId == PunktZrodlowyId)
            yield return new ValidationResult("Kierunek nie może prowadzić do punktu, z którego wychodzi.", [nameof(PunktDocelowyId)]);
    }

    public static KierunekFormularz Z(Kierunek k) => new()
    {
        Id = k.Id, PunktZrodlowyId = k.PunktZrodlowyId, Azymut = k.Azymut,
        RodzajCelu = k.SalaDocelowaId is null ? CelPunkt : CelSala,
        PunktDocelowyId = k.PunktDocelowyId, SalaDocelowaId = k.SalaDocelowaId, Waga = k.Waga, RodzajPrzejscia = k.RodzajPrzejscia,
        OpisPrzejscia = k.OpisPrzejscia, CzyAktywny = k.CzyAktywny, CzyDostepnyBezSchodow = k.CzyDostepnyBezSchodow, UtworzPowrotny = false,
    };

    public Kierunek NaEncje(Kierunek k)
    {
        k.PunktZrodlowyId = PunktZrodlowyId!.Value; k.Azymut = Azymut!.Value;
        k.PunktDocelowyId = RodzajCelu == CelPunkt ? PunktDocelowyId : null;
        k.SalaDocelowaId = RodzajCelu == CelSala ? SalaDocelowaId : null;
        k.Waga = Waga!.Value; k.RodzajPrzejscia = RodzajPrzejscia!.Value; k.OpisPrzejscia = OpisPrzejscia;
        k.CzyAktywny = CzyAktywny; k.CzyDostepnyBezSchodow = CzyDostepnyBezSchodow;
        return k;
    }
}

/// <summary>Strona potwierdzenia usunięcia albo dezaktywacji — osobna strona, nie okno confirm() (wymóg zadania).</summary>
public sealed record PotwierdzenieViewModel(
    string Tytul,
    string Pytanie,
    string? Wyjasnienie,
    string? Przeszkoda,
    string TekstPrzycisku,
    string AkcjaFormularza,
    string AdresAnulowania,
    string TekstAnulowania,
    bool PokazOpcjePowrotnego = false);
