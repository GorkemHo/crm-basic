namespace CRM.Infrastructure.Messaging;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>When false (default), a no-op publisher is used and no consumers start.</summary>
    public bool Enabled { get; set; }

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = "guest";

    /// <summary>Keep out of appsettings.json: use user-secrets or RabbitMq__Password.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Durable topic exchange all messages are published to.</summary>
    public string ExchangeName { get; set; } = "crm.events";

    public string ClientProvidedName { get; set; } = "CRM.API";

    /// <summary>Unacknowledged messages a consumer may hold at once.</summary>
    public ushort PrefetchCount { get; set; } = 10;
}
