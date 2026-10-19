using System.Collections.Concurrent;
using System.Text.Json;
using CRM.BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CRM.BuildingBlocks.Persistence.Outbox;

/// <summary>
/// Delivers a module's outbox: in-process <see cref="IIntegrationEventHandler{TEvent}"/>s first, then the
/// external broker (<see cref="IMessagePublisher"/>). Rows are claimed with <c>FOR UPDATE SKIP LOCKED</c>,
/// so several app instances can run it. Delivery is at-least-once; failed messages are retried
/// up to <see cref="MaxAttempts"/> times with the last error stored on the row.
/// </summary>
internal sealed class OutboxProcessor<TContext> : BackgroundService
    where TContext : ModuleDbContext
{
    public const int MaxAttempts = 5;
    private const int BatchSize = 20;

    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor<TContext>> _logger;
    private readonly TimeSpan _interval;

    public OutboxProcessor(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<OutboxProcessor<TContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(configuration.GetValue("Outbox:PollingIntervalSeconds", 5d));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        do
        {
            try
            {
                while(await ProcessBatchAsync(stoppingToken) == BatchSize)
                {
                    // Keep draining while full batches come back.
                }
            }
            catch(Exception exception) when(!stoppingToken.IsCancellationRequested)
            {
                // E.g. database not migrated yet; try again on the next tick.
                _logger.LogWarning(exception, "Outbox processing for {Context} failed", typeof(TContext).Name);
            }
        }
        while(await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <returns>Number of messages handled in this batch.</returns>
    internal async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var messages = await db.OutboxMessages
            .FromSqlRaw($"""
                SELECT * FROM "{db.Schema}".outbox_messages
                WHERE processed_at IS NULL AND attempt_count < {MaxAttempts}
                ORDER BY occurred_at
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach(var message in messages)
        {
            try
            {
                var eventType = ResolveType(message.Type)
                    ?? throw new InvalidOperationException($"Unknown integration event type '{message.Type}'.");

                var integrationEvent = (IIntegrationEvent)JsonSerializer.Deserialize(message.Content, eventType, JsonSerializerOptions.Web)!;

                await DispatchInProcessAsync(integrationEvent, eventType, cancellationToken);
                await publisher.PublishAsync(integrationEvent, eventType, cancellationToken: cancellationToken);

                message.ProcessedAt = DateTime.UtcNow;
                message.Error = null;
            }
            catch(Exception exception)
            {
                message.AttemptCount++;
                message.Error = exception.ToString();

                _logger.LogError(exception, "Delivering outbox message {MessageId} ({Type}) failed, attempt {Attempt}",
                    message.Id, message.Type, message.AttemptCount);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return messages.Count;
    }

    private async Task DispatchInProcessAsync(IIntegrationEvent integrationEvent, Type eventType, CancellationToken cancellationToken)
    {
        // Each handler gets its own scope so it can use its own module's DbContext.
        await using var scope = _scopeFactory.CreateAsyncScope();

        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
        var method = handlerType.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!;

        foreach(var handler in scope.ServiceProvider.GetServices(handlerType))
        {
            await (Task)method.Invoke(handler, [integrationEvent, cancellationToken])!;
        }
    }

    private static Type? ResolveType(string typeName) => TypeCache.GetOrAdd(typeName, static name => Type.GetType(name));
}
