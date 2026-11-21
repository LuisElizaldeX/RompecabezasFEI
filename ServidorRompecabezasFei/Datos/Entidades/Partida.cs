namespace Datos.Entidades
{
    public partial class Partida
    {
        public int IdPartida { get; set; }
        public int IdSala { get; set; }
        public DificultadPartida Dificultad { get; set; }

        public virtual Sala Sala { get; set; }
    }
}
