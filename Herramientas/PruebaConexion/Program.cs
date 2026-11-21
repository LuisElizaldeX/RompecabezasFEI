using System;
using System.Collections.Concurrent;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using RompecabezasFei.ServicioRompecabezasFei;

// Uso:
//   dotnet run -- <ip-del-servidor> [puerto] [--base-datos]
//
// Simula a dos jugadores invitados (A y B) que entran a la misma sala y
// comprueba que el servidor les manda callbacks: aviso de nuevo jugador,
// mensajes de chat y aviso de desconexión. Con --base-datos también registra
// un jugador de prueba en PostgreSQL e inicia sesión con él.
namespace PruebaConexion
{
    internal class CallbackJugador : IServicioJugadorCallback
    {
        public void ProbarConexionJugador() { }
    }

    internal class CallbackSala : IServicioSalaCallback
    {
        private readonly string jugador;
        public ConcurrentQueue<string> Eventos { get; } = new ConcurrentQueue<string>();

        public CallbackSala(string jugador) { this.jugador = jugador; }

        private void Registrar(string evento)
        {
            Console.WriteLine($"   callback recibido por {jugador}: {evento}");
            Eventos.Enqueue(evento);
        }

        public void MostrarNuevaPartida() => Registrar("MostrarNuevaPartida");
        public void MostrarMensajeDeSala(string mensaje) => Registrar("MostrarMensajeDeSala: " + mensaje);
        public void MostrarNuevoJugadorEnSala(CuentaJugador nuevoJugador) =>
            Registrar("MostrarNuevoJugadorEnSala: " + nuevoJugador.NombreJugador);
        public void MostrarMensajeExpulsionDeSala() => Registrar("MostrarMensajeExpulsionDeSala");
        public void MostrarDesconexionDeJugadorEnSala(string nombre) =>
            Registrar("MostrarDesconexionDeJugadorEnSala: " + nombre);
        public void MostrarFuncionesDeAnfitrionEnSala() => Registrar("MostrarFuncionesDeAnfitrionEnSala");
        public void HabilitarInicioDePartida() => Registrar("HabilitarInicioDePartida");
        public void DeshabilitarInicioDePartida() => Registrar("DeshabilitarInicioDePartida");
        public void MostrarAmigoDisponible(CuentaJugador cuentaAmigo) => Registrar("MostrarAmigoDisponible");
        public void OcultarAmigoNoDisponible(string nombreAmigo) => Registrar("OcultarAmigoNoDisponible");
    }

    internal static class Program
    {
        private static int fallos;
        private static EndpointAddress direccion;

        private static int Main(string[] argumentos)
        {
            string[] posicionales = argumentos.Where(a => !a.StartsWith("--")).ToArray();
            string host = posicionales.Length > 0 ? posicionales[0] : "localhost";
            string puerto = posicionales.Length > 1 ? posicionales[1] : "8089";
            bool probarBaseDatos = argumentos.Contains("--base-datos");
            direccion = new EndpointAddress($"net.tcp://{host}:{puerto}/Contratos");
            Console.WriteLine($"Probando {direccion.Uri}\n");

            try
            {
                ProbarSalaConCallbacks();

                if (probarBaseDatos)
                {
                    ProbarBaseDatos();
                }
            }
            catch (Exception excepcion) when (excepcion is CommunicationException ||
                excepcion is TimeoutException)
            {
                Console.WriteLine($"\nNo se pudo hablar con el servidor: {excepcion.Message}");
                Console.WriteLine("Revisa que el servidor esté corriendo, la IP, el puerto y el firewall.");
                return 1;
            }

            Console.WriteLine(fallos == 0 ? "\nTodo funciona." : $"\nPruebas fallidas: {fallos}");
            return fallos == 0 ? 0 : 1;
        }

        private static void ProbarSalaConCallbacks()
        {
            string sufijo = (DateTime.Now.Ticks % 100000).ToString();
            string nombreA = "PruebaA" + sufijo;
            string nombreB = "PruebaB" + sufijo;
            var callbackA = new CallbackSala("A");
            var callbackB = new CallbackSala("B");

            var jugadorA = new ServicioJugadorClient(
                new InstanceContext(new CallbackJugador()), CrearEnlace(), direccion);
            var jugadorB = new ServicioJugadorClient(
                new InstanceContext(new CallbackJugador()), CrearEnlace(), direccion);
            var salaA = new ServicioSalaClient(new InstanceContext(callbackA), CrearEnlace(), direccion);
            var salaB = new ServicioSalaClient(new InstanceContext(callbackB), CrearEnlace(), direccion);

            Comprobar(jugadorA.IniciarSesionComoInvitado(nombreA) != null, "A inicia sesión como invitado");
            Comprobar(jugadorB.IniciarSesionComoInvitado(nombreB) != null, "B inicia sesión como invitado");

            string codigoSala = salaA.GenerarCodigoParaNuevaSala();
            Comprobar(salaA.CrearNuevaSala(nombreA, codigoSala), "A crea una sala");
            Comprobar(salaA.UnirseASala(nombreA, codigoSala), "A entra a su sala");
            Comprobar(salaB.UnirseASala(nombreB, codigoSala), "B entra a la sala de A");
            Comprobar(Esperar(callbackA, "MostrarNuevoJugadorEnSala: " + nombreB),
                "A recibe el callback de que B entró");

            salaB.EnviarMensajeDeSala(nombreB, codigoSala, "hola");
            Comprobar(Esperar(callbackA, "MostrarMensajeDeSala") &&
                Esperar(callbackB, "MostrarMensajeDeSala"), "A y B reciben el mensaje de chat");

            Console.WriteLine("   (cortando la conexión de B de golpe, como si cerrara el juego)");
            salaB.Abort();
            jugadorB.Abort();
            Comprobar(Esperar(callbackA, "MostrarDesconexionDeJugadorEnSala: " + nombreB, 15000),
                "El servidor detecta que B se desconectó y avisa a A");

            jugadorA.CerrarSesion(nombreA);
            salaA.Abort();
            jugadorA.Abort();
        }

        private static void ProbarBaseDatos()
        {
            string nombre = "Prueba" + (DateTime.Now.Ticks % 100000);
            var jugador = new ServicioJugadorClient(
                new InstanceContext(new CallbackJugador()), CrearEnlace(), direccion);

            Comprobar(jugador.RegistrarJugador(new CuentaJugador
            {
                NombreJugador = nombre,
                Correo = nombre + "@ejemplo.com",
                Contrasena = "contrasena-de-prueba",
                NumeroAvatar = 1
            }), $"Registrar al jugador {nombre} en la base de datos");
            Comprobar(jugador.IniciarSesionComoJugador(nombre, "contrasena-de-prueba")?.NombreJugador == nombre,
                "Iniciar sesión con ese jugador");
            jugador.CerrarSesion(nombre);
            jugador.Abort();
        }

        // Debe coincidir con el servidor: net.tcp sin seguridad
        private static NetTcpBinding CrearEnlace() => new NetTcpBinding(SecurityMode.None);

        private static bool Esperar(CallbackSala callback, string inicioEvento, int milisegundos = 5000)
        {
            DateTime limite = DateTime.Now.AddMilliseconds(milisegundos);

            while (DateTime.Now < limite)
            {
                if (callback.Eventos.Any(evento => evento.StartsWith(inicioEvento)))
                {
                    return true;
                }

                Thread.Sleep(50);
            }

            return false;
        }

        private static void Comprobar(bool condicion, string descripcion)
        {
            Console.WriteLine((condicion ? "[OK]    " : "[FALLO] ") + descripcion);

            if (!condicion)
            {
                fallos++;
            }
        }
    }
}
