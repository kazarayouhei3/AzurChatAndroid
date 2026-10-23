using AzurChat.Model;

namespace AzurChat.Services;

public class MessageCacheService
{
    private readonly Dictionary<int, List<Message>> _cache = new();

    private readonly object _lock = new();

    private const int MaxMessages = 20;

    public void Initialize(int convId)
    {
        lock (_lock)
        {
            if (!_cache.ContainsKey(convId))
                _cache[convId] = new List<Message>();
        }
    }

    public void AddMessage(Message message)
    {
        lock (_lock)
        {
            if (!_cache.TryGetValue(message.ConvId, out var messages))
                return;

            if (messages.Any(m => m.Id == message.Id))
                return;

            messages.Add(message);

            messages.Sort((a, b) => a.Id.CompareTo(b.Id));

            if (messages.Count > MaxMessages)
                messages.RemoveRange(0, messages.Count - MaxMessages);
        }
    }

    public List<Message>? GetMessages(int convId)
    {
        lock (_lock)
        {
            if (!_cache.TryGetValue(convId, out var messages))
                return null;

            return new List<Message>(messages);
        }
    }
}