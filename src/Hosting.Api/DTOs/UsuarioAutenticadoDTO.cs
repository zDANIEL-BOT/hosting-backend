namespace Hosting.Api.DTOs;

public class UsuarioAutenticadoDTO
{
    public Guid Id { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Correo { get; set; }

    public string Rol { get; set; } = null!;

    public List<string> Proveedores { get; set; } = new();
}