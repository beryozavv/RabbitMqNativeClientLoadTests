namespace RabbitMqStreamLoadTests;

public class Program
{
    public static async Task Main(string[] args)
    {
        // инициализация очереди
        await new ActionStreamQueueChannelBasicProducer().InitializeStreamQueue();

        // тесты упорядочены по скорости публикации
        await new ActionStreamQueueChannelBasicProducer().PublishTest();
        await new ActionStreamProducerCallbacks().PublishTest();
        await new ActionStreamRawProducer().PublishTest();
        //await new ActionStreamNativeBatch().PublishTest(); //это работает сравнительно медленно, надо разбираться todo
    }
}