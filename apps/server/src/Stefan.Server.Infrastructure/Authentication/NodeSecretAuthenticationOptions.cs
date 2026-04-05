using Microsoft.AspNetCore.Authentication;

namespace Stefan.Server.Infrastructure.Authentication;

public class NodeSecretAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "NodeSecret";
    public string Scheme => DefaultScheme;
    public string AuthenticationType = DefaultScheme;
}
