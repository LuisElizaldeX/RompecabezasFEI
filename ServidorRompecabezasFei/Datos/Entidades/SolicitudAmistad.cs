namespace Datos.Entidades
{
    public partial class SolicitudAmistad
    {
        public int IdJugadorOrigen { get; set; }
        public int IdJugadorDestino { get; set; }

        public virtual Jugador JugadorOrigen { get; set; }
        public virtual Jugador JugadorDestino { get; set; }
    }
}
