using Microsoft.Extensions.Configuration;
using System;

namespace Datos
{
    // Configuración del servidor (base de datos, correo, etc.).
    //
    // Orden en que se cargan los valores (cada uno sobrescribe al anterior):
    //   1. appsettings.json                  (versionado, sin contraseñas)
    //   2. appsettings.{Entorno}.json        (versionado, sin contraseñas)
    //   3. User secrets                      (solo en Development, fuera del repositorio)
    //   4. Variables de entorno ROMPECABEZAS_ (para Production)
    //
    // El entorno se toma de la variable DOTNET_ENVIRONMENT. Si no existe,
    // se usa Development al compilar en Debug y Production al compilar en Release.
    public static class ConfiguracionServidor
    {
        public const string IdSecretosUsuario = "6a4816e4-f4c8-41ff-9bbd-37bd6a3baf52";
        public const string EntornoDesarrollo = "Development";
        public const string EntornoProduccion = "Production";

        private const string PrefijoVariablesEntorno = "ROMPECABEZAS_";
        private const string VariableEntorno = "DOTNET_ENVIRONMENT";

        private static readonly Lazy<IConfiguration> configuracion =
            new Lazy<IConfiguration>(CargarConfiguracion);

        public static string Entorno { get; } = ObtenerEntorno();

        internal static IConfiguration Configuracion => configuracion.Value;

        // Devuelve el valor de una clave, por ejemplo "Correo:Direccion",
        // o null si no está configurada
        public static string ObtenerValor(string clave)
        {
            return Configuracion[clave];
        }

        private static string ObtenerEntorno()
        {
            string entorno = Environment.GetEnvironmentVariable(VariableEntorno);

            if (string.IsNullOrWhiteSpace(entorno))
            {
#if DEBUG
                entorno = EntornoDesarrollo;
#else
                entorno = EntornoProduccion;
#endif
            }

            return entorno;
        }

        private static IConfiguration CargarConfiguracion()
        {
            var constructor = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile($"appsettings.{Entorno}.json", optional: true, reloadOnChange: false);

            if (Entorno == EntornoDesarrollo)
            {
                // Los user secrets viven en
                // %APPDATA%\Microsoft\UserSecrets\<IdSecretosUsuario>\secrets.json,
                // fuera de la carpeta del proyecto, así que nunca se suben al repositorio
                constructor.AddUserSecrets(IdSecretosUsuario);
            }

            constructor.AddEnvironmentVariables(PrefijoVariablesEntorno);

            return constructor.Build();
        }
    }
}
