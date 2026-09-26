namespace ExamenParcial.Services;

public interface IAlgoliaIncidenciasService
{
    /// <summary>
    /// Devuelve los objectID que Algolia devuelve para el termino buscado.
    /// Devuelve <c>null</c> cuando Algolia no esta disponible (sin credenciales o con error),
    /// para que la aplicacion pueda degradar a la base de datos local.
    /// </summary>
    Task<List<int>?> BuscarIdsAsync(string termino, CancellationToken cancellationToken = default);
}
