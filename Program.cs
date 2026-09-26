using ExamenParcial.Configuration;
using ExamenParcial.Data;
using ExamenParcial.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<AlgoliaSettings>(builder.Configuration.GetSection(AlgoliaSettings.SectionName));
builder.Services.AddSingleton<IAlgoliaIncidenciasService, AlgoliaIncidenciasService>();

// Redis: la configuracion puede llegar como URL (redis:// / rediss://), como cadena
// nativa de StackExchange.Redis o como variables separadas por host, puerto y clave.
var redis = RedisConfiguracionResolver.Resolver(builder.Configuration);
Exception? errorRegistroRedis = null;

if (redis.Habilitado)
{
    try
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.ConfigurationOptions = redis.Opciones;
            options.InstanceName = "ExamenParcial:";
        });
    }
    catch (Exception ex)
    {
        // Si el registro falla, la app sigue con cache en memoria.
        errorRegistroRedis = ex;
    }
}

if (errorRegistroRedis is not null || !redis.Habilitado)
{
    // Respaldo: cache en memoria. Nunca se propagan errores de Redis al arrancar.
    builder.Services.RemoveAll<IDistributedCache>();
    builder.Services.AddDistributedMemoryCache();
}

var app = builder.Build();

if (errorRegistroRedis is not null)
{
    app.Logger.LogError(errorRegistroRedis, "No se pudo registrar el cache de Redis. Se usara cache en memoria y la base de datos local.");
}
else if (redis.Habilitado)
{
    app.Logger.LogInformation("Cache de Redis habilitado, configuracion obtenida de: {Origen}.", redis.Origen);
}
else
{
    app.Logger.LogWarning("Redis no disponible ({Diagnostico}). Se usara cache en memoria y la base de datos local.", redis.Diagnostico);
}

// Crea la base de datos SQLite y sus tablas si todavia no existen.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();
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
