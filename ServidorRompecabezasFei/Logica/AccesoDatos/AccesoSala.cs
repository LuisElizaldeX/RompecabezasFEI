using Datos.Entidades;
using Logica;

namespace Logica.AccesoDatos
{
    public static class AccesoSala
    {
        public static bool CrearNuevaSala(string codigoSala)
        {
            bool resultado = false;

            using (var contexto = new Datos.EntidadesRompecabezasFei())
            {
                Datos.Entidades.Sala nuevaSala = new Datos.Entidades.Sala
                {
                    Codigo = codigoSala,
                    MaximoJugadores = Logica.Sala.CantidadMaximaJugadores,
                    MinimoJugadores = Logica.Sala.CantidadMinimaJugadoresParaPartida,
                };
                contexto.Sala.Add(nuevaSala);
                resultado = contexto.SaveChanges() > 0;
            }

            return resultado;
        }
    }
}