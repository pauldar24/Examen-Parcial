namespace ExamenParcial.Configuration;

public class AlgoliaSettings
{
    public const string SectionName = "Algolia";

    public string AppId { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string IndexName { get; set; } = "incidencias";
}
