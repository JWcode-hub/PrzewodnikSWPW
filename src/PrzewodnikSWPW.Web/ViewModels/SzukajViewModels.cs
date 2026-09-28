using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.Web.ViewModels;

/// <summary>Strona wyszukiwania. <see cref="Wynik"/> = <c>null</c> — formularz bez wysłanego zapytania.</summary>
public sealed record SzukajViewModel(string? Fraza, WynikWyszukiwania? Wynik);

public sealed record SaleBudynkuViewModel(BudynekNaLiscie Budynek, IReadOnlyList<SalaWyszukana> Sale);

/// <summary>Element podpowiedzi zwracany do combobox (JSON).</summary>
public sealed record PodpowiedzDto(int Id, string Tekst, string Adres);
