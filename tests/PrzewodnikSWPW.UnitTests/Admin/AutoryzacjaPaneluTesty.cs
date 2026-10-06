using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using PrzewodnikSWPW.Web.Areas.Admin.Controllers;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.UnitTests.Admin;

/// <summary>
/// Strażnik reguł bezpieczeństwa (05 §8, P-19): nowy kontroler albo akcja bez atrybutu wywraca test,
/// zanim trafi na serwer.
/// </summary>
public class AutoryzacjaPaneluTesty
{
    private static readonly Assembly Aplikacja = typeof(AdminKontroler).Assembly;

    private static IEnumerable<Type> Kontrolery() =>
        Aplikacja.GetTypes().Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract);

    private static IEnumerable<MethodInfo> Akcje(Type kontroler) =>
        kontroler.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(Controller) && m.DeclaringType != typeof(ControllerBase)
                        && m.DeclaringType != typeof(object) && m.GetCustomAttribute<NonActionAttribute>() is null);

    public static TheoryData<string> KontroleryPanelu()
    {
        var dane = new TheoryData<string>();
        foreach (var k in Kontrolery().Where(t => typeof(AdminKontroler).IsAssignableFrom(t)))
        {
            dane.Add(k.Name);
        }
        return dane;
    }

    [Theory]
    [MemberData(nameof(KontroleryPanelu))]
    public void KazdaAkcjaPanelu_MaAuthorizeZRolaAdministrator(string nazwa)
    {
        var kontroler = Kontrolery().Single(t => t.Name == nazwa);
        var akcje = Akcje(kontroler).ToList();
        Assert.NotEmpty(akcje);

        Assert.Equal(AutoryzacjaObszaruAdmin.Obszar, kontroler.GetCustomAttribute<AreaAttribute>(inherit: true)?.RouteValue);
        Assert.All(akcje, a =>
        {
            var authorize = a.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToList();
            Assert.True(authorize.Any(x => x.Roles?.Split(',').Contains(Role.Administrator) == true),
                $"{kontroler.Name}.{a.Name} nie ma [Authorize(Roles = \"Administrator\")] na akcji.");
            Assert.Null(a.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true));
        });
    }

    [Fact]
    public void KazdaAkcjaPost_WCalejAplikacji_MaValidateAntiForgeryToken()
    {
        var bezTokenu = Kontrolery()
            .SelectMany(k => Akcje(k).Select(a => (k, a)))
            .Where(x => x.a.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any(h => h.HttpMethods.Contains("POST")))
            .Where(x => x.a.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>(inherit: true) is null)
            .Select(x => $"{x.k.Name}.{x.a.Name}")
            .ToList();

        Assert.Empty(bezTokenu);
    }
}
