using Datos.Entidades;
using System.Linq;

namespace Logica.AccesoDatos
{
    public static class AccesoResultadoPartida
    {
        public static bool AgregarResultadoPartida(string codigoSala,
            CuentaJugador cuentaJugador, bool esGanador)
        {
            bool resultado = false;

            using (var contexto = new Datos.EntidadesRompecabezasFei())
            {
                Datos.Entidades.Partida partidaEncontrada = contexto.Partida.
                    OrderByDescending(partida => partida.IdPartida).
                    Where(partida => partida.Sala.Codigo == codigoSala).
                    FirstOrDefault();

                if (partidaEncontrada != null)
                {
                    Datos.Entidades.ResultadoPartida resultadoPartida = new Datos.Entidades.ResultadoPartida
                    {
                        IdPartida = partidaEncontrada.IdPartida,
                        IdJugador = cuentaJugador.IdJugador,
                        EsGanador = esGanador,
                        Puntaje = cuentaJugador.Puntaje,
                    };
                    contexto.ResultadoPartida.Add(resultadoPartida);
                    resultado = contexto.SaveChanges() > 0;
                }
            }

            return resultado;
        }
    }
}
