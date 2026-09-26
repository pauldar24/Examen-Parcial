namespace ExamenParcial.Services;

public interface IAlgoliaIncidenciasService
{
    Task<List<int>> BuscarIdsAsync(string termino, CancellationToken cancellationToken = default);
}
