# Desplegar el servidor en Ubuntu (homelab)

Esta guía instala el servidor de RompecabezasFEI en un equipo con Ubuntu
24.04 o 26.04, para jugar en la red local desde varios equipos con Windows.
Todo se hace por SSH, con comandos que puedes copiar y pegar.

```
 Equipo Windows (cliente)  ─┐
 Equipo Windows (cliente)  ─┼──  red local  ──►  Ubuntu (homelab)
 Equipo Windows (cliente)  ─┘   net.tcp 8089      ├─ Servidor .NET 10 (systemd)
                                                  └─ PostgreSQL (solo local)
```

Los clientes se conectan al servidor por el puerto **8089**. Los callbacks
(avisos del servidor a los jugadores) viajan por esa misma conexión, así que
**no hay que abrir ningún puerto en los equipos cliente**.

En los ejemplos, la IP del servidor es `192.168.1.50` y el usuario de Ubuntu
es `luis`. Cámbialos por los tuyos.

---

## 1. Conectarte al servidor por SSH

Desde Windows abre PowerShell o Terminal:

```powershell
ssh luis@192.168.1.50
```

La primera vez te pregunta si confías en el equipo: escribe `yes`. Después te
pide la contraseña de tu usuario de Ubuntu.

> **Tip:** para no escribir la contraseña cada vez, crea una llave SSH en
> Windows con `ssh-keygen` y cópiala al servidor:
> `type $env:USERPROFILE\.ssh\id_ed25519.pub | ssh luis@192.168.1.50 "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys"`

Todo lo que sigue se ejecuta **dentro** de la sesión SSH, salvo donde se diga
que es en Windows.

## 2. Darle una IP fija al servidor

Los clientes guardan la IP del servidor, así que no debe cambiar.

**Opción A (recomendada): reserva DHCP en el router.** Entra a la página de
tu router, busca "DHCP", "Reserva de direcciones" o "IP estática", y asigna
una IP fija a la MAC del servidor. Para ver la MAC y la IP actual:

```bash
ip -brief address
ip link
```

**Opción B: IP fija en Ubuntu con netplan.** Mira el nombre de tu tarjeta de
red (por ejemplo `enp3s0`) con `ip -brief address` y edita el archivo de
netplan:

```bash
ls /etc/netplan/
sudo nano /etc/netplan/50-cloud-init.yaml
```

```yaml
network:
  version: 2
  ethernets:
    enp3s0:
      dhcp4: false
      addresses: [192.168.1.50/24]
      routes:
        - to: default
          via: 192.168.1.1        # IP de tu router
      nameservers:
        addresses: [1.1.1.1, 8.8.8.8]
```

```bash
sudo netplan try     # si en 120 s no confirmas, regresa a la configuración anterior
```

> Cuidado: si te equivocas aquí puedes perder la conexión SSH. `netplan try`
> te protege porque deshace el cambio si no lo confirmas.

## 3. Instalar .NET 10 y PostgreSQL

```bash
sudo apt update
sudo apt install -y aspnetcore-runtime-10.0 postgresql git
dotnet --list-runtimes     # debe mostrar Microsoft.AspNetCore.App 10.0.x
```

Si vas a compilar el servidor en el propio Ubuntu (paso 5, opción A), instala
también el SDK:

```bash
sudo apt install -y dotnet-sdk-10.0
```

## 4. Crear la base de datos

Elige una contraseña para la base de datos y sustitúyela abajo:

```bash
sudo -u postgres psql -c "CREATE USER rompecabezasuser WITH PASSWORD 'cambia_esta_contrasena';"
sudo -u postgres psql -c 'CREATE DATABASE "RompecabezasFEI" OWNER rompecabezasuser;'
```

Las tablas se crean en el paso 5, cuando ya tengas los archivos del servidor.

PostgreSQL solo escucha en `localhost`, así que la base de datos no queda
expuesta a la red. Solo el servidor del juego habla con ella.

## 5. Publicar el servidor

### Opción A: compilar en el propio Ubuntu (recomendada para aprender)

```bash
cd ~
git clone https://github.com/LuisElizaldeX/RompecabezasFEI.git
cd RompecabezasFEI/ServidorRompecabezasFei
dotnet publish Servidor/Servidor.csproj -c Release -o ~/rompecabezasfei-publicado
```

### Opción B: compilar en Windows y copiar por SSH

En Windows, con el SDK de .NET 10 (viene con Visual Studio 2026 o más nuevo):

```powershell
cd ServidorRompecabezasFei
dotnet publish Servidor\Servidor.csproj -c Release -o publicado
scp -r publicado luis@192.168.1.50:~/rompecabezasfei-publicado
```

`scp` copia archivos por SSH, con el mismo usuario y contraseña.

### Instalar los archivos en /opt

En Ubuntu, los programas que no vienen de `apt` suelen ir en `/opt`. Además,
el servidor corre con un usuario propio sin permisos de administrador:

```bash
sudo useradd --system --no-create-home --shell /usr/sbin/nologin rompecabezas
sudo mkdir -p /opt/rompecabezasfei
sudo cp -r ~/rompecabezasfei-publicado/. /opt/rompecabezasfei/
sudo mkdir -p /opt/rompecabezasfei/Registros
sudo chown -R rompecabezas:rompecabezas /opt/rompecabezasfei/Registros
```

La carpeta `Registros` es donde el servidor escribe sus archivos de errores,
así que su usuario necesita permiso de escritura ahí.

### Crear las tablas (solo la primera vez)

El script que crea las tablas viene junto al servidor publicado, en la carpeta
`Sql/`. Ejecútalo **una sola vez**, porque borra las tablas si ya existen:

```bash
psql -h localhost -U rompecabezasuser -d RompecabezasFEI -f /opt/rompecabezasfei/Sql/ModeloDatosRompecabezasFei.postgresql.sql
```

Te pide la contraseña que elegiste en el paso 4.

## 6. Guardar las contraseñas

Las contraseñas no van en el repositorio. Van en un archivo que solo puede leer
`root` (systemd lo lee al arrancar el servicio):

```bash
sudo mkdir -p /etc/rompecabezasfei
sudo cp ~/RompecabezasFEI/ServidorRompecabezasFei/Despliegue/rompecabezasfei.env.ejemplo /etc/rompecabezasfei/rompecabezasfei.env
sudo nano /etc/rompecabezasfei/rompecabezasfei.env      # pon tus contraseñas
sudo chmod 600 /etc/rompecabezasfei/rompecabezasfei.env
```

Si usaste la opción B, crea el archivo a mano con `sudo nano` y copia el
contenido de `Despliegue/rompecabezasfei.env.ejemplo`.

## 7. Probar a mano antes de crear el servicio

```bash
cd /opt/rompecabezasfei
sudo -u rompecabezas env $(sudo cat /etc/rompecabezasfei/rompecabezasfei.env | grep -v '^#' | xargs) dotnet Servidor.dll
```

Debes ver la línea `Servidor operacional en net.tcp://0.0.0.0:8089/Contratos`.
ASP.NET Core también muestra `Now listening on: http://0.0.0.0:8089`. Dice
"http" pero es el mismo puerto net.tcp. Detenlo con `Ctrl+C`.

## 8. Ejecutarlo como servicio (systemd)

systemd es el programa de Ubuntu que arranca y vigila los servicios. Con él,
el servidor arranca solo al prender el equipo y se reinicia si se cae.

```bash
sudo cp ~/RompecabezasFEI/ServidorRompecabezasFei/Despliegue/rompecabezasfei.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now rompecabezasfei
```

Comandos que vas a usar seguido:

| Para… | Comando |
|---|---|
| Ver si está corriendo | `systemctl status rompecabezasfei` |
| Ver los mensajes en vivo | `journalctl -u rompecabezasfei -f` |
| Ver los mensajes de hoy | `journalctl -u rompecabezasfei --since today` |
| Reiniciar | `sudo systemctl restart rompecabezasfei` |
| Detener | `sudo systemctl stop rompecabezasfei` |
| Que no arranque al prender | `sudo systemctl disable rompecabezasfei` |
| Ver que escucha en el puerto | `sudo ss -ltnp \| grep 8089` |

## 9. Abrir el puerto en el firewall (ufw)

> **Importante:** permite SSH **antes** de activar ufw, o te quedarás fuera.

```bash
sudo ufw allow OpenSSH
sudo ufw allow from 192.168.1.0/24 to any port 8089 proto tcp   # solo tu red local
sudo ufw enable
sudo ufw status verbose
```

`192.168.1.0/24` significa "todas las IP de la 192.168.1.1 a la 192.168.1.254".
Si tu red usa otro rango (por ejemplo `192.168.0.x`), cámbialo.

**No abras el puerto 8089 en el router** (port forwarding). El servidor usa
`security mode="None"`, que está bien en tu red de casa pero no en internet.

## 10. Comprobar desde un equipo Windows

En PowerShell:

```powershell
Test-NetConnection 192.168.1.50 -Port 8089      # TcpTestSucceeded : True
```

Para probar también los callbacks sin abrir el juego, usa la herramienta del
repositorio (necesita el SDK de .NET 10):

```powershell
cd Herramientas\PruebaConexion
dotnet run -- 192.168.1.50                  # salas, chat y desconexiones
dotnet run -- 192.168.1.50 --base-datos     # además registra un jugador de prueba
```

## 11. Apuntar los clientes al servidor

Los clientes siguen en .NET Framework, no hay que migrarlos. Solo cambia la
dirección del servidor en su configuración.

**En la carpeta del juego ya compilado**, el archivo es
`RompecabezasFei.exe.config` y está junto a `RompecabezasFei.exe`. Ábrelo
en PowerShell dentro de esa carpeta y ejecuta:

```powershell
(Get-Content RompecabezasFei.exe.config) -replace 'net.tcp://localhost:8089', 'net.tcp://192.168.1.50:8089' | Set-Content RompecabezasFei.exe.config
```

**En el código**, el archivo es `ClienteRompecabezasFei/RompecabezasFei/App.config`,
con las 6 líneas `<endpoint address="net.tcp://localhost:8089/Contratos" ...>`.

Copia la carpeta del juego con el `.config` ya cambiado a cada equipo.

## 12. Actualizar el servidor cuando cambies el código

```bash
cd ~/RompecabezasFEI && git pull
cd ServidorRompecabezasFei
dotnet publish Servidor/Servidor.csproj -c Release -o ~/rompecabezasfei-publicado
sudo systemctl stop rompecabezasfei
sudo cp -r ~/rompecabezasfei-publicado/. /opt/rompecabezasfei/
sudo systemctl start rompecabezasfei
```

## Problemas comunes

| Síntoma | Qué revisar |
|---|---|
| El cliente tarda y dice que no encuentra el servidor | `systemctl status rompecabezasfei`, la IP en el `.config` del cliente, y `sudo ufw status` |
| `Test-NetConnection` falla pero en el servidor `ss` muestra el puerto | El firewall (`ufw`) o el rango de red en la regla |
| `address already in use` en `journalctl` | Hay otro servidor corriendo en el 8089 (por ejemplo, el que lanzaste a mano en el paso 7) |
| `No se encontró la cadena de conexión` | `DOTNET_ENVIRONMENT` en el archivo `.env` y `Datos/appsettings.Production.json` |
| `password authentication failed for user "rompecabezasuser"` | La contraseña en `/etc/rompecabezasfei/rompecabezasfei.env` |
| No llegan los correos de confirmación | `ROMPECABEZAS_Correo__Contrasena` debe ser una contraseña de aplicación de Gmail |
| Errores del juego | `journalctl -u rompecabezasfei` y los archivos en `/opt/rompecabezasfei/Registros/` |
