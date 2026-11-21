using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Logica
{
    [DataContract]
    public class Sala
    {
        public const int CantidadMaximaJugadores = 4;
        
        public const int CantidadMinimaJugadoresParaPartida = 2;
        
        public const int CantidadJugadoresVacia = 0;

        public const int MaximoMensajesHistorial = 100;

        private readonly ConcurrentQueue<string> historialMensajes =
            new ConcurrentQueue<string>();

        public bool HayPartidaCreada { get; set; }

        [DataMember]
        public string CodigoSala { get; set; }

        [DataMember]
        public string NombreAnfitrion { get; set; }
        
        [DataMember]
        public int ContadorJugadores { get; set; }
        
        public ConcurrentDictionary<string, string> NombresDeJugadores { get; set; }

        public Partida Partida { get; set; }        

        public bool EstaVacia()
        {
            return ContadorJugadores == CantidadJugadoresVacia;
        }

        public bool HayCantidadJugadoresMinimaParaPartida()
        {
            return ContadorJugadores >= CantidadMinimaJugadoresParaPartida;
        }

        // Guarda el mensaje del chat y conserva solo los últimos
        // MaximoMensajesHistorial, para que quien regrese a la sala los vea
        public void AgregarMensaje(string mensaje)
        {
            historialMensajes.Enqueue(mensaje);

            while (historialMensajes.Count > MaximoMensajesHistorial &&
                historialMensajes.TryDequeue(out _))
            {
            }
        }

        public List<string> ObtenerMensajes()
        {
            return historialMensajes.ToList();
        }

        public bool AgregarNombreDeJugador(string nombreJugador)
        {
            bool esNombreAgregado = NombresDeJugadores.
                TryAdd(nombreJugador, null);

            if (esNombreAgregado)
            {
                ContadorJugadores++;
            }

            return esNombreAgregado;
        }

        public bool RemoverNombreDeJugador(string nombreJugador)
        {
            bool esNombreRemovido = NombresDeJugadores.
                TryRemove(nombreJugador, out _);

            if (esNombreRemovido)
            {
                ContadorJugadores--;
            }

            return esNombreRemovido;
        }
    }
}