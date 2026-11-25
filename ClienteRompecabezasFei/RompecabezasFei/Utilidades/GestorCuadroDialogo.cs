using System.Windows;

namespace RompecabezasFei.Utilidades
{
    public static class GestorCuadroDialogo
    {
        public static MessageBoxResult MostrarPreguntaNormal(string mensaje, string titulo)
        {
            return MessageBox.Show(mensaje, titulo, MessageBoxButton.YesNo, 
                MessageBoxImage.Question);
        }

        public static MessageBoxResult MostrarPreguntaConAdvertencia(string mensaje, 
            string titulo)
        {
            return MessageBox.Show(mensaje, titulo, MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
        }

        public static void MostrarAdvertencia(string mensaje, string titulo)
        {
            MessageBox.Show(mensaje, titulo, MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        public static void MostrarError(string mensaje, string titulo)
        {
            MessageBox.Show(mensaje, titulo, MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        // Muestra el error sobre la ventana indicada, para que no quede escondido
        // detrás de ella. Se debe llamar desde el hilo de la interfaz.
        public static void MostrarError(Window ventanaPropietaria, string mensaje, string titulo)
        {
            if (ventanaPropietaria != null)
            {
                MessageBox.Show(ventanaPropietaria, mensaje, titulo, MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else
            {
                MostrarError(mensaje, titulo);
            }
        }

        public static void MostrarInformacion(string mensaje, string titulo)
        {
            MessageBox.Show(mensaje, titulo, MessageBoxButton.OK,
                MessageBoxImage.Information);
        }        
    }
}