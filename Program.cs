using ExamenParcial.Configuration;
using ExamenParcial.Data;
using ExamenParcial.Models;
using ExamenParcial.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<AlgoliaSettings>(builder.Configuration.GetSection(AlgoliaSettings.SectionName));
builder.Services.AddSingleton<IAlgoliaIncidenciasService, AlgoliaIncidenciasService>();

var app = builder.Build();

// Crea la base de datos SQLite y sus tablas si todavia no existen.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();

    if (!context.Incidencias.Any())
    {
        context.Incidencias.AddRange(
            new Incidencia { Estacion = "Estacion Central", Descripcion = "Torniquetes sin lectura de tarjeta", Prioridad = "Alta" },
            new Incidencia { Estacion = "Estacion Norte", Descripcion = "Falla en la iluminacion del anden", Prioridad = "Media" },
            new Incidencia { Estacion = "Estacion Sur", Descripcion = "Basura acumulada en la salida", Prioridad = "Baja" },
            new Incidencia { Estacion = "Estacion Central", Descripcion = "Panel de horario desactualizado", Prioridad = "Media" });
        context.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
