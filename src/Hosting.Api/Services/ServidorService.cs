using Hosting.Api.Data;
using Hosting.Api.DTOs;
using Hosting.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hosting.Api.Services;

public class ServidorService
{
    private readonly HostingDbContext _context;
    private readonly DockerService _dockerService;
    private readonly ConfiguracionTerrariaService _configuracionTerrariaService;
    private readonly PuertoService _puertoService;
    private readonly ProteccionContrasenaService _proteccionContrasenaService;
    private readonly ILogger<ServidorService> _logger;
    private readonly string _storageRoot;

    public ServidorService(
        HostingDbContext context,
        DockerService dockerService,
        ConfiguracionTerrariaService configuracionTerrariaService,
        PuertoService puertoService,
        ProteccionContrasenaService proteccionContrasenaService,
        IConfiguration configuration,
        ILogger<ServidorService> logger)
    {
        _context = context;
        _dockerService = dockerService;
        _configuracionTerrariaService = configuracionTerrariaService;
        _puertoService = puertoService;
        _proteccionContrasenaService = proteccionContrasenaService;
        _logger = logger;
        _storageRoot = configuration["Hosting:StorageRoot"]
            ?? throw new InvalidOperationException(
                "No se configuró Hosting:StorageRoot."
            );
    }

    private async Task<VersionJuego> ObtenerVersionTerrariaAsync()
    {
        var version = await _context.VersionesJuegos
            .Where(v =>
                v.Activa &&
                v.Juego.Codigo == "TERRARIA"
            )
            .OrderByDescending(v => v.IdVersion)
            .FirstOrDefaultAsync();

        if (version == null)
        {
            throw new InvalidOperationException(
                "No hay una versión activa de Terraria disponible."
            );
        }

        if (string.IsNullOrWhiteSpace(version.EtiquetaImagen))
        {
            throw new InvalidOperationException(
                "La versión de Terraria no tiene una imagen Docker configurada."
            );
        }

        return version;
    }

    private void ValidarSolicitud(CrearServidorDTO solicitud)
    {
        if (string.IsNullOrWhiteSpace(solicitud.Nombre) ||
            solicitud.Nombre.Length > 100)
        {
            throw new ArgumentException(
                "El nombre del servidor es obligatorio y no puede superar los 100 caracteres."
            );
        }

        if (string.IsNullOrWhiteSpace(solicitud.NombreMundo) ||
            solicitud.NombreMundo.Length > 100)
        {
            throw new ArgumentException(
                "El nombre del mundo es obligatorio y no puede superar los 100 caracteres."
            );
        }

        if (solicitud.MaxJugadores < 1 ||
            solicitud.MaxJugadores > 255)
        {
            throw new ArgumentException(
                "La cantidad de jugadores debe estar entre 1 y 255."
            );
        }
    }


    private async Task<Usuario> ObtenerUsuarioActivoAsync(Guid idUsuario)
    {
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u =>
                u.IdUsuario == idUsuario &&
                u.Activo
            );

        if (usuario == null)
        {
            throw new InvalidOperationException(
                "El usuario no existe o su cuenta está desactivada."
            );
        }

        return usuario;
    }


    private async Task<(
        Guid IdServidor,
        Usuario Usuario,
        VersionJuego Version,
        int PuertoHost
    )> ObtenerDatosCreacionAsync(
        Guid idUsuario,
        CrearServidorDTO solicitud)
    {
        ValidarSolicitud(solicitud);

        var usuario = await ObtenerUsuarioActivoAsync(idUsuario);

        var version = await ObtenerVersionTerrariaAsync();

        int puertoHost = await ObtenerPuertoDisponibleAsync();

        Guid idServidor = Guid.NewGuid();

        return (
            idServidor,
            usuario,
            version,
            puertoHost
        );
    }


    private async Task GuardarRegistrosAsync(
        Guid idServidor,
        Guid idUsuario,
        VersionJuego version,
        int puertoHost,
        string rutaDatos,
        CrearServidorDTO solicitud)
    {
        var servidor = new Servidor
        {
            IdServidor = idServidor,
            IdUsuario = idUsuario,
            IdVersion = version.IdVersion,
            Nombre = solicitud.Nombre.Trim(),
            IdContenedor = null,
            EstadoDeseado = "ENCENDIDO",
            EstadoActual = "CREANDO",
            FechaCreacion = DateTime.UtcNow
        };

        var configuracion = new ConfiguracionServidor
        {
            IdServidor = idServidor,
            NombreMundo = solicitud.NombreMundo.Trim(),
            TamanoMundo = solicitud.TamanoMundo,
            Dificultad = solicitud.Dificultad,
            MaxJugadores = solicitud.MaxJugadores,
            ContrasenaCifrada = string.IsNullOrWhiteSpace(solicitud.Contrasena)
                ? null
                : _proteccionContrasenaService.Proteger(solicitud.Contrasena),
            Seguro = true
        };

        var puerto = new PuertoServidor
        {
            IdPuerto = Guid.NewGuid(),
            IdServidor = idServidor,
            PuertoHost = puertoHost,
            PuertoContenedor = 7777,
            Protocolo = "TCP",
            Activo = true
        };

        var almacenamiento = new AlmacenamientoServidor
        {
            IdAlmacenamiento = Guid.NewGuid(),
            IdServidor = idServidor,
            ClaveAlmacenamiento = Guid.NewGuid(),
            RutaAlmacenamiento = rutaDatos,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Servidores.Add(servidor);
        _context.ConfiguracionesServidores.Add(configuracion);
        _context.PuertosServidores.Add(puerto);
        _context.AlmacenamientosServidores.Add(almacenamiento);

        await _context.SaveChangesAsync();
    }


    private async Task<(
        Guid IdServidor,
        VersionJuego Version,
        int PuertoHost,
        string RutaDatos,
        string RutaConfiguracion
    )> PrepararServidorAsync(
        Guid idUsuario,
        CrearServidorDTO solicitud)
    {
        // 1. Obtener los datos necesarios.
        var datos = await ObtenerDatosCreacionAsync(
            idUsuario,
            solicitud
        );

        string rutaServidor = ObtenerRutaServidor(datos.IdServidor);

        try
        {
            // 2. Preparar los directorios y el archivo de configuración.
            var almacenamiento = await PrepararAlmacenamientoAsync(
                solicitud,
                datos.IdServidor
            );

            // 3. Guardar los cuatro registros en PostgreSQL.
            await GuardarRegistrosAsync(
                datos.IdServidor,
                idUsuario,
                datos.Version,
                datos.PuertoHost,
                almacenamiento.RutaDatos,
                solicitud
            );

            // 4. Devolver los datos que necesitaremos para Docker.
            return (
                datos.IdServidor,
                datos.Version,
                datos.PuertoHost,
                almacenamiento.RutaDatos,
                almacenamiento.RutaConfiguracion
            );
        }
        catch
        {
            // Si falla la preparación o el guardado, limpiar el almacenamiento.
            if (Directory.Exists(rutaServidor))
            {
                Directory.Delete(rutaServidor, true);
            }

            throw;
        }
    }

    public async Task<Guid> CrearServidorAsync(
        Guid idUsuario,
        CrearServidorDTO solicitud)
    {
        // 1. Preparar el servidor y guardar sus registros.
        var datos = await PrepararServidorAsync(
            idUsuario,
            solicitud
        );

        string? idContenedor = null;

        try
        {
            // 2. Generar un nombre único para el contenedor.
            string nombreContenedor = $"terraria-{datos.IdServidor:N}";

            // 3. Crear el contenedor Docker.
            idContenedor = await _dockerService.CrearContenedorAsync(
                datos.IdServidor,
                nombreContenedor,
                datos.Version.EtiquetaImagen!,
                datos.PuertoHost,
                datos.RutaDatos,
                datos.RutaConfiguracion
            );

            // 4. Iniciar el contenedor.
            bool iniciado = await _dockerService.IniciarContenedorAsync(
                idContenedor
            );

            if (!iniciado)
            {
                throw new InvalidOperationException(
                    "No fue posible iniciar el contenedor del servidor."
                );
            }

            // 5. Actualizar el registro del servidor en PostgreSQL.
            var servidor = await _context.Servidores
                .FirstAsync(s => s.IdServidor == datos.IdServidor);

            servidor.IdContenedor = idContenedor;
            servidor.EstadoActual = "ENCENDIDO";
            servidor.FechaActualizacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // 6. Devolver el identificador del servidor.
            return datos.IdServidor;
        }
        catch
        {
            // 7. Si ocurre un error, intentar limpiar los recursos creados.
            try
            {
                await LimpiarCreacionFallidaAsync(
                    datos.IdServidor,
                    ObtenerRutaServidor(datos.IdServidor),
                    idContenedor
                );
            }
            catch (Exception errorLimpieza)
            {
                _logger.LogError(
                    errorLimpieza,
                    "También falló la limpieza del servidor {IdServidor}.",
                    datos.IdServidor
                );
            }

            throw;
        }
    }

    private async Task LimpiarCreacionFallidaAsync(
        Guid idServidor,
        string rutaServidor,
        string? idContenedor)
    {
        bool contenedorEliminado = true;

        // 1. Intentar detener y eliminar el contenedor.
        if (!string.IsNullOrWhiteSpace(idContenedor))
        {
            try
            {
                await _dockerService.DetenerContenedorAsync(idContenedor);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo detener el contenedor {IdContenedor}.",
                    idContenedor
                );
            }

            try
            {
                await _dockerService.EliminarContenedorAsync(idContenedor);
            }
            catch (Exception ex)
            {
                contenedorEliminado = false;

                _logger.LogError(
                    ex,
                    "No se pudo eliminar el contenedor {IdContenedor}.",
                    idContenedor
                );
            }
        }

        // 2. Si Docker no permitió eliminarlo, conservar los registros
        // y archivos para no perder el control de ese contenedor.
        if (!contenedorEliminado)
        {
            var servidorExistente = await _context.Servidores
                .FirstOrDefaultAsync(s => s.IdServidor == idServidor);

            if (servidorExistente != null)
            {
                servidorExistente.IdContenedor = idContenedor;
                servidorExistente.EstadoActual = "ERROR";
                servidorExistente.FechaActualizacion = DateTime.UtcNow;

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "No se pudo actualizar el estado del servidor {IdServidor}.",
                        idServidor
                    );
                }
            }

            return;
        }

        // 3. Eliminar los registros relacionados en PostgreSQL.
        try
        {
            var configuracion = await _context.ConfiguracionesServidores
                .FirstOrDefaultAsync(c => c.IdServidor == idServidor);

            var puerto = await _context.PuertosServidores
                .FirstOrDefaultAsync(p => p.IdServidor == idServidor);

            var almacenamiento = await _context.AlmacenamientosServidores
                .FirstOrDefaultAsync(a => a.IdServidor == idServidor);

            var servidor = await _context.Servidores
                .FirstOrDefaultAsync(s => s.IdServidor == idServidor);

            if (configuracion != null)
                _context.ConfiguracionesServidores.Remove(configuracion);

            if (puerto != null)
                _context.PuertosServidores.Remove(puerto);

            if (almacenamiento != null)
                _context.AlmacenamientosServidores.Remove(almacenamiento);

            if (servidor != null)
                _context.Servidores.Remove(servidor);

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "No se pudieron eliminar los registros del servidor {IdServidor}.",
                idServidor
            );

            // Si falla la eliminación de los registros,
            // conservamos también los archivos.
            return;
        }

        // 4. Eliminar los archivos del servidor.
        try
        {
            if (Directory.Exists(rutaServidor))
            {
                Directory.Delete(rutaServidor, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "No se pudo eliminar el almacenamiento del servidor {IdServidor}.",
                idServidor
            );
        }
    }

    private async Task<int> ObtenerPuertoDisponibleAsync()
    {
        int? puerto = await _puertoService.ObtenerPuertoDisponibleAsync();

        if (puerto == null)
        {
            throw new InvalidOperationException(
                "No hay puertos disponibles para crear otro servidor."
            );
        }

        return puerto.Value;
    }

    private string ObtenerRutaServidor(Guid idServidor)
    {
        return Path.Combine(_storageRoot, idServidor.ToString());
    }

    private async Task<(string RutaDatos, string RutaConfiguracion)>
        PrepararAlmacenamientoAsync(
            CrearServidorDTO solicitud,
            Guid idServidor)
    {
        string rutaServidor = ObtenerRutaServidor(idServidor);

        string rutaDatos = Path.Combine(rutaServidor, "data");

        string rutaConfiguracion = Path.Combine(
            rutaServidor,
            "serverconfig.txt"
        );

        await _configuracionTerrariaService.GenerarArchivoAsync(
            solicitud,
            idServidor,
            rutaDatos,
            rutaConfiguracion
        );

        return (rutaDatos, rutaConfiguracion);
    }


    public async Task<List<ServidorListadoDTO>> ObtenerServidoresAsync(Guid idUsuario)
    {
        return await _context.Servidores
            .AsNoTracking()
            .Where(s =>
                s.IdUsuario == idUsuario &&
                s.FechaEliminacion == null)
            .OrderByDescending(s => s.FechaCreacion)
            .Select(s => new ServidorListadoDTO
            {
                IdServidor = s.IdServidor,
                Nombre = s.Nombre,
                EstadoActual = s.EstadoActual,
                FechaCreacion = s.FechaCreacion,

                NombreMundo = s.Configuracion.NombreMundo,
                TamanoMundo = s.Configuracion.TamanoMundo,
                Dificultad = s.Configuracion.Dificultad,
                MaxJugadores = s.Configuracion.MaxJugadores,

                PuertoHost = s.Puerto.PuertoHost,
                Protocolo = s.Puerto.Protocolo,

                VersionJuego = s.VersionJuego.Version
            })
            .ToListAsync();
    }
}