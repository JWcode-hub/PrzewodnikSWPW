namespace PrzewodnikSWPW.Web.Services;

/// <summary>Polska odmiana rzeczownika po liczebniku: 1 sala, 2–4 sale, 5 sal, 12 sal, 22 sale.</summary>
public static class Odmiana
{
    public static string PoLiczbie(long n, string jeden, string kilka, string wiele)
    {
        var bezwzgledna = Math.Abs(n);
        return bezwzgledna == 1 ? jeden
            : bezwzgledna % 10 is >= 2 and <= 4 && bezwzgledna % 100 is < 12 or > 14 ? kilka
            : wiele;
    }

    /// <summary>Mianownik: „1 sala”, „2 sale”, „5 sal”.</summary>
    public static string Sale(int n) => $"{n} {PoLiczbie(n, "sala", "sale", "sal")}";

    /// <summary>Biernik, np. po „Znaleziono”: „1 salę”, „2 sale”, „5 sal”.</summary>
    public static string SaleBiernik(int n) => $"{n} {PoLiczbie(n, "salę", "sale", "sal")}";
}
