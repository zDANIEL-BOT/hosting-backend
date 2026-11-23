using Hosting.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hosting.Api.Services;

public class PuertoService
{
    private readonly HostingDbContext _context;
    private readonly int _puertoInicial;
    private readonly int _puertoFinal;

    public PuertoService(
        HostingDbContext context,
        IConfiguration configuration)
    {
        _context = context;

        if (!int.TryParse(
                configuration["Hosting:Ports:Start"],
                out _puertoInicial) ||
            !int.TryParse(
                configuration["Hosting:Ports:End"],
                out _puertoFinal))
        {
            throw new InvalidOperationException(
                "El rango de puertos no está configurado correctamente."
            );
        }

        if (_puertoInicial < 1 ||
            _puertoFinal > 65535 ||
            _puertoInicial > _puertoFinal)
        {
            throw new InvalidOperationException(
                "El rango de puertos configurado no es válido."
            );
        }
    }

    public async Task<int?> ObtenerPuertoDisponibleAsync()
    {
        var puertosActivos = await _context.PuertosServidores
            .Where(p => p.Activo)
            .Select(p => p.PuertoHost)
            .ToListAsync();

        for (int puerto = _puertoInicial;
             puerto <= _puertoFinal;
             puerto++)
        {
            if (!puertosActivos.Contains(puerto))
            {
                return puerto;
            }
        }

        return null;
    }
}