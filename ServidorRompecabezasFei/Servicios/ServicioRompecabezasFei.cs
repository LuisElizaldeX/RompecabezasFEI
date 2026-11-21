using Contratos;
using Datos;
using Logica;
using Logica.AccesoDatos;
using Logica.Enumeraciones;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.ServiceModel;

namespace Servicios
{    
    #region IServicioJugador
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, 
        InstanceContextMode = InstanceContextMode.Single)]
    public partial class ServicioRompecabezasFei : IServicioJugador
    {
        private readonly ConcurrentDictionary<string, CuentaJugador> jugadoresActivos = 
            new ConcurrentDictionary<string, CuentaJugador>();

        private readonly ConcurrentDictionary<string, Logica.Sala> salas = 
            new ConcurrentDictionary<string, Logica.Sala>();

        public bool RegistrarJugador(CuentaJugador cuentaJugador)
        {
            bool operacionRealizada = false;

            lock (Bloqueador.BloqueoParaRegistrarJugador)
            {
                if (!ExisteNombreJugadorRegistrado(cuentaJugador.NombreJugador))
                {
                    try
                    {
                        operacionRealizada = AccesoCuentaJugador.
                            RegistrarJugador(cuentaJugador);
                    }
                    catch (EntityException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                }
            }

            return operacionRealizada;
        }

        public bool ActualizarNombreJugador(string nombreAnterior, 
            string nuevoNombre)
        {
            bool operacionRealizada = false;

            lock (Bloqueador.BloqueoParaActualizarNombreJugador)
            {
                if (!ExisteNombreJugadorRegistrado(nuevoNombre))
                {
                    try
                    {
                        operacionRealizada = AccesoCuentaJugador.
                            ActualizarNombreJugador(nombreAnterior, nuevoNombre);
                    }
                    catch (EntityException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }

                    if (operacionRealizada)
                    {
                        ReflejarCambiosNombreJugadorActualizado(nombreAnterior, nuevoNombre);
                    }
                }
            }

            return operacionRealizada;
        }        

        public bool ActualizarNumeroAvatar(string nombreJugador, 
            int nuevoNumeroAvatar)
        {
            bool operacionRealizada = false;

            try
            {
                operacionRealizada = AccesoCuentaJugador.
                    ActualizarNumeroAvatar(nombreJugador, nuevoNumeroAvatar);
            }
            catch (EntityException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }

            if (operacionRealizada)
            {
                ReflejarCambiosNumeroAvatarActualizado(nombreJugador, nuevoNumeroAvatar);
            }

            return operacionRealizada;
        }

        public bool ActualizarContrasena(string correo, string contrasena)
        {
            bool operacionRealizada = false;

            try
            {
                operacionRealizada = AccesoCuentaJugador.
                    ActualizacionContrasena(contrasena, correo);
            }
            catch (EntityException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }

            return operacionRealizada;
        }

        public bool EsLaMismaContrasenaDeJugador(string nombreJugador, 
            string contrasena)
        {
            bool hayCoincidencias = false;

            try
            {
                hayCoincidencias = AccesoCuentaJugador.
                    HayCoincidenciasEnContrasenaDeJugador(
                    nombreJugador, contrasena);
            }
            catch (EntityException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }

            return hayCoincidencias;
        }

        public bool ExisteNombreJugadorRegistrado(string nombreJugador)
        {
            bool hayExistencias = false;
            
            try
            {
                hayExistencias = AccesoCuentaJugador.
                    ExisteNombreJugadorRegistrado(nombreJugador);
            }
            catch (EntityException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }

            return hayExistencias;
        }

        public CuentaJugador IniciarSesionComoJugador(string nombreJugador, 
            string contrasena)
        {            
            CuentaJugador cuentaRecuperada = null;
            
            lock (Bloqueador.BloqueoParaIniciarSesionComoJugador)
            {
                if (ExisteNombreJugadorRegistrado(nombreJugador) && 
                    !jugadoresActivos.ContainsKey(nombreJugador))
                {
                    try
                    {
                        cuentaRecuperada = AccesoCuentaJugador.
                            IniciarSesion(nombreJugador, contrasena);
                    }
                    catch (EntityException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                }

                if (cuentaRecuperada != null)
                {
                    cuentaRecuperada.ContextoOperacionConexion = 
                        OperationContext.Current;
                    cuentaRecuperada.TipoInterfazCallback = 
                        typeof(IServicioJugadorCallback);
                    cuentaRecuperada.ContextoOperacionConexion.Channel.Faulted += 
                        (objetoOrigen, evento) => 
                        CerrarSesion(cuentaRecuperada.NombreJugador);
                    jugadoresActivos.TryAdd(cuentaRecuperada.
                        NombreJugador, cuentaRecuperada);
                }
            }

            return cuentaRecuperada;
        }

        public CuentaJugador IniciarSesionComoInvitado(string nombreInvitado)
        {
            CuentaJugador cuentaInvitado = null;

            lock (Bloqueador.BloqueoParaIniciarSesionComoInvitado)
            {
                if (!jugadoresActivos.ContainsKey(nombreInvitado))
                {
                    cuentaInvitado = new CuentaJugador()
                    {
                        NombreJugador = nombreInvitado,
                        NumeroAvatar = (int)NumeroAvatar.Invitado,
                        EsInvitado = true,
                        Puntaje = Pieza.PuntajeVacio,
                        Estado = EstadoJugador.Conectado,
                        ContextoOperacionConexion = OperationContext.Current,
                        TipoInterfazCallback = typeof(IServicioJugadorCallback)
                    };
                    cuentaInvitado.ContextoOperacionConexion.
                        Channel.Faulted +=(objetoOrigen, evento) =>
                        CerrarSesion(cuentaInvitado.NombreJugador);
                    jugadoresActivos.TryAdd(cuentaInvitado.NombreJugador, 
                        cuentaInvitado);
                }
            }

            return cuentaInvitado;
        }

        public void CerrarSesion(string nombreJugador)
        {
            lock (Bloqueador.BloqueoParaCerrarSesion)
            {
                if (jugadoresActivos.ContainsKey(nombreJugador))
                {
                    string codigoSala = ObtenerCodigoSalaDeJugador(nombreJugador);

                    switch (jugadoresActivos[nombreJugador].Estado)
                    {
                        case EstadoJugador.EnSala:
                            RemoverJugadorDeSala(nombreJugador, codigoSala);
                            break;

                        case EstadoJugador.EnPartida:
                            RemoverJugadorDePartida(nombreJugador, codigoSala);
                            break;
                    }

                    CambiarEstadoJugador(nombreJugador, EstadoJugador.Desconectado);
                }
            }
        }        
    }
    #endregion

    #region IServicioCorreo 
    public partial class ServicioRompecabezasFei : IServicioCorreo
    {
        public bool ExisteCorreoRegistrado(string correo)
        {
            bool operacionRealizada = false;

            try
            {
                operacionRealizada = AccesoCuentaJugador.
                    ExisteCorreoRegistrado(correo);
            }
            catch (EntityException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }

            return operacionRealizada;
        }

        public bool EnviarMensajeACorreo(string encabezado, string correo,
            string asunto, string mensaje)
        {
            bool operacionRealizada = false;

            try
            {
                operacionRealizada = GeneradorMensajeCorreo.EnviarMensaje(
                    encabezado, correo, asunto, mensaje);
            }
            catch (EntityException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }

            return operacionRealizada;
        }
    }
    #endregion

    #region IServicioAmistades
    public partial class ServicioRompecabezasFei : IServicioAmistades
    {
        public List<CuentaJugador> ObtenerAmigosDeJugador(string nombreJugador)
        {
            List<CuentaJugador> cuentasJugador = new List<CuentaJugador>();

            try
            {
                cuentasJugador = AccesoAmistad.
                    ObtenerAmigosDeJugador(nombreJugador);
            }
            catch (EntityException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }

            if (cuentasJugador.Any())
            {
                cuentasJugador = AgregarConectividadAJugadores(cuentasJugador);
            }

            return cuentasJugador;
        }

        public List<CuentaJugador> ObtenerJugadoresConSolicitudPendiente(
            string nombreJugador)
        {
            List<CuentaJugador> cuentasJugador = new List<CuentaJugador>();

            try
            {
                cuentasJugador = AccesoSolicitudAmistad.
                    ObtenerJugadoresOrigenSolicitudAmistadPendiente(nombreJugador);
            }
            catch (EntityException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }

            if (cuentasJugador.Any())
            {
                cuentasJugador = AgregarConectividadAJugadores(cuentasJugador);
            }

            return cuentasJugador;
        }

        public bool EnviarSolicitudDeAmistad(string nombreJugadorOrigen,
            string nombreJugadorDestino)
        {            
            bool operacionRealizada = false;

            try
            {
                operacionRealizada = AccesoSolicitudAmistad.
                    CrearNuevaSolicitudDeAmistad(nombreJugadorOrigen, 
                    nombreJugadorDestino);
            }
            catch (EntityException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }

            try
            {
                if (operacionRealizada && jugadoresActivos[nombreJugadorDestino].
                TipoInterfazCallback == typeof(IServicioAmistadesCallback))
                {
                    try
                    {
                        jugadoresActivos[nombreJugadorDestino].ContextoOperacion?.
                            GetCallbackChannel<IServicioAmistadesCallback>().
                            MostrarSolicitudDeAmistadRecibida(
                            jugadoresActivos[nombreJugadorOrigen]);
                    }
                    catch (CommunicationException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                    catch (InvalidCastException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                }
            }
            catch (KeyNotFoundException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }
           
            return operacionRealizada;
        }

        public bool AceptarSolicitudDeAmistad(string nombreJugadorOrigen,
            string nombreJugadorDestino)
        {
            bool operacionRealizada = false;

            lock (Bloqueador.BloqueoParaAceptarSolicitudDeAmistad)
            {
                if (!ExisteAmistadConJugador(nombreJugadorOrigen, nombreJugadorDestino))
                {
                    operacionRealizada = AccesoAmistad.
                        RegistrarNuevaAmistad(nombreJugadorOrigen, nombreJugadorDestino);
                }

                if (ExisteSolicitudDeAmistad(nombreJugadorOrigen, nombreJugadorDestino))
                {
                    try
                    {
                        AccesoSolicitudAmistad.EliminarSolicitudDeAmistad(
                            nombreJugadorOrigen, nombreJugadorDestino);
                    }
                    catch (EntityException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                }

                if (ExisteSolicitudDeAmistad(nombreJugadorDestino, nombreJugadorOrigen))
                {
                    try
                    {
                        AccesoSolicitudAmistad.EliminarSolicitudDeAmistad(
                            nombreJugadorDestino, nombreJugadorOrigen);
                    }
                    catch (EntityException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                }
            }

            if (operacionRealizada)
            {
                MostrarNuevoAmigoAJugador(nombreJugadorOrigen, nombreJugadorDestino);
            }

            return operacionRealizada;
        }

        public bool EliminarAmistad(string nombreJugadorA,
            string nombreJugadorB)
        {
            bool operacionRealizada = false;

            try
            {
                operacionRealizada = AccesoAmistad.
                    EliminarAmistad(nombreJugadorA, nombreJugadorB);
            }
            catch (EntityException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }

            if (operacionRealizada)
            {
                MostrarEliminacionDeAmigoAJugador(nombreJugadorB, nombreJugadorA);
            }

            return operacionRealizada;
        }

        public bool RechazarSolicitudDeAmistad(string nombreJugadorOrigen,
            string nombreJugadorDestino)
        {
            bool operacionRealizada = false;

            try
            {
                operacionRealizada = AccesoSolicitudAmistad.
                    EliminarSolicitudDeAmistad(nombreJugadorOrigen, 
                    nombreJugadorDestino);
            }
            catch (EntityException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }

            return operacionRealizada;
        }

        public bool ExisteSolicitudDeAmistad(string nombreJugadorOrigen,
            string nombreJugadorDestino)
        {
            bool hayExistencias = false;

            try
            {
                hayExistencias = AccesoSolicitudAmistad.
                    ExisteSolicitudDeAmistad(nombreJugadorOrigen, 
                    nombreJugadorDestino);
            }
            catch (EntityException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }

            return hayExistencias;
        }

        public bool ExisteAmistadConJugador(string nombreJugadorA, 
            string nombreJugadorB)
        {
            bool hayExistencias = false;

            try
            {
                hayExistencias = AccesoAmistad.
                    ExisteAmistad(nombreJugadorA, nombreJugadorB);
            }
            catch (EntityException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }

            return hayExistencias;
        }

        public void ActivarNotificacionesDeAmistades(string nombreJugador)
        {
            if (AsignarCallbackActual(nombreJugador, typeof(IServicioAmistadesCallback)))
            {
                CambiarEstadoJugador(nombreJugador, EstadoJugador.Disponible);
            }
        }

        public void DesactivarNotificacionesDeAmistades(string nombreJugador)
        {
            LiberarCallbackActual(nombreJugador);
        }
    }
    #endregion

    #region IServicioInvitaciones
    public partial class ServicioRompecabezasFei : IServicioInvitaciones
    {
        public void ActivarInvitacionesDeSala(string nombreJugador)
        {
            if (AsignarCallbackActual(nombreJugador, typeof(IServicioInvitacionesCallback)))
            {
                CambiarEstadoJugador(nombreJugador, EstadoJugador.Disponible);
            }
        }

        public void DesactivarInvitacionesDeSala(string nombreJugador)
        {
            LiberarCallbackActual(nombreJugador);
        }
    }
    #endregion

    #region IServicioSala
    public partial class ServicioRompecabezasFei : IServicioSala
    {
        public bool CrearNuevaSala(string nombreAnfitrion, string codigoSala)
        {
            bool operacionRealizada = false;

            try
            {
                operacionRealizada = AccesoSala.
                    CrearNuevaSala(codigoSala);
            }
            catch (EntityException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }

            if (operacionRealizada)
            {
                Logica.Sala nuevaSala = new Logica.Sala()
                {
                    Partida = null,
                    HayPartidaCreada = false,
                    CodigoSala = codigoSala,
                    NombreAnfitrion = nombreAnfitrion,
                    ContadorJugadores = Logica.Sala.CantidadJugadoresVacia,
                    NombresDeJugadores = new ConcurrentDictionary<string, string>(),                    
                };
                operacionRealizada = salas.TryAdd(codigoSala, nuevaSala);
            }

            return operacionRealizada;
        }

        // Devuelve al anfitrión de la sala solo si sigue conectado y está en la
        // pantalla de sala; en cualquier otro caso devuelve null
        private CuentaJugador ObtenerAnfitrionEnSala(string codigoSala)
        {
            CuentaJugador anfitrion = null;

            if (salas.TryGetValue(codigoSala, out Logica.Sala sala) &&
                sala.NombreAnfitrion != null &&
                jugadoresActivos.TryGetValue(sala.NombreAnfitrion,
                out CuentaJugador cuentaAnfitrion) &&
                cuentaAnfitrion.TipoInterfazCallback == typeof(IServicioSalaCallback))
            {
                anfitrion = cuentaAnfitrion;
            }

            return anfitrion;
        }

        // Anfitriones que siguen conectados, están en la pantalla de sala
        // y son amigos del jugador indicado
        private List<CuentaJugador> ObtenerAnfitrionesAmigosEnSala(string nombreJugador)
        {
            var anfitriones = new List<CuentaJugador>();

            foreach (var nombreAnfitrion in ObtenerNombresDeAnfitrionesEnSala())
            {
                if (nombreAnfitrion != null &&
                    jugadoresActivos.TryGetValue(nombreAnfitrion, out CuentaJugador anfitrion) &&
                    anfitrion.TipoInterfazCallback == typeof(IServicioSalaCallback) &&
                    ExisteAmistadConJugador(nombreJugador, nombreAnfitrion))
                {
                    anfitriones.Add(anfitrion);
                }
            }

            return anfitriones;
        }

        public bool ExisteSalaDisponible(string codigoSala)
        {
            return salas.ContainsKey(codigoSala) && salas[codigoSala].
                ContadorJugadores < Logica.Sala.CantidadMaximaJugadores && 
                !salas[codigoSala].HayPartidaCreada;
        }

        public List<CuentaJugador> ObtenerJugadoresEnSala(string codigoSala)
        {
            return jugadoresActivos.Values.Where(jugador =>
                salas[codigoSala].NombresDeJugadores.
                ContainsKey(jugador.NombreJugador)).ToList();
        }

        public bool UnirseASala(string nombreJugador, string codigoSala)
        {
            bool operacionRealizada = false;

            lock (Bloqueador.BloqueoParaUnirseASala)
            {
                if (EsJugadorAnfitrionDeSala(nombreJugador, codigoSala) &&
                    salas[codigoSala].HayPartidaCreada &&
                    salas[codigoSala].Partida.Estado == EstadoPartida.Finalizada)
                {
                    HabilitarRegresoASalaAJugadoresEnPartida(codigoSala);
                }

                if (jugadoresActivos.ContainsKey(nombreJugador) &&
                    salas.TryGetValue(codigoSala, out Logica.Sala sala) &&
                    sala.NombresDeJugadores.ContainsKey(nombreJugador))
                {
                    ActivarNotificacionesDeSala(nombreJugador);
                    CambiarEstadoJugador(nombreJugador, EstadoJugador.EnSala);
                    operacionRealizada = true;
                }
                else if (ExisteSalaDisponible(codigoSala) &&
                    jugadoresActivos.ContainsKey(nombreJugador))
                {
                    ActivarNotificacionesDeSala(nombreJugador);
                    CambiarEstadoJugador(nombreJugador, EstadoJugador.EnSala);
                    MostrarNuevoJugadorActivoEnSala(nombreJugador, codigoSala);
                    operacionRealizada = salas[codigoSala].
                        AgregarNombreDeJugador(nombreJugador);

                    if (salas[codigoSala].HayCantidadJugadoresMinimaParaPartida())
                    {
                        HabilitarInicioDePartida(codigoSala);
                    }
                }
            }

            return operacionRealizada;
        }

        public void ActivarNotificacionesDeSala(string nombreJugador)
        {
            AsignarCallbackActual(nombreJugador, typeof(IServicioSalaCallback));
        }

        public void DesactivarNotificacionesDeSala(string nombreJugador)
        {
            LiberarCallbackActual(nombreJugador);
        }

        public void AbandonarSala(string nombreJugador, string codigoSala)
        {
            if (RemoverJugadorDeSala(nombreJugador, codigoSala))
            {
                CambiarEstadoJugador(nombreJugador, EstadoJugador.Disponible);
                DesactivarNotificacionesDeSala(nombreJugador);
            }
        }

        public void EnviarMensajeDeSala(string nombreJugador, string codigoSala,
            string mensaje)
        {
            string mensajeFormateado = DateTime.Now.ToShortTimeString() +
                $" {nombreJugador}: {mensaje}";

            if (salas.TryGetValue(codigoSala, out Logica.Sala sala))
            {
                sala.AgregarMensaje(mensajeFormateado);
            }

            foreach (var jugador in ObtenerJugadoresEnSala(codigoSala))
            {
                try
                {
                    jugador.ContextoOperacion?.
                        GetCallbackChannel<IServicioSalaCallback>().
                        MostrarMensajeDeSala(mensajeFormateado);
                }
                catch (CommunicationException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
                catch (InvalidCastException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
            }
        }

        public List<string> ObtenerMensajesDeSala(string codigoSala)
        {
            List<string> mensajes = new List<string>();

            if (salas.TryGetValue(codigoSala, out Logica.Sala sala))
            {
                mensajes = sala.ObtenerMensajes();
            }

            return mensajes;
        }

        public string GenerarCodigoParaNuevaSala()
        {
            return Guid.NewGuid().ToString();
        }

        public void ConvertirJugadorEnAnfitrionDesdeSala(string nombreJugador, string codigoSala)
        {
            ConvertirJugadorEnAnfitrion(nombreJugador, codigoSala,
                typeof(IServicioSalaCallback));
        }

        public void InvitarJugador(string nombreJugador,
            string nombreAnfitrion, string codigoSala)
        {
            if (jugadoresActivos.TryGetValue(nombreJugador, out CuentaJugador jugador) &&
                jugador.TipoInterfazCallback == typeof(IServicioInvitacionesCallback))
            {
                try
                {
                    jugador.ContextoOperacion?.
                        GetCallbackChannel<IServicioInvitacionesCallback>().
                        MostrarInvitacionDeSala(nombreAnfitrion, codigoSala);
                }
                catch (CommunicationException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
                catch (InvalidCastException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
            }
        }

        public void ExpulsarJugadorEnSala(string nombreJugadorExpulsion,
            string codigoSala)
        {
            if (jugadoresActivos.TryGetValue(nombreJugadorExpulsion,
                out CuentaJugador jugadorExpulsado) &&
                jugadorExpulsado.TipoInterfazCallback == typeof(IServicioSalaCallback))
            {
                try
                {
                    jugadorExpulsado.ContextoOperacion?.
                        GetCallbackChannel<IServicioSalaCallback>().
                        MostrarMensajeExpulsionDeSala();
                }
                catch (CommunicationException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
                catch (InvalidCastException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
            }

            // RemoverJugadorDeSala ya avisa a los demás jugadores y además
            // oculta el botón de iniciar partida si quedan menos de 2
            RemoverJugadorDeSala(nombreJugadorExpulsion, codigoSala);
        }

        public List<CuentaJugador> ObtenerAmigosDisponibles(string nombreAnfitrion)
        {
            return jugadoresActivos.Values.Where(jugador => !jugador.EsInvitado &&
                ExisteAmistadConJugador(nombreAnfitrion, jugador.NombreJugador) &&
                jugador.Estado == EstadoJugador.Disponible).ToList();
        }
    }
    #endregion

    #region IServicioPartida
    public partial class ServicioRompecabezasFei : IServicioPartida
    {
        public bool CrearNuevaPartida(string codigoSala, 
            DificultadPartida dificultad, int numeroImagen)
        {
            bool operacionRealizada = false;

            // Los demás jugadores pudieron salir mientras el anfitrión estaba
            // en la página de creación, donde no recibe avisos de la sala
            if (!salas.TryGetValue(codigoSala, out Logica.Sala sala) ||
                !sala.HayCantidadJugadoresMinimaParaPartida())
            {
                return false;
            }

            try
            {
                operacionRealizada = AccesoPartida.
                    CrearNuevaPartida(codigoSala, dificultad);
            }
            catch (EntityException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }

            if (operacionRealizada)
            {
                salas[codigoSala].Partida = new Logica.Partida
                {
                    PuntajesDeJugadores = new ConcurrentDictionary<string, int>(),
                    ConfirmacionesJugadores = 
                        new ConcurrentDictionary<string, bool>(),
                    Dificultad = dificultad,
                    Estado = EstadoPartida.SinIniciar,
                };
                salas[codigoSala].HayPartidaCreada = true;
                salas[codigoSala].Partida.Tablero.
                    NumeroImagenRompecabezas = numeroImagen;
                RegistrarNombresDeJugadoresEnPartida(codigoSala);
                NotificarCreacionDePartidaAJugadores(codigoSala);
            }

            return operacionRealizada;
        }

        public void UnirseAPartida(string codigoSala, string nombreJugador)
        {
            if (AsignarCallbackActual(nombreJugador, typeof(IServicioPartidaCallback)) &&
                salas.ContainsKey(codigoSala) && salas[codigoSala].HayPartidaCreada &&
                salas[codigoSala].RemoverNombreDeJugador(nombreJugador))
            {
                salas[codigoSala].Partida.ConfirmacionesJugadores[nombreJugador] = true;
                CambiarEstadoJugador(nombreJugador, EstadoJugador.EnPartida);
            }
        }

        public List<CuentaJugador> ObtenerJugadoresConPresenciaSinConfirmarEnPartida(
            string codigoSala)
        {
            return jugadoresActivos.Values.Where(jugador =>
                salas[codigoSala].HayPartidaCreada && salas[codigoSala].
                Partida.PuntajesDeJugadores.
                ContainsKey(jugador.NombreJugador)).ToList();
        }

        public void AbandonarPartida(string codigoSala, string nombreJugador)
        {
            RemoverJugadorDePartida(nombreJugador, codigoSala);
            CambiarEstadoJugador(nombreJugador, EstadoJugador.Disponible);
            LiberarCallbackActual(nombreJugador);
        }

        public void CancelarPartida(string codigoSala)
        {
            foreach (var jugadorEnPartida in
                ObtenerJugadoresConPresenciaConfirmadaEnPartida(codigoSala))
            {
                try
                {
                    jugadorEnPartida.ContextoOperacion?.
                        GetCallbackChannel<IServicioPartidaCallback>().
                        MostrarMensajePartidaCancelada();
                }
                catch (CommunicationException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
                catch (InvalidCastException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
            }

            salas[codigoSala].Partida.Estado = EstadoPartida.Cancelada;
        }

        public void IniciarPartida(string codigoSala)
        {
            RemoverJugadoresSinPresenciaConfirmadaEnPartida(codigoSala);
            var jugadoresEnPartida = 
                ObtenerJugadoresConPresenciaConfirmadaEnPartida(codigoSala);

            if (jugadoresEnPartida.Count() >= 
                Logica.Sala.CantidadMinimaJugadoresParaPartida)
            {
                salas[codigoSala].Partida.Estado = EstadoPartida.EnCurso;

                foreach (var jugador in jugadoresEnPartida)
                {
                    try
                    {
                        jugador.ContextoOperacion?.
                            GetCallbackChannel<IServicioPartidaCallback>().
                            MostrarTableroDePartida(salas[codigoSala].Partida.Tablero);
                    }
                    catch (CommunicationException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                    catch (InvalidCastException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                }
            }
            else
            {
                foreach (var jugador in jugadoresEnPartida)
                {
                    try
                    {
                        jugador.ContextoOperacion?.
                            GetCallbackChannel<IServicioPartidaCallback>().
                            MostrarMensajePartidaCancelada();
                    }
                    catch (CommunicationException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                    catch (InvalidCastException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                }
            }
        }        

        public void BloquearPieza(string codigoSala, int numeroPieza, 
            string nombreJugador)
        {
            lock (Bloqueador.BloqueoParaBloquearPieza)
            {
                if (!salas[codigoSala].Partida.Tablero.
                    Piezas[numeroPieza].EstaBloqueada)
                {
                    salas[codigoSala].Partida.Tablero.
                        Piezas[numeroPieza].EstaBloqueada = true;

                    foreach (var jugadorEnPartida in
                        ObtenerJugadoresConPresenciaConfirmadaEnPartida(codigoSala))
                    {
                        try
                        {
                            jugadorEnPartida.ContextoOperacion?.
                                GetCallbackChannel<IServicioPartidaCallback>().
                                MostrarBloqueoDePieza(numeroPieza, nombreJugador);
                        }
                        catch (CommunicationException excepcion)
                        {
                            Registros.Registrador.EscribirRegistro(excepcion);
                        }
                        catch (InvalidCastException excepcion)
                        {
                            Registros.Registrador.EscribirRegistro(excepcion);
                        }
                    }
                }
            }
        }

        public void DesbloquearPieza(string codigoSala, int numeroPieza, 
            string nombreJugador)
        {
            salas[codigoSala].Partida.Tablero.
                Piezas[numeroPieza].EstaBloqueada = false;

            foreach (var jugadorEnPartida in
                ObtenerJugadoresConPresenciaConfirmadaEnPartida(codigoSala))
            {
                try
                {
                    jugadorEnPartida.ContextoOperacion?.
                        GetCallbackChannel<IServicioPartidaCallback>().
                        MostrarDesbloqueoDePieza(numeroPieza, nombreJugador);
                }
                catch (CommunicationException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
                catch (InvalidCastException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
            }
        }

        public void ColocarPieza(string codigoSala, int numeroPieza, 
            string nombreJugador, Posicion posicion)
        {
            lock (Bloqueador.BloqueoParaColocarPieza)
            {
                int puntaje = salas[codigoSala].Partida.
                    Tablero.Piezas[numeroPieza].Puntaje;
                salas[codigoSala].Partida.
                    PuntajesDeJugadores[nombreJugador] += puntaje;
                salas[codigoSala].Partida.Tablero.
                    Piezas[numeroPieza].EstaDentroDeCelda = true;
            
                foreach (var jugadorEnPartida in
                    ObtenerJugadoresConPresenciaConfirmadaEnPartida(codigoSala))
                {
                    try
                    {
                        jugadorEnPartida.ContextoOperacion?.
                            GetCallbackChannel<IServicioPartidaCallback>().
                            MostrarColocacionDePieza(numeroPieza, nombreJugador, 
                            puntaje, posicion);
                    }
                    catch (CommunicationException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                    catch (InvalidCastException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                }

                if (salas[codigoSala].Partida.
                    Tablero.EsRompecabezasCompletado())
                {
                    FinalizarPartida(codigoSala);                    
                }
            }
        }        

        public int ObtenerNumeroDePartidasJugadas(string nombreJugador)
        {
            int numeroPartidasJugadas = 0;

            try
            {
                numeroPartidasJugadas = AccesoPartida.
                    ObtenerNumeroPartidasJugadas(nombreJugador);
            }
            catch (EntityException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }

            return numeroPartidasJugadas;
        }

        public int ObtenerNumeroDePartidasGanadas(string nombreJugador)
        {
            int numeroPartidasGanadas = 0;

            try
            {
                numeroPartidasGanadas = AccesoPartida.
                    ObtenerNumeroPartidasGanadas(nombreJugador);
            }
            catch (EntityException excepcion)
            {
                Registros.Registrador.EscribirRegistro(excepcion);
            }

            return numeroPartidasGanadas;
        }

        public void ExpulsarJugadorEnPartida(string nombreJugadorExpulsion,
            string codigoSala)
        {
            if (jugadoresActivos.TryGetValue(nombreJugadorExpulsion,
                out CuentaJugador jugadorExpulsado) &&
                jugadorExpulsado.TipoInterfazCallback == typeof(IServicioPartidaCallback))
            {
                try
                {
                    jugadorExpulsado.ContextoOperacion?.
                        GetCallbackChannel<IServicioPartidaCallback>().
                        MostrarMensajeExpulsionDePartida();
                }
                catch (CommunicationException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
                catch (InvalidCastException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
            }

            // RemoverJugadorDePartida avisa a los demás y cancela la partida
            // solo si quedan menos jugadores que el mínimo en la partida
            RemoverJugadorDePartida(nombreJugadorExpulsion, codigoSala);
            CambiarEstadoJugador(nombreJugadorExpulsion, EstadoJugador.Disponible);
        }

        public void ConvertirJugadorEnAnfitrionDesdePartida(string nombreJugador, string codigoSala)
        {
            ConvertirJugadorEnAnfitrion(nombreJugador, codigoSala,
                typeof(IServicioPartidaCallback));
        }
    }
    #endregion

    #region Métodos privados
    public partial class ServicioRompecabezasFei
    {
        private void CambiarEstadoJugador(string nombreJugador, EstadoJugador nuevoEstado)
        {
            // Si el jugador ya no está activo (por ejemplo, se desconectó
            // mientras se procesaba otra petición) no hay estado que cambiar
            if (jugadoresActivos.TryGetValue(nombreJugador, out CuentaJugador jugador))
            {
                EstadoJugador estadoAnterior = jugador.Estado;

                switch (nuevoEstado)
                {
                    case EstadoJugador.Conectado:
                    case EstadoJugador.EnSala:
                    case EstadoJugador.EnPartida:

                        if (estadoAnterior == EstadoJugador.Disponible)
                        {
                            NotificarAnfitrionesSobreAmigoNoDisponible(nombreJugador);
                        }
                        else
                        {
                            NotificarAmigosSobreCambioEstadoJugador(nombreJugador, nuevoEstado);
                        }

                        break;

                    case EstadoJugador.Desconectado:
                        jugadoresActivos.TryRemove(nombreJugador, out _);
                        NotificarAmigosSobreCambioEstadoJugador(nombreJugador, nuevoEstado);
                        break;

                    case EstadoJugador.Disponible:
                        NotificarAnfitrionesSobreAmigoDisponible(nombreJugador);
                        NotificarAmigosSobreCambioEstadoJugador(nombreJugador, nuevoEstado);

                        break;
                }

                if (nuevoEstado != EstadoJugador.Desconectado)
                {
                    jugador.Estado = nuevoEstado;
                }
            }
        }

        // Quita la conexión de callbacks del jugador, pero SOLO si la petición
        // llega por esa misma conexión. Las páginas usan conexiones distintas y
        // el servidor puede recibir sus peticiones en desorden: sin esta
        // revisión, el "Desactivar" atrasado de la página anterior podría borrar
        // la conexión que la página nueva acaba de registrar
        private void LiberarCallbackActual(string nombreJugador)
        {
            if (jugadoresActivos.TryGetValue(nombreJugador, out CuentaJugador jugador) &&
                jugador.ContextoOperacion?.Channel == OperationContext.Current?.Channel)
            {
                jugador.ContextoOperacion = null;
                jugador.TipoInterfazCallback = typeof(IServicioJugadorCallback);
            }
        }

        // Guarda la conexión de la petición actual como la conexión por la que
        // se le enviarán callbacks al jugador (la de la pantalla que tiene abierta)
        private bool AsignarCallbackActual(string nombreJugador, Type tipoInterfazCallback)
        {
            bool asignacionRealizada = false;

            if (jugadoresActivos.TryGetValue(nombreJugador, out CuentaJugador jugador))
            {
                jugador.ContextoOperacion = OperationContext.Current;
                jugador.TipoInterfazCallback = tipoInterfazCallback;
                asignacionRealizada = true;
            }

            return asignacionRealizada;
        }

        private void ConvertirJugadorEnAnfitrion(string nombreJugador,
            string codigoSala, Type tipoInterfazCallbackEsperado)
        {
            if (salas.TryGetValue(codigoSala, out Logica.Sala sala))
            {
                bool hayPartidaCreada =
                    tipoInterfazCallbackEsperado == typeof(IServicioPartidaCallback);
                bool avisoEntregado =
                    jugadoresActivos.TryGetValue(nombreJugador, out CuentaJugador jugador) &&
                    jugador.TipoInterfazCallback == tipoInterfazCallbackEsperado &&
                    NotificarFuncionesDeAnfitrion(jugador, hayPartidaCreada);

                if (avisoEntregado)
                {
                    // Solo se cambia el anfitrión si el jugador elegido
                    // realmente recibió el aviso
                    sala.NombreAnfitrion = nombreJugador;
                }
                else
                {
                    // El anfitrión anterior ya ocultó sus funciones en su pantalla,
                    // así que hay que elegir a alguien para que la sala
                    // no se quede sin anfitrión
                    ElegirNuevoAnfitrion(codigoSala);
                }
            }
        }

        private List<CuentaJugador> AgregarConectividadAJugadores(
            List<CuentaJugador> cuentasJugador)
        {
            var cuentasConEstadoConectividad = new List<CuentaJugador>();

            foreach (CuentaJugador cuenta in cuentasJugador)
            {
                if (jugadoresActivos.ContainsKey(cuenta.NombreJugador))
                {
                    cuenta.Estado = jugadoresActivos
                        [cuenta.NombreJugador].Estado;
                }
                else
                {
                    cuenta.Estado = EstadoJugador.Desconectado;
                }

                cuentasConEstadoConectividad.Add(cuenta);
            }

            return cuentasConEstadoConectividad;
        }

        private List<CuentaJugador> ObtenerAmigosConectados(string nombreJugador)
        {
            return jugadoresActivos.Values.Where(jugador => 
                !jugador.EsInvitado && ExisteAmistadConJugador(nombreJugador, 
                jugador.NombreJugador)).ToList();
        }

        private bool RemoverJugadorDeSala(string nombreJugador, string codigoSala)
        {
            bool operacionRealizada = false;

            if (salas.ContainsKey(codigoSala) && salas[codigoSala].
                RemoverNombreDeJugador(nombreJugador))
            {
                operacionRealizada = true;

                foreach (var jugadorEnSala in 
                    ObtenerJugadoresEnSala(codigoSala))
                {
                    try
                    {
                        jugadorEnSala.ContextoOperacion?.
                            GetCallbackChannel<IServicioSalaCallback>().
                            MostrarDesconexionDeJugadorEnSala(nombreJugador);
                    }
                    catch (CommunicationException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                    catch (InvalidCastException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                }

                if (!salas[codigoSala].EstaVacia())
                {
                    if (EsJugadorAnfitrionDeSala(nombreJugador, codigoSala))
                    {
                        ElegirNuevoAnfitrion(codigoSala);
                    }
                    
                    if (!salas[codigoSala].HayCantidadJugadoresMinimaParaPartida())
                    {
                        DeshabilitarInicioDePartida(codigoSala);
                    }
                }
                else
                {
                    salas.TryRemove(codigoSala, out _);
                }
            }

            return operacionRealizada;
        }

        private void RemoverJugadorDePartida(string nombreJugador, 
            string codigoSala)
        {
            if (salas.ContainsKey(codigoSala) && salas[codigoSala].HayPartidaCreada && 
                salas[codigoSala].Partida.RemoverNombreDeJugador(nombreJugador))
            {
                foreach (var jugadorEnPartida in
                    ObtenerJugadoresConPresenciaConfirmadaEnPartida(codigoSala))
                {
                    try
                    {
                        jugadorEnPartida.ContextoOperacion?.
                            GetCallbackChannel<IServicioPartidaCallback>().
                            MostrarDesconexionDeJugadorEnPartida(nombreJugador);
                    }
                    catch (CommunicationException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                    catch (InvalidCastException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                }

                if (!salas[codigoSala].Partida.
                    HayCantidadMinimaJugadoresParaPartida() && 
                    (salas[codigoSala].Partida.Estado == EstadoPartida.SinIniciar ||
                    salas[codigoSala].Partida.Estado == EstadoPartida.EnCurso))
                {
                    CancelarPartida(codigoSala);
                }
                else if (EsJugadorAnfitrionDeSala(nombreJugador, codigoSala))
                {
                    ElegirNuevoAnfitrion(codigoSala);
                }
            }
        }

        private void NotificarCreacionDePartidaAJugadores(string codigoSala)
        {
            salas[codigoSala].Partida.Estado = EstadoPartida.EnCurso;

            foreach (var jugadorEnSala in 
                ObtenerJugadoresEnSala(codigoSala).Where(jugador => 
                jugador.NombreJugador != salas[codigoSala].NombreAnfitrion))
            {
                try
                {
                    jugadorEnSala.ContextoOperacion?.
                        GetCallbackChannel<IServicioSalaCallback>().
                        MostrarNuevaPartida();
                }
                catch (CommunicationException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
                catch (InvalidCastException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
            }
        }

        private void HabilitarInicioDePartida(string codigoSala)
        {
            CuentaJugador anfitrion = ObtenerAnfitrionEnSala(codigoSala);

            if (anfitrion != null)
            {
                try
                {
                    anfitrion.ContextoOperacion?.
                        GetCallbackChannel<IServicioSalaCallback>().
                        HabilitarInicioDePartida();
                }
                catch (CommunicationException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
                catch (InvalidCastException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
            }
        }

        private void DeshabilitarInicioDePartida(string codigoSala) //VERIFICAR
        {
            if (jugadoresActivos[salas[codigoSala].NombreAnfitrion].
                TipoInterfazCallback == typeof(IServicioSalaCallback))
            {
                try
                {
                    jugadoresActivos[salas[codigoSala].
                        NombreAnfitrion].ContextoOperacion?.
                        GetCallbackChannel<IServicioSalaCallback>().
                        DeshabilitarInicioDePartida();
                }
                catch (CommunicationException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
                catch (InvalidCastException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
            }
        }

        private void ElegirNuevoAnfitrion(string codigoSala)
        {
            if (salas.TryGetValue(codigoSala, out Logica.Sala sala))
            {
                bool hayPartidaCreada = sala.HayPartidaCreada;
                List<CuentaJugador> candidatos = hayPartidaCreada ?
                    ObtenerJugadoresConPresenciaConfirmadaEnPartida(codigoSala) :
                    ObtenerJugadoresEnSala(codigoSala);
                CuentaJugador nuevoAnfitrion = null;

                // Se prueba con cada candidato, uno por uno. Si la conexión de
                // un candidato está muerta, se pasa al siguiente en lugar de
                // volver a llamar a este método (antes eso causaba recursión
                // infinita, porque siempre se volvía a elegir al mismo jugador)
                foreach (var candidato in candidatos)
                {
                    if (NotificarFuncionesDeAnfitrion(candidato, hayPartidaCreada))
                    {
                        nuevoAnfitrion = candidato;
                        break;
                    }
                }

                // Si ningún candidato pudo recibir el aviso, se asigna al primero
                // de todos modos. Así, cuando el servidor procese su desconexión,
                // se volverá a elegir anfitrión entre los que queden
                if (nuevoAnfitrion == null)
                {
                    nuevoAnfitrion = candidatos.FirstOrDefault();
                }

                if (nuevoAnfitrion != null)
                {
                    sala.NombreAnfitrion = nuevoAnfitrion.NombreJugador;
                }
            }
        }

        private bool NotificarFuncionesDeAnfitrion(CuentaJugador candidato,
    bool hayPartidaCreada)
        {
            bool avisoEntregado = false;
            // Se lee una sola vez para que otro hilo no lo cambie a la mitad
            OperationContext contexto = candidato.ContextoOperacion;

            if (contexto != null)
            {
                try
                {
                    if (hayPartidaCreada)
                    {
                        contexto.GetCallbackChannel<IServicioPartidaCallback>().
                            HabilitarFuncionesDeAnfitrionEnPartida();
                    }
                    else
                    {
                        contexto.GetCallbackChannel<IServicioSalaCallback>().
                            MostrarFuncionesDeAnfitrionEnSala();
                    }

                    avisoEntregado = true;
                }
                catch (CommunicationException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
                catch (TimeoutException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
                catch (InvalidCastException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
            }

            return avisoEntregado;
        }

        private void EnviarResultadosAJugadores(string codigoSala, 
            string nombreJugadorGanador)
        {
            foreach (var jugador in 
                ObtenerJugadoresConPresenciaConfirmadaEnPartida(codigoSala))
            {
                try
                {
                    jugador.ContextoOperacion?.
                        GetCallbackChannel<IServicioPartidaCallback>().
                        MostrarResultadosDePartida(nombreJugadorGanador);
                }
                catch (CommunicationException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
                catch (InvalidCastException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
            }
        }

        private bool EsJugadorAnfitrionDeSala(string nombreJugador, string codigoSala)
        {
            return salas.ContainsKey(codigoSala) && 
                salas[codigoSala].NombreAnfitrion == nombreJugador;
        }

        private List<string> ObtenerNombresDeAnfitrionesEnSala()
        {
            List<string> nombresDeAnfitriones = new List<string>();

            foreach (var sala in salas.Values.Where(sala => 
                sala.Partida?.Estado == EstadoPartida.SinIniciar))
            {
                nombresDeAnfitriones.Add(sala.NombreAnfitrion);
            }

            return nombresDeAnfitriones;
        }

        private string ObtenerCodigoSalaDeJugador(string nombreJugador)
        {
            string codigoSala = "";

            if (jugadoresActivos[nombreJugador].Estado == EstadoJugador.EnSala || 
                jugadoresActivos[nombreJugador].Estado == EstadoJugador.EnPartida)
            {
                try
                {
                    codigoSala = salas.Values.First(sala => sala.
                        NombresDeJugadores.Keys.Any(nombre => nombre == nombreJugador) ||
                        (string.IsNullOrEmpty(codigoSala) && sala.HayPartidaCreada &&
                        sala.Partida.PuntajesDeJugadores.Keys.Any(
                        nombre => nombre == nombreJugador)))?.CodigoSala;
                }
                catch (InvalidOperationException excepcion) 
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
            }

            return codigoSala;
        }

        private void NotificarAnfitrionesSobreAmigoDisponible(
    string nombreJugador)
        {
            if (jugadoresActivos.TryGetValue(nombreJugador, out CuentaJugador amigo))
            {
                foreach (var anfitrion in ObtenerAnfitrionesAmigosEnSala(nombreJugador))
                {
                    try
                    {
                        anfitrion.ContextoOperacion?.
                            GetCallbackChannel<IServicioSalaCallback>().
                            MostrarAmigoDisponible(amigo);
                    }
                    catch (CommunicationException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                    catch (InvalidCastException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                }
            }
        }

        private void NotificarAnfitrionesSobreAmigoNoDisponible(
            string nombreJugador)
        {
            foreach (var anfitrion in ObtenerAnfitrionesAmigosEnSala(nombreJugador))
            {
                try
                {
                    anfitrion.ContextoOperacion?.
                        GetCallbackChannel<IServicioSalaCallback>().
                        OcultarAmigoNoDisponible(nombreJugador);
                }
                catch (CommunicationException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
                catch (InvalidCastException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
            }
        }

        private void RegistrarNombresDeJugadoresEnPartida(string codigoSala)
        {
            foreach (var nombreJugadorEnSala in 
                ObtenerJugadoresEnSala(codigoSala).
                Select(jugador => jugador.NombreJugador))
            {
                salas[codigoSala].Partida.
                    AgregarNombreDeJugador(nombreJugadorEnSala);
            }

            // el anfitrión no se encuentra en la página de sala al crear
            // la partida, por ello se agrega a la partida por separado
            salas[codigoSala].Partida.AgregarNombreDeJugador(
                salas[codigoSala].NombreAnfitrion);
        }

        private void RemoverJugadoresSinPresenciaConfirmadaEnPartida(string codigoSala)
        {
            foreach (var nombreJugador in
                ObtenerJugadoresConPresenciaConfirmadaEnPartida(codigoSala).
                Select(jugador => jugador.NombreJugador))
            {
                if (!salas[codigoSala].Partida.
                    EsJugadorConPresenciaConfirmada(nombreJugador))
                {
                    RemoverJugadorDePartida(nombreJugador, codigoSala);
                }
            }
        }

        private List<CuentaJugador> ObtenerJugadoresConPresenciaConfirmadaEnPartida(
            string codigoSala)
        {
            return jugadoresActivos.Values.Where(jugador =>
                salas[codigoSala].HayPartidaCreada && salas[codigoSala].
                Partida.EsJugadorConPresenciaConfirmada(
                jugador.NombreJugador)).ToList();
        }

        private void NotificarAmigosSobreCambioEstadoJugador(string nombreJugador, 
            EstadoJugador estado)
        {
            foreach (var jugador in ObtenerAmigosConectados(nombreJugador).
                Where(jugador => jugador.TipoInterfazCallback == 
                typeof(IServicioAmistadesCallback)))
            {
                try
                {
                    jugador.ContextoOperacion?.GetCallbackChannel
                        <IServicioAmistadesCallback>().
                        ActualizarEstadoDeJugador(nombreJugador, estado);
                }
                catch (CommunicationException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
                catch (InvalidCastException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
            }
        }

        private void MostrarNuevoAmigoAJugador(string nombreJugador, string nombreNuevoAmigo)
        {
            // Antes se usaba jugadoresActivos[nombreNuevoAmigo] != null, pero
            // el indexador nunca devuelve null: lanza KeyNotFoundException
            if (jugadoresActivos.TryGetValue(nombreJugador, out CuentaJugador jugador) &&
                jugador.TipoInterfazCallback == typeof(IServicioAmistadesCallback) &&
                jugadoresActivos.TryGetValue(nombreNuevoAmigo, out CuentaJugador nuevoAmigo))
            {
                try
                {
                    jugador.ContextoOperacion?.
                        GetCallbackChannel<IServicioAmistadesCallback>().
                        AgregarAmigoAListaDeAmigos(nuevoAmigo);
                }
                catch (CommunicationException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
                catch (InvalidCastException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
            }
        }

        private void MostrarEliminacionDeAmigoAJugador(string nombreJugador,
            string nombreAmigoEliminacion)
        {
            if (jugadoresActivos.TryGetValue(nombreJugador, out CuentaJugador jugador) &&
                jugador.TipoInterfazCallback == typeof(IServicioAmistadesCallback))
            {
                try
                {
                    jugador.ContextoOperacion?.
                        GetCallbackChannel<IServicioAmistadesCallback>().
                        RemoverAmigoDeListaDeAmigos(nombreAmigoEliminacion);
                }
                catch (CommunicationException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
                catch (InvalidCastException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
            }
        }

        private void ReflejarCambiosNombreJugadorActualizado(string nombreAnterior, string nuevoNombre)
        {
            // TryRemove saca al jugador y lo devuelve en un solo paso:
            // si devuelve true, la cuenta ya es nuestra y nadie más la puede quitar
            if (jugadoresActivos.TryRemove(nombreAnterior,
                out CuentaJugador cuentaJugadorAntigua))
            {
                cuentaJugadorAntigua.NombreJugador = nuevoNombre;
                jugadoresActivos.TryAdd(nuevoNombre, cuentaJugadorAntigua);

                foreach (var amigo in ObtenerAmigosConectados(nuevoNombre))
                {
                    MostrarEliminacionDeAmigoAJugador(amigo.NombreJugador, nombreAnterior);
                    MostrarNuevoAmigoAJugador(amigo.NombreJugador, nuevoNombre);
                }
            }
        }

        private void ReflejarCambiosNumeroAvatarActualizado(string nombreJugador,
            int nuevoNumeroAvatar)
        {
            if (jugadoresActivos.TryGetValue(nombreJugador, out CuentaJugador jugador))
            {
                jugador.NumeroAvatar = nuevoNumeroAvatar;

                foreach (var amigo in ObtenerAmigosConectados(nombreJugador))
                {
                    MostrarEliminacionDeAmigoAJugador(amigo.NombreJugador, nombreJugador);
                    MostrarNuevoAmigoAJugador(amigo.NombreJugador, nombreJugador);
                }
            }
        }

        private void MostrarNuevoJugadorActivoEnSala(string nombreJugador, string codigoSala)
        {
            if (jugadoresActivos.TryGetValue(nombreJugador, out CuentaJugador nuevoJugador))
            {
                foreach (var jugadorEnSala in ObtenerJugadoresEnSala(codigoSala))
                {
                    try
                    {
                        jugadorEnSala.ContextoOperacion?.
                            GetCallbackChannel<IServicioSalaCallback>().
                            MostrarNuevoJugadorEnSala(nuevoJugador);
                    }
                    catch (CommunicationException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                    catch (InvalidCastException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                }
            }
        }

        private void HabilitarRegresoASalaAJugadoresEnPartida(string codigoSala)
        {
            foreach (var jugadorEnPartida in
                ObtenerJugadoresConPresenciaConfirmadaEnPartida(codigoSala))
            {
                try
                {
                    jugadorEnPartida.ContextoOperacion?.
                        GetCallbackChannel<IServicioPartidaCallback>().
                        HabilitarOpcionDeRegresoASala();
                }
                catch (CommunicationException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
                catch (InvalidCastException excepcion)
                {
                    Registros.Registrador.EscribirRegistro(excepcion);
                }
            }

            salas[codigoSala].Partida = null;
            salas[codigoSala].HayPartidaCreada = false;
        }

        private void FinalizarPartida(string codigoSala)
        {
            var nombresDeJugadoresConPuntajeMaximo =
                salas[codigoSala].Partida.PuntajesDeJugadores.
                Where(puntaje => puntaje.Value == salas[codigoSala].
                Partida.PuntajesDeJugadores.Values.Max()).
                Select(puntaje => puntaje.Key).ToList();
            bool hayGanador = nombresDeJugadoresConPuntajeMaximo.Count ==
                Logica.Partida.CantidadGanadoresPorPartidaPermitidos;
            var nombreJugadorGanador = "";

            if (hayGanador)
            {
                nombreJugadorGanador = nombresDeJugadoresConPuntajeMaximo[0];
            }

            salas[codigoSala].Partida.Estado = EstadoPartida.Finalizada;

            foreach (var jugador in
                ObtenerJugadoresConPresenciaConfirmadaEnPartida(codigoSala))
            {
                if (!jugador.EsInvitado)
                {
                    bool esGanador = hayGanador &&
                        jugador.NombreJugador == nombreJugadorGanador;
                    jugador.Puntaje = salas[codigoSala].Partida.
                        PuntajesDeJugadores[jugador.NombreJugador];

                    try
                    {
                        AccesoResultadoPartida.
                            AgregarResultadoPartida(codigoSala, jugador, esGanador);
                    }
                    catch (EntityException excepcion)
                    {
                        Registros.Registrador.EscribirRegistro(excepcion);
                    }
                }

                jugador.Puntaje = Pieza.PuntajeVacio;
            }

            EnviarResultadosAJugadores(codigoSala, nombreJugadorGanador);
        }
    }
    #endregion
}