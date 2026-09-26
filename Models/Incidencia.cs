namespace ExamenParcial.Models;

public class Incidencia
{
    public int Id { get; set; }

    public string Estacion { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public string Prioridad { get; set; } = string.Empty;

    public string Estado { get; set; } = "Abierta";
}
