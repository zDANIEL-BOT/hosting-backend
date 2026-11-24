using Hosting.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Hosting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DockerController : ControllerBase
{
    private readonly DockerService _dockerService;

    public DockerController(DockerService dockerService)
    {
        _dockerService = dockerService;
    }

    [HttpGet("contenedores")]
    public async Task<IActionResult> ObtenerContenedores()
    {
        var contenedores = await _dockerService.ObtenerContenedoresAsync();

        return Ok(contenedores);
    }
}