using System.ComponentModel.DataAnnotations;

namespace PrzewodnikSWPW.Web.Models;

/// <summary>Rejestr zmian danych: kto, kiedy, którą encję i jak zmienił (WF-28).</summary>
public class WpisAudytu
{
    public long Id { get; set; }

    /// <summary>Identyfikator użytkownika ASP.NET Core Identity (AspNetUsers.Id).</summary>
    [StringLength(450, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Użytkownik")]
    public string? UzytkownikId { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(100, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Encja")]
    public string Encja { get; set; } = string.Empty;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(100, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Klucz encji")]
    public string KluczEncji { get; set; } = string.Empty;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(10, ErrorMessage = Komunikaty.MaksDlugosc)]
    [RegularExpression("^(INSERT|UPDATE|DELETE)$", ErrorMessage = "Operacja musi mieć wartość INSERT, UPDATE albo DELETE.")]
    [Display(Name = "Operacja")]
    public string Operacja { get; set; } = string.Empty;

    /// <summary>JSON.</summary>
    [Display(Name = "Wartości przed zmianą")]
    public string? WartosciStare { get; set; }

    /// <summary>JSON.</summary>
    [Display(Name = "Wartości po zmianie")]
    public string? WartosciNowe { get; set; }

    [Display(Name = "Data operacji")]
    public DateTime DataOperacji { get; set; }
}
