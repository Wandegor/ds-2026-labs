using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RankCalculator;

class Program
{
    private const string QueueName = "valuator.processing.rank";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Consumer started");

        ConnectionFactory factory = new ConnectionFactory
        {
            HostName = "localhost",
        };
        await using IConnection connection = await factory.CreateConnectionAsync();
        await using IChannel channel = await connection.CreateChannelAsync();

        await DeclareTopologyAsync(channel);
        string consumerTag = await RunConsumer(channel);

        Console.WriteLine("Press Enter to exit");
        Console.ReadLine();

        await channel.BasicCancelAsync(consumerTag);

        Console.WriteLine("done");
    }

    private static async Task<string> RunConsumer(IChannel channel)
    {
        AsyncEventingBasicConsumer consumer = new(channel);
        consumer.ReceivedAsync += (_, eventArgs) => ConsumeAsync(channel, eventArgs);
        return await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer
        );
    }

    private static async Task ConsumeAsync(IChannel channel, BasicDeliverEventArgs eventArgs)
    {
        Console.WriteLine("Consuming");
        string message = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
        Console.WriteLine($"Consuming: {message} from subject {eventArgs.Exchange}");
        await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
    }


    /// <summary>
    ///  Определяет топологию: queue -> consumer.
    /// </summary>
    private static async Task DeclareTopologyAsync(IChannel channel)
    {
        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false
        );
    }
}