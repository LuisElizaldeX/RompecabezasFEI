namespace Datos.Entidades
{
    public partial class ResultadoPartida
    {
        public ResultadoPartida()
        {
            Puntaje = 0;
            EsGanador = false;
        }

        public int IdJugador { get; set; }
        public int IdPartida { get; set; }
        public int Puntaje { get; set; }
        public bool EsGanador { get; set; }

        public virtual Jugador Jugador { get; set; }
        public virtual Partida Partida { get; set; }
    }
}
