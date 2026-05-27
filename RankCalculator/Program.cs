using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;
using Valuator.Models;

namespace RankCalculator;

class Program
{
    private const string RankExchange = "events.rank.calculated";
    private const string QueueName = "valuator.processing.rank";
    
    private static readonly Dictionary<string, IConnectionMultiplexer> _redisConnections = new();

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Consumer started");

        // Получает Env Var из Docker
        string mainAddr = Environment.GetEnvironmentVariable("DB_MAIN") ?? "localhost:6379,password=wandersPassword";
        string ruAddr   = Environment.GetEnvironmentVariable("DB_RU")   ?? "localhost:6380,password=wandersPassword";
        string euAddr   = Environment.GetEnvironmentVariable("DB_EU")   ?? "localhost:6381,password=wandersPassword";
        string asiaAddr = Environment.GetEnvironmentVariable("DB_ASIA") ?? "localhost:6382,password=wandersPassword";
        string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        string rabbitUser = Environment.GetEnvironmentVariable("RabbitMQ_User") ?? "guest";
        string rabbitPass = Environment.GetEnvironmentVariable("RabbitMQ_Pass") ?? "guest";
        
        _redisConnections["MAIN"] = await ConnectionMultiplexer.ConnectAsync(mainAddr);
        _redisConnections["RU"]   = await ConnectionMultiplexer.ConnectAsync(ruAddr);
        _redisConnections["EU"]   = await ConnectionMultiplexer.ConnectAsync(euAddr);
        _redisConnections["ASIA"] = await ConnectionMultiplexer.ConnectAsync(asiaAddr);
        
        IDatabase mainDb = _redisConnections["MAIN"].GetDatabase();
        
        ConnectionFactory factory = new()
        {
            HostName = rabbitHost,
            UserName = rabbitUser,
            Password = rabbitPass,
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
            await DeclareTopologyAsync(channel); // только первый экземпляр создаст очередь (Идемпотентность)
        
            await RunConsumer(channel, mainDb);

            Console.WriteLine("--- Consumer is running. Press Ctrl+C to stop ---");
            await Task.Delay(Timeout.Infinite);
        }
    }

    private static async Task RunConsumer(IChannel channel, IDatabase mainDb)
    {
        AsyncEventingBasicConsumer consumer = new(channel);
        
        // Обработчик события, запустит ConsumeAsync после получения сообщения
        consumer.ReceivedAsync += (_, eventArgs) => ConsumeAsync(channel, eventArgs, mainDb);
        
        // Остальные экземпляры подписываются на очередь
        await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false, // Ожидание личного подтверждения (Ack)
            consumer: consumer
        );
    }

    /// <summary>
    /// Обрабатывает входящее событие:
    ///     считает Rank, записывает в БД, публикует RankCalculated, выводит в консоль и подтверждает.
    /// </summary>
    private static async Task ConsumeAsync(IChannel channel, BasicDeliverEventArgs eventArgs, IDatabase mainDb)
    {
        // pa5 Принудительное ожидание для проверки эффекта
        TimeSpan interval = TimeSpan.FromSeconds(new Random().Next(3, 15));
        Console.WriteLine($"Waiting {interval}");
        await Task.Delay(interval);
        
        string id = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
        Console.WriteLine($"Received task for ID: {id}");
        
        // pa6-pa7
        string? metadataJson = await mainDb.StringGetAsync(id);
        if (string.IsNullOrEmpty(metadataJson))
        {
            Console.WriteLine($"ERROR: Metadata for ID {id} not found in DB_MAIN");
            await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
            return;
        }
        
        DocumentMetadata? metadata = JsonSerializer.Deserialize<DocumentMetadata>(metadataJson);

        if (metadata == null)
        {
            Console.WriteLine($"ERROR: Failed to deserialize metadata for ID {id}");
            await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
            return;
        }
        
        Console.WriteLine($"LOOKUP: {id}, {metadata.DbRegion}");
        
        IDatabase shardDb = _redisConnections[metadata.DbRegion].GetDatabase();
        
        // pa3
        string? text = await shardDb.StringGetAsync($"TEXT-{id}");
        
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

            await shardDb.StringSetAsync($"RANK-{id}", rankString);
            
            // (pa4) публикация события RankCalculated
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
    
    /// <summary>
    ///  Публикует событие завершения вычисления Rank с id и числом rank
    /// </summary>
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