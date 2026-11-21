namespace Datos.Entidades
{
    public partial class Amistad
    {
        public int IdJugadorA { get; set; }
        public int IdJugadorB { get; set; }

        public virtual Jugador JugadorA { get; set; }
        public virtual Jugador JugadorB { get; set; }
    }
}
