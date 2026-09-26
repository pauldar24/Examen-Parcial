using ExamenParcial.Data;
using ExamenParcial.Models;
using ExamenParcial.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamenParcial.Controllers;

public class OperacionesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAlgoliaIncidenciasService _algolia;

    public OperacionesController(ApplicationDbContext context, IAlgoliaIncidenciasService algolia)
    {
        _context = context;
        _algolia = algolia;
    }

    public async Task<IActionResult> Incidencias(string? q)
    {
        var termino = q?.Trim() ?? string.Empty;
        ViewData["Termino"] = termino;

        // Sin busqueda se devuelve el listado habitual completo desde la base de datos local.
        if (string.IsNullOrEmpty(termino))
        {
            var abiertas = await _context.Incidencias
                .Where(i => i.Estado == "Abierta")
                .OrderBy(i => i.Id)
                .ToListAsync();

            return View(abiertas);
        }

        // Algolia devuelve los objectID que coinciden con la busqueda.
        var ids = await _algolia.BuscarIdsAsync(termino, HttpContext.RequestAborted);

        // De esos ids solo se muestran los que siguen Abiertas en la base de datos local.
        var coincidencias = await _context.Incidencias
            .Where(i => i.Estado == "Abierta" && ids.Contains(i.Id))
            .ToListAsync();

        // Se respeta el orden de relevancia devuelto por Algolia.
        var posicion = ids.Select((id, i) => new { Id = id, Posicion = i })
            .ToDictionary(x => x.Id, x => x.Posicion);

        var ordenadas = coincidencias
            .OrderBy(i => posicion.TryGetValue(i.Id, out var p) ? p : int.MaxValue)
            .ToList();

        return View(ordenadas);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarIncidencia(int id)
    {
        var incidencia = await _context.Incidencias.FindAsync(id);

        if (incidencia != null)
        {
            incidencia.Estado = "Cerrada";
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Incidencias));
    }
}
