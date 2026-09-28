namespace PrzewodnikSWPW.Web.Models;

/// <summary>
/// Wspólne komunikaty walidacji. {0} to nazwa pola z atrybutu Display.
/// </summary>
public static class Komunikaty
{
    public const string Wymagane = "Pole „{0}” jest wymagane.";
    public const string MaksDlugosc = "Pole „{0}” może mieć najwyżej {1} znaków.";
    public const string Zakres = "Pole „{0}” musi mieć wartość od {1} do {2}.";
}
