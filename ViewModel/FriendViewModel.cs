using AzurChat.Data;
using AzurChat.Model;
using AzurChat.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;

namespace AzurChat.ViewModel;

public partial class FriendViewModel : ObservableObject
{
    private INavigation? _navigation;
    private readonly UserContext _db;
    private readonly IAuthService _authService;
    private readonly ChatService _chatService;
    private readonly MessageCacheService _messageCache;
    [ObservableProperty] private string friendId = "";
    [ObservableProperty] private string nickname = "";
    [ObservableProperty] private string friendName = "";
    [ObservableProperty] private string verify = "";
    [ObservableProperty]
    private string friendGroup = "";
    [ObservableProperty] private string nicknamePlaceholder = "";
    [ObservableProperty] private string avatarImage = "";
    
    

    public FriendViewModel(
        UserContext db,
        IAuthService authService,
        ChatService chatService,
        MessageCacheService messageCache)
    {
        _db = db;
        _authService = authService;
        _chatService = chatService;
        _messageCache = messageCache;
    }
    public void SetNavigation(INavigation navigation)
    {
        _navigation = navigation;
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        if (_navigation != null)
            await _navigation.PopAsync();
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        string nickname = GetInputName();
        string verifyMsg = GetVerifyMsg();
        string username = _authService.CurrentUser?.Username ?? "";

        // 检查是否已经添加
        bool exists = await _db.Friend.AnyAsync(f =>
            f.UserName == username && f.Id == FriendId);

        if (exists)
            return;

        // 插入好友
        string groupName = string.IsNullOrWhiteSpace(FriendGroup)
            ? "我的舰娘"
            : FriendGroup.Trim();

        _db.Friend.Add(new Friend
        {
            Id = FriendId,
            UserName = username,
            FriendName = FriendName,
            NickName = nickname,
            Avatar = AvatarImage,
            GroupName = groupName
        });

        await _db.SaveChangesAsync();
        await AddConversationAsync(username, nickname);

        int? convId = await GetConvIdAsync(username, FriendId);
        
        if (convId.HasValue)
            _messageCache.Initialize(convId.Value);
        
        if (!string.IsNullOrEmpty(verifyMsg) && convId.HasValue)
        {
            await InsertMessageAsync(convId.Value, "user", verifyMsg);
            await UpdateConversationAsync(username, FriendId, verifyMsg);
            _ = _chatService.GenerateReplyAsync(convId.Value, verifyMsg);
        }
        
        Reset();

        if (_navigation != null)
            await _navigation.PopToRootAsync();
    }
    
    private async Task InsertMessageAsync(
        int convId,
        string sender,
        string content)
    {
        var message = new Message
        {
            ConvId = convId,
            Sender = sender,
            Content = content,
            Time = DateTime.Now
        };

        _db.Message.Add(message);

        await _db.SaveChangesAsync();

        _messageCache.AddMessage(message);
    }
    
    private async Task UpdateConversationAsync(
        string username,
        string friendId,
        string message)
    {
        var conversation = await _db.Conversation
            .FirstOrDefaultAsync(c =>
                c.UserName == username &&
                c.FriendId == friendId);

        if (conversation == null)
            return;

        conversation.LastMessage = message;
        conversation.LastTime = DateTime.Now;

        await _db.SaveChangesAsync();
    }
    
    
    private async Task AddConversationAsync(string username, string nickname)
    {
        bool exists = await _db.Conversation.AnyAsync(c =>
            c.UserName == username &&
            c.FriendId == FriendId);

        if (exists)
            return;
        
        _db.Conversation.Add(new Conversation
        {
            UserName = username,
            FriendId = FriendId,
            FriendName = FriendName,
            NickName = nickname,
            Avatar = AvatarImage,
            LastMessage = "",
            LastTime = DateTime.Now,
            Tag = 0
        });

        await _db.SaveChangesAsync();
    }
    
    private async Task<int?> GetConvIdAsync(string username, string friendId)
    {
        return await _db.Conversation
            .Where(c => c.UserName == username && c.FriendId == friendId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync();
    }
    public void SetUserInfo(string id, string name, string avatar)
    {
        FriendId = id;
        FriendName = name;
        AvatarImage = avatar;
        FriendGroup = "";
        Nickname= "";
        NicknamePlaceholder = name;
        Verify = "";
    }
    
    public void Reset()
    {
        Nickname = "";
        Verify = "";
    }


    public string GetInputName()
    {
        string nickname = Nickname?.Trim() ?? "";
        return string.IsNullOrEmpty(nickname)
            ? FriendName
            : nickname;
    }

    public string GetVerifyMsg()
    {
        return Verify?.Trim() ?? "";
    }
}