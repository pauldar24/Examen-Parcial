using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using ExamenParcial.Configuration;
using Microsoft.Extensions.Options;

namespace ExamenParcial.Services;

public class AlgoliaIncidenciasService : IAlgoliaIncidenciasService
{
    private static readonly List<string> AtributosBuscables = new() { "estacion", "descripcion" };

    private readonly ILogger<AlgoliaIncidenciasService> _logger;
    private readonly SearchClient? _client;
    private readonly string _indexName;

    public AlgoliaIncidenciasService(IOptions<AlgoliaSettings> settings, ILogger<AlgoliaIncidenciasService> logger)
    {
        _logger = logger;
        _indexName = settings.Value.IndexName;

        if (string.IsNullOrWhiteSpace(settings.Value.AppId) || string.IsNullOrWhiteSpace(settings.Value.ApiKey))
        {
            _logger.LogWarning("Algolia no esta configurado. Completa la seccion '{Seccion}' de appsettings.json para activar la busqueda.", AlgoliaSettings.SectionName);
            return;
        }

        _client = new SearchClient(settings.Value.AppId, settings.Value.ApiKey);
    }

    public async Task<List<int>?> BuscarIdsAsync(string termino, CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            // Sin credenciales: la busqueda degrada a la base de datos local.
            return null;
        }

        if (string.IsNullOrWhiteSpace(termino))
        {
            return new List<int>();
        }

        try
        {
            var parametros = new SearchParams(new SearchParamsObject
            {
                Query = termino.Trim(),
                HitsPerPage = 100,
                RestrictSearchableAttributes = AtributosBuscables
            });

            var respuesta = await _client.SearchSingleIndexAsync<Hit>(
                _indexName,
                parametros,
                cancellationToken: cancellationToken);

            return respuesta.Hits
                .Select(hit => int.TryParse(hit.ObjectID, out var id) ? id : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();
        }
        catch (OperationCanceledException)
        {
            // Cancelacion de la peticion: no se degrada, se propaga.
            throw;
        }
        catch (Exception ex)
        {
            // Cualquier fallo de Algolia (credenciales invalidas, indice inexistente,
            // host inaccesible o sin red) degrada a la base de datos local sin error fatal.
            _logger.LogWarning(ex, "Algolia no respondio al consultar el indice '{Indice}'. La busqueda continua con la base de datos local.", _indexName);
            return null;
        }
    }
}
