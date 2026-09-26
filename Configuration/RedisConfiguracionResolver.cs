using System.Net;
using StackExchange.Redis;

namespace ExamenParcial.Configuration;

/// <summary>
/// Normaliza la configuracion de Redis al formato que entiende StackExchange.Redis.
/// Acepta, en este orden:
///   1. REDIS_CONNECTION  -> URL redis://usuario:clave@host:puerto/base o cadena nativa host:puerto,password=...
///   2. REDIS_HOST        -> con REDIS_PORT, REDIS_USER, REDIS_PASSWORD, REDIS_SSL y REDIS_DB
///   3. ConnectionStrings:Redis (appsettings.json)
/// Si nada es valido devuelve el respaldo en memoria: nunca lanza una excepcion.
/// </summary>
public static class RedisConfiguracionResolver
{
    public const string VariableConexion = "REDIS_CONNECTION";
    public const string VariableHost = "REDIS_HOST";
    public const string VariablePuerto = "REDIS_PORT";
    public const string VariableUsuario = "REDIS_USER";
    public const string VariablePassword = "REDIS_PASSWORD";
    public const string VariableSsl = "REDIS_SSL";
    public const string VariableBaseDatos = "REDIS_DB";
    public const string Seccion = "Redis";
    public const string NombreConexion = "Redis";

    private const int PuertoPorDefecto = 6379;

    public static RedisConfiguracion Resolver(IConfiguration configuration)
    {
        var motivos = new List<string>();

        var conexion = Environment.GetEnvironmentVariable(VariableConexion);

        if (!string.IsNullOrWhiteSpace(conexion))
        {
            if (TryCrear(conexion!, out var desdeVariable, out var error))
            {
                return RedisConfiguracion.Activo(desdeVariable!, $"{VariableConexion} de las variables de entorno");
            }

            // Si la configuracion se indico de forma explicita pero no es valida,
            // no se prueban otros origenes: es mejor la cache en memoria que
            // conectar con un servidor que el operador no pretendia usar.
            motivos.Add($"{VariableConexion} no es valida: {error}");

            return RedisConfiguracion.RespaldoEnMemoria(string.Join(" | ", motivos));
        }

        var host = Environment.GetEnvironmentVariable(VariableHost);

        if (!string.IsNullOrWhiteSpace(host))
        {
            if (TryCrearDesdePartes(
                    host!,
                    Environment.GetEnvironmentVariable(VariablePuerto),
                    Environment.GetEnvironmentVariable(VariableUsuario),
                    Environment.GetEnvironmentVariable(VariablePassword),
                    Environment.GetEnvironmentVariable(VariableSsl),
                    Environment.GetEnvironmentVariable(VariableBaseDatos),
                    out var desdePartes,
                    out var errorPartes))
            {
                return RedisConfiguracion.Activo(desdePartes!, $"{VariableHost} de las variables de entorno");
            }

            motivos.Add($"{VariableHost} no es valida: {errorPartes}");

            return RedisConfiguracion.RespaldoEnMemoria(string.Join(" | ", motivos));
        }

        var seccion = configuration.GetSection(Seccion);
        var hostSeccion = seccion["Host"];

        if (!string.IsNullOrWhiteSpace(hostSeccion))
        {
            if (TryCrearDesdePartes(hostSeccion!, seccion["Port"], seccion["User"], seccion["Password"], seccion["Ssl"], seccion["Database"], out var desdeSeccion, out var errorSeccion))
            {
                return RedisConfiguracion.Activo(desdeSeccion!, $"seccion {Seccion} de la configuracion");
            }

            motivos.Add($"la seccion {Seccion} no es valida: {errorSeccion}");

            return RedisConfiguracion.RespaldoEnMemoria(string.Join(" | ", motivos));
        }

        var desdeArchivo = configuration.GetConnectionString(NombreConexion);

        if (!string.IsNullOrWhiteSpace(desdeArchivo))
        {
            if (TryCrear(desdeArchivo!, out var desdeAppsettings, out var errorAppsettings))
            {
                return RedisConfiguracion.Activo(desdeAppsettings!, $"ConnectionStrings:{NombreConexion} de appsettings.json");
            }

            motivos.Add($"ConnectionStrings:{NombreConexion} no es valida: {errorAppsettings}");

            return RedisConfiguracion.RespaldoEnMemoria(string.Join(" | ", motivos));
        }

        return RedisConfiguracion.RespaldoEnMemoria("no se encontro ninguna variable de conexion de Redis");
    }

    private static bool TryCrear(string valor, out ConfigurationOptions? opciones, out string? error)
    {
        var texto = valor.Trim();

        // Cualquier cadena con esquema pasa por el conversor de URL, que ademas
        // rechaza los esquemas que no son redis ni rediss.
        if (texto.Contains("://", StringComparison.Ordinal))
        {
            return TryConvertirUrl(texto, out opciones, out error);
        }

        try
        {
            var nativas = ConfigurationOptions.Parse(texto);

            if (!NormalizarYValidarServidores(nativas, out error))
            {
                opciones = null;
                return false;
            }

            AjustarResiliencia(nativas);
            opciones = nativas;
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            opciones = null;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// ConfigurationOptions.Parse deja el puerto en 0 si no se indica y acepta
    /// silenciosamente textos que no son una configuracion valida, asi que aqui
    /// se completa el puerto por defecto (6379) y se comprueba que los servidores
    /// sean reales antes de darlos por buenos.
    /// </summary>
    private static bool NormalizarYValidarServidores(ConfigurationOptions opciones, out string? error)
    {
        error = null;

        if (opciones.EndPoints.Count == 0)
        {
            error = "no indica ningun servidor de Redis";
            return false;
        }

        foreach (var servidor in opciones.EndPoints.ToList())
        {
            var host = servidor switch
            {
                DnsEndPoint dns => dns.Host,
                IPEndPoint ip => ip.Address.ToString(),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(host) || host.Contains('/') || host.Any(char.IsWhiteSpace))
            {
                error = $"el servidor '{host}' no es valido";
                return false;
            }

            if (servidor is DnsEndPoint { Port: <= 0 } sinPuerto)
            {
                var posicion = opciones.EndPoints.IndexOf(sinPuerto);
                opciones.EndPoints[posicion] = new DnsEndPoint(sinPuerto.Host, PuertoPorDefecto);
            }
        }

        return true;
    }

    private static bool TryConvertirUrl(string url, out ConfigurationOptions? opciones, out string? error)
    {
        opciones = null;
        error = null;

        Uri uri;

        try
        {
            uri = new Uri(url);
        }
        catch (UriFormatException ex)
        {
            error = $"URL no valida ({ex.Message})";
            return false;
        }

        var esquema = uri.Scheme.ToLowerInvariant();

        if (esquema is not ("redis" or "rediss"))
        {
            error = $"el esquema '{uri.Scheme}' no es redis ni rediss";
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            error = "la URL no indica un host";
            return false;
        }

        // rediss:// significa Redis con TLS; redis:// es conexion simple.
        var resultado = new ConfigurationOptions { Ssl = esquema == "rediss" };

        try
        {
            resultado.EndPoints.Add(uri.Host, uri.Port > 0 ? uri.Port : PuertoPorDefecto);
        }
        catch (Exception ex)
        {
            error = $"el servidor '{uri.Host}' no es valido ({ex.Message})";
            return false;
        }

        // redis://[:]usuario:clave@host  ->  usuario / clave
        var usuario = string.Empty;
        var clave = string.Empty;

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var separador = uri.UserInfo.IndexOf(':');

            if (separador >= 0)
            {
                usuario = Uri.UnescapeDataString(uri.UserInfo[..separador]);
                clave = Uri.UnescapeDataString(uri.UserInfo[(separador + 1)..]);
            }
            else
            {
                clave = Uri.UnescapeDataString(uri.UserInfo);
            }
        }

        if (!string.IsNullOrWhiteSpace(usuario))
        {
            resultado.User = usuario;
        }

        if (!string.IsNullOrWhiteSpace(clave))
        {
            resultado.Password = clave;
        }

        // redis://host:puerto/3  ->  base de datos 3
        var baseDatos = uri.AbsolutePath.Trim('/');

        if (!string.IsNullOrEmpty(baseDatos))
        {
            if (!int.TryParse(baseDatos, out var numero))
            {
                error = $"la base de datos '{baseDatos}' no es numerica";
                return false;
            }

            resultado.DefaultDatabase = numero;
        }

        AjustarResiliencia(resultado);
        opciones = resultado;
        return true;
    }

    private static bool TryCrearDesdePartes(
        string host,
        string? puerto,
        string? usuario,
        string? clave,
        string? ssl,
        string? baseDatos,
        out ConfigurationOptions? opciones,
        out string? error)
    {
        opciones = null;
        error = null;

        if (!int.TryParse(puerto, out var numeroPuerto) || numeroPuerto is <= 0 or > 65535)
        {
            error = $"el puerto '{puerto}' no es valido";
            return false;
        }

        var resultado = new ConfigurationOptions
        {
            Ssl = EsVerdadero(ssl),
            DefaultDatabase = int.TryParse(baseDatos, out var numeroBase) ? numeroBase : -1
        };

        if (!string.IsNullOrWhiteSpace(usuario))
        {
            resultado.User = usuario;
        }

        if (!string.IsNullOrWhiteSpace(clave))
        {
            resultado.Password = clave;
        }

        resultado.EndPoints.Add(host.Trim(), numeroPuerto);

        AjustarResiliencia(resultado);

        if (!NormalizarYValidarServidores(resultado, out error))
        {
            return false;
        }

        opciones = resultado;
        return true;
    }

    /// <summary>
    /// Redis no debe tumbar la aplicacion: si no responde, los comandos fallan rapido
    /// y el consumidor usa la base de datos local mientras el multiplexer reconecta solo.
    /// </summary>
    private static void AjustarResiliencia(ConfigurationOptions opciones)
    {
        opciones.AbortOnConnectFail = false;
        opciones.ConnectRetry = 3;
        opciones.ConnectTimeout = 2000;
        opciones.SyncTimeout = 2000;
        opciones.BacklogPolicy = BacklogPolicy.FailFast;
    }

    private static bool EsVerdadero(string? valor)
        => bool.TryParse(valor, out var resultado) && resultado;
}
