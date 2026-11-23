namespace Hosting.Api.Auth;

/// <summary>
/// Proveedores de inicio de sesión (SSO / OAuth) soportados por la aplicación.
/// </summary>
public static class ProveedoresAuth
{
    public const string Discord = "Discord";
    public const string GitHub = "GitHub";
    public const string Google = "Google";
    public const string TikTok = "TikTok";

    public static readonly string[] Todos = { Discord, GitHub, Google, TikTok };

    /// <summary>
    /// Indica si un proveedor tiene configuradas sus credenciales (ClientId y ClientSecret).
    /// </summary>
    public static bool TieneCredenciales(IConfiguration configuration, string proveedor)
    {
        var clientId = configuration[$"Auth:{proveedor}:ClientId"];
        var clientSecret = configuration[$"Auth:{proveedor}:ClientSecret"];

        return !string.IsNullOrWhiteSpace(clientId) &&
               !string.IsNullOrWhiteSpace(clientSecret);
    }
}