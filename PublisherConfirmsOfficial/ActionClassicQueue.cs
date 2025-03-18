using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using RabbitMQ.Client;

namespace PublisherConfirmsOfficial;

public class ActionClassicQueue : IDisposable
{
    private const int MessageCount = 2_000_000;
    const ushort MaxOutstandingConfirms = 4096;
    public const string ClassicQueueName = "classic-queue";
    private const string ClassicKey = $"{ClassicQueueName}_key";
    private const string ClassicExchange = $"{ClassicQueueName}_exchange";

    private static readonly int MaxParallelism = MaxOutstandingConfirms / 2; //Environment.ProcessorCount * 8;

    private readonly CreateChannelOptions _channelOpts = new(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true,
        outstandingPublisherConfirmationsRateLimiter: new ThrottlingRateLimiter(MaxOutstandingConfirms)
    );

    public async Task InitializeQueue()
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            UserName = "guest",
            Password = "guest"
        };
        using var connection = await factory.CreateConnectionAsync();

        using var channel = await connection.CreateChannelAsync(_channelOpts);

// Создаем обменник (например, direct)
        // await channel.ExchangeDeclareAsync(exchange: ClassicExchange, type: "direct");

        await channel.QueueDeclareAsync(queue: ClassicQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

// Привязываем очередь к обменнику
// Exchange негативно влияет на производительность todo
        // await channel.QueueBindAsync(queue: ClassicQueueName,
        //     exchange:ClassicExchange,
        //     routingKey: ClassicKey);
    }

    public async Task PublishTest()
    {
        Console.WriteLine(
            $"{DateTime.Now.ToString("O")} [INFO] publishing {MessageCount:N0} messages and handling confirms in Action blocks");

        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            UserName = "guest",
            Password = "guest"
        };

        var connection = await factory.CreateConnectionAsync();

        var props = new BasicProperties
        {
            Persistent = true
        };

        using var publishChannel = await connection.CreateChannelAsync(_channelOpts);

        // Настройка публикации с параллелизмом <button class="citation-flag" data-index="1"><button class="citation-flag" data-index="6">
        var options = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = MaxParallelism
        };

        var actionBlock = new ActionBlock<int>(async i =>
        {
            var message = new TestMessage { Text = $"LoadTest. Item{i}" };

            try
            {
                var body = JsonSerializer.SerializeToUtf8Bytes(message);

                
                await publishChannel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: ClassicQueueName,
                    basicProperties: props,
                    body: body,
                    mandatory: true);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }, options);

        var stopwatch = Stopwatch.StartNew();
        // Публикация сообщений
        for (int i = 0; i < MessageCount; i++)
        {
            await actionBlock.SendAsync(i);
        }

        actionBlock.Complete();
        await actionBlock.Completion;

        stopwatch.Stop();

        Console.WriteLine(
            $"{DateTime.Now.ToString("O")} [INFO] published {MessageCount:N0} messages in action blocks {stopwatch.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"Speed {MessageCount / stopwatch.Elapsed.TotalSeconds:F2} msg/s");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public record TestMessage
    {
        public string Text { get; init; } = null!;
    }
}