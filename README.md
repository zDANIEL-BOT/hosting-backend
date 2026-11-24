# Guía de instalación, configuración y ejecución del proyecto Hosting

## 1\. Descripción general

Hosting es una aplicación web para administrar servidores de Terraria Vanilla. Permite a los usuarios autenticarse, crear sus servidores y realizar operaciones de administración sobre sus propios servidores.

Tecnologías utilizadas:

- Backend: ASP.NET Core Web API con .NET 10.
- Frontend: React, TypeScript y Vite.
- Base de datos: PostgreSQL 18.
- Contenedores: Docker.
- Persistencia: PostgreSQL para los datos de la aplicación y almacenamiento en disco para los datos de los servidores.

El proyecto está dividido en dos repositorios:

- `hosting-backend`
- `hosting-frontend`

## 2\. Requisitos previos

Instalar las siguientes herramientas:

- Git.
- .NET SDK 10.
- PostgreSQL 18.
- Node.js 22 o superior y npm.
- Docker Engine.
- Docker Compose, si se utiliza posteriormente la configuración integrada del equipo.

Comprobar las instalaciones:

```bash
git --version
dotnet --version
psql --version
node --version
npm --version
docker --version
```

Para las migraciones de Entity Framework Core, instalar la herramienta:

```bash
dotnet tool install --global dotnet-ef --version 10.0.4
```

Si ya está instalada, comprobar su versión:

```bash
dotnet ef --version
```

## 3\. Descargar los repositorios

Clonar los dos repositorios:

```bash
git clone URL_DEL_REPOSITORIO_BACKEND
git clone URL_DEL_REPOSITORIO_FRONTEND
```

Entrar al backend:

```bash
cd hosting-backend
```

Actualizar la rama principal:

```bash
git switch main
git pull
```

La misma operación se realiza en el repositorio frontend.

## 4\. Preparar PostgreSQL

Crear un usuario y una base de datos para la aplicación.

Entrar a PostgreSQL:

```bash
sudo -u postgres psql
```

Ejecutar las siguientes sentencias, reemplazando `CAMBIAR_PASSWORD` por una contraseña propia:

```sql
CREATE ROLE hosting_app
WITH LOGIN PASSWORD 'CAMBIAR_PASSWORD';

CREATE DATABASE hosting_db
OWNER hosting_app;

GRANT ALL PRIVILEGES ON DATABASE hosting_db TO hosting_app;
```

Salir:

```sql
\q
```

Comprobar la conexión:

```bash
psql -h localhost -U hosting_app -d hosting_db
```

PostgreSQL solicitará la contraseña configurada.

## 5\. Restaurar y compilar el backend

Desde la carpeta raíz de `hosting-backend`:

```bash
dotnet restore Hosting.slnx
dotnet build Hosting.slnx
```

La restauración descarga las dependencias y la compilación comprueba que el proyecto no tenga errores.

## 6\. Configurar User Secrets del backend

Las credenciales y configuraciones privadas no deben guardarse en `appsettings.json` ni subirse a GitHub.

El proyecto utiliza User Secrets durante el desarrollo.

Si el proyecto todavía no tiene un identificador de User Secrets:

```bash
dotnet user-secrets init --project src/Hosting.Api
```

Si ya está inicializado, no es necesario repetirlo.

Crear las carpetas de almacenamiento:

```bash
mkdir -p "$HOME/terraria-hosting/servers"
mkdir -p "$HOME/terraria-hosting/keys"
```

Configurar la conexión a PostgreSQL:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=hosting_db;Username=hosting_app;Password=CAMBIAR_PASSWORD" --project src/Hosting.Api
```

Configurar las rutas de almacenamiento:

```bash
dotnet user-secrets set "Hosting:StorageRoot" "$HOME/terraria-hosting/servers" --project src/Hosting.Api

dotnet user-secrets set "Hosting:DataProtectionKeys" "$HOME/terraria-hosting/keys" --project src/Hosting.Api
```

Configurar el origen del frontend y CORS:

```bash
dotnet user-secrets set "Auth:FrontendUrl" "http://localhost:5173" --project src/Hosting.Api

dotnet user-secrets set "Cors:AllowedOrigins" "http://localhost:5173" --project src/Hosting.Api
```

### Credenciales OAuth

La aplicación utiliza credenciales de proveedores OAuth. Para cada proveedor habilitado se deben obtener un Client ID y un Client Secret desde su plataforma de desarrolladores.

Configurar las credenciales de Google:

```bash
dotnet user-secrets set "Auth:Google:ClientId" "CLIENT_ID" --project src/Hosting.Api

dotnet user-secrets set "Auth:Google:ClientSecret" "CLIENT_SECRET" --project src/Hosting.Api
```

Configurar las credenciales de GitHub:

```bash
dotnet user-secrets set "Auth:GitHub:ClientId" "Ov23liuLxYefrV9RBZpX" --project src/Hosting.Api

dotnet user-secrets set "Auth:GitHub:ClientSecret" "53526da2d4e729869a445fd0a63c8e3ce3322164" --project src/Hosting.Api
```

Configurar las credenciales de Discord:

```bash
dotnet user-secrets set "Auth:Discord:ClientId" "1574171398513889341" --project src/Hosting.Api

dotnet user-secrets set "Auth:Discord:ClientSecret" "XB_LdVnMXBZVdkKT_ECzo2SAjBav0pvM" --project src/Hosting.Api
```

Los valores anteriores son marcadores: deben reemplazarse por credenciales reales.

Las URL de callback de cada proveedor deben coincidir exactamente con las rutas configuradas en el backend y registradas en las respectivas plataformas OAuth. Para consultar las rutas configuradas en el código:

```bash
grep -RniE "CallbackPath|signin-|AddGoogle|AddGitHub|AddDiscord" src/Hosting.Api
```

No se deben inventar las URL de callback ni reutilizar credenciales de otra persona.

Comprobar que los secretos se guardaron:

```bash
dotnet user-secrets list --project src/Hosting.Api
```

**Importante:** este comando muestra información sensible. No se debe compartir su salida completa ni incluirla en capturas públicas.

## 7\. Aplicar las migraciones de Entity Framework Core

Desde la raíz del backend, consultar las migraciones disponibles:

```bash
dotnet ef migrations list --project src/Hosting.Api --startup-project src/Hosting.Api
```

Aplicar las migraciones a la base de datos:

```bash
dotnet ef database update --project src/Hosting.Api --startup-project src/Hosting.Api
```

Este comando crea o actualiza las tablas de acuerdo con las migraciones existentes.

El proyecto cuenta con un inicializador de datos (`DatabaseSeeder`) que incorpora datos iniciales, entre ellos los roles ADMIN y USER y los datos relacionados con Terraria y su versión 1.4.5.8.

No se deben crear manualmente las tablas si las migraciones ya se encargan de ello.

Para comprobar las tablas:

```bash
psql -h localhost -U hosting_app -d hosting_db
```

Dentro de PostgreSQL:

```sql
\dt
```

Salir:

```sql
\q
```

## 8\. Ejecutar el backend

Desde la raíz del backend:

```bash
dotnet run --project src/Hosting.Api --urls http://localhost:5157
```

Comprobar que la API responde:

```bash
curl -i http://localhost:5157/api/auth/providers
```

Se espera una respuesta HTTP 200 con la información de los proveedores de autenticación disponibles.

La API queda disponible en:

`http://localhost:5157`

## 9\. Instalar y ejecutar el frontend

En otra terminal, entrar al repositorio frontend:

```bash
cd hosting-frontend
```

Instalar las dependencias:

```bash
npm ci
```

Crear el archivo de configuración local a partir del ejemplo:

```bash
cp .env.example .env
```

Configurar en `.env`:

```env
VITE_API_URL=http://localhost:5157
```

Compilar el frontend:

```bash
npm run build
```

Ejecutar en modo desarrollo:

```bash
npm run dev
```

Vite mostrará la dirección local, normalmente:

`http://localhost:5173`

Abrir esa dirección en el navegador.

El frontend utiliza cookies para las solicitudes autenticadas, por lo que las peticiones a la API deben conservar `credentials: "include"`. El backend debe permitir el origen del frontend mediante CORS y `AllowCredentials()`.

## 10\. Construir las imágenes Docker del backend

Desde la raíz de `hosting-backend`:

Construir la imagen de Miranda:

```bash
docker build -f Dockerfile.Miranda -t hosting-api:miranda .
```

Construir la imagen de Viza:

```bash
docker build -f Dockerfile.Viza -t hosting-api:viza .
```

Los Dockerfiles utilizan una compilación multi-etapa con .NET 10, publican `Hosting.Api` y ejecutan la aplicación mediante ASP.NET Core Runtime.

El puerto interno configurado para la API es `8080`.

Comprobar las imágenes:

```bash
docker images
```

## 11\. Ejecutar una prueba del backend en Docker

Para una prueba local en Ubuntu, el contenedor puede utilizar la red del host. En ese caso, la conexión a PostgreSQL debe apuntar a un servidor accesible desde el host.

Preparar las carpetas:

```bash
mkdir -p "$HOME/terraria-hosting/servers"
mkdir -p "$HOME/terraria-hosting/keys"
```

Ejecutar la imagen, reemplazando la contraseña:

```bash
docker run -d \
  --name hosting-api-test \
  --network host \
  -e "ConnectionStrings__DefaultConnection=Host=127.0.0.1;Port=5432;Database=hosting_db;Username=hosting_app;Password=CAMBIAR_PASSWORD" \
  -e "Hosting__StorageRoot=/data/servers" \
  -e "Hosting__DataProtectionKeys=/data/keys" \
  -v "$HOME/terraria-hosting/servers:/data/servers" \
  -v "$HOME/terraria-hosting/keys:/data/keys" \
  hosting-api:miranda
```

Consultar los registros:

```bash
docker logs hosting-api-test
```

Comprobar la API:

```bash
curl -i http://localhost:8080/api/auth/providers
```

Para detener y eliminar el contenedor de prueba:

```bash
docker rm -f hosting-api-test
```

**Importante:** User Secrets no se cargan automáticamente dentro de una imagen Docker. Las configuraciones deben pasarse mediante variables de entorno o mediante el mecanismo de configuración que utilice el despliegue.

Las claves de Data Protection deben mantenerse en almacenamiento persistente y compartido cuando se ejecuten varias instancias de la API, para conservar la compatibilidad de las cookies de autenticación.

## 12\. Construir la imagen Docker del frontend

Desde la raíz de `hosting-frontend`:

```bash
docker build \
  --build-arg VITE_API_URL=http://localhost:8080 \
  -t hosting-frontend:miranda .
```

Ejecutar:

```bash
docker run -d \
  --name hosting-frontend-test \
  -p 8081:80 \
  hosting-frontend:miranda
```

Abrir:

`http://localhost:8081`

En esta modalidad, el navegador accede a la API mediante `http://localhost:8080`. Por tanto, el backend debe permitir el origen `http://localhost:8081` en su configuración CORS.

Para configurar ese origen en el entorno del backend:

```bash
dotnet user-secrets set "Cors:AllowedOrigins" "http://localhost:8081" --project src/Hosting.Api
```

Si se ejecuta el backend en Docker, se debe pasar la configuración equivalente mediante:

```text
Cors__AllowedOrigins=http://localhost:8081
```

Después de cambiar la configuración de un contenedor, hay que recrearlo para que utilice los nuevos valores.

**Estado de esta prueba:** las imágenes de frontend y backend se construyeron correctamente por separado. La comunicación final del frontend en Docker con el backend debe comprobarse después de configurar CORS.

## 13\. Comprobaciones funcionales

Una vez iniciados los servicios:

1. Abrir el frontend.
2. Consultar los proveedores de autenticación.
3. Iniciar sesión.
4. Comprobar que el usuario autenticado puede acceder a sus servidores.
5. Crear un servidor.
6. Comprobar que se crea el contenedor Docker correspondiente.
7. Consultar su estado.
8. Iniciar, detener y reiniciar el servidor.
9. Comprobar que los eventos quedan registrados.
10. Eliminar un servidor propio y comprobar que se libera el puerto asociado.

Comandos útiles para revisar Docker:

```bash
docker ps -a
docker logs NOMBRE_DEL_CONTENEDOR
docker inspect NOMBRE_DEL_CONTENEDOR
```

## 14\. Solución de problemas comunes

### Error de conexión con PostgreSQL

Verificar que PostgreSQL esté iniciado, que la base de datos exista y que la contraseña de `ConnectionStrings:DefaultConnection` sea correcta.

### Error de contraseña SCRAM

Asegurarse de que la variable `ConnectionStrings__DefaultConnection` llegue al contenedor y contenga una contraseña válida. Una variable vacía puede provocar este error.

### Error `Failed to fetch` en el navegador

Comprobar:

- Que el backend esté ejecutándose.
- Que la URL de `VITE_API_URL` sea accesible desde el navegador.
- Que el origen del frontend esté permitido en CORS.
- Que CORS utilice `AllowCredentials()` cuando se usan cookies.
- Que el backend no esté redirigiendo las peticiones a una URL HTTPS no configurada.

### Error `libgssapi_krb5.so.2`

La imagen final del backend necesita la biblioteca del sistema `libgssapi-krb5-2`. Los Dockerfiles del backend incluyen su instalación.

### Los usuarios pierden la sesión al recrear contenedores

Comprobar que las claves de ASP.NET Core Data Protection estén almacenadas de forma persistente. En despliegues con varias instancias, todas deben compartir el mismo conjunto de claves.

## 15\. Consideraciones para el despliegue

- No subir contraseñas, Client Secrets ni archivos `.env` con valores reales a GitHub.
- Mantener los datos de PostgreSQL en un volumen persistente.
- Mantener los mundos y archivos de los servidores en almacenamiento persistente.
- Compartir las claves de Data Protection entre las instancias de la API.
- Configurar correctamente los orígenes CORS y las URL de callback OAuth.
- Utilizar una dirección de API accesible desde el navegador, no el nombre interno de un contenedor.

## 16\. Elementos que deben verificarse antes de considerar terminada la instalación integrada

La ejecución local del backend y frontend, las migraciones y la construcción individual de las imágenes Docker tienen procedimientos definidos en esta guía.

Sin embargo, no se debe asumir que están terminados o verificados los siguientes puntos hasta probarlos en el repositorio final:

- Docker Compose de desarrollo con backend y PostgreSQL.
- Integración del frontend en el mismo Docker Compose.
- Publicación de la imagen final del backend en Docker Hub.
- Docker Compose de liberación con cuatro instancias de backend.
- Prueba de autenticación y cookies compartidas entre las cuatro instancias.
- Comunicación final del frontend Docker con el backend mediante CORS o proxy.

Estos puntos forman parte de la verificación de la entrega y deben documentarse únicamente cuando existan los archivos y pruebas correspondientes.
