using System;
using System.IO;
using System.Net;
using System.Reflection;
using Contratos;
using CoreWCF;
using CoreWCF.Configuration;
using Datos;
using log4net;
using log4net.Config;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Servicios;

namespace Servidor
{
    // Punto de entrada del servidor de RompecabezasFEI.
    //
    // Antes (.NET Framework) el servicio se levantaba con ServiceHost y su
    // configuración vivía en App.config. Ahora (.NET 8) se usa CoreWCF, que
    // corre igual en Windows y en Linux, y la configuración se hace aquí en código.
    //
    // Los clientes no cambian: siguen conectándose por net.tcp a
    // net.tcp://<ip-del-servidor>:<puerto>/Contratos con seguridad "None".
    public static class Servidor
    {
        private const int PuertoPredeterminado = 8089;
        private const string ClavePuerto = "Servidor:Puerto";
        private const string RutaEndpoints = "/Contratos";
        private const string ArchivoConfiguracionRegistros = "log4net.config";

        public static void Main(string[] args)
        {
            ConfigurarRegistros();
            int puerto = ObtenerPuerto();

            WebApplicationBuilder constructor = WebApplication.CreateBuilder(
                new WebApplicationOptions
                {
                    Args = args,
                    ContentRootPath = AppContext.BaseDirectory
                });

            // Si el servidor corre como servicio de systemd (Linux) o como
            // servicio de Windows, avisa al sistema cuando ya está listo y se
            // detiene limpiamente. Fuera de un servicio no hacen nada.
            constructor.Host.UseSystemd();
            constructor.Host.UseWindowsService();

            // Escucha en todas las interfaces de red (0.0.0.0), no solo en
            // localhost, para que los otros equipos de la red puedan conectarse
            constructor.WebHost.UseNetTcp(IPAddress.Any, puerto);

            constructor.Services.AddServiceModelServices();

            // InstanceContextMode.Single: una sola instancia del servicio para
            // todos los jugadores (ahí viven las salas y los jugadores activos)
            constructor.Services.AddSingleton<ServicioRompecabezasFei>();

            WebApplication aplicacion = constructor.Build();

            aplicacion.UseServiceModel(configuracionServicios =>
            {
                var enlace = new NetTcpBinding(SecurityMode.None);

                configuracionServicios.AddService<ServicioRompecabezasFei>();
                configuracionServicios
                    .AddServiceEndpoint<ServicioRompecabezasFei, IServicioAmistades>(
                        enlace, RutaEndpoints)
                    .AddServiceEndpoint<ServicioRompecabezasFei, IServicioCorreo>(
                        enlace, RutaEndpoints)
                    .AddServiceEndpoint<ServicioRompecabezasFei, IServicioInvitaciones>(
                        enlace, RutaEndpoints)
                    .AddServiceEndpoint<ServicioRompecabezasFei, IServicioJugador>(
                        enlace, RutaEndpoints)
                    .AddServiceEndpoint<ServicioRompecabezasFei, IServicioPartida>(
                        enlace, RutaEndpoints)
                    .AddServiceEndpoint<ServicioRompecabezasFei, IServicioSala>(
                        enlace, RutaEndpoints);
            });

            aplicacion.Lifetime.ApplicationStarted.Register(() =>
                Console.WriteLine($"Servidor operacional en net.tcp://0.0.0.0:{puerto}" +
                    $"{RutaEndpoints} (entorno: {ConfiguracionServidor.Entorno})"));

            aplicacion.Run();
        }

        // El puerto se puede cambiar en appsettings.json ("Servidor:Puerto")
        // o con la variable de entorno ROMPECABEZAS_Servidor__Puerto
        private static int ObtenerPuerto()
        {
            string valor = ConfiguracionServidor.ObtenerValor(ClavePuerto);

            return int.TryParse(valor, out int puerto) ? puerto : PuertoPredeterminado;
        }

        // log4net ya no lee su configuración de App.config, sino de log4net.config
        private static void ConfigurarRegistros()
        {
            string ruta = Path.Combine(AppContext.BaseDirectory, ArchivoConfiguracionRegistros);

            if (File.Exists(ruta))
            {
                XmlConfigurator.Configure(
                    LogManager.GetRepository(Assembly.GetEntryAssembly()), new FileInfo(ruta));
            }
        }
    }
}
