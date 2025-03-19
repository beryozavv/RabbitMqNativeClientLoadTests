using System.Diagnostics;
using System.Text;
using System.Threading.Tasks.Dataflow;
using RabbitMQ.Client;

namespace RabbitMqStreamLoadTests;

public class ActionStreamQueueChannelBasicProducer
{
    public const int MessageCount = 1_000_000;
    const ushort MaxOutstandingConfirms = 4096;
    private const string StreamQueue = "stream_queue";

    private readonly CreateChannelOptions _channelOpts = new(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true,
        outstandingPublisherConfirmationsRateLimiter: new ThrottlingRateLimiter(MaxOutstandingConfirms)
    );

    public async Task InitializeStreamQueue()
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
        // await channel.ExchangeDeclareAsync(exchange: "stream_exchange", type: "direct");

// Создаем очередь-стрим с аргументом x-queue-type=stream [[4]][[6]]
        var args = new Dictionary<string, object?>
        {
            { "x-queue-type", "stream" },
            { "x-queue-leader-locator", "least-leaders" }
            //{ "x-max-length", 1000000 } // Максимальная длина стрима (опционально)
        };

        await channel.QueueDeclareAsync(queue: StreamQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: args);

// Привязываем очередь к обменнику
// Exchange негативно влияет на производительность todo
        // await channel.QueueBindAsync(queue: "stream_queue",
        //     exchange: "stream_exchange",
        //     routingKey: "stream_key");
    }

    public async Task PublishTest()
    {
        Console.WriteLine(
            $"{DateTime.Now.ToString("O")} [INFO] Start {nameof(ActionStreamQueueChannelBasicProducer)}. Publishing {MessageCount:N0} messages to StreamQueue and handling confirms by Action blocks");

        var factory = new ConnectionFactory { HostName = "localhost" };
        await using var connection = await factory.CreateConnectionAsync();
        var props = new BasicProperties
        {
            Persistent = true
        };
        await using var channel = await connection.CreateChannelAsync( /*_channelOpts*/); // todo
        // Настройка ActionBlock с контролируемым параллелизмом
        var options = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = MaxOutstandingConfirms / 2 // Используем async-await для параллелизма
        };
        var actionBlock = new ActionBlock<string>(async message =>
        {
            try
            {
                // Публикуем сообщение в обменник
                // присоединить Guid.NewGuid todo
                var body = Encoding.UTF8.GetBytes(message);
                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: StreamQueue,
                    basicProperties: props,
                    body: body,
                    mandatory: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }, options);

        // Генерация и отправка сообщений
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < MessageCount; i++)
        {
            actionBlock.Post($"Message {i}");
        }

        // Ожидание завершения всех задач
        actionBlock.Complete();
        await actionBlock.Completion;
        sw.Stop();

        Console.WriteLine(
            $"{DateTime.Now.ToString("O")} [INFO] published to StreamQueue {MessageCount:N0} messages in action blocks. Total time: {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine(
            $"{DateTime.Now.ToString("O")} [INFO] Speed {MessageCount / sw.Elapsed.TotalSeconds:F2} msg/s");

        // Закрытие соединения
        await channel.CloseAsync();
        await connection.CloseAsync();
    }
}