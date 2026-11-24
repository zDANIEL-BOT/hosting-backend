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
}
