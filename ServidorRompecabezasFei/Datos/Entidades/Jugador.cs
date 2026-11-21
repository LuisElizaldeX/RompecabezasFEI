namespace Datos.Entidades
{
    public partial class Jugador
    {
        public int IdJugador { get; set; }
        public string NombreJugador { get; set; }
        public int NumeroAvatar { get; set; }

        public virtual Cuenta Cuenta { get; set; }
    }
}
