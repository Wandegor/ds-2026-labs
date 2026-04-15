using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EventsLogger;

class Program
{
    private const string SimilarityExchange = "events.similarity.calculated";
    private const string RankExchange = "events.rank.calculated";

    public static async Task Main(string[] args)
    {
        string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        
        var factory = new ConnectionFactory { HostName = rabbitHost };
        
        IConnection? connection = null;
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
            
            string queueName = await DeclareTopologyAsync(channel);
            
            await RunConsumer(channel, queueName);

            Console.WriteLine($"--- EventsLogger is running. Listening on queue: {queueName}. Press Ctrl+C to stop ---");
            await Task.Delay(Timeout.Infinite);
        }
    }
    
    /// <summary>
    /// Объявляет fanout обменники, создаёт уникальную очередь и привязывает её к обоим exchange.
    /// Возвращает имя созданной очереди.
    /// </summary>
    private static async Task<string> DeclareTopologyAsync(IChannel channel)
    {
        // Обменники
        await channel.ExchangeDeclareAsync(SimilarityExchange, ExchangeType.Fanout, durable: true);
        await channel.ExchangeDeclareAsync(RankExchange, ExchangeType.Fanout, durable: true);

        // Эксклюзивная очередь с уникальным именем
        string queueName = $"events.logger.{Guid.NewGuid()}";
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: true,
            autoDelete: true // нет эффекта
        );

        // Привязка очереди к обменникам
        await channel.QueueBindAsync(queueName, SimilarityExchange, routingKey: "");
        await channel.QueueBindAsync(queueName, RankExchange, routingKey: "");

        return queueName;
    }

    /// <summary>
    /// Запускает асинхронного потребителя на указанной очереди.
    /// </summary>
    private static async Task RunConsumer(IChannel channel, string queueName)
    {
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, eventArgs) => ConsumeAsync(channel, eventArgs);
        
        await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer
        );
    }

    /// <summary>
    /// Обрабатывает входящее событие: выводит в консоль и подтверждает.
    /// </summary>
    private static async Task ConsumeAsync(IChannel channel, BasicDeliverEventArgs eventArgs)
    {
        string message = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
        string exchange = eventArgs.Exchange;

        // Определяем тип события по имени exchange
        string eventType = exchange == SimilarityExchange ? "SimilarityCalculated" : "RankCalculated";

        Console.WriteLine($"[{eventType}] {message}");

        await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
    }
}