# CI/CD con GitHub Actions en el homelab

El repositorio tiene dos workflows en `.github/workflows/`. Los dos corren
en un **runner self-hosted** instalado en el servidor Ubuntu, con la
etiqueta `rompecabezas`, así que no gastan minutos de GitHub.

| Workflow | Cuándo corre | Qué hace |
|---|---|---|
| `ci.yml` | En cada push a `master`, en cada pull request hacia `master` y a mano | Compila el servidor y el cliente. Después levanta un servidor de prueba en el puerto 18089 y corre `PruebaConexion` (salas, chat y desconexiones) |
| `deploy.yml` | Cuando `ci.yml` pasa en un push a `master`, y a mano | Publica el servidor en `/opt/rompecabezasfei`, reinicia el servicio y comprueba que responde. Si falla, regresa a la versión anterior |

```
push a master ──► CI (compila + prueba) ──✓──► Deploy (publica + reinicia + comprueba)
                                        └─✗──► no se despliega nada
```

> Esta guía supone que ya seguiste [DESPLIEGUE-UBUNTU.md](DESPLIEGUE-UBUNTU.md):
> el servidor ya está en `/opt/rompecabezasfei` y el servicio
> `rompecabezasfei` funciona.

---

## 1. Crear un usuario para el runner

El runner va a ejecutar código del repositorio, así que conviene que tenga su
propio usuario, sin permisos de administrador:

```bash
sudo useradd --create-home --shell /bin/bash github-runner
sudo usermod -aG systemd-journal github-runner     # para leer journalctl en el deploy
sudo apt install -y rsync curl
```

## 2. Darle permisos solo para lo que necesita el deploy

**a) Que pueda reemplazar los archivos del servidor.** La carpeta pasa a ser
del usuario del runner. Solo `Registros/` sigue siendo del usuario del
servicio, porque ahí escribe el servidor:

```bash
sudo chown -R github-runner:github-runner /opt/rompecabezasfei
sudo chown -R rompecabezas:rompecabezas /opt/rompecabezasfei/Registros
```

El servicio sigue pudiendo leer los archivos, porque son legibles para todos.

**b) Que pueda detener e iniciar el servicio, y nada más.** Crea una regla de
`sudo` que no pida contraseña solo para esos comandos:

```bash
sudo visudo -f /etc/sudoers.d/github-runner
```

Escribe esto, guarda y sal:

```
github-runner ALL=(root) NOPASSWD: /usr/bin/systemctl stop rompecabezasfei, /usr/bin/systemctl start rompecabezasfei, /usr/bin/systemctl restart rompecabezasfei
```

`visudo` revisa la sintaxis antes de guardar. Si hay un error, te avisa y no
rompe `sudo`. Para comprobar que funciona:

```bash
sudo -u github-runner sudo -n systemctl restart rompecabezasfei && echo "permiso OK"
```

## 3. Instalar el runner

1. En GitHub, abre el repositorio y ve a **Settings → Actions → Runners → New self-hosted runner**.
2. Elige **Linux** y **x64**. GitHub te muestra los comandos con la versión
   actual y un **token** que dura una hora.
3. En el servidor, entra como el usuario del runner y ejecuta los comandos de
   la sección **Download** de esa página:

   ```bash
   sudo -iu github-runner
   mkdir actions-runner && cd actions-runner
   # aquí van los comandos de "Download" que te da GitHub (curl ... y tar xzf ...)
   ```

4. Configúralo con la etiqueta `rompecabezas`. Usa la URL y el token de la
   sección **Configure**:

   ```bash
   ./config.sh --url https://github.com/LuisElizaldeX/RompecabezasFEI \
     --token EL_TOKEN_DE_GITHUB \
     --name homelab \
     --labels rompecabezas \
     --unattended
   exit        # regresa a tu usuario
   ```

5. Instálalo como servicio para que arranque solo al prender el equipo:

   ```bash
   cd /home/github-runner/actions-runner
   sudo ./svc.sh install github-runner
   sudo ./svc.sh start
   sudo ./svc.sh status
   ```

En **Settings → Actions → Runners** debe aparecer `homelab` como **Idle**, con
las etiquetas `self-hosted`, `Linux`, `X64` y `rompecabezas`.

## 4. Guardar la contraseña de la base de datos para la CI

La prueba de humo de la CI levanta un servidor temporal que se conecta a la
misma base de datos (crear salas la consulta). Ese servidor necesita la
contraseña de PostgreSQL:

1. En GitHub, ve a **Settings → Secrets and variables → Actions → New repository secret**.
2. Nombre: `BD_CONTRASENA`. Valor: la contraseña de `rompecabezasuser`.

El deploy no la necesita, porque el servicio ya la lee de
`/etc/rompecabezasfei/rompecabezasfei.env`.

## 5. Proteger el servidor (repositorio público)

Un runner self-hosted ejecuta el código que le llegue, y tu repositorio es
público. Para que nadie pueda correr código en tu servidor con un pull
request desde un fork:

- `ci.yml` ya **ignora los pull requests que vienen de forks**. Solo corre en
  ramas de tu propio repositorio.
- Además, en **Settings → Actions → General → Fork pull request workflows from
  outside collaborators**, elige **Require approval for all outside collaborators**.
- No agregues workflows nuevos con `runs-on: [self-hosted, rompecabezas]` que
  se disparen con `pull_request_target`.

## 6. Probarlo

1. Ve a **Actions → CI → Run workflow** y lánzalo sobre `master`. Debe
   terminar en verde con `Todo funciona.` en el paso de la prueba de humo.
2. Ve a **Actions → Deploy → Run workflow**. Al terminar, revisa en el servidor:

   ```bash
   systemctl status rompecabezasfei
   ls -la --time-style=+%H:%M /opt/rompecabezasfei/Servidor.dll    # hora de la actualización
   ```

A partir de ahí, cada vez que hagas merge de un PR a `master`, primero corre
la CI y, si pasa, el deploy actualiza el servidor solo.

## Cómo funciona el deploy

1. Publica el servidor en una carpeta temporal.
2. Respalda `/opt/rompecabezasfei` (sin `Registros/`) en
   `/home/github-runner/rompecabezasfei-respaldo`.
3. Detiene el servicio, copia la versión nueva con `rsync --delete` (sin tocar
   `Registros/`) y lo vuelve a iniciar.
4. Espera a que el puerto 8089 responda y corre `PruebaConexion` contra el
   servidor real. Esto crea dos invitados de prueba unos segundos.
5. Si algún paso falla, muestra las últimas líneas de `journalctl`, restaura
   el respaldo y reinicia el servicio con la versión anterior.

## Problemas comunes

| Síntoma | Qué revisar |
|---|---|
| El job se queda en "Waiting for a runner" | Que el runner esté **Idle** en Settings → Actions → Runners (`sudo ./svc.sh status`) y que tenga la etiqueta `rompecabezas` |
| `dotnet: command not found` en el job | El runner corre como `github-runner`. Revisa con `sudo -u github-runner dotnet --info` |
| `sudo: a password is required` | La regla de `/etc/sudoers.d/github-runner`; la ruta debe ser `/usr/bin/systemctl` (compruébala con `which systemctl`) |
| `rsync: ... Permission denied` | El dueño de `/opt/rompecabezasfei` (paso 2a) |
| La prueba de humo de la CI falla al crear la sala | El secreto `BD_CONTRASENA` (paso 4) |
| `log4net:ERROR Could not create Appender` en el registro de la CI | No afecta. `/tmp/.dotnet` lo creó otro usuario; el servidor sigue funcionando |
