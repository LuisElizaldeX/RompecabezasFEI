using Datos.Entidades;
using System.Linq;

namespace Logica.AccesoDatos
{
    public static class AccesoPartida
    {
        public static bool CrearNuevaPartida(string codigoSala, Datos.DificultadPartida dificultad)
        {
            bool operacionRealizada = false;

            using (var contexto = new Datos.EntidadesRompecabezasFei())
            {
                Datos.Entidades.Sala salaEncontrada = contexto.Sala.FirstOrDefault(
                    sala => sala.Codigo.Equals(codigoSala));

                if (salaEncontrada != null)
                {
                    Datos.Entidades.Partida nuevaPartida = new Datos.Entidades.Partida
                    {
                        Dificultad = dificultad,
                        IdSala = salaEncontrada.IdSala,      
                    };
                    contexto.Partida.Add(nuevaPartida);
                    operacionRealizada = contexto.SaveChanges() > 0;
                }
            }

            return operacionRealizada;
        }

        public static int ObtenerNumeroPartidasJugadas(string nombreJugador)
        {
            int partidasJugadas;

            using (var contexto = new Datos.EntidadesRompecabezasFei())
            {
                partidasJugadas = (from jugador in contexto.Jugador
                                   from partidaJugada in contexto.ResultadoPartida
                                   where partidaJugada.Jugador.Equals(jugador) &&
                                   jugador.NombreJugador.Equals(nombreJugador)
                                   select jugador).Count();
            }

            return partidasJugadas;
        }

        public static int ObtenerNumeroPartidasGanadas(string nombreJugador)
        {
            int partidasGanadas;

            using (var contexto = new Datos.EntidadesRompecabezasFei())
            {
                partidasGanadas = (from partidaJugada in contexto.ResultadoPartida
                                   where partidaJugada.Jugador.NombreJugador.
                                   Equals(nombreJugador) && partidaJugada.EsGanador
                                   select partidaJugada).Count();
            }

            return partidasGanadas;
        }
    }
}