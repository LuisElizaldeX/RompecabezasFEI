# RompecabezasFEI

RompecabezasFEI es un juego multijugador de rompecabezas hecho para la
**Universidad Veracruzana**. Está ambientado en la **Facultad de Estadística e
Informática (FEI)**. Hasta 4 jugadores entran a una sala, platican por chat y
arman juntos y en tiempo real un rompecabezas. Cada pieza colocada da puntos
y gana quien termina con más puntos.

| | |
|---|---|
| **Lenguaje** | C# |
| **Cliente** | WPF sobre .NET Framework 4.7.2 (Windows) |
| **Servidor** | .NET 8 con [CoreWCF](https://github.com/CoreWCF/CoreWCF), que corre en Windows y en Linux |
| **Comunicación** | WCF `netTcpBinding` duplex (callbacks), puerto 8089 |
| **Base de datos** | PostgreSQL, con Entity Framework 6 y Npgsql |
| **Registros** | log4net |
| **Pruebas** | MSTest |
| **Idiomas del juego** | Español (es-MX) e inglés (en-US) |

## Funcionalidades

- Registro de jugadores con verificación del correo mediante un código enviado por Gmail.
- Inicio de sesión con cuenta o como invitado.
- Recuperación y cambio de contraseña. Las contraseñas viajan como hash SHA-512.
- Perfil del jugador: avatar, nombre y estadísticas de partidas jugadas y ganadas.
- Amistades: solicitudes, amigos y estado de conexión en tiempo real.
- Salas de hasta 4 jugadores con código para unirse, chat, invitaciones a amigos, expulsión y cambio de anfitrión.
- Partidas en tres dificultades (fácil, medio y difícil). Las piezas se bloquean
  mientras un jugador las mueve, para que dos jugadores no tomen la misma.
- Al final de la partida se muestran los resultados y se puede regresar a la sala.

## Arquitectura

El juego es cliente-servidor. Cada jugador ejecuta el cliente WPF, que se
conecta por net.tcp al servidor. El servidor guarda en memoria el estado del
juego (jugadores conectados, salas y partidas) y la información permanente en
PostgreSQL.

```
┌──────────────────────┐                     ┌──────────────────────────────────┐
│ Cliente WPF (Windows)│  peticiones  ───►   │ Servidor (.NET 8 + CoreWCF)      │
│  Páginas XAML        │                     │  Servicios ── Logica ── Datos ───┼──► PostgreSQL
│  Servicios (proxies) │  ◄───  callbacks    │  (estado en memoria: jugadores,  │
└──────────────────────┘  net.tcp :8089      │   salas y partidas)              │
                                             └──────────────────────────────────┘
```

**Callbacks duplex.** Cada servicio tiene dos interfaces, una que el cliente
llama en el servidor y otra de *callback* que el servidor llama en el cliente.
Con ellas avisa en tiempo real de eventos como "entró un jugador", "llegó un
mensaje" o "alguien colocó una pieza". Los callbacks viajan por la misma
conexión TCP que abrió el cliente, así que el cliente no necesita abrir puertos.

**Una sola instancia del servicio.** El servicio usa
`InstanceContextMode.Single` y `ConcurrencyMode.Multiple`. Una única instancia
de `ServicioRompecabezasFei` atiende a todos los jugadores y guarda en
diccionarios concurrentes a los jugadores activos y las salas. De cada jugador
guarda su `OperationContext`, que sirve para mandarle callbacks y para detectar
cuando se desconecta.

### Servicios (contratos WCF)

Todos se exponen en `net.tcp://<servidor>:8089/Contratos`, sin seguridad de
transporte (`SecurityMode.None`), pensado para red local.

| Servicio | Para qué sirve | Callbacks al cliente |
|---|---|---|
| `IServicioJugador` | Registro, inicio y cierre de sesión (con cuenta o invitado), cambio de nombre, avatar y contraseña | Detectar que la conexión sigue viva |
| `IServicioCorreo` | Validar si un correo ya existe y enviar códigos de verificación | (no tiene) |
| `IServicioAmistades` | Solicitudes de amistad, lista de amigos y eliminar amigos | Solicitud recibida, amigo agregado o eliminado, cambio de estado de un amigo |
| `IServicioInvitaciones` | Recibir invitaciones a salas | Mostrar invitación |
| `IServicioSala` | Crear sala, unirse, salir, chat, invitar, expulsar y cambiar anfitrión | Nuevo jugador, mensaje, expulsión, desconexión, funciones de anfitrión, habilitar o deshabilitar el inicio de la partida |
| `IServicioPartida` | Crear, unirse, iniciar o cancelar partida, bloquear y colocar piezas, estadísticas | Tablero, bloqueo y colocación de piezas, resultados, desconexión de jugadores, expulsión |

### Base de datos

PostgreSQL con 7 tablas: `Jugador`, `Cuenta`, `SolicitudAmistad`, `Amistad`,
`Sala`, `Partida` y `ResultadoPartida`. Las crea el script
[`ModeloDatosRompecabezasFei.postgresql.sql`](ServidorRompecabezasFei/Datos/Sql/ModeloDatosRompecabezasFei.postgresql.sql).
Entity Framework 6 (Code First) solo lee y escribe, no crea ni modifica tablas.

## Estructura del repositorio

```
RompecabezasFEI/
├── ClienteRompecabezasFei/          Cliente WPF (.NET Framework 4.7.2)
│   ├── RompecabezasFei/             Aplicación: páginas XAML, imágenes, sonidos, idiomas
│   │   ├── Servicios/               Envoltorios de cada servicio WCF y sus callbacks
│   │   ├── Connected Services/      Proxy WCF generado (Reference.cs)
│   │   ├── Utilidades/              Diálogos, temporizador, generador de imágenes
│   │   └── App.config               Dirección del servidor (endpoints net.tcp)
│   ├── Dominio/                     Clases del juego en el cliente (Tablero, Pieza, Celda…)
│   ├── Seguridad/                   Validación de datos y hash SHA-512 de contraseñas
│   ├── AccesoServidor/              Acceso a la información del jugador
│   ├── Registros/                   Registro de errores (log4net)
│   └── Pruebas/                     Pruebas del cliente
│
├── ServidorRompecabezasFei/         Servidor (.NET 8 + CoreWCF)
│   ├── Servidor/                    Programa principal: levanta CoreWCF en el puerto 8089
│   ├── Servicios/                   ServicioRompecabezasFei: implementa los 6 contratos
│   ├── Contratos/                   Interfaces WCF (IServicio*) y sus callbacks
│   ├── Logica/                      Reglas del juego (Sala, Partida, Tablero, Pieza) y AccesoDatos/
│   ├── Datos/                       Entity Framework, entidades, configuración y script SQL
│   ├── Registros/                   Registro de errores (log4net)
│   ├── Pruebas/                     Pruebas con MSTest
│   ├── Despliegue/                  Guía y archivos para instalarlo en Ubuntu con systemd
│   ├── CONFIGURACION.md             Configuración, contraseñas y entornos
│   ├── Directory.Build.props        Propiedades comunes (net8.0)
│   └── Directory.Packages.props     Versiones de los paquetes NuGet
│
└── Herramientas/
    └── PruebaConexion/              Consola que prueba el servidor y sus callbacks desde la red
```

Dependencias entre los proyectos del servidor:

```
Servidor ──► Servicios ──► Contratos ──► Logica ──► Datos
                 │                          └─────► Registros
                 └──► Registros
```

## Cómo ejecutarlo

### Servidor

Necesitas el [SDK de .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0),
que viene con Visual Studio 2022 17.8 o más nuevo, y PostgreSQL.

1. Crea la base de datos con el script SQL. Las instrucciones están en
   [CONFIGURACION.md](ServidorRompecabezasFei/CONFIGURACION.md).
2. Guarda la contraseña de la base de datos en *user secrets* (también en
   CONFIGURACION.md).
3. Ejecuta el servidor:

   ```bash
   cd ServidorRompecabezasFei
   dotnet run --project Servidor
   ```

   O abre `RompecabezasFeiServidor.sln` en Visual Studio y ejecuta el proyecto `Servidor`.

Para dejarlo corriendo en un servidor Ubuntu (homelab) y jugar en red local,
sigue la guía [Despliegue/DESPLIEGUE-UBUNTU.md](ServidorRompecabezasFei/Despliegue/DESPLIEGUE-UBUNTU.md).

### Cliente

Abre `ClienteRompecabezasFei/RompecabezasFeiCliente.sln` en Visual Studio
(Windows) y ejecuta `RompecabezasFei`. Por defecto se conecta a
`net.tcp://localhost:8089/Contratos`. Para jugar contra un servidor en otro
equipo, cambia `localhost` por la IP del servidor en `App.config` (o en
`RompecabezasFei.exe.config` del juego compilado).

### Probar la conexión sin abrir el juego

```bash
cd Herramientas/PruebaConexion
dotnet run -- <ip-del-servidor>
```

Simula a dos jugadores que entran a una sala y comprueba que llegan los
callbacks de nuevo jugador, chat y desconexión.
