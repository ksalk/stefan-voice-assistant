using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Stefan.Server.Infrastructure.Authentication;

public class NodeSecretAuthenticationHandler : AuthenticationHandler<NodeSecretAuthenticationOptions>
{
    private readonly IConfiguration _configuration;

    public NodeSecretAuthenticationHandler(
        IOptionsMonitor<NodeSecretAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expectedSecret = _configuration["NodeSecret"];
        if (string.IsNullOrEmpty(expectedSecret))
        {
            Logger.LogWarning("NodeSecret is not configured in application settings");
            return Task.FromResult(AuthenticateResult.Fail("Node secret is not configured"));
        }

        if (!Request.Headers.TryGetValue("X-Node-Secret", out var providedSecretValues))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing X-Node-Secret header"));
        }

        var providedSecret = providedSecretValues.FirstOrDefault();
        if (string.IsNullOrEmpty(providedSecret))
        {
            return Task.FromResult(AuthenticateResult.Fail("X-Node-Secret header is empty"));
        }

        if (providedSecret != expectedSecret)
        {
            Logger.LogWarning("Invalid X-Node-Secert header provided");
            return Task.FromResult(AuthenticateResult.Fail("Invalid node secret"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "node"),
            new Claim(ClaimTypes.Role, "Node")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
