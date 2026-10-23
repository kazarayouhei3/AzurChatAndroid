
using AzurChat.Data;
using AzurChat.Model;
using AzurChat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Extensions.DependencyInjection;

namespace AzurChat;

public partial class ChatPage : ContentPage
{
    private readonly IAuthService _authService;
    private readonly IDbContextFactory<UserContext> _dbFactory;
    private readonly ChatService _chatService;
    private readonly MessageCacheService _messageCache;
    private readonly IServiceProvider _serviceProvider;
    
    private string _currentFid = "";
    private string _currentName = "";
    private string _currentAvatar = "";

    private int _convId;
    private DateTime? _lastMessageTime;
    private bool _isLoading;
    private bool _isSending;

    public ChatPage(
        IAuthService authService,
        IDbContextFactory<UserContext> dbFactory,
        ChatService chatService,
        MessageCacheService messageCache,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();

        _authService = authService;
        _dbFactory = dbFactory;
        _chatService = chatService;
        _messageCache = messageCache;
        _serviceProvider = serviceProvider;

    }
    protected override void OnAppearing()
    {
        base.OnAppearing();

        // 等待页面布局完成后，滚动到最新消息
        MessageScroll.SizeChanged += MessageScroll_SizeChanged;
    }

    private async void MessageScroll_SizeChanged(object? sender, EventArgs e)
    {
        if (MessageScroll.Height <= 0)
            return;

        MessageScroll.SizeChanged -= MessageScroll_SizeChanged;

        await ScrollToBottomAsync();
    }

    // 初始化当前聊天
    public async Task SetChatAsync(
        string fid,
        string name,
        string prompt,
        string avatar)
    {
        _currentFid = fid;
        _currentName = name;
        _currentAvatar = avatar;

        Title = name;
        _lastMessageTime = null;

        string username = _authService.CurrentUser?.Username ?? "";

        if (string.IsNullOrWhiteSpace(username))
            return;

        await using var db = await _dbFactory.CreateDbContextAsync();

        var conversation = await db.Conversation
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.UserName == username &&
                c.FriendId == fid);

        if (conversation == null)
            return;

        _convId = conversation.Id;

        _messageCache.Initialize(_convId);

        await LoadChatAsync();
    }

    // 加载历史消息
    private async Task LoadChatAsync()
    {
        if (_convId == 0)
            return;

        _isLoading = true;

        try
        {
            MessageContainer.Children.Clear();
            _lastMessageTime = null;

            await using var db =
                await _dbFactory.CreateDbContextAsync();

            var messages = await db.Message
                .AsNoTracking()
                .Where(m => m.ConvId == _convId)
                .OrderBy(m => m.Id)
                .ToListAsync();

            foreach (var message in messages)
            {
                DateTime time = message.Time ?? DateTime.Now;

                if (message.Sender == "user")
                    AddRightMessage(message.Content, time);
                else
                    AddLeftMessage(message.Content, time);
            }
            
        }
        finally
        {
            _isLoading = false;
        }
    }

    // AI 消息：左侧白色气泡
    private void AddLeftMessage(string text, DateTime time)
    {
        CheckAndAddTime(time);

        var avatar = new Image
        {
            Source = _currentAvatar,
            WidthRequest = 36,
            HeightRequest = 36,
            Aspect = Aspect.AspectFill,
            VerticalOptions = LayoutOptions.Start,
            Clip = new EllipseGeometry
            {
                Center = new Point(18, 18),
                RadiusX = 18,
                RadiusY = 18
            }
        };

        var bubble = CreateBubble(text, Colors.White);

        var layout = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Start,
            Children =
            {
                avatar,
                bubble
            }
        };

        MessageContainer.Children.Add(layout);

        if (!_isLoading)
            _ = ScrollToBottomAsync();
    }

    // 用户消息：右侧绿色气泡
    private void AddRightMessage(string text, DateTime time)
    {
        CheckAndAddTime(time);

        var bubble = CreateBubble(
            text,
            Color.FromArgb("#95EC69"));

        var layout = new HorizontalStackLayout
        {
            HorizontalOptions = LayoutOptions.End,
            Children =
            {
                bubble
            }
        };

        MessageContainer.Children.Add(layout);

        if (!_isLoading)
            _ = ScrollToBottomAsync();
    }

    // 创建消息气泡
    private Border CreateBubble(string text, Color background)
    {
        return new Border
        {
            BackgroundColor = background,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(10)
            },
            Padding = new Thickness(8),
            MaximumWidthRequest = 300,
            Content = new Label
            {
                Text = text,
                FontSize = 14,
                TextColor = Colors.Black,
                LineBreakMode = LineBreakMode.WordWrap
            }
        };
    }

    // 每隔 5 分钟显示一次时间
    private void CheckAndAddTime(DateTime time)
    {
        if (_lastMessageTime == null ||
            (time - _lastMessageTime.Value).TotalSeconds >= 300)
        {
            AddTimeLabel(FormatMessageTime(time));
        }

        _lastMessageTime = time;
    }

    // 时间标签
    private void AddTimeLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 11,
            TextColor = Color.FromArgb("#999999"),
            HorizontalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Fill,
            Margin = new Thickness(0, 10)
        };

        MessageContainer.Children.Add(label);
    }

    // 时间格式
    private string FormatMessageTime(DateTime time)
    {
        DateTime now = DateTime.Now;

        if (time.Date == now.Date)
            return time.ToString("HH:mm");

        if (time.Date == now.Date.AddDays(-1))
            return "昨天 " + time.ToString("HH:mm");

        if (time.Year == now.Year)
            return time.ToString("MM-dd HH:mm");

        return time.ToString("yyyy-MM-dd HH:mm");
    }

    // 点击输入法键盘的“发送”键
    private async void MessageInput_Completed(
        object? sender,
        EventArgs e)
    {
        await SendMessageAsync();
    }

    private async void MenuButton_Clicked(object sender, EventArgs e)
    {
        // 通过 DI 创建好友信息页面
        var page = _serviceProvider.GetRequiredService<CharaPage>();

        // 设置好友信息
        page.SetCharaInfo(_currentName, _currentAvatar);

        // 进入好友信息页面
        await Navigation.PushAsync(page);
    }
    
private async Task SendMessageAsync()
{
    // 正在加载、正在发送或会话不存在时，不允许发送
    if (_isLoading || _isSending || _convId == 0)
        return;

    string text = MessageInput.Text?.Trim() ?? "";

    if (string.IsNullOrEmpty(text))
        return;

    _isSending = true;
    MessageInput.Text = "";

    try
    {
        // =========================
        // 1. 保存用户消息
        // =========================
        DateTime now = DateTime.Now;

        await using (var db =
            await _dbFactory.CreateDbContextAsync())
        {
            var message = new Message
            {
                ConvId = _convId,
                Sender = "user",
                Content = text,
                Time = now
            };

            db.Message.Add(message);

            // 更新会话最后一条消息
            var conversation = await db.Conversation
                .FirstOrDefaultAsync(c => c.Id == _convId);

            if (conversation != null)
            {
                conversation.LastMessage = text;
                conversation.LastTime = now;
            }

            await db.SaveChangesAsync();

            // 更新消息缓存
            _messageCache.AddMessage(message);
        }

        // =========================
        // 2. 显示用户消息
        // =========================
        AddRightMessage(text, now);

        // =========================
        // 3. 显示 AI 正在输入
        // =========================
        Title = "对方正在输入...";

        // =========================
        // 4. 调用 AI 获取回复
        // =========================
        string reply = await _chatService.ChatAsync(
            _convId,
            text
        );

        // 与原 Python 版本一致，模拟输入延迟
        double delay = Math.Min(reply.Length * 0.05, 2.0);

        if (delay > 0)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(delay)
            );
        }

        // =========================
        // 5. 保存 AI 回复
        // =========================
        DateTime replyTime = DateTime.Now;

        await using (var db =
            await _dbFactory.CreateDbContextAsync())
        {
            var message = new Message
            {
                ConvId = _convId,
                Sender = "assistant",
                Content = reply,
                Time = replyTime
            };

            db.Message.Add(message);

            // 更新会话最后一条消息
            var conversation = await db.Conversation
                .FirstOrDefaultAsync(c => c.Id == _convId);

            if (conversation != null)
            {
                conversation.LastMessage = reply;
                conversation.LastTime = replyTime;
            }

            await db.SaveChangesAsync();

            // 更新消息缓存
            _messageCache.AddMessage(message);
        }

        // =========================
        // 6. 显示 AI 回复
        // =========================
        AddLeftMessage(reply, replyTime);
    }
    catch (Exception ex)
    {
        // 显示错误，并保留用户已经发送的消息
        await DisplayAlert(
            "发送失败",
            ex.ToString(),
            "确定"
        );
    }
    finally
    {
        // =========================
        // 7. 恢复界面状态
        // =========================
        Title = _currentName;

        _isSending = false;
    }
}

    // 滚动到底部
    public async Task ScrollToBottomAsync()
    {
        await Task.Yield();

        await MessageScroll.ScrollToAsync(
            0,
            MessageScroll.ContentSize.Height,
            false);
    }
    

    
}
