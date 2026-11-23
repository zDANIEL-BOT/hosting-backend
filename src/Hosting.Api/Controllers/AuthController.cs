using System.Security.Claims;
using Hosting.Api.Auth;
using Hosting.Api.Data;
using Hosting.Api.DTOs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hosting.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly HostingDbContext _context;
    private readonly IAuthenticationSchemeProvider _schemes;
    private readonly IConfiguration _configuration;

    public AuthController(
        HostingDbContext context,
        IAuthenticationSchemeProvider schemes,
        IConfiguration configuration)
    {
        _context = context;
        _schemes = schemes;
        _configuration = configuration;
    }

    /// <summary>Lista los proveedores OAuth que están configurados y disponibles.</summary>
    [HttpGet("providers")]
    public async Task<IActionResult> ProveedoresDisponibles()
    {
        var disponibles = new List<string>();

        foreach (var proveedor in ProveedoresAuth.Todos)
        {
            var scheme = await _schemes.GetSchemeAsync(proveedor);

            if (scheme is not null)
            {
                disponibles.Add(proveedor);
            }
        }

        return Ok(disponibles);
    }

    /// <summary>Inicia el flujo de login con el proveedor indicado.</summary>
    [HttpGet("login/{proveedor}")]
    public async Task<IActionResult> Login(
        string proveedor,
        [FromQuery] string? returnUrl)
    {
        var proveedorNormalizado = ProveedoresAuth.Todos
            .FirstOrDefault(p => p.Equals(proveedor, StringComparison.OrdinalIgnoreCase));

        if (proveedorNormalizado is null)
        {
            return NotFound(new { error = "Proveedor no soportado." });
        }

        var scheme = await _schemes.GetSchemeAsync(proveedorNormalizado);

        if (scheme is null)
        {
            return BadRequest(new { error = "Proveedor no configurado." });
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = ResolverReturnUrl(returnUrl)
        };

        return Challenge(properties, scheme.Name);
    }

    /// <summary>Devuelve la identidad del usuario autenticado (recurso protegido).</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var idTexto = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(idTexto, out var idUsuario))
        {
            return Unauthorized();
        }

        var proveedores = await _context.IdentidadesExternas
            .Where(x => x.IdUsuario == idUsuario)
            .Select(x => x.Proveedor)
            .OrderBy(x => x)
            .ToListAsync();

        var dto = new UsuarioAutenticadoDTO
        {
            Id = idUsuario,
            Nombre = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Usuario",
            Correo = User.FindFirstValue(ClaimTypes.Email),
            Rol = User.FindFirstValue(ClaimTypes.Role) ?? "USER",
            Proveedores = proveedores
        };

        return Ok(dto);
    }

    /// <summary>Cierra la sesión actual (elimina la cookie de autenticación).</summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return Ok(new { mensaje = "Sesión cerrada correctamente." });
    }

    private string ResolverReturnUrl(string? returnUrl)
    {
        var frontend = _configuration["Auth:FrontendUrl"] ?? "http://localhost:5173";

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return frontend;
        }

        // Evita "open redirect": solo se permite volver al frontend configurado.
        return returnUrl.StartsWith(frontend, StringComparison.OrdinalIgnoreCase)
            ? returnUrl
            : frontend;
    }
}