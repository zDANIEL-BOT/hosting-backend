using System.ComponentModel.DataAnnotations;

namespace Hosting.Api.DTOs;

public class LoginUsuarioDTO
{
    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Correo { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Contrasena { get; set; } = string.Empty;
}