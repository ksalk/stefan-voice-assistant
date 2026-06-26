using Microsoft.AspNetCore.Authentication;

namespace Stefan.Server.Infrastructure.Authentication;

public class DashboardAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "Dashboard";
}