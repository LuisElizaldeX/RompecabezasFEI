using Npgsql;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Data.Entity;
using Datos.Entidades;

namespace Datos
{
    // Contexto de base de datos de RompecabezasFEI (Code First).
    // Las tablas se describen aquí en OnModelCreating, en lugar de en un .edmx.
    // La base de datos se crea con ModeloDatosRompecabezasFei.postgresql.sql.
    [DbConfigurationType(typeof(ConfiguracionEntityFramework))]
    public class EntidadesRompecabezasFei : DbContext
    {
        private const string Esquema = "public";

        static EntidadesRompecabezasFei()
        {
            // Entity Framework no crea ni modifica la base de datos:
            // las tablas ya existen porque las crea el script .sql
            Database.SetInitializer<EntidadesRompecabezasFei>(null);
        }

        // Toma la cadena de conexión de appsettings.json, user secrets y
        // variables de entorno (ver ConfiguracionBaseDatos).
        // Es el constructor que usan las clases de Logica/AccesoDatos.
        public EntidadesRompecabezasFei()
            : this(ConfiguracionBaseDatos.ObtenerCadenaConexion())
        {
        }

        // Permite pasar la cadena de conexión directamente (por ejemplo, en pruebas)
        public EntidadesRompecabezasFei(string cadenaConexion)
            : this(new NpgsqlConnection(cadenaConexion))
        {
        }

        public EntidadesRompecabezasFei(DbConnection conexion)
            : base(conexion, contextOwnsConnection: true)
        {
        }

        public virtual DbSet<Cuenta> Cuenta { get; set; }
        public virtual DbSet<Jugador> Jugador { get; set; }
        public virtual DbSet<SolicitudAmistad> SolicitudAmistad { get; set; }
        public virtual DbSet<Amistad> Amistad { get; set; }
        public virtual DbSet<Sala> Sala { get; set; }
        public virtual DbSet<ResultadoPartida> ResultadoPartida { get; set; }
        public virtual DbSet<Partida> Partida { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(Esquema);

            ConfigurarJugador(modelBuilder);
            ConfigurarCuenta(modelBuilder);
            ConfigurarSolicitudAmistad(modelBuilder);
            ConfigurarAmistad(modelBuilder);
            ConfigurarSala(modelBuilder);
            ConfigurarPartida(modelBuilder);
            ConfigurarResultadoPartida(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }

        private static void ConfigurarJugador(DbModelBuilder modelBuilder)
        {
            var jugador = modelBuilder.Entity<Jugador>();

            jugador.ToTable("Jugador");
            jugador.HasKey(x => x.IdJugador);

            jugador.Property(x => x.IdJugador)
                .HasColumnName("IdJugador")
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            jugador.Property(x => x.NombreJugador)
                .HasColumnName("NombreJugador")
                .HasMaxLength(15)
                .IsRequired();

            jugador.Property(x => x.NumeroAvatar)
                .HasColumnName("NumeroAvatar");
        }

        private static void ConfigurarCuenta(DbModelBuilder modelBuilder)
        {
            var cuenta = modelBuilder.Entity<Cuenta>();

            cuenta.ToTable("Cuenta");
            cuenta.HasKey(x => x.IdCuenta);

            cuenta.Property(x => x.IdCuenta)
                .HasColumnName("IdCuenta")
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            cuenta.Property(x => x.Correo)
                .HasColumnName("Correo")
                .HasMaxLength(65)
                .IsRequired();

            cuenta.Property(x => x.Contrasena)
                .HasColumnName("Contrasena")
                .HasMaxLength(128)
                .IsFixedLength()
                .IsRequired();

            // Relación uno a uno: cada cuenta pertenece a un jugador y cada
            // jugador tiene una cuenta. La clase Cuenta no tiene una propiedad
            // para la llave foránea; la columna "Jugador_IdJugador" solo
            // existe en la tabla, por eso se indica con MapKey
            cuenta.HasRequired(x => x.Jugador)
                .WithRequiredDependent(x => x.Cuenta)
                .Map(x => x.MapKey("Jugador_IdJugador"))
                .WillCascadeOnDelete(false);
        }

        private static void ConfigurarSolicitudAmistad(DbModelBuilder modelBuilder)
        {
            var solicitud = modelBuilder.Entity<SolicitudAmistad>();

            solicitud.ToTable("SolicitudAmistad");
            solicitud.HasKey(x => new { x.IdJugadorOrigen, x.IdJugadorDestino });

            solicitud.Property(x => x.IdJugadorOrigen)
                .HasColumnName("IdJugadorOrigen");

            solicitud.Property(x => x.IdJugadorDestino)
                .HasColumnName("IdJugadorDestino");

            solicitud.HasRequired(x => x.JugadorOrigen)
                .WithMany()
                .HasForeignKey(x => x.IdJugadorOrigen)
                .WillCascadeOnDelete(false);

            solicitud.HasRequired(x => x.JugadorDestino)
                .WithMany()
                .HasForeignKey(x => x.IdJugadorDestino)
                .WillCascadeOnDelete(false);
        }

        private static void ConfigurarAmistad(DbModelBuilder modelBuilder)
        {
            var amistad = modelBuilder.Entity<Amistad>();

            amistad.ToTable("Amistad");
            amistad.HasKey(x => new { x.IdJugadorA, x.IdJugadorB });

            amistad.Property(x => x.IdJugadorA)
                .HasColumnName("IdJugadorA");

            amistad.Property(x => x.IdJugadorB)
                .HasColumnName("IdJugadorB");

            amistad.HasRequired(x => x.JugadorA)
                .WithMany()
                .HasForeignKey(x => x.IdJugadorA)
                .WillCascadeOnDelete(false);

            amistad.HasRequired(x => x.JugadorB)
                .WithMany()
                .HasForeignKey(x => x.IdJugadorB)
                .WillCascadeOnDelete(false);
        }

        private static void ConfigurarSala(DbModelBuilder modelBuilder)
        {
            var sala = modelBuilder.Entity<Sala>();

            sala.ToTable("Sala");
            sala.HasKey(x => x.IdSala);

            sala.Property(x => x.IdSala)
                .HasColumnName("IdSala")
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            sala.Property(x => x.Codigo)
                .HasColumnName("Codigo")
                .HasMaxLength(36)
                .IsRequired();

            sala.Property(x => x.MaximoJugadores)
                .HasColumnName("MaximoJugadores");

            sala.Property(x => x.MinimoJugadores)
                .HasColumnName("MinimoJugadores");
        }

        private static void ConfigurarPartida(DbModelBuilder modelBuilder)
        {
            var partida = modelBuilder.Entity<Partida>();

            partida.ToTable("Partida");
            partida.HasKey(x => x.IdPartida);

            partida.Property(x => x.IdPartida)
                .HasColumnName("IdPartida")
                .HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            partida.Property(x => x.IdSala)
                .HasColumnName("IdSala");

            partida.Property(x => x.Dificultad)
                .HasColumnName("Dificultad");

            partida.HasRequired(x => x.Sala)
                .WithMany()
                .HasForeignKey(x => x.IdSala)
                .WillCascadeOnDelete(false);
        }

        private static void ConfigurarResultadoPartida(DbModelBuilder modelBuilder)
        {
            var resultado = modelBuilder.Entity<ResultadoPartida>();

            resultado.ToTable("ResultadoPartida");
            resultado.HasKey(x => new { x.IdJugador, x.IdPartida });

            resultado.Property(x => x.IdJugador)
                .HasColumnName("IdJugador");

            resultado.Property(x => x.IdPartida)
                .HasColumnName("IdPartida");

            resultado.Property(x => x.Puntaje)
                .HasColumnName("Puntaje");

            resultado.Property(x => x.EsGanador)
                .HasColumnName("EsGanador");

            resultado.HasRequired(x => x.Jugador)
                .WithMany()
                .HasForeignKey(x => x.IdJugador)
                .WillCascadeOnDelete(false);

            resultado.HasRequired(x => x.Partida)
                .WithMany()
                .HasForeignKey(x => x.IdPartida)
                .WillCascadeOnDelete(false);
        }
    }
}
