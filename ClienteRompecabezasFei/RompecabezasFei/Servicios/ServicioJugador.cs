using RompecabezasFei.ServicioRompecabezasFei;
using RompecabezasFei.Utilidades;
using System;
using System.ServiceModel;
using System.Threading;
using System.Windows;

namespace RompecabezasFei.Servicios
{
    public class ServicioJugador : Servicio
    {
        private static ServicioJugadorClient clienteServicioJugador;

        private static bool esConexionCerradaPorCliente = false;

        // Evita mostrar el aviso de conexión perdida más de una vez y que,
        // después de mostrarlo, aparezcan otros errores de conexión
        private static bool esConexionPerdidaNotificada = false;

        public static bool EsConexionPerdidaNotificada => esConexionPerdidaNotificada;

        // Cada cuánto se comprueba que el servidor sigue respondiendo.
        // Si el servidor se cae, el cliente no se entera hasta que le envía algo,
        // así que sin esta comprobación el aviso solo salía al hacer otra acción
        // o al cerrar la ventana
        private const int MilisegundosEntreComprobaciones = 5000;

        private static Timer temporizadorComprobacion;

        private static int hayComprobacionEnCurso = 0;

        // Indica si el canal del jugador ya no está abierto sin que el
        // cliente lo haya cerrado, es decir, si se perdió la conexión
        public static bool EsCanalCaido()
        {
            return clienteServicioJugador != null && !esConexionCerradaPorCliente &&
                clienteServicioJugador.State != CommunicationState.Opened;
        }

        public override void AbrirConexion()
        {
            try
            {
                esConexionCerradaPorCliente = false;
                esConexionPerdidaNotificada = false;
                clienteServicioJugador = new ServicioJugadorClient(
                    new InstanceContext(VentanaPrincipal.ObtenerVentanaActual()));
                clienteServicioJugador.Open();
                clienteServicioJugador.InnerChannel.Closed +=
                    (objetoOrigen, evento) =>
                    ManejarConexionConServidorCerrada(); 
                clienteServicioJugador.InnerChannel.Faulted +=
                    (objetoOrigen, evento) =>
                    MostrarMensajeConexionPerdida();
                IniciarComprobacionDeConexion();
                EstadoOperacion = EstadoOperacion.Correcto;
            }
            catch (CommunicationException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (TimeoutException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (InvalidOperationException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
        }

        // Se llama desde los eventos Faulted/Closed del canal, que WCF lanza en
        // un hilo en segundo plano. Antes el mensaje se mostraba desde ese hilo,
        // sin ventana dueña, y quedaba detrás de la ventana del juego hasta que
        // el usuario intentaba cerrarla. Ahora todo se hace en el hilo de la
        // interfaz, con la ventana principal como dueña del mensaje.
        public static void NotificarConexionPerdida()
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (esConexionPerdidaNotificada || Dominio.CuentaJugador.Actual == null ||
                    VentanaPrincipal.EstaCerrandose)
                {
                    return;
                }

                esConexionPerdidaNotificada = true;
                DetenerComprobacionDeConexion();
                Dominio.CuentaJugador.Actual = null;
                clienteServicioJugador?.Abort();
                GestorCuadroDialogo.MostrarError(Application.Current.MainWindow,
                    Properties.Resources.ETIQUETA_ERRORCONEXIONPERDIDASERVIDOR_MENSAJE,
                    Properties.Resources.ETIQUETA_ERRORCONEXIONPERDIDASERVIDOR_TITULO);
                VentanaPrincipal.CambiarPagina(new PaginaInicioSesion());
            }));
        }

        private static void IniciarComprobacionDeConexion()
        {
            DetenerComprobacionDeConexion();
            temporizadorComprobacion = new Timer(estado => ComprobarConexion(), null,
                MilisegundosEntreComprobaciones, MilisegundosEntreComprobaciones);
        }

        private static void DetenerComprobacionDeConexion()
        {
            temporizadorComprobacion?.Dispose();
            temporizadorComprobacion = null;
        }

        // Se ejecuta en un hilo en segundo plano para no congelar la ventana
        // mientras espera la respuesta del servidor
        private static void ComprobarConexion()
        {
            ServicioJugadorClient cliente = clienteServicioJugador;

            if (cliente == null || esConexionCerradaPorCliente ||
                Interlocked.Exchange(ref hayComprobacionEnCurso, 1) == 1)
            {
                return;
            }

            try
            {
                if (cliente.State == CommunicationState.Opened)
                {
                    cliente.ComprobarConexion();
                }
            }
            catch (ActionNotSupportedException excepcion)
            {
                // El servidor respondió, pero no conoce ComprobarConexion porque
                // tiene una versión anterior. La conexión sigue viva: se registra
                // y se dejan de hacer comprobaciones, en lugar de desconectar
                ManejarServidorSinComprobacion(excepcion);
            }
            catch (FaultException excepcion)
            {
                ManejarServidorSinComprobacion(excepcion);
            }
            catch (CommunicationException excepcion)
            {
                ManejarFalloDeComprobacion(cliente, excepcion);
            }
            catch (TimeoutException excepcion)
            {
                ManejarFalloDeComprobacion(cliente, excepcion);
            }
            catch (ObjectDisposedException excepcion)
            {
                ManejarFalloDeComprobacion(cliente, excepcion);
            }
            finally
            {
                Interlocked.Exchange(ref hayComprobacionEnCurso, 0);
            }
        }

        private static void ManejarServidorSinComprobacion(Exception excepcion)
        {
            Registros.Registrador.EscribirRegistro(excepcion);
            DetenerComprobacionDeConexion();
        }

        private static void ManejarFalloDeComprobacion(ServicioJugadorClient cliente,
            Exception excepcion)
        {
            Registros.Registrador.EscribirRegistro(excepcion);
            DetenerComprobacionDeConexion();
            cliente.Abort();
            NotificarConexionPerdida();
        }

        private void MostrarMensajeConexionPerdida()
        {
            EstadoOperacion = EstadoOperacion.Error;
            NotificarConexionPerdida();
        }

        private void ManejarConexionConServidorCerrada()
        {
            if (!esConexionCerradaPorCliente)
            {
                MostrarMensajeConexionPerdida();
            }
        }

        public override void CerrarConexion()
        {
            if (clienteServicioJugador.State == CommunicationState.Opened)
            {
                try
                {
                    esConexionCerradaPorCliente = true;
                    DetenerComprobacionDeConexion();
                    clienteServicioJugador.Close();
                    EstadoOperacion = EstadoOperacion.Correcto;
                }
                catch (CommunicationException excepcion)
                {
                    ManejarExcepcionDeServidor(excepcion);
                }
                catch (TimeoutException excepcion)
                {
                    ManejarExcepcionDeServidor(excepcion);
                }
                catch (InvalidOperationException excepcion)
                {
                    ManejarExcepcionDeServidor(excepcion);
                }
            }
        }

        public bool RegistrarJugador(CuentaJugador cuentaJugador)
        {
            bool resultado = false;

            try
            {
                resultado = clienteServicioJugador.
                    RegistrarJugador(cuentaJugador);
                EstadoOperacion = EstadoOperacion.Correcto;
            }
            catch (EndpointNotFoundException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationObjectFaultedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationObjectAbortedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (ObjectDisposedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (TimeoutException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            finally
            {
                if (EstadoOperacion == EstadoOperacion.Error)
                {
                    clienteServicioJugador.Abort();
                }
            }

            return resultado;
        }

        public CuentaJugador IniciarSesionComoJugador(
            string nombreJugador, string contrasena)
        {
            CuentaJugador cuentaJugador = null;

            try
            {
                cuentaJugador = clienteServicioJugador.
                    IniciarSesionComoJugador(nombreJugador, contrasena);
                EstadoOperacion = EstadoOperacion.Correcto;
            }
            catch (EndpointNotFoundException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationObjectFaultedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationObjectAbortedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (ObjectDisposedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (TimeoutException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            finally
            {
                if (EstadoOperacion == EstadoOperacion.Error)
                {
                    clienteServicioJugador.Abort();
                }
            }

            return cuentaJugador;
        }

        public CuentaJugador IniciarSesionComoInvitado(string nombreJugador)
        {
            CuentaJugador cuentaInvitado = null;

            try
            {
                cuentaInvitado = clienteServicioJugador.
                    IniciarSesionComoInvitado(nombreJugador);
                EstadoOperacion = EstadoOperacion.Correcto;
            }
            catch (EndpointNotFoundException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationObjectFaultedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationObjectAbortedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (ObjectDisposedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (TimeoutException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            finally
            {
                if (EstadoOperacion == EstadoOperacion.Error)
                {
                    clienteServicioJugador.Abort();
                }
            }

            return cuentaInvitado;
        }

        public void CerrarSesion(string nombreJugador)
        {
            if (clienteServicioJugador.State == CommunicationState.Opened)
            {
                try
                {
                    clienteServicioJugador.CerrarSesion(nombreJugador);
                    EstadoOperacion = EstadoOperacion.Correcto;
                }
                catch (EndpointNotFoundException excepcion)
                {
                    ManejarExcepcionDeServidor(excepcion);
                }
                catch (CommunicationObjectFaultedException excepcion)
                {
                    ManejarExcepcionDeServidor(excepcion);
                }
                catch (CommunicationObjectAbortedException excepcion)
                {
                    ManejarExcepcionDeServidor(excepcion);
                }
                catch (CommunicationException excepcion)
                {
                    ManejarExcepcionDeServidor(excepcion);
                }
                catch (ObjectDisposedException excepcion)
                {
                    ManejarExcepcionDeServidor(excepcion);
                }
                catch (TimeoutException excepcion)
                {
                    ManejarExcepcionDeServidor(excepcion);
                }
                finally
                {
                    if (EstadoOperacion == EstadoOperacion.Error)
                    {
                        clienteServicioJugador.Abort();
                    }
                }
            }
        }

        public bool ActualizarNombreJugador(string nombreAnterior,
            string nuevoNombre)
        {
            bool resultado = false;

            try
            {
                resultado = clienteServicioJugador.
                    ActualizarNombreJugador(nombreAnterior, nuevoNombre);
                EstadoOperacion = EstadoOperacion.Correcto;
            }
            catch (EndpointNotFoundException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationObjectFaultedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationObjectAbortedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (ObjectDisposedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (TimeoutException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            finally
            {
                if (EstadoOperacion == EstadoOperacion.Error)
                {
                    clienteServicioJugador.Abort();
                }
            }

            return resultado;
        }

        public bool ActualizarNumeroAvatar(string nombreJugador,
            int nuevoNumeroAvatar)
        {
            bool resultado = false;

            try
            {
                resultado = clienteServicioJugador.
                    ActualizarNumeroAvatar(nombreJugador, nuevoNumeroAvatar);
                EstadoOperacion = EstadoOperacion.Correcto;
            }
            catch (EndpointNotFoundException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationObjectFaultedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationObjectAbortedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (ObjectDisposedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (TimeoutException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            finally
            {
                if (EstadoOperacion == EstadoOperacion.Error)
                {
                    clienteServicioJugador.Abort();
                }
            }

            return resultado;
        }

        public bool ActualizarContrasena(string correo, string nuevaContrasena)
        {
            bool resultado = false;

            try
            {
                resultado = clienteServicioJugador.
                    ActualizarContrasena(correo, nuevaContrasena);
                EstadoOperacion = EstadoOperacion.Correcto;
            }
            catch (EndpointNotFoundException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationObjectFaultedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationObjectAbortedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (ObjectDisposedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (TimeoutException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            finally
            {
                if (EstadoOperacion == EstadoOperacion.Error)
                {
                    clienteServicioJugador.Abort();
                }
            }

            return resultado;
        }

        public bool EsLaMismaContrasenaDeJugador(string nombreJugador, string contrasena)
        {
            bool resultado = false;

            try
            {
                resultado = clienteServicioJugador.
                    EsLaMismaContrasenaDeJugador(nombreJugador, contrasena);
                EstadoOperacion = EstadoOperacion.Correcto;
            }
            catch (EndpointNotFoundException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationObjectFaultedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationObjectAbortedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (ObjectDisposedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (TimeoutException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            finally
            {
                if (EstadoOperacion == EstadoOperacion.Error)
                {
                    clienteServicioJugador.Abort();
                }
            }

            return resultado;
        }

        public bool ExisteNombreJugadorRegistrado(string nombreJugador)
        {
            bool resultado = false;

            try
            {
                resultado = clienteServicioJugador.
                    ExisteNombreJugadorRegistrado(nombreJugador);
                EstadoOperacion = EstadoOperacion.Correcto;
            }
            catch (EndpointNotFoundException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationObjectFaultedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationObjectAbortedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (CommunicationException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (ObjectDisposedException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            catch (TimeoutException excepcion)
            {
                ManejarExcepcionDeServidor(excepcion);
            }
            finally
            {
                if (EstadoOperacion == EstadoOperacion.Error)
                {
                    clienteServicioJugador.Abort();
                }
            }

            return resultado;
        }
    }
}