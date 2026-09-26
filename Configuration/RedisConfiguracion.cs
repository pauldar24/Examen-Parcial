using StackExchange.Redis;

namespace ExamenParcial.Configuration;

/// <summary>
/// Resultado de resolver la configuracion de Redis: si quedo habilitado, con que
/// formato y desde que origen; si no, el diagnostico de por que se opto por el
/// respaldo en memoria.
/// </summary>
public class RedisConfiguracion
{
    public bool Habilitado { get; init; }

    public ConfigurationOptions? Opciones { get; init; }

    public string Origen { get; init; } = string.Empty;

    public string? Diagnostico { get; init; }

    public static RedisConfiguracion Activo(ConfigurationOptions opciones, string origen)
        => new() { Habilitado = true, Opciones = opciones, Origen = origen };

    public static RedisConfiguracion RespaldoEnMemoria(string diagnostico)
        => new() { Habilitado = false, Diagnostico = diagnostico };
}
