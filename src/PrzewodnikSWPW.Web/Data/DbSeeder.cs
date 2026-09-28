using Microsoft.EntityFrameworkCore;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Data;

/// <summary>
/// Dane początkowe zachodniej części parteru budynku A — odpowiednik
/// docs/sql/02_dane_poczatkowe.sql i docs/diagrams/graf-nawigacji.mmd.
/// Słowniki (TypSali, TypPunktu, Udogodnienie) pochodzą z HasData w migracji.
/// </summary>
public static class DbSeeder
{
    /// <summary>Kod punktu startowego — wejście główne budynku A.</summary>
    public const string KodWejsciaGlownego = "A-0-P01";

    private sealed record DaneKrawedzi(
        string Zrodlo, string? DoPunktu, string? DoSali, int Azymut, decimal Waga,
        RodzajPrzejscia Rodzaj, string Opis, bool CzyAktywny = true, bool BezSchodow = true);

    /// <summary>Zasila bazę, jeśli nie ma w niej jeszcze żadnego budynku. Zwraca <c>true</c>, gdy dane zostały wstawione.</summary>
    public static async Task<bool> ZasiejAsync(PrzewodnikDbContext db, CancellationToken ct = default)
    {
        if (await db.Budynki.AnyAsync(ct))
        {
            return false;
        }

        await using var transakcja = await db.Database.BeginTransactionAsync(ct);

        var typySal = await db.TypySal.ToDictionaryAsync(t => t.Nazwa, ct);
        var typyPunktow = await db.TypyPunktow.ToDictionaryAsync(t => t.Nazwa, ct);
        var udogodnienia = await db.Udogodnienia.ToDictionaryAsync(u => u.Nazwa, ct);

        var budynekA = new Budynek
        {
            Kod = "A",
            Nazwa = "Budynek A - Szkoła Wyższa im. Pawła Włodkowica",
            Adres = "ul. Al. Kilińskiego 12, 09-402 Płock",
            CzyMaWinde = true,
            OpisDostepnosciArchitektonicznej =
                "Wejście główne od strony ulicy, bez progu, drzwi dwuskrzydłowe otwierane ręcznie. " +
                "Korytarze o szerokości powyżej 150 cm. Winda dostępna z holu głównego, " +
                "z sygnalizacją głosową i oznaczeniami brajlowskimi. Toaleta dostosowana na parterze. " +
                "Do budynku można wejść z psem asystującym. Miejsca parkingowe dla osób " +
                "z niepełnosprawnością wyznaczone przed wejściem głównym.",
        };

        var parter = new Pietro
        {
            Budynek = budynekA,
            Numer = 0,
            Nazwa = "Parter",
            Opis = "Parter budynku A - część zachodnia i wschodnia",
        };

        PunktRuchu Punkt(string kod, string typ, string nazwa, string opis, string opisGlosowy,
                         int azymut, decimal x, decimal y) => new()
        {
            Pietro = parter,
            TypPunktu = typyPunktow[typ],
            Kod = kod,
            Nazwa = nazwa,
            Opis = opis,
            OpisGlosowy = opisGlosowy,
            AzymutDomyslny = azymut,
            X = x,
            Y = y,
        };

        var punkty = new[]
        {
            Punkt("A-0-P01", "Hol", "Wejście główne",
                "Wejście główne do budynku A od strony ulicy. Drzwi dwuskrzydłowe, bez progu.",
                "Jesteś przy wejściu głównym do budynku A. Drzwi są przed tobą, bez progu. Za drzwiami zaczyna się hol.",
                0, 0, 0),
            Punkt("A-0-P02", "Hol", "Hol główny przy portierni",
                "Duży hol. Po prawej stronie okienko portierni. Po lewej tablica informacyjna.",
                "Jesteś w holu głównym. Po prawej stronie, w odległości około trzech metrów, jest okienko portierni. Prosto przed tobą korytarz.",
                0, 0, 6),
            Punkt("A-0-P03", "Skrzyżowanie", "Skrzyżowanie korytarzy",
                "Skrzyżowanie korytarza zachodniego i wschodniego. Na podłodze ścieżka dotykowa.",
                "Jesteś na skrzyżowaniu korytarzy. W lewo prowadzi korytarz zachodni z salami komputerowymi. W prawo korytarz wschodni do sekretariatu. Prosto jest winda.",
                0, 0, 14),
            Punkt("A-0-P04", "Korytarz", "Korytarz zachodni przy sali A12",
                "Odcinek korytarza. Po prawej stronie drzwi do sali A12.",
                "Jesteś w korytarzu zachodnim. Drzwi do sali A dwanaście są po twojej prawej stronie.",
                270, -5, 14),
            Punkt("A-0-P05", "Korytarz", "Korytarz zachodni przy salach A14 i A15",
                "Odcinek korytarza. Po prawej drzwi do sali A14, po lewej do sali A15.",
                "Jesteś w korytarzu zachodnim. Po prawej stronie drzwi do sali A czternaście, po lewej do sali A piętnaście.",
                270, -11, 14),
            Punkt("A-0-P06", "Korytarz", "Koniec korytarza zachodniego",
                "Koniec korytarza. Prosto schody na piętro. Po lewej toaleta dostosowana.",
                "Jesteś na końcu korytarza zachodniego. Prosto przed tobą są schody na pierwsze piętro. Po lewej stronie toaleta dostosowana dla osób z niepełnosprawnością.",
                270, -17, 14),
            Punkt("A-0-P07", "Podest schodów", "Podest schodów zachodnich",
                "Spocznik przed biegiem schodów na piętro pierwsze. Poręcz po obu stronach.",
                "Jesteś na podeście schodów. Schody prowadzą w górę na pierwsze piętro. Poręcz jest po obu stronach. Osiemnaście stopni.",
                0, -17, 17),
            Punkt("A-0-P08", "Przed windą", "Przed windą",
                "Miejsce przed drzwiami windy. Przycisk przywołania po prawej, oznaczony brajlem.",
                "Jesteś przed windą. Przycisk przywołania jest po prawej stronie drzwi, na wysokości ręki, oznaczony pismem Braille'a. Winda zapowiada piętra głosem.",
                0, 0, 18),
            Punkt("A-0-P09", "Korytarz", "Korytarz wschodni przy sekretariacie",
                "Odcinek korytarza wschodniego. Po prawej drzwi sekretariatu A16.",
                "Jesteś w korytarzu wschodnim. Drzwi sekretariatu Wydziału Informatyki są po twojej prawej stronie.",
                90, 7, 14),
        };
        var punkt = punkty.ToDictionary(p => p.Kod[^3..]); // "P01" … "P09"

        Sala NowaSala(string symbol, string typ, string punktWejsciowy, string nazwa, string aliasy,
                      string opis, string opisGlosowy, int? liczbaMiejsc) => new()
        {
            Pietro = parter,
            TypSali = typySal[typ],
            PunktWejsciowy = punkt[punktWejsciowy],
            Symbol = symbol,
            Nazwa = nazwa,
            Aliasy = aliasy,
            Opis = opis,
            OpisGlosowy = opisGlosowy,
            LiczbaMiejsc = liczbaMiejsc,
            CzyDostepnaDlaWozkow = true,
        };

        var sale = new[]
        {
            NowaSala("A12", "Sala komputerowa", "P04", "Pracownia komputerowa", "12;sala 12;pracownia",
                "Sala z 16 stanowiskami komputerowymi.",
                "Sala A dwanaście, pracownia komputerowa. Szesnaście stanowisk. Wejście bez progu.", 16),
            NowaSala("A14", "Sala ćwiczeniowa", "P05", "Sala ćwiczeniowa", "14;sala 14",
                "Sala ćwiczeniowa na 30 osób.",
                "Sala A czternaście, sala ćwiczeniowa na trzydzieści osób.", 30),
            NowaSala("A15", "Sala komputerowa", "P05", "Pracownia sieci komputerowych", "15;sala 15;sieci",
                "Pracownia sieciowa z 12 stanowiskami i szafą rack.",
                "Sala A piętnaście, pracownia sieci komputerowych. Dwanaście stanowisk.", 12),
            NowaSala("A16", "Sekretariat", "P09", "Sekretariat Wydziału Informatyki", "dziekanat;sekretariat;16",
                "Sekretariat czynny od poniedziałku do piątku w godzinach 8:00-15:00.",
                "Sekretariat Wydziału Informatyki, sala A szesnaście. Czynny od poniedziałku do piątku, od ósmej do piętnastej.", null),
            NowaSala("WC-0-Z", "Toaleta", "P06", "Toaleta dostosowana", "wc;toaleta;łazienka",
                "Toaleta dla osób z niepełnosprawnością, z poręczami i przyciskiem alarmowym.",
                "Toaleta dostosowana. Drzwi otwierają się na zewnątrz. W środku poręcze po obu stronach i przycisk alarmowy przy umywalce.", null),
        };
        var sala = sale.ToDictionary(s => s.Symbol);

        // Azymuty dla budynku A: 0 = w głąb budynku (północ planu), 90 = w prawo od wejścia (wschód),
        // 180 = w stronę wyjścia (południe), 270 = w lewo od wejścia (zachód).
        var krawedzie = new DaneKrawedzi[]
        {
            new("P01", "P02", null,   0, 6.0m, RodzajPrzejscia.Korytarz, "Przejdź przez drzwi wejściowe prosto do holu, około sześć metrów."),
            new("P02", "P01", null, 180, 6.0m, RodzajPrzejscia.Korytarz, "Zawróć i wyjdź drzwiami na zewnątrz, około sześć metrów."),
            new("P02", "P03", null,   0, 8.0m, RodzajPrzejscia.Korytarz, "Idź prosto przez hol, około osiem metrów, do skrzyżowania korytarzy."),
            new("P03", "P02", null, 180, 8.0m, RodzajPrzejscia.Korytarz, "Idź prosto około osiem metrów, wrócisz do holu głównego."),
            new("P03", "P04", null, 270, 5.0m, RodzajPrzejscia.Korytarz, "Skręć w korytarz zachodni, około pięć metrów."),
            new("P04", "P03", null,  90, 5.0m, RodzajPrzejscia.Korytarz, "Wróć korytarzem do skrzyżowania, około pięć metrów."),
            new("P03", "P09", null,  90, 7.0m, RodzajPrzejscia.Korytarz, "Skręć w korytarz wschodni, około siedem metrów."),
            new("P09", "P03", null, 270, 7.0m, RodzajPrzejscia.Korytarz, "Wróć korytarzem do skrzyżowania, około siedem metrów."),
            new("P03", "P08", null,   0, 4.0m, RodzajPrzejscia.Korytarz, "Idź prosto około cztery metry, dojdziesz do windy."),
            new("P08", "P03", null, 180, 4.0m, RodzajPrzejscia.Korytarz, "Odejdź od windy około cztery metry, wrócisz na skrzyżowanie."),
            new("P04", "P05", null, 270, 6.0m, RodzajPrzejscia.Korytarz, "Idź dalej korytarzem, około sześć metrów."),
            new("P05", "P04", null,  90, 6.0m, RodzajPrzejscia.Korytarz, "Wróć korytarzem, około sześć metrów."),
            new("P05", "P06", null, 270, 6.0m, RodzajPrzejscia.Korytarz, "Idź dalej korytarzem do jego końca, około sześć metrów."),
            new("P06", "P05", null,  90, 6.0m, RodzajPrzejscia.Korytarz, "Wróć korytarzem, około sześć metrów."),
            new("P06", "P07", null,   0, 3.0m, RodzajPrzejscia.Schody,   "Podejdź do podestu schodów, około trzy metry. Dalej są schody w górę.", BezSchodow: false),
            new("P07", "P06", null, 180, 3.0m, RodzajPrzejscia.Schody,   "Wróć od schodów do korytarza, około trzy metry.", BezSchodow: false),
            new("P04", null, "A12",    0, 1.0m, RodzajPrzejscia.Drzwi,   "Drzwi do sali A12 po prawej stronie, jeden metr."),
            new("P05", null, "A14",    0, 1.0m, RodzajPrzejscia.Drzwi,   "Drzwi do sali A14 po prawej stronie, jeden metr."),
            new("P05", null, "A15",  180, 1.0m, RodzajPrzejscia.Drzwi,   "Drzwi do sali A15 po lewej stronie, jeden metr."),
            new("P09", null, "A16",    0, 1.0m, RodzajPrzejscia.Drzwi,   "Drzwi sekretariatu A16, jeden metr."),
            new("P06", null, "WC-0-Z", 180, 2.0m, RodzajPrzejscia.Drzwi, "Drzwi toalety dostosowanej, dwa metry."),

            // Kierunek NIEAKTYWNY — brak przejścia, ale z opisem orientacyjnym (WF-04, US-02).
            // Cel (A12) jest wymagany wyłącznie przez CK_Kierunek_JedenCel.
            new("P04", null, "A12",  180, 0.5m, RodzajPrzejscia.Drzwi,
                "Po lewej stronie nie ma przejścia. Jest tam ściana z gablotą informacyjną.", CzyAktywny: false),
        };

        var kierunki = krawedzie.Select(k => new Kierunek
        {
            PunktZrodlowy = punkt[k.Zrodlo],
            PunktDocelowy = k.DoPunktu is null ? null : punkt[k.DoPunktu],
            SalaDocelowa = k.DoSali is null ? null : sala[k.DoSali],
            Azymut = k.Azymut,
            Waga = k.Waga,
            RodzajPrzejscia = k.Rodzaj,
            OpisPrzejscia = k.Opis,
            CzyAktywny = k.CzyAktywny,
            CzyDostepnyBezSchodow = k.BezSchodow,
        }).ToList();

        void DodajUdogodnienie(PunktRuchu p, string nazwa, string uwagi) =>
            db.PunktUdogodnienia.Add(new PunktUdogodnienie { PunktRuchu = p, Udogodnienie = udogodnienia[nazwa], Uwagi = uwagi });

        DodajUdogodnienie(punkt["P08"], "Winda", "Winda z zapowiedzią głosową pięter");
        DodajUdogodnienie(punkt["P08"], "Oznaczenia brajlowskie", "Przyciski oznaczone brajlem");
        DodajUdogodnienie(punkt["P03"], "Ścieżka dotykowa", "Ścieżka dotykowa prowadzi od wejścia do windy");
        DodajUdogodnienie(punkt["P07"], "Oznaczenie kontrastowe", "Kontrastowe oznaczenie pierwszego i ostatniego stopnia");
        db.SalaUdogodnienia.Add(new SalaUdogodnienie
        {
            Sala = sala["A16"],
            Udogodnienie = udogodnienia["Pętla indukcyjna"],
            Uwagi = "Pętla indukcyjna przy okienku",
        });

        db.Budynki.Add(budynekA);
        db.PunktyRuchu.AddRange(punkty);
        db.Sale.AddRange(sale);
        db.Kierunki.AddRange(kierunki);
        await db.SaveChangesAsync(ct);

        // Kierunek powrotny: dla A->B wskazujemy krawędź B->A (tylko krawędzie między punktami ruchu).
        foreach (var k in kierunki.Where(k => k.PunktDocelowy is not null))
        {
            k.KierunekPowrotny = kierunki.SingleOrDefault(p =>
                p.PunktZrodlowy == k.PunktDocelowy && p.PunktDocelowy == k.PunktZrodlowy);
        }
        await db.SaveChangesAsync(ct);

        await transakcja.CommitAsync(ct);
        return true;
    }
}
