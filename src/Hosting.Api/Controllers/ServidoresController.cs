
using Hosting.Api.DTOs;
using Hosting.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hosting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
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
            // Temporalmente utilizamos un usuario existente.
            // Después obtendremos este identificador desde el JWT.
            Guid idUsuario = Guid.Parse(
                "68445ede-7bd4-4a1f-b80c-2a697c93bfd5"
            );

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
