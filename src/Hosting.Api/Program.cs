using Hosting.Api.Data;
using Microsoft.EntityFrameworkCore;
using Docker.DotNet;
using Hosting.Api.Services;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<HostingDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

builder.Services.AddSingleton<IDockerClient>(_ =>
    new DockerClientConfiguration(
        new Uri("unix:///var/run/docker.sock")
    ).CreateClient()
);

var rutaClaves = builder.Configuration["Hosting:DataProtectionKeys"]
    ?? throw new InvalidOperationException(
        "No se configuró Hosting:DataProtectionKeys."
    );

Directory.CreateDirectory(rutaClaves);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(rutaClaves))
    .SetApplicationName("Hosting");

builder.Services.AddSingleton<DockerService>();

builder.Services.AddScoped<ServidorService>();
builder.Services.AddScoped<PuertoService>();
builder.Services.AddSingleton<ConfiguracionTerrariaService>();
builder.Services.AddSingleton<ProteccionContrasenaService>();

// Agrega los servicios al contenedor.

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Comprobar conexión con Docker en desarrollo
if (app.Environment.IsDevelopment())
{
    try
    {
        var dockerService = app.Services
            .GetRequiredService<DockerService>();

        var version = await dockerService.ObtenerVersionAsync();

        app.Logger.LogInformation(
            "Conexión con Docker Engine establecida. Versión: {Version}",
            version.Version
        );
    }
    catch (Exception ex)
    {
        app.Logger.LogError(
            ex,
            "No fue posible establecer conexión con Docker Engine."
        );
    }
}

// Inicializa los datos de la base de datos
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider
        .GetRequiredService<HostingDbContext>();

    await DatabaseSeeder.InitializeAsync(context);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();