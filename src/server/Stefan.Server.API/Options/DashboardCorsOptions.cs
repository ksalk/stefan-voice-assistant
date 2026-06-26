namespace Stefan.Server.API.Options;

public class DashboardCorsOptions
{
    public const string SectionName = "Cors:Dashboard";

    public string[] AllowedOrigins { get; set; } = [];
    public bool AllowAnyHeader { get; set; } = true;
    public bool AllowAnyMethod { get; set; } = true;
}