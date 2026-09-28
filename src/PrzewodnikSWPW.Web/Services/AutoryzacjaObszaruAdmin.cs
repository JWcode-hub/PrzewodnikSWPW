using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace PrzewodnikSWPW.Web.Services;

/// <summary>
/// Autoryzacja na poziomie OBSZARU „Admin”: każdy kontroler z [Area("Admin")] dostaje filtr
/// wymagający roli Administrator — także kontroler dodany w przyszłości bez atrybutu [Authorize].
/// Atrybuty na kontrolerach i akcjach są drugą, niezależną warstwą (05 §8, P-19).
/// </summary>
public sealed class AutoryzacjaObszaruAdmin : IControllerModelConvention
{
    public const string Obszar = "Admin";
    public const string Rola = Role.Administrator;

    public void Apply(ControllerModel kontroler)
    {
        if (kontroler.RouteValues.TryGetValue("area", out var obszar) && obszar == Obszar)
        {
            kontroler.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireRole(Rola).Build()));
        }
    }
}

public static class Role
{
    public const string Administrator = "Administrator";
    public const string Redaktor = "Redaktor";
    public const string Superadministrator = "Superadministrator";

    public static readonly IReadOnlyList<string> Wszystkie = [Administrator, Redaktor, Superadministrator];
}
