var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    // Strona błędu bez szczegółów technicznych — szczegóły wyłącznie do logu.
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Zdjęcia w wwwroot/media są wgrywane w czasie działania aplikacji, więc nie ma ich
// w manifeście MapStaticAssets — obsługuje je UseStaticFiles.
app.UseStaticFiles();

app.UseRouting();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
