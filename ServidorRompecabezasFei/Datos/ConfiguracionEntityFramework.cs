using Npgsql;
using System.Data.Entity;

namespace Datos
{
    // Registra en Entity Framework el proveedor de PostgreSQL (Npgsql).
    // Entity Framework encuentra esta clase automáticamente porque está en
    // el mismo ensamblado que EntidadesRompecabezasFei, así que funciona
    // aunque el App.config del programa que use Datos no lo registre.
    public class ConfiguracionEntityFramework : DbConfiguration
    {
        public const string NombreProveedor = "Npgsql";

        public ConfiguracionEntityFramework()
        {
            SetProviderFactory(NombreProveedor, NpgsqlFactory.Instance);
            SetProviderServices(NombreProveedor, NpgsqlServices.Instance);
            SetDefaultConnectionFactory(new NpgsqlConnectionFactory());
        }
    }
}
