namespace Hosting.Api.DTOs;

public class ServidorListadoDTO
{
    public Guid IdServidor { get; set; }

    public string Nombre { get; set; } = null!;

    public string EstadoActual { get; set; } = null!;

    public DateTime FechaCreacion { get; set; }

    public string NombreMundo { get; set; } = null!;

    public string TamanoMundo { get; set; } = null!;

    public string Dificultad { get; set; } = null!;

    public short MaxJugadores { get; set; }

    public int PuertoHost { get; set; }

    public string Protocolo { get; set; } = null!;

    public string VersionJuego { get; set; } = null!;
}