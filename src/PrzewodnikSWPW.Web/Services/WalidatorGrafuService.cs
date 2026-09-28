using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Services;

public enum PoziomUwagi
{
    /// <summary>Dane są błędne — przewodnik poprowadzi źle albo wcale.</summary>
    Blad,
    /// <summary>Podejrzane — redaktor decyduje (np. strona względem nazwanego obiektu jest poprawna).</summary>
    Ostrzezenie,
}

public enum RodzajRekordu { Budynek, PunktRuchu, Kierunek, Sala }

/// <summary>Jedna uwaga walidatora z podpowiedzią, jak ją naprawić, i wskazaniem rekordu do edycji.</summary>
public sealed record UwagaWalidatora(
    PoziomUwagi Poziom,
    string Regula,
    string Tresc,
    string Podpowiedz,
    RodzajRekordu Rekord,
    int RekordId,
    string OpisRekordu);

/// <summary>
/// Walidator grafu nawigacji (UC-19, WF-25). Reguły są czystymi funkcjami na danych w pamięci —
/// testowalne bez bazy. Zakres: reguły UC-19 (odpowiedniki zapytań kontrolnych z rozdz. 8
/// <c>docs/sql/01_schemat.sql</c>), słowa stronne w opisach punktów (D-06), spójność kierunków
/// powrotnych (D-07), wejście główne i osiągalność punktów z niego (D-08).
/// </summary>
public sealed partial class WalidatorGrafuService(IAdministracjaRepozytorium repo)
{
    /// <summary>Różnica wag pary tam–z powrotem, powyżej której zgłaszamy ostrzeżenie (D-07 pkt 3).</summary>
    public const decimal ProgRoznicyWag = 0.5m;

    /// <summary>Odległość, powyżej której krawędź to prawdopodobna pomyłka (UC-19). Twardy limit bazy to 200 m.</summary>
    public const decimal MaksWagaBezOstrzezenia = 100m;

    /// <summary>Tekst alternatywny krótszy niż tyle znaków jest podejrzany (P-02, zapytanie 8.4). Baza wymusza tylko 5.</summary>
    public const int MinDlugoscTekstuAlternatywnego = 15;

    /// <summary>Opis głosowy krótszy niż tyle znaków traktujemy jak brak opisu (zapytanie 8.5).</summary>
    public const int MinDlugoscOpisuGlosowego = 10;

    public const string RegulaSlepyZaulek = "UC-19 ślepy zaułek";
    public const string RegulaBrakPowrotu = "UC-19 brak pary powrotnej";
    public const string RegulaWaga = "UC-19 odległość krawędzi";
    public const string RegulaKonfliktAzymutu = "UC-19 konflikt azymutu";
    public const string RegulaSalaBezWejscia = "UC-19 sala bez wejścia";
    public const string RegulaTekstAlternatywny = "UC-19 tekst alternatywny";
    public const string RegulaOpisGlosowy = "UC-19 opis głosowy";

    public const string RegulaSlowaStronne = "D-06 słowa stronne";
    public const string RegulaPowrotWzajemny = "D-07 powrót wzajemny";
    public const string RegulaPowrotKonce = "D-07 końce powrotu";
    public const string RegulaPowrotAzymut = "D-07 azymut powrotu";
    public const string RegulaPowrotWaga = "D-07 waga powrotu";
    public const string RegulaWejscieBrak = "D-08 brak wejścia głównego";
    public const string RegulaWejscieInnyBudynek = "D-08 wejście w innym budynku";
    public const string RegulaNieosiagalny = "D-08 punkt nieosiągalny";

    /// <summary>
    /// Słowa stronne z D-06. Dodatkowo „po twojej lewej/prawej” — wariant, który występował
    /// w danych początkowych i jest najjaskrawszym przypadkiem odniesienia do użytkownika.
    /// </summary>
    [GeneratedRegex(@"\b(po (twojej )?lewej|po (twojej )?prawej|z lewej|z prawej|na lewo|na prawo)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    public static partial Regex SlowaStronne();

    public async Task<IReadOnlyList<UwagaWalidatora>> WalidujAsync(CancellationToken ct = default)
    {
        var budynki = await repo.Zapytanie<Budynek>().AsNoTracking().ToListAsync(ct);
        var punkty = await repo.Zapytanie<PunktRuchu>().AsNoTracking().Include(p => p.Pietro).ToListAsync(ct);
        var kierunki = await repo.Zapytanie<Kierunek>().AsNoTracking().ToListAsync(ct);
        var sale = await repo.Zapytanie<Sala>().AsNoTracking().ToListAsync(ct);
        var zdjecia = await repo.Zapytanie<Zdjecie>().AsNoTracking().ToListAsync(ct);
        return Waliduj(budynki, punkty, kierunki, sale, zdjecia);
    }

    /// <summary>Wszystkie reguły; błędy przed ostrzeżeniami.</summary>
    public static IReadOnlyList<UwagaWalidatora> Waliduj(IReadOnlyList<Budynek> budynki, IReadOnlyList<PunktRuchu> punkty,
        IReadOnlyList<Kierunek> kierunki, IReadOnlyList<Sala>? sale = null, IReadOnlyList<Zdjecie>? zdjecia = null) =>
        SprawdzSlepeZaulki(punkty, kierunki)
            .Concat(SprawdzParyPowrotne(punkty, kierunki))
            .Concat(SprawdzWagi(punkty, kierunki))
            .Concat(SprawdzKonfliktyAzymutow(punkty, kierunki))
            .Concat(SprawdzWejsciaSal(sale ?? []))
            .Concat(SprawdzTekstyAlternatywne(zdjecia ?? [], punkty, sale ?? []))
            .Concat(SprawdzOpisyGlosowe(punkty))
            .Concat(SprawdzSlowaStronne(punkty))
            .Concat(SprawdzPowroty(punkty, kierunki))
            .Concat(SprawdzWejsciaIOsiagalnosc(budynki, punkty, kierunki))
            .OrderBy(u => u.Poziom)
            .ThenBy(u => u.Regula)
            .ThenBy(u => u.OpisRekordu)
            .ToList();

    // --- UC-19 -------------------------------------------------------------------------------------------

    /// <summary>Reguła 1 (zapytanie 8.1): aktywny punkt bez żadnej aktywnej krawędzi wychodzącej.</summary>
    public static IEnumerable<UwagaWalidatora> SprawdzSlepeZaulki(IEnumerable<PunktRuchu> punkty, IReadOnlyList<Kierunek> kierunki)
    {
        var zWyjsciem = kierunki.Where(k => k.CzyAktywny).Select(k => k.PunktZrodlowyId).ToHashSet();
        foreach (var p in punkty.Where(p => p.CzyAktywny && !zWyjsciem.Contains(p.Id)))
        {
            yield return new UwagaWalidatora(PoziomUwagi.Blad, RegulaSlepyZaulek,
                "Z tego punktu nie prowadzi żaden aktywny kierunek — użytkownik wejdzie tu i nie będzie mógł wyjść.",
                "Dodaj kierunek wychodzący (zwykle powrót do punktu, z którego się tu przychodzi) albo dezaktywuj punkt.",
                RodzajRekordu.PunktRuchu, p.Id, $"punkt {p.Kod}");
        }
    }

    /// <summary>
    /// Reguła 2 (zapytanie 8.2): aktywna krawędź A→B, do której nie istnieje aktywna krawędź B→A.
    /// Krawędzie do sal nie mają pary — sala to cel, nie punkt ruchu.
    /// </summary>
    public static IEnumerable<UwagaWalidatora> SprawdzParyPowrotne(IEnumerable<PunktRuchu> punkty, IReadOnlyList<Kierunek> kierunki)
    {
        var kody = KodyPunktow(punkty);
        var aktywne = kierunki.Where(k => k.CzyAktywny && k.PunktDocelowyId is not null).ToList();
        var pary = aktywne.Select(k => (k.PunktZrodlowyId, k.PunktDocelowyId!.Value)).ToHashSet();
        foreach (var k in aktywne.Where(k => !pary.Contains((k.PunktDocelowyId!.Value, k.PunktZrodlowyId))))
        {
            var cel = kody.GetValueOrDefault(k.PunktDocelowyId!.Value, $"#{k.PunktDocelowyId}");
            yield return new UwagaWalidatora(PoziomUwagi.Blad, RegulaBrakPowrotu,
                $"Z punktu {cel} nie prowadzi aktywny kierunek z powrotem.",
                $"Dodaj w punkcie {cel} kierunek o azymucie {Azymuty.Normalizuj(k.Azymut + 180)}° do punktu " +
                $"{kody.GetValueOrDefault(k.PunktZrodlowyId)} albo aktywuj istniejący. Bez niego użytkownik przejdzie tam, ale nie wróci.",
                RodzajRekordu.Kierunek, k.Id, OpisKierunku(k, kody));
        }
    }

    /// <summary>Reguła 3: waga ≤ 0 (błąd — Dijkstra wymaga wag dodatnich) albo &gt; 100 m (prawdopodobna pomyłka).</summary>
    public static IEnumerable<UwagaWalidatora> SprawdzWagi(IEnumerable<PunktRuchu> punkty, IReadOnlyList<Kierunek> kierunki)
    {
        var kody = KodyPunktow(punkty);
        foreach (var k in kierunki.Where(k => k.CzyAktywny))
        {
            if (k.Waga <= 0)
            {
                yield return new UwagaWalidatora(PoziomUwagi.Blad, RegulaWaga,
                    $"Odległość wynosi {k.Waga} m — musi być większa od zera.",
                    "Wpisz odległość w metrach zmierzoną na miejscu. Zerowa lub ujemna odległość psuje wyznaczanie najkrótszej trasy.",
                    RodzajRekordu.Kierunek, k.Id, OpisKierunku(k, kody));
            }
            else if (k.Waga > MaksWagaBezOstrzezenia)
            {
                yield return new UwagaWalidatora(PoziomUwagi.Ostrzezenie, RegulaWaga,
                    $"Odległość {NawigacjaService.Metry(k.Waga)} przekracza {MaksWagaBezOstrzezenia:0} metrów.",
                    "Tak długie przejście między sąsiednimi punktami to zwykle pomyłka (np. centymetry zamiast metrów). " +
                    "Sprawdź pomiar albo wstaw punkty pośrednie na długim korytarzu.",
                    RodzajRekordu.Kierunek, k.Id, OpisKierunku(k, kody));
            }
        }
    }

    /// <summary>
    /// Reguła 4: dwie krawędzie z jednego punktu o tym samym azymucie — przewodnik nie wie, dokąd prowadzi „prosto”.
    /// Baza ma indeks unikalny (PunktZrodlowyId, Azymut); reguła łapie dane spoza niego (np. import).
    /// </summary>
    public static IEnumerable<UwagaWalidatora> SprawdzKonfliktyAzymutow(IEnumerable<PunktRuchu> punkty, IReadOnlyList<Kierunek> kierunki)
    {
        var kody = KodyPunktow(punkty);
        foreach (var grupa in kierunki.GroupBy(k => (k.PunktZrodlowyId, k.Azymut)).Where(g => g.Count() > 1))
        {
            foreach (var k in grupa.OrderBy(k => k.Id).Skip(1))
            {
                yield return new UwagaWalidatora(PoziomUwagi.Blad, RegulaKonfliktAzymutu,
                    $"Z punktu {kody.GetValueOrDefault(k.PunktZrodlowyId)} prowadzą {grupa.Count()} kierunki o azymucie {k.Azymut}°.",
                    "W jedną stronę z punktu może prowadzić tylko jeden kierunek. Popraw azymut tej krawędzi albo ją dezaktywuj.",
                    RodzajRekordu.Kierunek, k.Id, OpisKierunku(k, kody));
            }
        }
    }

    /// <summary>Reguła 5 (zapytanie 8.3): aktywna sala bez punktu wejściowego — nie da się do niej wyznaczyć trasy.</summary>
    public static IEnumerable<UwagaWalidatora> SprawdzWejsciaSal(IEnumerable<Sala> sale)
    {
        foreach (var s in sale.Where(s => s.CzyAktywna && s.PunktWejsciowyId is null))
        {
            yield return new UwagaWalidatora(PoziomUwagi.Blad, RegulaSalaBezWejscia,
                "Sala nie ma przypisanego punktu wejściowego.",
                "Wybierz w edycji sali punkt ruchu na korytarzu przed jej drzwiami. Bez niego wyszukiwarka tras nie doprowadzi do tej sali.",
                RodzajRekordu.Sala, s.Id, $"sala {s.Symbol}");
        }
    }

    /// <summary>
    /// Reguła 7 (zapytanie 8.4, P-02): zdjęcie informacyjne z tekstem alternatywnym krótszym niż 15 znaków.
    /// Zdjęcia nie mają osobnego formularza — link prowadzi do edycji właściciela (punktu albo sali).
    /// </summary>
    public static IEnumerable<UwagaWalidatora> SprawdzTekstyAlternatywne(IEnumerable<Zdjecie> zdjecia, IEnumerable<PunktRuchu> punkty, IEnumerable<Sala> sale)
    {
        var kody = KodyPunktow(punkty);
        var symbole = sale.ToDictionary(s => s.Id, s => s.Symbol);
        foreach (var z in zdjecia.Where(z => !z.CzyDekoracyjne && (z.TekstAlternatywny ?? "").Trim().Length < MinDlugoscTekstuAlternatywnego))
        {
            var (rekord, id, wlasciciel) = z.PunktRuchuId is int punkt
                ? (RodzajRekordu.PunktRuchu, punkt, $"punktu {kody.GetValueOrDefault(punkt, $"#{punkt}")}")
                : (RodzajRekordu.Sala, z.SalaId ?? 0, $"sali {symbole.GetValueOrDefault(z.SalaId ?? 0, $"#{z.SalaId}")}");
            var tekst = (z.TekstAlternatywny ?? "").Trim();
            yield return new UwagaWalidatora(PoziomUwagi.Ostrzezenie, RegulaTekstAlternatywny,
                tekst.Length == 0
                    ? "Zdjęcie informacyjne nie ma tekstu alternatywnego."
                    : $"Tekst alternatywny „{tekst}” ma {tekst.Length} znaków — za mało, by opisać, co widać na zdjęciu.",
                $"Opisz treść zdjęcia w co najmniej {MinDlugoscTekstuAlternatywnego} znakach (co na nim widać i po co je pokazujemy). " +
                "Jeśli zdjęcie niczego nie wnosi, oznacz je jako dekoracyjne.",
                rekord, id, $"zdjęcie {Path.GetFileName(z.SciezkaPliku)} {wlasciciel}");
        }
    }

    /// <summary>Reguła 8 (zapytanie 8.5): aktywny punkt bez opisu głosowego (pusty albo krótszy niż 10 znaków).</summary>
    public static IEnumerable<UwagaWalidatora> SprawdzOpisyGlosowe(IEnumerable<PunktRuchu> punkty)
    {
        foreach (var p in punkty.Where(p => p.CzyAktywny && (p.OpisGlosowy ?? "").Trim().Length < MinDlugoscOpisuGlosowego))
        {
            yield return new UwagaWalidatora(PoziomUwagi.Ostrzezenie, RegulaOpisGlosowy,
                string.IsNullOrWhiteSpace(p.OpisGlosowy)
                    ? "Punkt nie ma opisu głosowego."
                    : $"Opis głosowy „{p.OpisGlosowy.Trim()}” jest za krótki, by powiedzieć, gdzie jestem.",
                "Napisz opis do słuchania: gdzie jestem i co jest wokół, liczby słownie, bez skrótów (06 §2.3). " +
                "Czytanie na głos korzysta z opisu głosowego, nie z opisu do czytania.",
                RodzajRekordu.PunktRuchu, p.Id, $"punkt {p.Kod}");
        }
    }

    // --- D-06 --------------------------------------------------------------------------------------------

    public static IEnumerable<UwagaWalidatora> SprawdzSlowaStronne(IEnumerable<PunktRuchu> punkty)
    {
        foreach (var p in punkty)
        {
            foreach (var (pole, tekst) in new[] { ("Opis", p.Opis), ("Opis głosowy", p.OpisGlosowy) })
            {
                if (tekst is null || SlowaStronne().Match(tekst) is not { Success: true } trafienie) continue;

                yield return new UwagaWalidatora(PoziomUwagi.Ostrzezenie, RegulaSlowaStronne,
                    $"{pole} zawiera „{trafienie.Value}”: „{Skrot(tekst)}”.",
                    "Do punktu wchodzi się z kilku stron, więc strona względem idącego jest fałszem przy części dojść. " +
                    "Wymień punkty orientacyjne bez stron („Są tu drzwi do sali A14 i do sali A15”) albo nazwij obiekt " +
                    "odniesienia („po prawej stronie drzwi windy” — takie zdanie jest poprawne i może zostać).",
                    RodzajRekordu.PunktRuchu, p.Id, $"punkt {p.Kod}");
            }
        }
    }

    // --- D-07 --------------------------------------------------------------------------------------------

    public static IEnumerable<UwagaWalidatora> SprawdzPowroty(IEnumerable<PunktRuchu> punkty, IReadOnlyList<Kierunek> kierunki)
    {
        var kody = KodyPunktow(punkty);
        var poId = kierunki.ToDictionary(k => k.Id);
        string Opis(Kierunek k) => OpisKierunku(k, kody);

        foreach (var k in kierunki.Where(k => k.KierunekPowrotnyId is not null))
        {
            if (!poId.TryGetValue(k.KierunekPowrotnyId!.Value, out var p)) continue; // klucz obcy gwarantuje istnienie

            if (p.KierunekPowrotnyId != k.Id)
            {
                yield return new UwagaWalidatora(PoziomUwagi.Blad, RegulaPowrotWzajemny,
                    $"Wskazuje jako powrót {Opis(p)}, ale tamten wskazuje {(p.KierunekPowrotnyId is null ? "brak powrotu" : "inny kierunek")}.",
                    "Powiązanie musi być wzajemne: A→B wskazuje B→A i odwrotnie. Popraw kierunek powrotny w obu krawędziach.",
                    RodzajRekordu.Kierunek, k.Id, Opis(k));
            }

            if (p.PunktZrodlowyId != k.PunktDocelowyId || p.PunktDocelowyId != k.PunktZrodlowyId)
            {
                yield return new UwagaWalidatora(PoziomUwagi.Blad, RegulaPowrotKonce,
                    $"Kierunek powrotny ({Opis(p)}) nie prowadzi z punktu docelowego z powrotem do punktu źródłowego.",
                    "Kierunek powrotny krawędzi A→B musi być krawędzią B→A. Wskaż właściwy kierunek albo usuń powiązanie.",
                    RodzajRekordu.Kierunek, k.Id, Opis(k));
            }

            if (p.Azymut != Azymuty.Normalizuj(k.Azymut + 180))
            {
                yield return new UwagaWalidatora(PoziomUwagi.Blad, RegulaPowrotAzymut,
                    $"Azymut powrotu to {p.Azymut}°, a powinien być {Azymuty.Normalizuj(k.Azymut + 180)}° (azymut + 180°).",
                    "Jeśli idzie się tam na wschód (90°), wraca się na zachód (270°). Błędny azymut da użytkownikowi złą instrukcję „w lewo / w prawo”.",
                    RodzajRekordu.Kierunek, k.Id, Opis(k));
            }

            // Wag nie porównujemy na równość — schody w górę mogą „kosztować” więcej niż w dół (D-07 pkt 3).
            // Każdą parę zgłaszamy raz: od krawędzi o mniejszym Id.
            if (k.Id < p.Id && Math.Min(k.Waga, p.Waga) > 0
                && Math.Max(k.Waga, p.Waga) / Math.Min(k.Waga, p.Waga) - 1 > ProgRoznicyWag)
            {
                yield return new UwagaWalidatora(PoziomUwagi.Ostrzezenie, RegulaPowrotWaga,
                    $"Odległość tam: {NawigacjaService.Metry(k.Waga)}, z powrotem: {NawigacjaService.Metry(p.Waga)} — różnica ponad {ProgRoznicyWag:P0}.",
                    "Różne wagi są dopuszczalne (np. schody w górę), ale tak duża różnica to częściej pomyłka przy wpisywaniu.",
                    RodzajRekordu.Kierunek, k.Id, Opis(k));
            }
        }
    }

    // --- D-08 --------------------------------------------------------------------------------------------

    public static IEnumerable<UwagaWalidatora> SprawdzWejsciaIOsiagalnosc(IEnumerable<Budynek> budynki, IReadOnlyList<PunktRuchu> punkty, IReadOnlyList<Kierunek> kierunki)
    {
        foreach (var b in budynki.Where(b => b.CzyAktywny))
        {
            var punktyBudynku = punkty.Where(p => p.Pietro.BudynekId == b.Id).ToDictionary(p => p.Id);

            if (b.PunktWejsciaGlownegoId is not int wejscie)
            {
                yield return new UwagaWalidatora(PoziomUwagi.Blad, RegulaWejscieBrak,
                    "Budynek nie ma ustawionego wejścia głównego.",
                    "Ustaw wejście główne w edycji budynku. Bez niego spacer nie ma punktu startowego, a walidator nie sprawdzi, czy każde miejsce jest osiągalne.",
                    RodzajRekordu.Budynek, b.Id, $"budynek {b.Kod}");
                continue;
            }

            if (!punktyBudynku.ContainsKey(wejscie))
            {
                yield return new UwagaWalidatora(PoziomUwagi.Blad, RegulaWejscieInnyBudynek,
                    "Wskazane wejście główne leży w innym budynku.",
                    "Wybierz jako wejście główne punkt ruchu tego budynku.",
                    RodzajRekordu.Budynek, b.Id, $"budynek {b.Kod}");
                continue;
            }

            // Przeszukiwanie wszerz od wejścia po aktywnych krawędziach do aktywnych punktów.
            var sasiedzi = kierunki
                .Where(k => k.CzyAktywny && k.PunktDocelowyId is int d && punktyBudynku.TryGetValue(d, out var cel) && cel.CzyAktywny)
                .ToLookup(k => k.PunktZrodlowyId, k => k.PunktDocelowyId!.Value);
            var odwiedzone = new HashSet<int> { wejscie };
            var kolejka = new Queue<int>([wejscie]);
            while (kolejka.TryDequeue(out var u))
            {
                foreach (var v in sasiedzi[u])
                {
                    if (odwiedzone.Add(v)) kolejka.Enqueue(v);
                }
            }

            foreach (var p in punktyBudynku.Values.Where(p => p.CzyAktywny && !odwiedzone.Contains(p.Id)))
            {
                yield return new UwagaWalidatora(PoziomUwagi.Blad, RegulaNieosiagalny,
                    "Z wejścia głównego nie da się dojść do tego punktu.",
                    "Punkt leży w odciętej części grafu. Dodaj brakujący kierunek (najczęściej brakuje kierunku powrotnego) albo dezaktywuj punkt.",
                    RodzajRekordu.PunktRuchu, p.Id, $"punkt {p.Kod}");
            }
        }
    }

    private static Dictionary<int, string> KodyPunktow(IEnumerable<PunktRuchu> punkty) => punkty.ToDictionary(p => p.Id, p => p.Kod);

    private static string OpisKierunku(Kierunek k, IReadOnlyDictionary<int, string> kody) =>
        $"kierunek {k.Azymut}° z punktu {kody.GetValueOrDefault(k.PunktZrodlowyId, $"#{k.PunktZrodlowyId}")}";

    private static string Skrot(string tekst) => tekst.Length <= 120 ? tekst : tekst[..117] + "…";
}
