using ExamenParcial.Data;
using ExamenParcial.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamenParcial.Controllers;

public class OperacionesController : Controller
{
    private readonly ApplicationDbContext _context;

    public OperacionesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Incidencias()
    {
        var incidencias = await _context.Incidencias
            .Where(i => i.Estado == "Abierta")
            .OrderBy(i => i.Id)
            .ToListAsync();

        return View(incidencias);
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
