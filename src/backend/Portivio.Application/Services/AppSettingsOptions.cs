namespace Portivio.Application.Services
{
    public class AppSettingsOptions
    {
        public const string SectionName = "Jwt";

        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
    }

    public class PostgresOptions
    {
        public const string SectionName = "Postgres";

        public string ConnectionString { get; set; } = string.Empty;
    }

    public class GoogleAuthOptions
    {
        public const string SectionName = "GoogleAuth";

        public string ClientId { get; set; } = string.Empty;
        public string AndroidClientId { get; set; } = string.Empty;
    }

    public class LoggingOptions
    {
        public const string SectionName = "Logging";

        public LogLevelOptions LogLevel { get; set; } = new();
    }

    public class LogLevelOptions
    {
        public string Default { get; set; } = "Information";
        public string MicrosoftAspNetCore { get; set; } = "Warning";
    }
}
