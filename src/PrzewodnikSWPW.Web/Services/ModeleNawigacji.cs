using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Services;

/// <summary>
/// Położenie punktu w adresie /spacer/{KodBudynku}/{NumerPietra}/{SegmentPunktu}. Cały stan spaceru
/// (punkt + zwrot) jest w adresie URL, nie w sesji — ryzyko P-12.
/// </summary>
public sealed record AdresMiejsca(string KodBudynku, int NumerPietra, string SegmentPunktu)
{
    /// <summary>Kod „A-0-P04” w budynku A na piętrze 0 daje segment „P04”; inny kod zostaje w całości.</summary>
    public static AdresMiejsca Z(PunktRuchu punkt) => Z(punkt.Pietro.Budynek.Kod, punkt.Pietro.Numer, punkt.Kod);

    public static AdresMiejsca Z(string kodBudynku, int numerPietra, string kodPunktu)
    {
        var prefiks = $"{kodBudynku}-{numerPietra}-";
        var segment = kodPunktu.StartsWith(prefiks, StringComparison.OrdinalIgnoreCase) && kodPunktu.Length > prefiks.Length
            ? kodPunktu[prefiks.Length..]
            : kodPunktu;
        return new AdresMiejsca(kodBudynku, numerPietra, segment);
    }
}

/// <summary>Miejsce widziane przez użytkownika stojącego w punkcie ruchu ze zwrotem <see cref="Zwrot"/>.</summary>
public sealed record WidokMiejsca(
    int PunktId,
    string Kod,
    string Nazwa,
    string? Opis,
    string? OpisGlosowy,
    int Zwrot,
    IReadOnlyList<ZdjecieMiejsca> Zdjecia,
    IReadOnlyList<OpcjaKierunku> Kierunki,
    AdresMiejsca Adres,
    string NazwaBudynku,
    string NazwaPietra);

public sealed record ZdjecieMiejsca(
    string SciezkaPliku,
    string TekstAlternatywny,
    bool CzyDekoracyjne,
    string? OpisRozszerzony,
    int? Szerokosc = null,
    int? Wysokosc = null,
    IReadOnlyList<ObszarNaZdjeciu>? Obszary = null)
{
    public static ZdjecieMiejsca Z(Zdjecie z) => new(z.SciezkaPliku, z.TekstAlternatywny, z.CzyDekoracyjne, z.OpisRozszerzony,
        z.Szerokosc, z.Wysokosc,
        z.ObszaryAktywne.OrderBy(o => o.Id).Select(o => new ObszarNaZdjeciu(o.KierunekId, o.Ksztalt, o.Wspolrzedne, o.Etykieta)).ToList());
}

public sealed record DostepnoscBudynku(string Nazwa, string? Adres, string? OpisDostepnosciArchitektonicznej);

/// <summary>Aktywny obszar zdjęcia (WF-27) — dodatkowy link do kierunku, który jest też na liście kierunków.</summary>
public sealed record ObszarNaZdjeciu(int KierunekId, string Ksztalt, string Wspolrzedne, string Etykieta);

/// <summary>
/// Jedna z czterech pozycji listy kierunków. Gdy <see cref="CzyMozliwy"/> = false, pozycja
/// jest renderowana jako zwykły tekst z opisem przeszkody, nie jako link.
/// </summary>
public sealed record OpcjaKierunku(
    KierunekWzgledny Kierunek,
    bool CzyMozliwy,
    string Tekst,
    int? PunktDocelowyId,
    int? SalaDocelowaId,
    decimal? OdlegloscMetry,
    string? OpisPrzejscia,
    int? KierunekId = null);

public enum RodzajWynikuPrzejscia
{
    DoPunktu,
    DoSali,
    BrakPrzejscia,
}

/// <summary>
/// Wynik próby przejścia. Przy <see cref="RodzajWynikuPrzejscia.BrakPrzejscia"/> użytkownik zostaje
/// w tym samym punkcie z tym samym zwrotem, a <see cref="Opis"/> mówi dlaczego.
/// <see cref="Adres"/> to adres punktu, w którym użytkownik jest po próbie (przy wejściu do sali —
/// punkt przed jej drzwiami).
/// </summary>
public sealed record WynikPrzejscia(
    RodzajWynikuPrzejscia Rodzaj,
    int PunktId,
    int? SalaId,
    int Zwrot,
    string Opis,
    AdresMiejsca Adres)
{
    public bool CzyPrzeszedl => Rodzaj != RodzajWynikuPrzejscia.BrakPrzejscia;
}

public sealed record BudynekNaLiscie(string Kod, string Nazwa, string? Adres);

public sealed record PunktNaLiscie(AdresMiejsca Adres, string Nazwa, int AzymutDomyslny);

public sealed record PietroNaLiscie(int Numer, string Nazwa, IReadOnlyList<PunktNaLiscie> Punkty);

public sealed record WidokBudynku(
    string Kod,
    string Nazwa,
    string? Adres,
    string? OpisDostepnosciArchitektonicznej,
    IReadOnlyList<PietroNaLiscie> Pietra,
    PunktNaLiscie? PunktStartowy);

public sealed record UdogodnienieSali(string Nazwa, string? Uwagi);

/// <summary>Karta sali. <see cref="Powrot"/> prowadzi na korytarz przed drzwiami, twarzą od drzwi.</summary>
public sealed record WidokSali(
    int Id,
    string Symbol,
    string Nazwa,
    string TypSali,
    string? Opis,
    string? OpisGlosowy,
    string NazwaBudynku,
    string NazwaPietra,
    int? LiczbaMiejsc,
    bool CzyDostepnaDlaWozkow,
    IReadOnlyList<ZdjecieMiejsca> Zdjecia,
    IReadOnlyList<UdogodnienieSali> Udogodnienia,
    PunktNaLiscie? Powrot,
    int? ZwrotPowrotu);
