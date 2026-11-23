using Docker.DotNet;
using Docker.DotNet.Models;

namespace Hosting.Api.Services;

public class DockerService
{
    private readonly IDockerClient _dockerClient;

    public DockerService(IDockerClient dockerClient)
    {
        _dockerClient = dockerClient;
    }

    public Task<VersionResponse> ObtenerVersionAsync()
    {
        return _dockerClient.System.GetVersionAsync();
    }

    public async Task<IList<ContainerListResponse>> ObtenerContenedoresAsync()
    {
        return await _dockerClient.Containers.ListContainersAsync(
            new ContainersListParameters
            {
                All = true
            }
        );
    }

    public async Task<string> CrearContenedorAsync(
    Guid idServidor,
    string nombreContenedor,
    string imagen,
    int puertoHost,
    string rutaDatos,
    string rutaConfiguracion)
    {
        var parametros = new CreateContainerParameters
        {
            Image = imagen,

            Name = nombreContenedor,

            Cmd = new List<string>
            {
                "-config",
                "/opt/terraria/serverconfig.txt"
            },

            ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                ["7777/tcp"] = new EmptyStruct()
            },

            Labels = new Dictionary<string, string>
            {
                ["hosting.managed"] = "true",
                ["hosting.server_id"] = idServidor.ToString()
            },

            HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    ["7777/tcp"] = new List<PortBinding>
                {
                    new PortBinding
                    {
                        HostPort = puertoHost.ToString()
                    }
                }
                },

                Binds = new List<string>
            {
                $"{rutaDatos}:/data",
                $"{rutaConfiguracion}:/opt/terraria/serverconfig.txt:ro"
            }
            }
        };

        var resultado = await _dockerClient.Containers.CreateContainerAsync(
            parametros
        );

        return resultado.ID;
    }

    public async Task<bool> IniciarContenedorAsync(string idContenedor)
    {
        return await _dockerClient.Containers.StartContainerAsync(
            idContenedor,
            new ContainerStartParameters()
        );
    }

    public async Task DetenerContenedorAsync(string idContenedor)
    {
        await _dockerClient.Containers.StopContainerAsync(
            idContenedor,
            new ContainerStopParameters()
        );
    }

    public async Task ReiniciarContenedorAsync(string idContenedor)
    {
        await _dockerClient.Containers.RestartContainerAsync(
            idContenedor,
            new ContainerRestartParameters()
        );
    }

    public async Task EliminarContenedorAsync(string idContenedor)
    {
        await _dockerClient.Containers.RemoveContainerAsync(
            idContenedor,
            new ContainerRemoveParameters
            {
                Force = false,
                RemoveVolumes = false
            }
        );
    }
}