using System.Text.Json;
using ExamenParcial.Data;
using ExamenParcial.Models;
using ExamenParcial.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace ExamenParcial.Controllers;

public class OperacionesController : Controller
{
    private const string ClaveListadoAbiertas = "Operaciones:Incidencias:Abiertas";

    private static readonly TimeSpan DuracionCache = TimeSpan.FromSeconds(60);

    private readonly ApplicationDbContext _context;
    private readonly IAlgoliaIncidenciasService _algolia;
    private readonly IDistributedCache _cache;
    private readonly ILogger<OperacionesController> _logger;

    public OperacionesController(
        ApplicationDbContext context,
        IAlgoliaIncidenciasService algolia,
        IDistributedCache cache,
        ILogger<OperacionesController> logger)
    {
        _context = context;
        _algolia = algolia;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IActionResult> Incidencias(string? q)
    {
        var termino = q?.Trim() ?? string.Empty;
        ViewData["Termino"] = termino;

        if (string.IsNullOrEmpty(termino))
        {
            // Sin busqueda: listado habitual completo (Redis con respaldo en la base de datos).
            var listado = await ObtenerListadoAbiertasAsync();

            return View(listado);
        }

        // Con busqueda: Algolia devuelve los objectID que coinciden.
        var ids = await _algolia.BuscarIdsAsync(termino, HttpContext.RequestAborted);

        // De esos ids solo se muestran los que siguen Abiertas en la base de datos local.
        var coincidencias = await _context.Incidencias
            .Where(i => i.Estado == "Abierta" && ids.Contains(i.Id))
            .ToListAsync();

        // Se respeta el orden de relevancia devuelto por Algolia.
        var posicion = ids
            .Select((id, i) => new { Id = id, Posicion = i })
            .ToDictionary(x => x.Id, x => x.Posicion);

        var ordenadas = coincidencias
            .OrderBy(i => posicion.TryGetValue(i.Id, out var p) ? p : int.MaxValue)
            .ToList();

        return View(ordenadas);
    }

    private async Task<List<Incidencia>> ObtenerListadoAbiertasAsync()
    {
        var contenidoCache = await _cache.GetStringAsync(ClaveListadoAbiertas);

        if (contenidoCache is not null)
        {
            _logger.LogInformation("Listado de incidencias abierto LEIDO DESDE REDIS (cache).");

            return JsonSerializer.Deserialize<List<Incidencia>>(contenidoCache) ?? new List<Incidencia>();
        }

        _logger.LogInformation("Listado de incidencias abierto LEIDO DESDE LA BASE DE DATOS (no existia en Redis).");

        var incidencias = await _context.Incidencias
            .Where(i => i.Estado == "Abierta")
            .OrderBy(i => i.Id)
            .ToListAsync();

        await _cache.SetStringAsync(
            ClaveListadoAbiertas,
            JsonSerializer.Serialize(incidencias),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = DuracionCache
            });

        return incidencias;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarIncidencia(int id)
    {
        var incidencia = await _context.Incidencias.FindAsync(id);

        // La clave del listado se invalida y elimina ANTES de guardar los cambios en la base de datos.
        await _cache.RemoveAsync(ClaveListadoAbiertas);
        _logger.LogInformation("Clave de cache '{Clave}' eliminada de Redis antes de guardar los cambios.", ClaveListadoAbiertas);

        if (incidencia != null)
        {
            incidencia.Estado = "Cerrada";
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Incidencias));
    }
}
