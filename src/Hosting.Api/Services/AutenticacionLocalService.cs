
using Hosting.Api.Data;
using Hosting.Api.DTOs;
using Hosting.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hosting.Api.Services;

public class AutenticacionLocalService
{
    private readonly HostingDbContext _context;
    private readonly IPasswordHasher<Usuario> _passwordHasher;

    public AutenticacionLocalService(
        HostingDbContext context,
        IPasswordHasher<Usuario> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<Usuario> RegistrarAsync(RegistroUsuarioDTO solicitud)
    {
        var nombreUsuario = solicitud.NombreUsuario.Trim();
        var correo = solicitud.Correo.Trim().ToLowerInvariant();

        // Comprobar si el correo ya está registrado.
        var correoExiste = await _context.Usuarios
            .AnyAsync(u => u.Correo.Trim().ToLower() == correo);

        if (correoExiste)
        {
            throw new InvalidOperationException(
                "El correo electrónico ya está registrado.");
        }

        // Comprobar si el nombre de usuario ya está ocupado.
        var nombreExiste = await _context.Usuarios
            .AnyAsync(u => u.NombreUsuario.Trim().ToLower() ==
                           nombreUsuario.ToLower());

        if (nombreExiste)
        {
            throw new InvalidOperationException(
                "El nombre de usuario ya está ocupado.");
        }

        // Obtener el rol USER desde la base de datos.
        var rolUsuario = await _context.Roles
            .FirstOrDefaultAsync(r => r.Codigo == "USER");

        if (rolUsuario is null)
        {
            throw new InvalidOperationException(
                "No se encontró el rol USER en la base de datos.");
        }

        var usuario = new Usuario
        {
            IdUsuario = Guid.NewGuid(),
            IdRol = rolUsuario.IdRol,
            NombreUsuario = nombreUsuario,
            Correo = correo,
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            Rol = rolUsuario
        };

        // Generar un hash seguro de la contraseña.
        usuario.ContrasenaHash = _passwordHasher.HashPassword(
            usuario,
            solicitud.Contrasena
        );

        _context.Usuarios.Add(usuario);

        await _context.SaveChangesAsync();

        return usuario;
    }

    public async Task<Usuario?> AutenticarAsync(LoginUsuarioDTO solicitud)
    {
        var correo = solicitud.Correo.Trim().ToLowerInvariant();

        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u =>
                u.Correo.Trim().ToLower() == correo);

        // No permitir acceso a usuarios inexistentes o desactivados.
        if (usuario is null || !usuario.Activo)
        {
            return null;
        }

        // Los usuarios registrados exclusivamente mediante OAuth
        // pueden no tener una contraseña local.
        if (string.IsNullOrWhiteSpace(usuario.ContrasenaHash))
        {
            return null;
        }

        var resultado = _passwordHasher.VerifyHashedPassword(
            usuario,
            usuario.ContrasenaHash,
            solicitud.Contrasena
        );

        if (resultado == PasswordVerificationResult.Failed)
        {
            return null;
        }

        // Actualizar el hash si el algoritmo necesita renovarse.
        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.ContrasenaHash = _passwordHasher.HashPassword(
                usuario,
                solicitud.Contrasena
            );

            await _context.SaveChangesAsync();
        }

        return usuario;
    }
}
