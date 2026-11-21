using CoreWCF;

namespace Logica
{
    public static class ExtensionesContextoOperacion
    {
        // En WCF de .NET Framework se usaba OperationContext.Channel para obtener
        // la conexión (canal) del cliente. CoreWCF no expone esa propiedad, pero
        // en una conexión duplex el canal de callbacks es el mismo objeto que el
        // canal de la sesión, así que sirve para lo mismo: detectar cuando el
        // cliente se desconecta (evento Faulted/Closed) y comparar conexiones.
        // Devuelve null si el contexto es null, igual que "contexto?.Channel".
        public static IContextChannel ObtenerCanal(this OperationContext contexto)
        {
            return contexto?.GetCallbackChannel<IContextChannel>();
        }
    }
}
