using Hosting.Api.DTOs;
using Hosting.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Hosting.Api.Controllers;


[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ServidoresController : ControllerBase
{
    private readonly ServidorService _servidorService;

    public ServidoresController(ServidorService servidorService)
    {
        _servidorService = servidorService;
    }


    [HttpGet]
    public async Task<IActionResult> ObtenerServidores()
    {
        var idUsuarioClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(idUsuarioClaim, out Guid idUsuario))
        {
            return Unauthorized();
        }

        var servidores = await _servidorService.ObtenerServidoresAsync(idUsuario);

        return Ok(servidores);
    }



    [HttpPost]
    public async Task<IActionResult> CrearServidor(
    [FromBody] CrearServidorDTO solicitud)
    {
        try
        {
            // Obtener el ID del usuario desde los claims de autenticación.
            var idUsuarioClaim = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (!Guid.TryParse(idUsuarioClaim, out Guid idUsuario))
            {
                return Unauthorized();
            }

            Guid idServidor = await _servidorService.CrearServidorAsync(
                idUsuario,
                solicitud
            );

            return Created(
                $"/api/Servidores/{idServidor}",
                new
                {
                    IdServidor = idServidor,
                    Mensaje = "El servidor se creó correctamente."
                }
            );
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                Mensaje = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                Mensaje = ex.Message
            });
        }
    }

    private Guid? ObtenerIdUsuario()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(claim, out Guid idUsuario)
            ? idUsuario
            : null;
    }

    [HttpGet("{idServidor:guid}/estado")]
    public async Task<IActionResult> ObtenerEstado(Guid idServidor)
    {
        var idUsuario = ObtenerIdUsuario();
        if (idUsuario == null) return Unauthorized();

        try
        {
            var estado = await _servidorService.ObtenerEstadoRealAsync(
                idUsuario.Value, idServidor
            );

            return Ok(new { Estado = estado });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Mensaje = ex.Message });
        }
    }

    [HttpPost("{idServidor:guid}/iniciar")]
    public async Task<IActionResult> Iniciar(Guid idServidor)
    {
        var idUsuario = ObtenerIdUsuario();
        if (idUsuario == null) return Unauthorized();

        try
        {
            await _servidorService.IniciarServidorAsync(idUsuario.Value, idServidor);
            return Ok(new { Mensaje = "Servidor iniciado correctamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Mensaje = ex.Message });
        }
    }

    [HttpPost("{idServidor:guid}/detener")]
    public async Task<IActionResult> Detener(Guid idServidor)
    {
        var idUsuario = ObtenerIdUsuario();
        if (idUsuario == null) return Unauthorized();

        try
        {
            await _servidorService.DetenerServidorAsync(idUsuario.Value, idServidor);
            return Ok(new { Mensaje = "Servidor detenido correctamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Mensaje = ex.Message });
        }
    }

    [HttpPost("{idServidor:guid}/reiniciar")]
    public async Task<IActionResult> Reiniciar(Guid idServidor)
    {
        var idUsuario = ObtenerIdUsuario();
        if (idUsuario == null) return Unauthorized();

        try
        {
            await _servidorService.ReiniciarServidorAsync(idUsuario.Value, idServidor);
            return Ok(new { Mensaje = "Servidor reiniciado correctamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Mensaje = ex.Message });
        }
    }

    [HttpDelete("{idServidor:guid}")]
    public async Task<IActionResult> Eliminar(Guid idServidor)
    {
        var idUsuario = ObtenerIdUsuario();
        if (idUsuario == null) return Unauthorized();

        try
        {
            await _servidorService.EliminarServidorAsync(idUsuario.Value, idServidor);
            return Ok(new { Mensaje = "Servidor eliminado correctamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Mensaje = ex.Message });
        }
    }
}
