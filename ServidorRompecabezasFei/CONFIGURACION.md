# Configuración de la base de datos del servidor

La configuración del servidor se lee con `Datos/ConfiguracionServidor.cs`, en este orden
(cada fuente sobrescribe a la anterior):

| # | Fuente | ¿Se sube al repositorio? | Qué contiene |
|---|--------|--------------------------|--------------|
| 1 | `Datos/appsettings.json` | Sí | Cadena de conexión por defecto y dirección del correo, **sin contraseñas** |
| 2 | `Datos/appsettings.{Entorno}.json` | Sí | Cadena de conexión de ese entorno, **sin contraseña** |
| 3 | User secrets (solo en `Development`) | No | La contraseña de tu base de datos local |
| 4 | Variables de entorno con prefijo `ROMPECABEZAS_` | No | La contraseña en producción |

**Nunca pongas contraseñas en los archivos `appsettings*.json`.**

## Entorno

El entorno se toma de la variable de entorno `DOTNET_ENVIRONMENT`.
Si no existe:

- Compilando en **Debug** → `Development`
- Compilando en **Release** → `Production`

## Desarrollo: guardar la contraseña en user secrets

Los user secrets se guardan en tu perfil de usuario, fuera del repositorio:

```
Windows: %APPDATA%\Microsoft\UserSecrets\6a4816e4-f4c8-41ff-9bbd-37bd6a3baf52\secrets.json
Linux:   ~/.microsoft/usersecrets/6a4816e4-f4c8-41ff-9bbd-37bd6a3baf52/secrets.json
```

Opción A, con la CLI de .NET (viene con Visual Studio 2022):

```
dotnet user-secrets set --id 6a4816e4-f4c8-41ff-9bbd-37bd6a3baf52 "BaseDatos:Contrasena" "tu_contraseña"
dotnet user-secrets list --id 6a4816e4-f4c8-41ff-9bbd-37bd6a3baf52
```

Opción B, a mano: crea el archivo `secrets.json` en la ruta de arriba con:

```json
{
  "BaseDatos:Contrasena": "tu_contraseña"
}
```

Si tu base local usa otro host, puerto, base o usuario, también puedes
sobrescribir la cadena completa en los user secrets:

```
dotnet user-secrets set --id 6a4816e4-f4c8-41ff-9bbd-37bd6a3baf52 "ConnectionStrings:EntidadesRompecabezasFei" "Host=localhost;Port=5433;Database=otra;Username=otro"
```

## Producción: variables de entorno

Los user secrets son solo para desarrollo. En producción las contraseñas van
en variables de entorno.

**Linux (Ubuntu, con systemd):** van en `/etc/rompecabezasfei/rompecabezasfei.env`.
Hay un ejemplo en `Despliegue/rompecabezasfei.env.ejemplo`, y la guía completa
está en [Despliegue/DESPLIEGUE-UBUNTU.md](Despliegue/DESPLIEGUE-UBUNTU.md).

**Windows:**

```
setx DOTNET_ENVIRONMENT Production
setx ROMPECABEZAS_BaseDatos__Contrasena "la_contraseña_de_produccion"
```

(`__` doble guion bajo equivale a `:` en la configuración. `setx` guarda la
variable para tu usuario; si el servidor corre con otra cuenta o como servicio,
usa `setx /M` desde una consola de administrador. Cierra y vuelve a abrir la
consola o el servidor para que tome los valores nuevos.)

`Datos/appsettings.Production.json` apunta a un PostgreSQL en el mismo equipo
(`localhost`). Si la base de datos está en otro equipo, cambia ahí el `Host` o
sobrescribe la cadena completa con la variable
`ROMPECABEZAS_ConnectionStrings__EntidadesRompecabezasFei`.

## Puerto del servidor

El servidor escucha en el puerto `8089` (`Servidor:Puerto` en
`Datos/appsettings.json`). Se puede cambiar con la variable de entorno
`ROMPECABEZAS_Servidor__Puerto`. Si lo cambias, cambia también el puerto en
el `App.config` de los clientes.

## Correo para los códigos de confirmación

El servidor envía correos con una cuenta de Gmail y una **contraseña de
aplicación** (no la contraseña normal de la cuenta). Se crea en
https://myaccount.google.com/apppasswords y requiere que la cuenta tenga
activada la verificación en dos pasos.

- La dirección va en `Datos/appsettings.json`:

  ```json
  "Correo": {
    "Direccion": "RompecabezasFEISoporte@gmail.com"
  }
  ```

- La contraseña de aplicación va en user secrets (desarrollo):

  ```
  dotnet user-secrets set --id 6a4816e4-f4c8-41ff-9bbd-37bd6a3baf52 "Correo:Contrasena" 'contraseña_de_aplicacion'
  ```

- En producción, en la variable de entorno `ROMPECABEZAS_Correo__Contrasena`.
