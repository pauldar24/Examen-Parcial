using ExamenParcial.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamenParcial.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Incidencia> Incidencias => Set<Incidencia>();
}
