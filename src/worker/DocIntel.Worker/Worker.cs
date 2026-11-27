using Azure.Identity;
using Azure.Messaging.ServiceBus;

namespace DocIntel.Worker;

/// <summary>
/// Reads the ingestion queue with the app's managed identity (AZURE_CLIENT_ID).
/// Without ServiceBus__FullyQualifiedNamespace it idles, so it also runs locally.
/// </summary>
public sealed class Worker(IConfiguration configuration, IngestionMessageHandler handler, ILogger<Worker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ns = configuration["ServiceBus:FullyQualifiedNamespace"];
        var queue = configuration["ServiceBus:QueueName"] ?? "ingestion";
        if (string.IsNullOrEmpty(ns))
        {
            logger.LogWarning("ServiceBus:FullyQualifiedNamespace is not set; the worker is idle.");
            await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { });
            return;
        }

        await using var client = new ServiceBusClient(ns, new DefaultAzureCredential());
        await using var processor = client.CreateProcessor(queue, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 1,
        });

        processor.ProcessMessageAsync += async args =>
        {
            handler.Handle(args.Message.Body.ToString());
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        };
        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Ingestion message failed ({Source}); it will be retried or dead-lettered.", args.ErrorSource);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);
        logger.LogInformation("Listening on {Namespace}/{Queue}", ns, queue);
        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { });
        await processor.StopProcessingAsync();
    }
}
