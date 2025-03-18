using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks.Dataflow;
using RabbitMQ.Stream.Client;
using RabbitMQ.Stream.Client.Reliable;

namespace RabbitMqStreamLoadTests;

public class ActionStreamProducerCallbacks
{
    public const int MessageCount = 3_000_000;

    private const string StreamName = "custom-prod-stream";
    private const int Parallelism = 1024;

    public async Task PublishTest()
    {
        Console.WriteLine(
            $"{DateTime.Now.ToString("O")} [INFO] Start {nameof(ActionStreamProducerCallbacks)}. Publishing {MessageCount:N0} messages to Stream and handling confirms by producer callbacks");

        // Инициализация клиента
        var dnsEndPoint = new DnsEndPoint("localhost", 5552);
        var config = new StreamSystemConfig { Endpoints = [dnsEndPoint] };
        var system = await StreamSystem.Create(config);

        // Создание стрима (если не существует)
        await system.CreateStream(new StreamSpec(StreamName));

        var producer = await Producer.Create(
                new ProducerConfig(
                    system,
                    StreamName)
                {
                    TimeoutMessageAfter = TimeSpan.FromSeconds(3),
                    ConfirmationHandler = async confirmation => // <5>
                    {
                        switch (confirmation.Status)
                        {
                            case ConfirmationStatus.Confirmed:
                                //Console.WriteLine("Message confirmed");
                                break;
                            default:
                                // case ConfirmationStatus.ClientTimeoutError:
                                // case ConfirmationStatus.StreamNotAvailable:
                                // case ConfirmationStatus.InternalError:
                                // case ConfirmationStatus.AccessRefused:
                                // case ConfirmationStatus.PreconditionFailed:
                                // case ConfirmationStatus.PublisherDoesNotExist:
                                // case ConfirmationStatus.UndefinedError:
                                Console.WriteLine("Message not confirmed with error: {0}", confirmation.Status);
                                break;
                        }


                        await Task.CompletedTask.ConfigureAwait(false);
                    }
                })
            .ConfigureAwait(false);

        // Настройка ActionBlock для параллелизма
        var options = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = Parallelism
        };

        var actionBlock =
            new ActionBlock<int>(async i =>
            {
                var messageBody = Encoding.UTF8.GetBytes($"Message {i}");
                //await producer.Send((ulong)i, new Message(messageBody));
                await producer.Send(new Message(messageBody));
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
        stopwatch.Stop();

        //await system.DeleteStream(StreamName);
        await system.Close();

        Console.WriteLine(
            $"{DateTime.Now.ToString("O")} [INFO] produced {MessageCount:N0} messages to Stream in action blocks. Total time: {stopwatch.ElapsedMilliseconds:N0} ms");
        Console.WriteLine(
            $"{DateTime.Now.ToString("O")} [INFO] Speed {MessageCount / stopwatch.Elapsed.TotalSeconds:F2} msg/s");
    }
}