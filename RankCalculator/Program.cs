using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

namespace RankCalculator;

class Program
{
    private const string RankExchange = "events.rank.calculated";
    private const string QueueName = "valuator.processing.rank";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Consumer started");

        string redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";
        string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        
        var redis = await ConnectionMultiplexer.ConnectAsync(redisHost);
        IDatabase db = redis.GetDatabase();
        
        ConnectionFactory factory = new ConnectionFactory
        {
            HostName = rabbitHost,
        };
        
        IConnection? connection = null;

        // RABBITMQ
        Console.WriteLine("Waiting for RabbitMQ...");
        while (connection == null)
        {
            try 
            {
                connection = await factory.CreateConnectionAsync();
            }
            catch (Exception)
            {
                Console.WriteLine("RabbitMQ is not ready yet. Retrying in 5 seconds...");
                await Task.Delay(5000);
            }
        }

        await using (connection)
        {
            await using IChannel channel = await connection.CreateChannelAsync();
            await DeclareTopologyAsync(channel);
        
            string consumerTag = await RunConsumer(channel, db);

            Console.WriteLine("--- Consumer is running. Press Ctrl+C to stop ---");
            await Task.Delay(Timeout.Infinite);
        }
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
            
            // (pa4) событие RankCalculated
            await PublishRankEventAsync(channel, id, rank);
                
            Console.WriteLine($"Calculated Rank: {rankString} for ID: {id}");
        }
        else
        {
            Console.WriteLine($"Text for ID {{id}} not found in Redis");
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
    
    private static async Task PublishRankEventAsync(IChannel channel, string id, double rank)
    {
        await channel.ExchangeDeclareAsync(RankExchange, ExchangeType.Fanout, durable: true);
    
        var eventData = new { Id = id, Rank = rank };
        string json = JsonSerializer.Serialize(eventData);
        byte[] body = Encoding.UTF8.GetBytes(json);
    
        await channel.BasicPublishAsync(
            exchange: RankExchange,
            routingKey: "",
            body: body
        );
    }
}