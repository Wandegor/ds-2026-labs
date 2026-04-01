using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

namespace RankCalculator;

class Program
{
    private const string QueueName = "valuator.processing.rank";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Consumer started");

        string redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";
        var redis = await ConnectionMultiplexer.ConnectAsync(redisHost);
        IDatabase db = redis.GetDatabase();
        
        ConnectionFactory factory = new ConnectionFactory
        {
            HostName = "localhost",
        };
        await using IConnection connection = await factory.CreateConnectionAsync();
        await using IChannel channel = await connection.CreateChannelAsync();

        await DeclareTopologyAsync(channel); // Создание очереди если нет
        
        string consumerTag = await RunConsumer(channel, db);

        Console.WriteLine("Press Enter to exit");
        Console.ReadLine();

        await channel.BasicCancelAsync(consumerTag);

        Console.WriteLine("done");
    }

    private static async Task<string> RunConsumer(IChannel channel, IDatabase db)
    {
        AsyncEventingBasicConsumer consumer = new(channel);
        consumer.ReceivedAsync += (_, eventArgs) => ConsumeAsync(channel, eventArgs, db);
        return await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false, // Ожидание личного подтверждения (Ack)
            consumer: consumer
        );
    }

    private static async Task ConsumeAsync(IChannel channel, BasicDeliverEventArgs eventArgs, IDatabase db)
    {
        string id = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
        Console.WriteLine($"Received task for ID: {id}");
        
        // pa3
        string? text = await db.StringGetAsync($"TEXT-{id}");
        
        if (!string.IsNullOrEmpty(text))
        {
            double rank = 0;
            foreach (char ch in text)
            {
                if (!char.IsLetter(ch)) rank++;
            }
            rank /= text.Length;

            string rankString = Math.Round(rank, 4)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);

            await db.StringSetAsync($"RANK-{id}", rankString);
                
            Console.WriteLine($"Calculated Rank: {rankString} for ID: {id}");
        }
        
        await channel.BasicAckAsync(eventArgs.DeliveryTag, false); // Подтверждение, сообщ удаляется из очереди
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