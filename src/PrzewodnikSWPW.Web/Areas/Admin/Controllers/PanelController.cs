using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.Web.Areas.Admin.Controllers;

[Route("admin")]
public class PanelController(AdministracjaService admin) : AdminKontroler
{
    [Authorize(Roles = Role.Administrator)]
    [HttpGet("")]
    public IActionResult Index() => View();

    /// <summary>Rejestr zmian (WF-28) — ostatnie wpisy.</summary>
    [Authorize(Roles = Role.Administrator)]
    [HttpGet("rejestr-zmian")]
    public async Task<IActionResult> RejestrZmian(CancellationToken ct) => View(await admin.OstatnieWpisyAudytu(100, ct));
}
