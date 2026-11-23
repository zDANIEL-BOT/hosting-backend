using System.Security.Claims;
using Docker.DotNet;
using Hosting.Api.Auth;
using Hosting.Api.Data;
using Hosting.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

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

builder.Services.AddSingleton<DockerService>();

builder.Services.AddScoped<ServidorService>();

builder.Services.AddScoped<AuthService>();

builder.Services.AddSingleton<ConfiguracionTerrariaService>();

// ---------------------------------------------------------------------------
// CORS: permite al frontend (React/Vite) llamar a la API con cookies.
// ---------------------------------------------------------------------------
var frontendUrl = configuration["Auth:FrontendUrl"] ?? "http://localhost:5173";

var origenesPermitidos = (configuration["Cors:AllowedOrigins"] ?? frontendUrl)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(origenesPermitidos)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// ---------------------------------------------------------------------------
// Autenticación: cookie de sesión + proveedores OAuth (SSO).
// ---------------------------------------------------------------------------
var auth = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
});

auth.AddCookie(options =>
{
    options.Cookie.Name = "hosting.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;

    // Para una API devolvemos 401/403 en lugar de redirigir a una página de login.
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// Vincula la identidad del proveedor con el usuario local (tablas usuarios/identidades_externas).
static Task VincularAsync(OAuthCreatingTicketContext context, string proveedor)
{
    var authService = context.HttpContext.RequestServices.GetRequiredService<AuthService>();
    return authService.VincularAsync(context.Principal!, proveedor);
}

// --- Discord ---
if (ProveedoresAuth.TieneCredenciales(configuration, ProveedoresAuth.Discord))
{
    auth.AddDiscord(options =>
    {
        options.ClientId = configuration["Auth:Discord:ClientId"]!;
        options.ClientSecret = configuration["Auth:Discord:ClientSecret"]!;
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
        options.Scope.Add("identify");
        options.Scope.Add("email");
        options.Events.OnCreatingTicket = context =>
            VincularAsync(context, ProveedoresAuth.Discord);
    });
}

// --- GitHub ---
if (ProveedoresAuth.TieneCredenciales(configuration, ProveedoresAuth.GitHub))
{
    auth.AddGitHub(options =>
    {
        options.ClientId = configuration["Auth:GitHub:ClientId"]!;
        options.ClientSecret = configuration["Auth:GitHub:ClientSecret"]!;
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
        options.Scope.Add("read:user");
        options.Scope.Add("user:email");
        options.Events.OnCreatingTicket = context =>
            VincularAsync(context, ProveedoresAuth.GitHub);
    });
}

// --- Google ---
if (ProveedoresAuth.TieneCredenciales(configuration, ProveedoresAuth.Google))
{
    auth.AddGoogle(options =>
    {
        options.ClientId = configuration["Auth:Google:ClientId"]!;
        options.ClientSecret = configuration["Auth:Google:ClientSecret"]!;
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
        options.Events.OnCreatingTicket = context =>
            VincularAsync(context, ProveedoresAuth.Google);
    });
}

// --- TikTok (OAuth genérico + PKCE, no existe paquete oficial en NuGet) ---
if (ProveedoresAuth.TieneCredenciales(configuration, ProveedoresAuth.TikTok))
{
    auth.AddOAuth(ProveedoresAuth.TikTok, options =>
    {
        options.ClientId = configuration["Auth:TikTok:ClientId"]!;
        options.ClientSecret = configuration["Auth:TikTok:ClientSecret"]!;
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
        options.AuthorizationEndpoint = "https://www.tiktok.com/v2/auth/authorize/";
        options.TokenEndpoint = "https://open.tiktokapis.com/v2/oauth/token/";
        options.UserInformationEndpoint =
            "https://open.tiktokapis.com/v2/user/info/?fields=open_id,union_id,avatar_url,display_name,username";
        options.UsePkce = true;
        options.Scope.Add("user.info.basic");
        options.SaveTokens = true;

        options.Events.OnCreatingTicket = context =>
        {
            // Respuesta de TikTok: { data: { user: { open_id, display_name, ... } } }
            if (context.User.TryGetProperty("data", out var data) &&
                data.TryGetProperty("user", out var user))
            {
                var openId = user.TryGetProperty("open_id", out var oi)
                    ? oi.GetString()
                    : null;
                var nombre = user.TryGetProperty("display_name", out var dn)
                    ? dn.GetString()
                    : null;

                if (string.IsNullOrEmpty(nombre))
                {
                    nombre = user.TryGetProperty("username", out var un)
                        ? un.GetString()
                        : openId;
                }

                if (openId is not null)
                {
                    context.Identity!.AddClaim(new Claim(ClaimTypes.NameIdentifier, openId));
                }

                if (nombre is not null)
                {
                    context.Identity!.AddClaim(new Claim(ClaimTypes.Name, nombre));
                }

                if (user.TryGetProperty("avatar_url", out var av) &&
                    av.GetString() is { Length: > 0 } avatar)
                {
                    context.Identity!.AddClaim(new Claim("urn:tiktok:avatar", avatar));
                }
            }

            return VincularAsync(context, ProveedoresAuth.TikTok);
        };
    });
}

builder.Services.AddAuthorization();

// Add services to the container.

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

// Inicializar los datos de la base de datos
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider
        .GetRequiredService<HostingDbContext>();

    await DatabaseSeeder.InitializeAsync(context);
}

// Configure the HTTP request pipeline.
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(options =>
    {
        options.Title = "Hosting API";
    });
}

app.UseCors("Frontend");

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();