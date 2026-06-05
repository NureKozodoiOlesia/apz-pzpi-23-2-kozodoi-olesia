using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class Message
{
    public string Id          { get; set; }
    public string ChannelId   { get; set; }
    public string SenderId    { get; set; }
    public string Text        { get; set; }
    public DateTime Timestamp { get; set; }
}

public class RedisCache
{
    private readonly Dictionary<string, List<string>> _subscriptions = new();
    private readonly Dictionary<string, string>       _cache         = new();

    public void Subscribe(string channelId, string clientId)
    {
        if (!_subscriptions.ContainsKey(channelId))
            _subscriptions[channelId] = new List<string>();
        _subscriptions[channelId].Add(clientId);
    }

    public List<string> GetSubscribers(string channelId)
    {
        return _subscriptions.TryGetValue(channelId, out var list) ? list : new List<string>();
    }

    public void Set(string key, string value) => _cache[key] = value;
    public string Get(string key) => _cache.TryGetValue(key, out var v) ? v : null;
}

public class KafkaQueue
{
    private readonly Queue<(string Topic, Message Msg)> _queue = new();

    public void Publish(string topic, Message message)
    {
        _queue.Enqueue((topic, message));
        Console.WriteLine($"[Kafka]   Подія опублікована у топік '{topic}'");
    }

    public (string Topic, Message Msg)? Poll()
    {
        return _queue.Count > 0 ? _queue.Dequeue() : null;
    }
}

public class Database
{
    private readonly List<Message> _messages = new();

    public void Save(Message message)
    {
        _messages.Add(message);
        Console.WriteLine($"[MySQL]   Повідомлення збережено (id={message.Id}, shard за channelId={message.ChannelId})");
    }
}

public class GatewayServer
{
    public void DeliverWebSocket(string clientId, Message message)
    {
        Console.WriteLine($"[Gateway] WebSocket → клієнт '{clientId}': \"{message.Text}\"");
    }
}

public class Dispatcher
{
    private readonly RedisCache    _cache;
    private readonly GatewayServer _gateway;

    public Dispatcher(RedisCache cache, GatewayServer gateway)
    {
        _cache   = cache;
        _gateway = gateway;
    }

    public void Route(Message message)
    {
        var subscribers = _cache.GetSubscribers(message.ChannelId);
        Console.WriteLine($"[Dispatcher] Знайдено підписників: {subscribers.Count}");
        foreach (var clientId in subscribers)
            _gateway.DeliverWebSocket(clientId, message);
    }
}

public class SearchService
{
    public void Index(Message message)
    {
        Console.WriteLine($"[Solr]    Індексування повідомлення id={message.Id}: \"{message.Text}\"");
    }
}

public class NotificationService
{
    private readonly RedisCache _cache;

    public NotificationService(RedisCache cache) => _cache = cache;

    public void Notify(Message message)
    {
        var status = _cache.Get($"presence:{message.ChannelId}:offline-user");
        if (status != null)
            Console.WriteLine($"[Notify]  Push-сповіщення → offline-user: нове повідомлення у каналі '{message.ChannelId}'");
        else
            Console.WriteLine($"[Notify]  Усі підписники онлайн, push не потрібен");
    }
}

public class ChatService
{
    private readonly Database    _db;
    private readonly Dispatcher  _dispatcher;
    private readonly KafkaQueue  _kafka;

    public ChatService(Database db, Dispatcher dispatcher, KafkaQueue kafka)
    {
        _db         = db;
        _dispatcher = dispatcher;
        _kafka      = kafka;
    }

    public void Send(Message message)
    {
        Console.WriteLine($"\n[ChatService] Отримано повідомлення від '{message.SenderId}' у канал '{message.ChannelId}'");
        _db.Save(message);
        _dispatcher.Route(message);
        _kafka.Publish("messages.new",    message);
        _kafka.Publish("messages.search", message);
    }
}

public class ApiGateway
{
    private readonly ChatService _chatService;

    public ApiGateway(ChatService chatService) => _chatService = chatService;

    public void HandleRequest(Message message)
    {
        Console.WriteLine($"[API Gateway] POST /api/chat.postMessage → ChatService");
        _chatService.Send(message);
    }
}

class Program
{
    static async Task Main()
    {
        Console.WriteLine("=== Симуляція потоку даних Slack ===\n");

        var redis      = new RedisCache();
        var kafka      = new KafkaQueue();
        var db         = new Database();
        var gateway    = new GatewayServer();
        var dispatcher = new Dispatcher(redis, gateway);
        var search     = new SearchService();
        var notify     = new NotificationService(redis);
        var chat       = new ChatService(db, dispatcher, kafka);
        var apiGateway = new ApiGateway(chat);

        redis.Subscribe("general", "user-alice");
        redis.Subscribe("general", "user-bob");
        redis.Subscribe("general", "user-carol");
        redis.Set("presence:general:offline-user", "offline");

        Console.WriteLine("[Redis] Підписки зареєстровано: user-alice, user-bob, user-carol");
        Console.WriteLine("[Redis] user-carol позначена як офлайн\n");

        var message = new Message
        {
            Id        = Guid.NewGuid().ToString()[..8],
            ChannelId = "general",
            SenderId  = "user-alice",
            Text      = "Привіт команда! Є оновлення по проєкту.",
            Timestamp = DateTime.UtcNow
        };

        Console.WriteLine("--- Крок 1: Клієнт надсилає HTTP-запит ---");
        apiGateway.HandleRequest(message);

        Console.WriteLine("\n--- Крок 6: Асинхронна обробка через Kafka ---");
        await Task.Delay(100);

        while (true)
        {
            var item = kafka.Poll();
            if (item == null) break;
            if (item.Value.Topic == "messages.search")
                search.Index(item.Value.Msg);
            else if (item.Value.Topic == "messages.new")
                notify.Notify(item.Value.Msg);
        }

        Console.WriteLine("\n=== Повідомлення успішно доставлено ===");
    }
}
