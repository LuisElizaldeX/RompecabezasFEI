using System.Configuration;
using System.Diagnostics;
using System.IO;
using System;
using System.Runtime.CompilerServices;
using log4net;

namespace Registros
{
    public static class Registrador
    {
        private static readonly string RutaArchivoPredeterminada =
            Path.Combine("Registros", "Registros.txt");

        public static ILog GetLogger([CallerFilePath] string nombreArchivo = "")
        {
            return LogManager.GetLogger(nombreArchivo);
        }

        public static void EscribirRegistro(Exception ex)
        {
            // Las rutas pueden ser relativas (por ejemplo "Registros/Registros.txt");
            // en ese caso se toman desde la carpeta donde está el servidor.
            // Así funciona igual en Windows y en Linux.
            string rutaArchivo = ResolverRuta(
                ConfigurationManager.AppSettings["Registros"], RutaArchivoPredeterminada);
            string path = Path.GetDirectoryName(rutaArchivo);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            StackTrace seguimientoDePila = new StackTrace();
            StackFrame marcoDeSeguimientoDePila = seguimientoDePila.GetFrame(1);
            string metodo = marcoDeSeguimientoDePila.GetMethod().Name;
            string clase = marcoDeSeguimientoDePila.GetMethod().DeclaringType.FullName;
            string rutaArchivoActual = Path.Combine
                (Environment.CurrentDirectory, $"{clase}.{metodo}.cs");
            string nombreExcepcion = ex.GetType().Name;

            using (StreamWriter escritorTextoPlano = new StreamWriter(rutaArchivo, true))
            {
                string mensajeFinal = $"{Environment.NewLine}{DateTime.Now} " + 
                    $"- Archivo: [{rutaArchivoActual}], " +
                    $"- Excepción: {nombreExcepcion}, Mensaje: {ex.Message}" +
                    DescribirExcepcionesInternas(ex);
                escritorTextoPlano.WriteLine(mensajeFinal);
            }
        }

        private static string ResolverRuta(string rutaConfigurada, string rutaPredeterminada)
        {
            string ruta = string.IsNullOrWhiteSpace(rutaConfigurada) ?
                rutaPredeterminada : rutaConfigurada;

            return Path.IsPathRooted(ruta) ?
                ruta : Path.Combine(AppContext.BaseDirectory, ruta);
        }

        // Agrega al registro las excepciones internas, que suelen tener
        // la causa real (por ejemplo, la de un SmtpException o EntityException)
        private static string DescribirExcepcionesInternas(Exception ex)
        {
            var descripcion = new System.Text.StringBuilder();

            for (Exception interna = ex.InnerException; interna != null;
                interna = interna.InnerException)
            {
                descripcion.Append($"{Environment.NewLine}    -> {interna.GetType().Name}: " +
                    interna.Message);
            }

            return descripcion.ToString();
        }
    }
}
