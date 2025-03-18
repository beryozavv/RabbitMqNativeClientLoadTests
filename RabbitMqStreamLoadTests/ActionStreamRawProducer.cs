using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks.Dataflow;
using RabbitMQ.Stream.Client;
using RabbitMQ.Stream.Client.Reliable;

namespace RabbitMqStreamLoadTests;

public class ActionStreamRawProducer
{
    public const int MessageCount = 3_000_000;

    private const string StreamName = "raw-prod-stream";
    private const int Parallelism = 1600;

    public async Task PublishTest()
    {
        Console.WriteLine(
            $"{DateTime.Now.ToString("O")} [INFO] Start {nameof(ActionStreamRawProducer)}. Publishing {MessageCount:N0} messages to Stream by Action blocks");
        // Инициализация клиента
        var dnsEndPoint = new DnsEndPoint("localhost", 5552);
        var config = new StreamSystemConfig { Endpoints = [dnsEndPoint] };
        var system = await StreamSystem.Create(config);

        // Создание стрима (если не существует)
        await system.CreateStream(new StreamSpec(StreamName));

        // // Инициализация Producer
        var producer = await system.CreateRawProducer(new RawProducerConfig(StreamName)
        {
            ConfirmHandler = confirmation => // <5>
            {
                switch (confirmation.Code)
                {
                    case ResponseCode.Ok:
                        //Console.WriteLine("Message confirmed");
                        break;
                    default:
                        Console.WriteLine("Message not confirmed with error: {0}", confirmation.Code);
                        break;
                }
            }
        });

        // Настройка ActionBlock для параллелизма
        var options = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = Parallelism
        };

        var actionBlock =
            new ActionBlock<int>(async i =>
            {
                var messageBody = Encoding.UTF8.GetBytes($"Message {i}");
                await producer.Send((ulong)i, new Message(messageBody));
            }, options);

        // Отправка сообщений
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < MessageCount; i++)
        {
            actionBlock.Post(i);
        }

        // Завершение работы
        actionBlock.Complete();
        await actionBlock.Completion;
        await producer.Close();
        //producer.Dispose();
        stopwatch.Stop();

        //await system.DeleteStream(StreamName);
        await system.Close();

        Console.WriteLine(
            $"{DateTime.Now.ToString("O")} [INFO] produced {MessageCount:N0} messages to Stream in action blocks. Total time: {stopwatch.ElapsedMilliseconds:N0} ms");
        Console.WriteLine(
            $"{DateTime.Now.ToString("O")} [INFO] Speed {MessageCount / stopwatch.Elapsed.TotalSeconds:F2} msg/s");
    }
}