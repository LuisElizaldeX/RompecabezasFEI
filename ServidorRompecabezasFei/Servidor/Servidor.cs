using System;
using Servicios;
using System.ServiceModel;

namespace Servidor
{
    public static class Servidor
    {
        public static void Main()
        {
            using (ServiceHost servidorRompecabezasFei = 
                new ServiceHost(typeof(ServicioRompecabezasFei)))
            {
                try
                {
                    servidorRompecabezasFei.Open();
                    Console.WriteLine("Servidor operacional");
                }
                catch (AddressAccessDeniedException exception)
                {
                    Console.WriteLine("Servidor detenido: " + exception.Message);
                }
                Console.ReadLine();
            }
        }
    }
}