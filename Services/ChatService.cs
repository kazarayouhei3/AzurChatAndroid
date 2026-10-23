using AzurChat.Data;
using AzurChat.Model;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using System.ClientModel;
using OpenAI.Chat;

namespace AzurChat.Services;

public class ChatService
{
    private readonly IDbContextFactory<UserContext> _dbFactory;

    private readonly MessageCacheService _messageCache;
    private readonly EncryptionService _encryptionService;
    public ChatService(
        IDbContextFactory<UserContext> dbFactory,
        MessageCacheService messageCache,
        EncryptionService encryptionService)
    {
        _dbFactory = dbFactory;
        _messageCache = messageCache;
        _encryptionService = encryptionService;
    }

    private async Task<User> LoadUserConfigAsync(string username)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var user = await db.User
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user == null)
            throw new InvalidOperationException("用户不存在");

        if (string.IsNullOrWhiteSpace(user.Api) ||
            string.IsNullOrWhiteSpace(user.Model))
            throw new InvalidOperationException("AI 配置不完整");

        return user;
    }

    private async Task<string> LoadPersonaAsync(string prompt)
    {
        string path = $"prompt/{prompt}";

        using var stream = await FileSystem.OpenAppPackageFileAsync(path);
        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync();
    }
    
    public async Task<string> ChatAsync(int convId, string userText)
    {
        // 1. 创建本次请求独立的数据库上下文
        await using var db = await _dbFactory.CreateDbContextAsync();

        // 2. 查询会话，确定当前用户和好友
        var conversation = await db.Conversation
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == convId);

        if (conversation == null)
            throw new InvalidOperationException("聊天会话不存在");

        // 这里还应通过 IAuthService 校验会话属于当前登录用户
        string username = conversation.UserName;
        string friendId = conversation.FriendId;

        // 3. 获取用户的 API 配置
        User config = await LoadUserConfigAsync(username);

        // 4. 获取好友对应的角色文件名
        string prompt = await db.AllFriend
            .Where(f => f.Id == friendId)
            .Select(f => f.Prompt)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("找不到好友角色");

        // 5. 加载角色设定
        string systemPrompt = await LoadPersonaAsync(prompt);

        // 6. 创建 API 客户端
        // 先解密数据库中保存的 API Key
        string apiKey = await _encryptionService.DecryptAsync(config.Api);

        // 创建 API 客户端
        var options = new OpenAI.OpenAIClientOptions();

        if (!string.IsNullOrWhiteSpace(config.Url))
            options.Endpoint = new Uri(config.Url);

        var client = new OpenAI.OpenAIClient(
            new System.ClientModel.ApiKeyCredential(apiKey),
            options
        );

        ChatClient chatClient = client.GetChatClient(config.Model);

        // 7. 初始化本次请求的消息列表
        List<ChatMessage> messages = new()
        {
            new SystemChatMessage(systemPrompt)
        };

        // 8. 从数据库读取最近的聊天记录
        var history = await db.Message
            .AsNoTracking()
            .Where(m => m.ConvId == convId)
            .OrderByDescending(m => m.Id)
            .Take(18)
            .ToListAsync();

        history.Reverse();

        foreach (var message in history)
        {
            if (message.Sender == "user")
                messages.Add(new UserChatMessage(message.Content));
            else if (message.Sender == "assistant")
                messages.Add(new AssistantChatMessage(message.Content));
        }

        // 9. 加入本次用户消息
        // 如果调用前已经保存到数据库，避免重复添加
        bool alreadyIncluded = history.Count > 0
            && history[^1].Sender == "user"
            && history[^1].Content == userText;

        if (!alreadyIncluded)
            messages.Add(new UserChatMessage(userText));

        // 10. 请求 AI
        ChatCompletion response = await chatClient.CompleteChatAsync(messages);

        string reply = string.Concat(
            response.Content
                .Where(part => !string.IsNullOrEmpty(part.Text))
                .Select(part => part.Text)
        );

        return reply;
    }
    
    public async Task GenerateReplyAsync(int convId, string text)
    {
        try
        {
            // 1. 调用 AI
            string reply = await ChatAsync(convId, text);

            // 2. 创建独立数据库上下文
            await using var db = await _dbFactory.CreateDbContextAsync();

            // 3. 保存 AI 回复
            var message = new Message
            {
                ConvId = convId,
                Sender = "assistant",
                Content = reply,
                Time = DateTime.Now
            };

            db.Message.Add(message);

            // 4. 更新会话
            var conversation = await db.Conversation
                .FirstOrDefaultAsync(c => c.Id == convId);

            if (conversation != null)
            {
                conversation.LastMessage = reply;
                conversation.LastTime = message.Time;
            }

            // 5. 提交数据库
            await db.SaveChangesAsync();

            // 6. 数据库保存成功后，更新内存缓存
            _messageCache.AddMessage(message);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AI错误: {ex}");
        }
    }
}