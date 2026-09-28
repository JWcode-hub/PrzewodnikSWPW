namespace PrzewodnikSWPW.Web.Models;

/// <summary>Tabela słownikowa (TypSali, TypPunktu, Udogodnienie) — nazwa unikalna, opis opcjonalny.</summary>
public interface ISlownik
{
    int Id { get; set; }
    string Nazwa { get; set; }
    string? Opis { get; set; }
}
