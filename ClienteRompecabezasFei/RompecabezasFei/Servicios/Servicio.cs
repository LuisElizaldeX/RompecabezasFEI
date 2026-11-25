using RompecabezasFei.Utilidades;
using System;
using System.ServiceModel;

namespace RompecabezasFei.Servicios
{
    public abstract class Servicio
    {
        // Permite identificar el estado después de invocar un método de cualquier servicio
        public EstadoOperacion EstadoOperacion { get; set; }

        public void ManejarExcepcionDeServidor(Exception excepcionGeneral)
        {
            if (excepcionGeneral is EndpointNotFoundException excepcionA)
            {
                Registros.Registrador.EscribirRegistro(excepcionA);
            }
            else if (excepcionGeneral is CommunicationObjectFaultedException excepcionB)
            {
                Registros.Registrador.EscribirRegistro(excepcionB);
            }
            else if (excepcionGeneral is CommunicationObjectAbortedException excepcionC)
            {
                Registros.Registrador.EscribirRegistro(excepcionC);
            }
            else if (excepcionGeneral is CommunicationException excepcionD)
            {
                Registros.Registrador.EscribirRegistro(excepcionD);
            }
            else if (excepcionGeneral is TimeoutException excepcionE)
            {
                Registros.Registrador.EscribirRegistro(excepcionE);
            }
            else if (excepcionGeneral is InvalidOperationException excepcionF)
            {
                Registros.Registrador.EscribirRegistro(excepcionF);
            }
            else if (excepcionGeneral is ObjectDisposedException excepcionG)
            {
                Registros.Registrador.EscribirRegistro(excepcionG);
            }
            else
            {
                Registros.Registrador.EscribirRegistro(excepcionGeneral);
            }

            EstadoOperacion = EstadoOperacion.Error;

            // Si ya se avisó que se perdió la conexión, no se muestra otro mensaje
            if (ServicioJugador.EsConexionPerdidaNotificada)
            {
                return;
            }

            // Con una sesión iniciada, un error de comunicación significa que el
            // servidor se cayó: se muestra un solo aviso y se regresa al inicio
            // de sesión, en lugar del error genérico de conexión
            if (Dominio.CuentaJugador.Actual != null && EsPerdidaDeConexion(excepcionGeneral))
            {
                ServicioJugador.NotificarConexionPerdida();
                return;
            }

            GestorCuadroDialogo.MostrarError(
                Properties.Resources.ETIQUETA_ERRORCONEXIONSERVIDOR_MENSAJE,
                Properties.Resources.ETIQUETA_ERRORCONEXIONSERVIDOR_TITULO);
        }

        // Los FaultException son errores que el servidor devolvió a propósito,
        // así que no indican que se haya perdido la conexión
        private static bool EsPerdidaDeConexion(Exception excepcion)
        {
            return ServicioJugador.EsCanalCaido() ||
                (excepcion is CommunicationException && !(excepcion is FaultException));
        }

        public abstract void AbrirConexion();

        public abstract void CerrarConexion();
    }
}