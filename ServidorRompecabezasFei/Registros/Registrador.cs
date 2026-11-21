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
        public static ILog GetLogger([CallerFilePath] string nombreArchivo = "")
        {
            return LogManager.GetLogger(nombreArchivo);
        }

        public static void EscribirRegistro(Exception ex)
        {
            string rutaArchivo = ConfigurationManager.AppSettings["Registros"];
            string path = ConfigurationManager.AppSettings["Directorio"];

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
