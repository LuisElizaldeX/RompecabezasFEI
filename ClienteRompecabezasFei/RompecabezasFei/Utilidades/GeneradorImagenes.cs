using Dominio;
using System;
using System.Windows.Media.Imaging;

namespace RompecabezasFei.Utilidades
{
    public static class GeneradorImagenes
    {
        public static BitmapImage GenerarFuenteImagenAvatar(int numeroAvatar)
        {
            string rutaImagen = $"/Imagenes/Avatares/{numeroAvatar}.png";
            BitmapImage fuenteImagenAvatar = new BitmapImage();
            fuenteImagenAvatar.BeginInit();
            fuenteImagenAvatar.UriSource = new Uri(rutaImagen, UriKind.RelativeOrAbsolute);
            fuenteImagenAvatar.EndInit();

            return fuenteImagenAvatar;
        }

        // Las imágenes de los rompecabezas van incrustadas en el .exe como
        // recursos (Build Action = Resource), igual que los avatares. Antes se
        // leían de la carpeta del código fuente ("..\..\Imagenes\..."
        // desde bin\Release), que solo existe en el equipo donde se compiló.
        // En otro equipo la imagen no se encontraba, el callback
        // MostrarTableroDePartida fallaba y el cliente se desconectaba.
        public static BitmapImage GenerarFuenteImagenRompecabezas(int numeroImagenRompecabezas)
        {
            string rutaImagen =
                $"pack://application:,,,/Imagenes/Rompecabezas/{numeroImagenRompecabezas}.png";

            return new BitmapImage(new Uri(rutaImagen, UriKind.Absolute));
        }
    }
}