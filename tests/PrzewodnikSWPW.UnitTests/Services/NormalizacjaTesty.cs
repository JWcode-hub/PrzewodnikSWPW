using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.UnitTests.Services;

/// <summary>Normalizacja frazy wyszukiwania (WF-11, ryzyko P-16).</summary>
public class NormalizacjaTesty
{
    [Theory]
    [InlineData("A15", "a15")]
    [InlineData("a15", "a15")]
    [InlineData("a 15", "a15")]
    [InlineData("A-15", "a15")]
    [InlineData("A.15", "a15")]
    [InlineData("  A – 15 ", "a15")]                       // półpauza i spacje wokół
    [InlineData("A—15", "a15")]                            // pauza
    [InlineData("Sekretariat Wydziału", "sekretariatwydzialu")]
    [InlineData("ŁAZIENKA", "lazienka")]                   // „Ł” nie rozkłada się w FormD
    [InlineData("Ćwiczeniowa", "cwiczeniowa")]
    [InlineData("zażółć gęślą jaźń", "zazolcgeslajazn")]  // wszystkie polskie znaki
    [InlineData("Pracownia\tsieci\nkomputerowych", "pracowniasiecikomputerowych")]
    [InlineData("WC-0-Z", "wc0z")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    [InlineData("A12;sala 12", "a12;sala12")]              // średnik zostaje — rozdziela aliasy przed normalizacją
    public void Normalizuj_SprowadzaWariantyDoJednejPostaci(string? wejscie, string oczekiwane)
    {
        Assert.Equal(oczekiwane, WyszukiwarkaSalService.Normalizuj(wejscie));
    }

    [Theory]
    [InlineData("sala 15", "15")]
    [InlineData("Sala A15", "a15")]
    [InlineData("sali 12", "12")]
    [InlineData("pokój 14", "14")]
    [InlineData("pok. 14", "14")]
    [InlineData("nr 16", "16")]
    [InlineData("sala nr 15", "15")]
    [InlineData("sala", "sala")]                            // samo słowo ogólne zostaje — wtedy go szukamy
    [InlineData("dziekanat", "dziekanat")]
    [InlineData("sala ćwiczeniowa", "cwiczeniowa")]
    public void NormalizujFraze_PomijaSlowaOgolne(string wejscie, string oczekiwane)
    {
        Assert.Equal(oczekiwane, WyszukiwarkaSalService.NormalizujFraze(wejscie));
    }

    [Theory]
    [InlineData("a15", "a15", 0)]
    [InlineData("a51", "a15", 1)]         // przestawienie sąsiednich znaków
    [InlineData("a16", "a15", 1)]         // zamiana
    [InlineData("a1", "a15", 1)]          // brak znaku
    [InlineData("dziekant", "dziekanat", 1)]
    [InlineData("a99", "a15", 2)]
    [InlineData("", "abc", 3)]
    public void Odleglosc_OSA(string a, string b, int oczekiwana)
    {
        Assert.Equal(oczekiwana, WyszukiwarkaSalService.Odleglosc(a, b));
    }

    [Theory]
    [InlineData(1, "1 sala")]
    [InlineData(2, "2 sale")]
    [InlineData(4, "4 sale")]
    [InlineData(5, "5 sal")]
    [InlineData(12, "12 sal")]
    [InlineData(14, "14 sal")]
    [InlineData(22, "22 sale")]
    [InlineData(0, "0 sal")]
    public void Odmiana_Sale(int n, string oczekiwane)
    {
        Assert.Equal(oczekiwane, Odmiana.Sale(n));
    }

    [Theory]
    [InlineData(1, "1 salę")]
    [InlineData(3, "3 sale")]
    [InlineData(5, "5 sal")]
    public void Odmiana_SaleBiernik_PoZnaleziono(int n, string oczekiwane)
    {
        Assert.Equal(oczekiwane, Odmiana.SaleBiernik(n));
    }
}
