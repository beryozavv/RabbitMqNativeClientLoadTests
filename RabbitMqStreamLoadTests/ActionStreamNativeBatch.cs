using System.Diagnostics;
using System.Net;
using System.Text;
using RabbitMQ.Stream.Client;

namespace RabbitMqStreamLoadTests;

public class ActionStreamNativeBatch
{
    public const int MessageCount = 100_000;

    private const string StreamName = "test-stream";
    private const int Parallelism = 1000;

    public async Task PublishTest()
    {
        // Инициализация клиента
        var dnsEndPoint = new DnsEndPoint("localhost", 5552);
        var config = new StreamSystemConfig { Endpoints = [dnsEndPoint] };
        var system = await StreamSystem.Create(config);

        // Создание стрима (если не существует)
        await system.CreateStream(new StreamSpec(StreamName));

        // Инициализация Producer
        var producer = await system.CreateRawProducer(new RawProducerConfig(StreamName));

        // Отправка сообщений
        var stopwatch = Stopwatch.StartNew();

        // Генерация списка сообщений, где каждому сообщению присваивается уникальный Id
        var chunks = Enumerable.Range(0, MessageCount)
            .AsParallel()
            .Select(i =>
            {
                var messageBody = Encoding.UTF8.GetBytes($"Message {i}");
                return ((ulong)i, new Message(messageBody));
            }).Chunk(Parallelism);

        // await Parallel.ForEachAsync(chunks, new ParallelOptions() { MaxDegreeOfParallelism = 4 }, async (batch, ct) =>
        // {
        //     // Если нужна коллекция типа IList<Message>, преобразуем текущий batch (массив) в List
        //     var messageBatch = batch.ToList();
        //     await producer.Send(messageBatch);
        // });
        
        foreach (var chunk in chunks)
        {
            // Если ваш метод принимает IList<Message>, можно преобразовать пачку в List:
            var messageBatch = chunk.ToList();
            await producer.Send(messageBatch);
        }

        stopwatch.Stop();
        // Завершение работы
        producer.Dispose();

        //await system.DeleteStream(StreamName);
        await system.Close();

        Console.WriteLine($"Test completed in {stopwatch.Elapsed.TotalSeconds} seconds");
        Console.WriteLine($"Speed {MessageCount / stopwatch.Elapsed.TotalSeconds} msg/s");
    }
}