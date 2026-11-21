using Datos;
using System;
using System.Net.Mail;
using System.Net;
using System.Text;
using log4net;
using Registros;
using System.Runtime.InteropServices;

namespace Logica
{
    public static class GeneradorMensajeCorreo
    {
        private static readonly ILog Log = Registrador.GetLogger();
        private static readonly int NumeroDePuerto = 587;
        private static readonly string Servidor = "smtp.gmail.com";

        // La dirección se guarda en appsettings.json y la contraseña de
        // aplicación en user secrets (desarrollo) o en una variable de
        // entorno ROMPECABEZAS_Correo__Contrasena (producción)
        private const string ClaveDireccion = "Correo:Direccion";
        private const string ClaveContrasena = "Correo:Contrasena";

        public static bool EnviarMensaje(string encabezado, string correo, string asunto, 
            string mensaje)
        {
            bool resultado = true;
            string direccion = ConfiguracionServidor.ObtenerValor(ClaveDireccion);
            string contrasena = ConfiguracionServidor.ObtenerValor(ClaveContrasena);

            if (string.IsNullOrWhiteSpace(direccion) || string.IsNullOrWhiteSpace(contrasena))
            {
                Registros.Registrador.EscribirRegistro(new InvalidOperationException(
                    $"Falta configurar \"{ClaveDireccion}\" o \"{ClaveContrasena}\" " +
                    $"para el entorno \"{ConfiguracionServidor.Entorno}\"."));
                return false;
            }

            SmtpClient clienteSmtp = new SmtpClient(Servidor, NumeroDePuerto)
            {
                EnableSsl = true
            };

            try
            {
                MailMessage mensajeCorreo = new MailMessage()
                {
                    From = new MailAddress(direccion, encabezado),
                    Subject = asunto,
                    Body = mensaje,
                    BodyEncoding = Encoding.UTF8,
                    IsBodyHtml = true
                };
                mensajeCorreo.To.Add(correo);

                clienteSmtp.Credentials = new NetworkCredential(direccion, contrasena);
                clienteSmtp.EnableSsl = true;
                clienteSmtp.Send(mensajeCorreo);                
            }
            catch (ArgumentException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
                resultado = false;
            }
            catch (SmtpException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
                resultado = false;
            }
            catch (FormatException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
                resultado = false;
            }
            finally
            {
                clienteSmtp.Dispose();
            }
            return resultado;
        }
    }
}