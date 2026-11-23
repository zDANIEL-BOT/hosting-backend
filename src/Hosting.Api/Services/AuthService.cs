using System.Security.Claims;
using Hosting.Api.Data;
using Hosting.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hosting.Api.Services;

/// <summary>
/// Vincula la identidad devuelta por un proveedor OAuth (Discord, GitHub, Google,
/// TikTok) con el usuario local de la aplicación, creándolo si no existe.
/// </summary>
public class AuthService
{
    private readonly HostingDbContext _context;

    public AuthService(HostingDbContext context)
    {
        _context = context;
    }

    public async Task VincularAsync(ClaimsPrincipal principal, string proveedor)
    {
        var identidad = principal.Identities.FirstOrDefault(i => i.IsAuthenticated)
            ?? principal.Identities.First();

        var externalId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email);
        var nombre = principal.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(externalId))
        {
            throw new InvalidOperationException(
                $"El proveedor {proveedor} no devolvió un identificador de usuario.");
        }

        // 1. Buscar una identidad externa ya registrada para este proveedor.
        var identidadDb = await _context.IdentidadesExternas
            .Include(x => x.Usuario)
            .ThenInclude(u => u.Rol)
            .FirstOrDefaultAsync(x =>
                x.Proveedor == proveedor &&
                x.IdUsuarioProveedor == externalId);

        Usuario usuario;

        if (identidadDb?.Usuario is not null)
        {
            usuario = identidadDb.Usuario;
        }
        else
        {
            // 2. Intentar vincular por correo si el proveedor lo entregó.
            Usuario? usuarioExistente = null;
            if (!string.IsNullOrWhiteSpace(email))
            {
                usuarioExistente = await _context.Usuarios
                    .Include(u => u.Rol)
                    .FirstOrDefaultAsync(u => u.Correo == email);
            }

            // 3. Si no existe, crear un usuario nuevo con rol USER.
            if (usuarioExistente is null)
            {
                var rolUsuario = await _context.Roles
                    .FirstOrDefaultAsync(r => r.Codigo == "USER");

                var correoFinal = string.IsNullOrWhiteSpace(email)
                    ? $"{proveedor.ToLowerInvariant()}-{externalId}@hosting.local"
                    : email;

                usuario = new Usuario
                {
                    IdUsuario = Guid.NewGuid(),
                    IdRol = rolUsuario?.IdRol ?? 2,
                    NombreUsuario = Recortar(nombre, 80)
                        ?? $"{proveedor}_{externalId[..Math.Min(externalId.Length, 12)]}",
                    Correo = Recortar(correoFinal, 255)!,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                };

                _context.Usuarios.Add(usuario);
            }
            else
            {
                usuario = usuarioExistente;
            }

            // 4. Registrar la identidad externa vinculada al usuario local.
            _context.IdentidadesExternas.Add(new IdentidadExterna
            {
                IdIdentidad = Guid.NewGuid(),
                IdUsuario = usuario.IdUsuario,
                Proveedor = proveedor,
                IdUsuarioProveedor = Recortar(externalId, 255)!,
                CorreoProveedor = email,
                FechaVinculacion = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }

        // 5. Sustituir los claims del proveedor por la identidad local.
        var rolCodigo = usuario.Rol?.Codigo ?? "USER";

        foreach (var tipo in new[]
                 {
                     ClaimTypes.NameIdentifier,
                     ClaimTypes.Name,
                     ClaimTypes.Email
                 })
        {
            foreach (var claim in identidad.FindAll(tipo).ToList())
            {
                identidad.RemoveClaim(claim);
            }
        }

        identidad.AddClaim(new Claim(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()));
        identidad.AddClaim(new Claim(ClaimTypes.Name, usuario.NombreUsuario));
        identidad.AddClaim(new Claim(ClaimTypes.Email, usuario.Correo));
        identidad.AddClaim(new Claim(ClaimTypes.Role, rolCodigo));
        identidad.AddClaim(new Claim("urn:hosting:proveedor", proveedor));
    }

    private static string? Recortar(string? valor, int maximo)
    {
        if (string.IsNullOrEmpty(valor))
        {
            return valor;
        }

        return valor.Length <= maximo ? valor : valor[..maximo];
    }
}