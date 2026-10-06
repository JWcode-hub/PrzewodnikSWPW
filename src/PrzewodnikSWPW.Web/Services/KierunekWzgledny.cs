namespace PrzewodnikSWPW.Web.Services;

/// <summary>
/// Kierunek względem aktualnego zwrotu użytkownika. Wartość to przesunięcie w stopniach
/// zgodnie z ruchem wskazówek zegara. NIGDY nie jest zapisywany w bazie — wyliczany
/// z azymutu bezwzględnego przez <see cref="Azymuty"/> (docs/04_BAZA_DANYCH.md rozdz. 5).
/// </summary>
public enum KierunekWzgledny
{
    Prosto = 0,
    WPrawo = 90,
    DoTylu = 180,
    WLewo = 270
}
