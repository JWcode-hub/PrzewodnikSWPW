namespace PrzewodnikSWPW.Web.Models;

/// <summary>
/// Rodzaj przejścia krawędzią grafu. Tryb windy wyklucza krawędzie typu <see cref="Schody"/>.
/// </summary>
public enum RodzajPrzejscia : byte
{
    Korytarz = 1,
    Drzwi = 2,
    Schody = 3,
    Winda = 4,
    Podjazd = 5
}
