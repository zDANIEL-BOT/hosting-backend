using Microsoft.AspNetCore.DataProtection;

namespace Hosting.Api.Services;

public class ProteccionContrasenaService
{
    private readonly IDataProtector _protector;

    public ProteccionContrasenaService(
        IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(
            "Hosting.Servidores.Contrasenas"
        );
    }

    public string Proteger(string contrasena)
    {
        return _protector.Protect(contrasena);
    }

    public string Desproteger(string contrasenaCifrada)
    {
        return _protector.Unprotect(contrasenaCifrada);
    }
}