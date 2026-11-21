using Microsoft.Extensions.Configuration;
using Npgsql;
using System;

namespace Datos
{
    // Arma la cadena de conexión de la base de datos a partir de
    // ConfiguracionServidor: la cadena sin contraseña viene de appsettings
    // y la contraseña de user secrets o de una variable de entorno.
    public static class ConfiguracionBaseDatos
    {
        private const string NombreCadenaConexion = "EntidadesRompecabezasFei";
        private const string ClaveContrasena = "BaseDatos:Contrasena";

        private static readonly Lazy<string> cadenaConexion =
            new Lazy<string>(ConstruirCadenaConexion);

        // La cadena se arma una sola vez, la primera vez que se pide
        public static string ObtenerCadenaConexion()
        {
            return cadenaConexion.Value;
        }

        private static string ConstruirCadenaConexion()
        {
            IConfiguration configuracion = ConfiguracionServidor.Configuracion;
            string cadenaBase = configuracion.GetConnectionString(NombreCadenaConexion);

            if (string.IsNullOrWhiteSpace(cadenaBase))
            {
                throw new InvalidOperationException(
                    $"No se encontró la cadena de conexión \"{NombreCadenaConexion}\" " +
                    $"para el entorno \"{ConfiguracionServidor.Entorno}\". Revisa " +
                    $"appsettings.json y appsettings.{ConfiguracionServidor.Entorno}.json.");
            }

            var cadena = new NpgsqlConnectionStringBuilder(cadenaBase);
            string contrasena = configuracion[ClaveContrasena];

            if (!string.IsNullOrEmpty(contrasena))
            {
                cadena.Password = contrasena;
            }

            return cadena.ConnectionString;
        }
    }
}
