using System.ComponentModel.DataAnnotations;

namespace Hosting.Api.DTOs;

public class RegistroUsuarioDTO
{
    [Required]
    [StringLength(80, MinimumLength = 3)]
    public string NombreUsuario { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Correo { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Contrasena { get; set; } = string.Empty;
}