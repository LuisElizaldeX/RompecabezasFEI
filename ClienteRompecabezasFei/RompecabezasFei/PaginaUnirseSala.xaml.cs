using RompecabezasFei.Servicios;
using RompecabezasFei.Utilidades;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RompecabezasFei
{
    public partial class PaginaUnirseSala : Page
    {
        public PaginaUnirseSala()
        {
            InitializeComponent();
        }

        private void IntentarUnirJugadorASala(object objetoOrigen, RoutedEventArgs evento)
        {
            var servicioSala = new ServicioSala();

            if (servicioSala.EstadoOperacion == EstadoOperacion.Correcto)
            {
                bool existeSalaDisponible = servicioSala.ExisteSalaDisponible(cuadroTextoCodigoSala.Text);
                // Se guarda el resultado de la consulta ANTES de cerrar, porque
                // CerrarConexion también cambia EstadoOperacion
                bool consultaRealizada =
                    servicioSala.EstadoOperacion == EstadoOperacion.Correcto;
                servicioSala.CerrarConexion();

                if (consultaRealizada)
                {
                    if (existeSalaDisponible)
                    {
                        PaginaSala paginaSala = new PaginaSala(false, 
                            cuadroTextoCodigoSala.Text);
                        
                        if (paginaSala.HayConexionConSala)
                        {
                            VentanaPrincipal.CambiarPagina(paginaSala);
                        }
                    }
                    else
                    {
                        GestorCuadroDialogo.MostrarAdvertencia(
                            Properties.Resources.ETIQUETA_UNIRSESALA_MENSAJESALANODISPONIBLE,
                            Properties.Resources.ETIQUETA_UNIRSESALA_SALANODISPONIBLE);
                    }
                }
            }
        }

        private void IrAPaginaMenuPrincipal(object objetoOrigen, MouseButtonEventArgs evento)
        {
            VentanaPrincipal.CambiarPagina(new PaginaMenuPrincipal());
        }
    }
}