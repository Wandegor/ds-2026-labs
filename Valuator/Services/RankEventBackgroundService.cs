using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Valuator.Hubs;

namespace Valuator.Services;

public class RankEventBackgroundService : BackgroundService
{
    private readonly IConnection _rabbitConnection;
    private readonly IHubContext<RankNotificationHub> _hubContext;
    private readonly ILogger<RankEventBackgroundService> _logger;
    private IChannel? _channel;
    private string? _queueName;

    public RankEventBackgroundService(
        IConnection rabbitConnection,
        IHubContext<RankNotificationHub> hubContext,
        ILogger<RankEventBackgroundService> logger)
    {
        _rabbitConnection = rabbitConnection;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await _rabbitConnection.CreateChannelAsync(cancellationToken: stoppingToken);
        
        await _channel.ExchangeDeclareAsync("events.rank.calculated", ExchangeType.Fanout, durable: true, cancellationToken: stoppingToken);
        
        // Эксклюзивная очередь подключенная к fanout Обменнику
        _queueName = $"valuator.rank.notifications.{Guid.NewGuid()}";
        await _channel.QueueDeclareAsync(_queueName, durable: false, exclusive: true, autoDelete: true, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(_queueName, "events.rank.calculated", "", cancellationToken: stoppingToken);
        
        AsyncEventingBasicConsumer consumer = new(_channel);
        consumer.ReceivedAsync += OnRankEventReceived;
        await _channel.BasicConsumeAsync(_queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        
        _logger.LogInformation("RankEventBackgroundService started, queue: {QueueName}", _queueName);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task OnRankEventReceived(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            string body = Encoding.UTF8.GetString(ea.Body.ToArray());
            RankCalculatedEvent? rankEvent = JsonSerializer.Deserialize<RankCalculatedEvent>(body);
            if (rankEvent != null)
            {
                _logger.LogInformation("Received RankCalculated for ID {Id}: {Rank}", rankEvent.Id, rankEvent.Rank);
                // отправка уведомлений через канал ValuatorSignalR в Redis
                // Redis Backplane игнорирует отправку клиенту, которого нет на этом сервере(app1, app2) 
                await _hubContext.Clients.Group($"rank-{rankEvent.Id}").SendAsync("RankCalculated", rankEvent.Rank);
            }
            await _channel!.BasicAckAsync(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing rank event");
            await _channel!.BasicAckAsync(ea.DeliveryTag, false);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }
        await base.StopAsync(cancellationToken);
    }

    private record RankCalculatedEvent(string Id, double Rank);
}