namespace Datos.Entidades
{
    public partial class Cuenta
    {
        public int IdCuenta { get; set; }
        public string Correo { get; set; }
        public string Contrasena { get; set; }

        public virtual Jugador Jugador { get; set; }
    }
}
