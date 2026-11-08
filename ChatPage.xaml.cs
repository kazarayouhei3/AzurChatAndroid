
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
    
    
    
    private async Task InitializeEmojiPanelAsync()
    {
        var categories = await EmojiService.LoadAsync();

        EmojiContainer.Children.Clear();

        foreach (var category in categories)
        {
            foreach (string emoji in category.Emojis)
            {
                string selectedEmoji = emoji;

                var button = new Button
                {
                    Text = emoji,
                    FontSize = 25,
                    WidthRequest = 46,
                    HeightRequest = 46,
                    Padding = new Thickness(0),
                    Margin = new Thickness(2),
                    BackgroundColor = Colors.Transparent
                };

                button.Clicked += (sender, e) =>
                {
                    InsertEmoji(selectedEmoji);
                };

                EmojiContainer.Children.Add(button);
            }
        }
    }



    private void InsertEmoji(string emoji)
    {
        string text = MessageInput.Text ?? "";

        int position = Math.Clamp(
            MessageInput.CursorPosition,
            0,
            text.Length);

        MessageInput.Text = text.Insert(position, emoji);

        MessageInput.CursorPosition =
            position + emoji.Length;
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

                
                if (message.MessageType == "image")
                {
                    AddImageMessage(
                        message.MediaPath ?? "",
                        time,
                        message.Sender == "user");
                }
                else if (message.MessageType == "sticker")
                {
                    AddStickerMessage(
                        message.MediaPath ?? "",
                        time,
                        message.Sender == "user");
                }
                else if (message.Sender == "user")
                {
                    AddRightMessage(message.Content, time);
                }
                else
                {
                    AddLeftMessage(message.Content, time);
                }

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

    
    private async Task DeleteMyEmojiAsync(string emojiPath)
    {
        bool confirm = await DisplayAlert(
            "删除表情",
            "确定删除这个收藏的表情吗？",
            "删除",
            "取消");

        if (!confirm)
            return;

        try
        {
            string folder = System.IO.Path.GetFullPath(
                GetMyEmojiFolder());

            string fullPath = System.IO.Path.GetFullPath(
                emojiPath);

            string prefix =
                System.IO.Path.TrimEndingDirectorySeparator(folder)
                + System.IO.Path.DirectorySeparatorChar;

            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!fullPath.StartsWith(prefix, comparison))
                return;

            // 兼容旧版本：如果历史消息仍引用收藏原文件，
            // 先保留文件，避免历史聊天丢图。
            await using var db =
                await _dbFactory.CreateDbContextAsync();

            bool referenced = await db.Message.AnyAsync(m =>
                m.MessageType == "sticker" &&
                m.MediaPath == emojiPath);

            if (referenced)
            {
                await DisplayAlert(
                    "暂时无法删除",
                    "该表情的旧聊天记录仍引用收藏文件，暂时不能安全删除。",
                    "确定");
                return;
            }

            if (File.Exists(fullPath))
                File.Delete(fullPath);

            LoadMyEmojis();
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "删除失败",
                ex.Message,
                "确定");
        }
    }


    // 用户消息：右侧绿色气泡
    // 用户消息：右侧绿色气泡 + 当前用户头像
    private void AddRightMessage(string text, DateTime time)
    {
        CheckAndAddTime(time);

        // 获取当前登录用户的头像
        string userAvatar = _authService.CurrentUser?.Avatar ?? "";

        var avatar = new Image
        {
            Source = string.IsNullOrWhiteSpace(userAvatar)
                ? "user.png"
                : userAvatar,

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

        // 原来的绿色消息气泡
        var bubble = CreateBubble(
            text,
            Color.FromArgb("#95EC69"));

        // 消息在左，用户头像在右
        var layout = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.End,

            Children =
            {
                bubble,
                avatar
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
                MessageType = "text",
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

        await SaveAndDisplayAssistantReplyAsync(reply);
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
    
    
private async Task SendImageAsync(FileResult photo)
{
    if (_convId == 0 || _isLoading || _isSending)
        return;

    _isSending = true;

    string? savedPath = null;
    bool messageSaved = false;

    try
    {
        string folder = System.IO.Path.Combine(
            FileSystem.AppDataDirectory,
            "chat_images");

        Directory.CreateDirectory(folder);

        string extension =
            System.IO.Path.GetExtension(photo.FileName).ToLowerInvariant();

        if (extension != ".jpg" &&
            extension != ".jpeg" &&
            extension != ".png" &&
            extension != ".webp")
        {
            extension = ".jpg";
        }

        string fileName =
            Guid.NewGuid().ToString("N") + extension;

        savedPath = System.IO.Path.Combine(folder, fileName);

        await using (var source =
            await photo.OpenReadAsync())
        await using (var destination =
            File.Create(savedPath))
        {
            await source.CopyToAsync(destination);
        }

        DateTime now = DateTime.Now;

        await using (var db =
            await _dbFactory.CreateDbContextAsync())
        {
            var message = new Message
            {
                ConvId = _convId,
                Sender = "user",
                Content = "[图片]",
                MessageType = "image",
                MediaPath = savedPath,
                Time = now
            };

            db.Message.Add(message);

            var conversation = await db.Conversation
                .FirstOrDefaultAsync(c => c.Id == _convId);

            if (conversation != null)
            {
                conversation.LastMessage = "[图片]";
                conversation.LastTime = now;
            }

            await db.SaveChangesAsync();
            messageSaved = true;

            _messageCache.AddMessage(message);
        }

        AddImageMessage(savedPath, now, true);

// 图片发送完成后收起功能面板
        FunctionPanel.IsVisible = false;
        FunctionPanel.Opacity = 1;
        _functionPanelOpen = false;

// 1. 显示 AI 正在输入
        Title = "对方正在输入...";

// 2. 请求 AI
// 图片已经保存到数据库，ChatService 会从历史记录读取
        string reply = await _chatService.ChatAsync(_convId, "");

// 3. 模拟输入延迟
        double delay = Math.Min(reply.Length * 0.05, 2.0);

        if (delay > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(delay));
        }

        await SaveAndDisplayAssistantReplyAsync(reply);
    }
    catch (Exception ex)
    {
        // 数据库尚未保存成功时，清理复制的图片
        if (!messageSaved &&
            savedPath != null &&
            File.Exists(savedPath))
        {
            File.Delete(savedPath);
        }

        await DisplayAlert(
            "图片发送失败",
            ex.Message,
            "确定");
    }
    finally
    {
        Title = _currentName;
        _isSending = false;
    }
}
private void EmojiTab_Tapped(
    object sender,
    TappedEventArgs e)
{
    SetEmojiTab(true);
}
private void SetEmojiTab(bool showEmoji)
{
    // Emoji 标签样式
    EmojiTab.BackgroundColor = showEmoji
        ? Colors.White
        : Colors.Transparent;

    EmojiTabText.TextColor = showEmoji
        ? Color.FromArgb("#3478C8")
        : Color.FromArgb("#738096");

    EmojiTabText.FontAttributes = showEmoji
        ? FontAttributes.Bold
        : FontAttributes.None;

    // 我的表情标签样式
    MyEmojiTab.BackgroundColor = showEmoji
        ? Colors.Transparent
        : Colors.White;

    MyEmojiTabText.TextColor = showEmoji
        ? Color.FromArgb("#738096")
        : Color.FromArgb("#3478C8");

    MyEmojiTabText.FontAttributes = showEmoji
        ? FontAttributes.None
        : FontAttributes.Bold;

    // 切换内容
    EmojiContent.IsVisible = showEmoji;
    MyEmojiContent.IsVisible = !showEmoji;
}
private void CloseBottomPanels()
{
    _myEmojiEditMode = false;
    EmojiPanel.IsVisible = false;
    EmojiPanel.Opacity = 1;

    FunctionPanel.IsVisible = false;
    FunctionPanel.Opacity = 1;

    _functionPanelOpen = false;
}

private async Task SaveAndDisplayAssistantReplyAsync(string reply)
{
    // 按照分隔符拆分 AI 回复
    var parts = reply
        .Split(
            new[] { "<MSG_SPLIT>" },
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries)
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .ToList();

    if (parts.Count == 0)
        return;

    var savedMessages = new List<Message>();

    DateTime replyTime = DateTime.Now;

    // 一次性保存所有分段消息
    await using (var db =
                 await _dbFactory.CreateDbContextAsync())
    {
        foreach (var part in parts)
        {
            var message = new Message
            {
                ConvId = _convId,
                Sender = "assistant",
                Content = part,
                MessageType = "text",
                Time = replyTime
            };

            db.Message.Add(message);
            savedMessages.Add(message);
        }

        // 更新会话最后一条消息
        var conversation = await db.Conversation
            .FirstOrDefaultAsync(c => c.Id == _convId);

        if (conversation != null)
        {
            conversation.LastMessage = parts.Last();
            conversation.LastTime = replyTime;
        }

        await db.SaveChangesAsync();

        // 更新消息缓存
        foreach (var message in savedMessages)
        {
            _messageCache.AddMessage(message);
        }
    }

    // 逐条显示 AI 消息
    foreach (var message in savedMessages)
    {
        AddLeftMessage(message.Content, message.Time ?? replyTime);

        // 分段显示时稍微停顿
        await Task.Delay(250);
    }
}

private void MyEmojiTab_Tapped(
    object sender,
    TappedEventArgs e)
{
    if (_myEmojiEditMode)
    {
        ExitMyEmojiEditMode();
        return;
    }

    SetEmojiTab(false);
    LoadMyEmojis();
    _myEmojiInitialized = true;
}
    private bool _functionPanelOpen = false;
    private bool _functionPanelBusy = false;
    private bool _emojiInitialized = false;
    private bool _emojiLoading = false;
    private bool _myEmojiInitialized = false;
    private bool _myEmojiLoading = false;
    private bool _myEmojiEditMode = false;
// 点击加号
    
    private async void PlusButton_Clicked(
        object sender,
        EventArgs e)
    {
        if (_functionPanelBusy || _emojiLoading)
            return;

        _functionPanelBusy = true;

        try
        {
            // 如果 Emoji 面板正在显示
            if (EmojiPanel.IsVisible)
            {
                CloseBottomPanels();
                return;
            }

            if (_functionPanelOpen)
            {
                await FunctionPanel.FadeTo(0, 120);

                FunctionPanel.IsVisible = false;
                FunctionPanel.Opacity = 1;
                _functionPanelOpen = false;
            }
            else
            {
                MessageInput.Unfocus();

                FunctionPanel.Opacity = 0;
                FunctionPanel.IsVisible = true;

                await FunctionPanel.FadeTo(1, 180);

                _functionPanelOpen = true;
            }
        }
        finally
        {
            _functionPanelBusy = false;
        }
    }
    private void EnterMyEmojiEditMode()
    {
        if (_myEmojiEditMode)
            return;

        _myEmojiEditMode = true;
        LoadMyEmojis();
    }

    private void ExitMyEmojiEditMode()
    {
        if (!_myEmojiEditMode)
            return;

        _myEmojiEditMode = false;
        LoadMyEmojis();
    }
    
    private string GetMyEmojiFolder()
    {
        string username = _authService.CurrentUser?.Username
                          ?? throw new InvalidOperationException("用户未登录");

        byte[] hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(username));

        string accountFolder = Convert.ToHexString(hash);

        string folder = System.IO.Path.Combine(
            FileSystem.AppDataDirectory,
            "my_emojis",
            accountFolder);

        Directory.CreateDirectory(folder);

        return folder;
    }

    private void AttachMyEmojiLongPress(Border border)
    {
#if ANDROID
    void Attach()
    {
        if (border.Handler?.PlatformView is not Android.Views.View view)
            return;

        view.LongClickable = true;

        view.LongClick += (sender, e) =>
        {
            EnterMyEmojiEditMode();
        };
    }

    border.HandlerChanged += (sender, e) => Attach();

    if (border.Handler != null)
        Attach();
#endif
    }

    
private void LoadMyEmojis()
{
    string folder = GetMyEmojiFolder();

    // 保留第一个添加按钮
    while (MyEmojiContainer.Children.Count > 1)
    {
        MyEmojiContainer.Children.RemoveAt(1);
    }

    var files = Directory.GetFiles(folder)
        .Where(path =>
        {
            string ext = System.IO.Path
                .GetExtension(path)
                .ToLowerInvariant();

            return ext == ".png"
                || ext == ".jpg"
                || ext == ".jpeg"
                || ext == ".webp";
        })
        .OrderByDescending(File.GetCreationTimeUtc)
        .ToList();

    foreach (string file in files)
    {
        string emojiPath = file;

        var image = new Image
        {
            Source = ImageSource.FromFile(emojiPath),
            WidthRequest = 72,
            HeightRequest = 72,
            Aspect = Aspect.AspectFill,
            InputTransparent = true
        };

        // 图片和删除按钮叠放
        var grid = new Grid
        {
            WidthRequest = 72,
            HeightRequest = 72
        };

        grid.Children.Add(image);

        if (_myEmojiEditMode)
        {
            var deleteButton = new Button
            {
                Text = "×",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                BackgroundColor = Colors.Red,
                WidthRequest = 24,
                HeightRequest = 24,
                CornerRadius = 12,
                Padding = 0,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                ZIndex = 10
            };

            deleteButton.Clicked += async (sender, e) =>
            {
                await DeleteMyEmojiAsync(emojiPath);
            };

            grid.Children.Add(deleteButton);
        }

        var border = new Border
        {
            WidthRequest = 72,
            HeightRequest = 72,
            Margin = new Thickness(4),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(12)
            },
            Content = grid
        };

        // 普通点击发送，编辑模式不发送
        var tap = new TapGestureRecognizer();

        tap.Tapped += async (sender, e) =>
        {
            if (_myEmojiEditMode)
                return;

            await SendStickerAsync(emojiPath);
        };

        border.GestureRecognizers.Add(tap);

        // Android 原生长按
        AttachMyEmojiLongPress(border);

        MyEmojiContainer.Children.Add(border);
    }
}


    private async void AddMyEmoji_Tapped(
        object sender,
        TappedEventArgs e)
    {
        if (_myEmojiLoading)
            return;

        _myEmojiLoading = true;

        try
        {
            // 打开系统相册
            var photo = await MediaPicker.Default.PickPhotoAsync();

            if (photo == null)
                return;

            string folder = GetMyEmojiFolder();

            string extension = System.IO.Path.GetExtension(
                photo.FileName).ToLowerInvariant();

            if (extension != ".png" &&
                extension != ".jpg" &&
                extension != ".jpeg" &&
                extension != ".webp")
            {
                extension = ".jpg";
            }

            string fileName =
                Guid.NewGuid().ToString("N") + extension;

            string savedPath = System.IO.Path.Combine(folder, fileName);

            // 将相册图片复制到 App 本地目录
            await using (var source = await photo.OpenReadAsync())
            await using (var destination = File.Create(savedPath))
            {
                await source.CopyToAsync(destination);
            }

            // 重新显示表情列表
            LoadMyEmojis();
            _myEmojiInitialized = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "添加表情失败",
                ex.Message,
                "确定");
        }
        finally
        {
            _myEmojiLoading = false;
        }
    }

    private async Task<string> CopyStickerToChatAsync(
        string stickerPath)
    {
        string folder = System.IO.Path.Combine(
            FileSystem.AppDataDirectory,
            "chat_stickers");

        Directory.CreateDirectory(folder);

        string extension =
            System.IO.Path.GetExtension(stickerPath).ToLowerInvariant();

        string fileName =
            Guid.NewGuid().ToString("N") + extension;

        string savedPath =
            System.IO.Path.Combine(folder, fileName);

        await using var source = File.OpenRead(stickerPath);
        await using var destination = File.Create(savedPath);

        await source.CopyToAsync(destination);

        return savedPath;
    }

private async Task SendStickerAsync(string stickerPath)
{
    if (_convId == 0 || _isLoading || _isSending)
        return;

    if (!File.Exists(stickerPath))
        return;

    _isSending = true;

    string? chatStickerPath = null;
    bool messageSaved = false;

    try
    {
        // 发送前先复制到聊天专用目录
        chatStickerPath = await CopyStickerToChatAsync(stickerPath);

        DateTime now = DateTime.Now;

        // 保存表情消息
        await using (var db =
            await _dbFactory.CreateDbContextAsync())
        {
            var message = new Message
            {
                ConvId = _convId,
                Sender = "user",
                Content = "[表情]",
                MessageType = "sticker",
                MediaPath = chatStickerPath,
                Time = now
            };

            db.Message.Add(message);

            var conversation = await db.Conversation
                .FirstOrDefaultAsync(c => c.Id == _convId);

            if (conversation != null)
            {
                conversation.LastMessage = "[表情]";
                conversation.LastTime = now;
            }

            await db.SaveChangesAsync();

            messageSaved = true;

            _messageCache.AddMessage(message);
        }

        // 显示发送的表情
        AddStickerMessage(chatStickerPath!, now, true);

        // 关闭底部菜单
        CloseBottomPanels();

        // AI 回复
        Title = "对方正在输入...";

        string reply = await _chatService.ChatAsync(
            _convId, "");

        double delay = Math.Min(
            reply.Length * 0.05, 2.0);

        if (delay > 0)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(delay));
        }

        DateTime replyTime = DateTime.Now;

        // 保存 AI 回复
        await using (var db =
            await _dbFactory.CreateDbContextAsync())
        {
            var aiMessage = new Message
            {
                ConvId = _convId,
                Sender = "assistant",
                Content = reply,
                MessageType = "text",
                Time = replyTime
            };

            db.Message.Add(aiMessage);

            var conversation = await db.Conversation
                .FirstOrDefaultAsync(c => c.Id == _convId);

            if (conversation != null)
            {
                conversation.LastMessage = reply;
                conversation.LastTime = replyTime;
            }

            await db.SaveChangesAsync();

            _messageCache.AddMessage(aiMessage);
        }

        AddLeftMessage(reply, replyTime);
    }
    catch (Exception ex)
    {
        // 数据库未保存成功时清理副本
        if (!messageSaved &&
            chatStickerPath != null &&
            File.Exists(chatStickerPath))
        {
            File.Delete(chatStickerPath);
        }

        await DisplayAlert(
            "发送表情失败",
            ex.Message,
            "确定");
    }
    finally
    {
        Title = _currentName;
        _isSending = false;
    }
}

private void AddStickerMessage(
    string stickerPath,
    DateTime time,
    bool isUser)
{
    CheckAndAddTime(time);

    string avatarPath = isUser
        ? (_authService.CurrentUser?.Avatar ?? "")
        : _currentAvatar;

    var avatar = new Image
    {
        Source = string.IsNullOrWhiteSpace(avatarPath)
            ? "user.png"
            : avatarPath,
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

    var sticker = new Image
    {
        Source = ImageSource.FromFile(stickerPath),
        WidthRequest = 120,
        HeightRequest = 120,
        Aspect = Aspect.AspectFit
    };

    var layout = new HorizontalStackLayout
    {
        Spacing = 8,
        HorizontalOptions = isUser
            ? LayoutOptions.End
            : LayoutOptions.Start
    };

    if (isUser)
    {
        layout.Children.Add(sticker);
        layout.Children.Add(avatar);
    }
    else
    {
        layout.Children.Add(avatar);
        layout.Children.Add(sticker);
    }

    MessageContainer.Children.Add(layout);

    if (!_isLoading)
        _ = ScrollToBottomAsync();
}

// 左侧语音消息
    private async void VoiceButton_Clicked(
        object sender, EventArgs e)
    {
        await DisplayAlert(
            "语音消息",
            "录音功能待接入",
            "确定");
    }
    private async void EmojiBack_Tapped(
        object sender,
        TappedEventArgs e)
    {
        if (_functionPanelBusy || _emojiLoading)
            return;

        _functionPanelBusy = true;

        try
        {
            EmojiPanel.IsVisible = false;
            EmojiPanel.Opacity = 1;

            FunctionPanel.Opacity = 0;
            FunctionPanel.IsVisible = true;

            _functionPanelOpen = true;

            await FunctionPanel.FadeTo(1, 180);
        }
        finally
        {
            _functionPanelBusy = false;
        }
    }
// 图片
    
    private async void PhotoButton_Clicked(
        object sender,
        EventArgs e)
    {
        try
        {
            var photo =
                await MediaPicker.Default.PickPhotoAsync();

            if (photo == null)
                return;

            await SendImageAsync(photo);
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "选择图片失败",
                ex.Message,
                "确定");
        }
    }


// 拍照
    private async void CameraButton_Clicked(
        object sender, EventArgs e)
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlert(
                    "提示",
                    "当前设备不支持拍照",
                    "确定");
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync();

            if (photo == null)
                return;

            // 下一步：进入聊天图片预览页面
            await SendImageAsync(photo);
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "拍照失败",
                ex.Message,
                "确定");
        }
    }
    private void ChatArea_Tapped(
        object sender,
        TappedEventArgs e)
    {
        CloseBottomPanels();
    }
// 表情
    
    private async void EmojiButton_Clicked(
        object sender,
        EventArgs e)
    {
        if (_functionPanelBusy || _emojiLoading)
            return;

        _emojiLoading = true;

        try
        {
            // 第一次打开时加载 JSON
            if (!_emojiInitialized)
            {
                await InitializeEmojiPanelAsync();
                _emojiInitialized = true;
            }

            // 关闭功能面板
            FunctionPanel.IsVisible = false;
            FunctionPanel.Opacity = 1;
            _functionPanelOpen = false;
            
// 每次打开默认显示 Emoji 表情
            SetEmojiTab(true);
            // 关闭键盘
            MessageInput.Unfocus();

            // 显示 Emoji 面板
            EmojiPanel.Opacity = 0;
            EmojiPanel.IsVisible = true;

            await EmojiPanel.FadeTo(1, 180);
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "加载表情失败",
                ex.Message,
                "确定");
        }
        finally
        {
            _emojiLoading = false;
        }
    }


// 语音通话
    private async void CallButton_Clicked(
        object sender, EventArgs e)
    {
        await DisplayAlert(
            "语音通话",
            "通话功能待接入",
            "确定");
    }

    private void AddImageMessage(
        string imagePath,
        DateTime time,
        bool isUser)
    {
        CheckAndAddTime(time);

        string avatarPath = isUser
            ? (_authService.CurrentUser?.Avatar ?? "")
            : _currentAvatar;

        var avatar = new Image
        {
            Source = string.IsNullOrWhiteSpace(avatarPath)
                ? "user.png"
                : avatarPath,
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

        var image = new Image
        {
            Source = ImageSource.FromFile(imagePath),
            WidthRequest = 200,
            HeightRequest = 200,
            Aspect = Aspect.AspectFit
        };

        var bubble = new Border
        {
            BackgroundColor = isUser
                ? Color.FromArgb("#95EC69")
                : Colors.White,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(10)
            },
            Padding = new Thickness(5),
            Content = image
        };

        var layout = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = isUser
                ? LayoutOptions.End
                : LayoutOptions.Start
        };

        if (isUser)
        {
            layout.Children.Add(bubble);
            layout.Children.Add(avatar);
        }
        else
        {
            layout.Children.Add(avatar);
            layout.Children.Add(bubble);
        }

        MessageContainer.Children.Add(layout);

        if (!_isLoading)
            _ = ScrollToBottomAsync();
    }

    
}
