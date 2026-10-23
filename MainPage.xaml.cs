using AzurChat.Data;
using AzurChat.Services;
using AzurChat.Model;
namespace AzurChat;

using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Extensions.DependencyInjection;

public class ContactItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Avatar { get; set; } = "";
}

public class ChatListItem
{
    public string Fid { get; set; } = "";
    public string Name { get; set; } = "";
    public string Message { get; set; } = "";
    public string TimeText { get; set; } = "";
    public string Avatar { get; set; } = "";
    public string Prompt { get; set; } = "";
}

public partial class MainPage : ContentPage
{
    private readonly IAuthService _authService;
    private readonly UserContext _db;
    private readonly IServiceProvider _serviceProvider;
    private bool _isGroupMode = true;
    private bool _isChatAnimating = false;
    public MainPage(
       IAuthService authService,
       UserContext db,
       IServiceProvider serviceProvider)
    {
       InitializeComponent();

       _authService = authService;
       _db = db;
       _serviceProvider = serviceProvider;

       SwitchTab("chat");
    }

    
    /// <summary>
    /// 页面显示时自动执行。
    /// 每次进入 MainPage 时重新加载当前用户信息。
    /// </summary>
    protected override async void OnAppearing()
    {
       base.OnAppearing();

       await LoadUserDataAsync();

       if (ContactContent.IsVisible)
          await LoadFriendsAsync();

       if (ChatContent.IsVisible)
          await LoadChatsAsync();
    }

    /// <summary>
    /// 从数据库重新获取当前登录用户的最新资料，
    /// 并更新 AuthService.CurrentUser。
    /// </summary>
    /// <returns>表示异步加载操作的任务。</returns>
    private async Task LoadUserDataAsync()
    {
       try
       {
          // 从数据库刷新当前登录用户信息
          await _authService.RefreshCurrentUserAsync();

          // 获取刷新后的用户对象
          var user = _authService.CurrentUser;

          if (user == null)
          {
             System.Diagnostics.Debug.WriteLine("当前用户不存在");
             return;
          }

          // 后续可在这里使用最新的用户信息更新页面
          System.Diagnostics.Debug.WriteLine(
             $"当前用户：{user.Username}，ID：{user.Id}");
       }
       catch (Exception ex)
       {
          System.Diagnostics.Debug.WriteLine(
             $"加载用户信息失败：{ex.Message}");
       }
    }


    private async void Chat_Tapped(object sender, TappedEventArgs e)
    {
       SwitchTab("chat");

       await LoadChatsAsync();
    }
    
    private async Task PlayChatAnimationAsync(List<ChatListItem> items)
    {
       if (_isChatAnimating)
          return;

       _isChatAnimating = true;

       try
       {
          ChatListContainer.Children.Clear();

          foreach (var chat in items)
          {
             if (!ChatContent.IsVisible)
                break;

             var item = CreateChatItem(chat);

             // 初始位置在右侧 300
             item.TranslationX = 300;

             ChatListContainer.Children.Add(item);

             // 先向左滑入，超过目标位置 10
             await item.TranslateTo(
                -10,
                0,
                300,
                Easing.CubicOut
             );

             // 再回弹到正常位置
             await item.TranslateTo(
                0,
                0,
                150,
                Easing.CubicOut
             );

             // 等待 300 毫秒，显示下一个好友
             await Task.Delay(300);
          }
       }
       finally
       {
          _isChatAnimating = false;
       }
    }
    
    private Border CreateChatItem(ChatListItem chat)
{
    var avatar = new Image
    {
        Source = chat.Avatar,
        WidthRequest = 44,
        HeightRequest = 44,
        Aspect = Aspect.AspectFill,
        Clip = new EllipseGeometry
        {
            Center = new Point(22, 22),
            RadiusX = 22,
            RadiusY = 22
        }
    };

    var name = new Label
    {
        Text = chat.Name,
        FontSize = 14,
        FontAttributes = FontAttributes.Bold,
        TextColor = Color.FromArgb("#222222"),
        LineBreakMode = LineBreakMode.TailTruncation
    };

    var message = new Label
    {
        Text = chat.Message,
        FontSize = 12,
        TextColor = Color.FromArgb("#777777"),
        LineBreakMode = LineBreakMode.TailTruncation
    };

    var time = new Label
    {
        Text = chat.TimeText,
        FontSize = 11,
        TextColor = Color.FromArgb("#999999"),
        HorizontalOptions = LayoutOptions.End,
        VerticalOptions = LayoutOptions.Start
    };

    var textLayout = new VerticalStackLayout
    {
        Spacing = 2,
        VerticalOptions = LayoutOptions.Center,
        Children =
        {
            name,
            message
        }
    };

    var grid = new Grid
    {
        ColumnDefinitions =
        {
            new ColumnDefinition { Width = new GridLength(44) },
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = GridLength.Auto }
        },
        ColumnSpacing = 10
    };

    grid.Add(avatar);
    Grid.SetColumn(avatar, 0);

    grid.Add(textLayout);
    Grid.SetColumn(textLayout, 1);

    grid.Add(time);
    Grid.SetColumn(time, 2);

    var border = new Border
    {
        BackgroundColor = Colors.White,
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle
        {
           CornerRadius = new CornerRadius(8)
        },
        Padding = new Thickness(12, 8),
        HeightRequest = 64,
        Content = grid
    };

// 点击会话好友
    var tap = new TapGestureRecognizer();

    bool isOpening = false;

    tap.Tapped += async (sender, e) =>
    {
       if (isOpening)
          return;

       isOpening = true;

       try
       {
          var page = _serviceProvider.GetRequiredService<ChatPage>();

          // 先加载聊天记录
          await page.SetChatAsync(
             chat.Fid,
             chat.Name,
             chat.Prompt,
             chat.Avatar
          );

          // 加载完成后才进入聊天页面
          await Navigation.PushAsync(page);
          
          await page.ScrollToBottomAsync();
       }
       catch (Exception ex)
       {
          await DisplayAlert(
             "进入聊天失败",
             ex.ToString(),
             "确定"
          );
       }
       finally
       {
          isOpening = false;
       }
    };

    border.GestureRecognizers.Add(tap);

    return border;
}
    private async Task LoadChatsAsync()
    {
       string username = _authService.CurrentUser?.Username ?? "";

       if (string.IsNullOrEmpty(username))
          return;

       // 查询当前用户的会话
       var conversations = await _db.Conversation
          .AsNoTracking()
          .Where(c => c.UserName == username)
          .OrderByDescending(c => c.LastTime)
          .ToListAsync();

       // 查询所有 AI 好友信息
       var allFriends = await _db.AllFriend
          .AsNoTracking()
          .ToDictionaryAsync(f => f.Id);

       // 生成聊天列表数据
       var items = conversations.Select(c => new ChatListItem
       {
          Fid = c.FriendId,

          Name = string.IsNullOrEmpty(c.NickName)
             ? c.FriendName
             : c.NickName,

          Message = c.LastMessage ?? "",

          TimeText = FormatChatTime(c.LastTime),

          Avatar = c.Avatar,

          Prompt = allFriends.TryGetValue(c.FriendId, out var friend)
             ? friend.Prompt
             : ""

       }).ToList();

       await PlayChatAnimationAsync(items);
    }
    
    
    private string FormatChatTime(DateTime? time)
    {
       if (time == null)
          return "";

       DateTime dt = time.Value;
       DateTime now = DateTime.Now;

       if (dt.Date == now.Date)
          return dt.ToString("HH:mm");

       if (dt.Date == now.Date.AddDays(-1))
          return "昨天";

       if (dt.Year == now.Year)
          return dt.ToString("MM-dd");

       return dt.ToString("yyyy-MM-dd");
    }

    private async void Contact_Tapped(object sender, TappedEventArgs e)
    {
       SwitchTab("contact");

       await LoadFriendsAsync();
    }
    
    private async Task LoadFriendsAsync()
    {
       string username = _authService.CurrentUser?.Username ?? "";

       if (string.IsNullOrWhiteSpace(username))
          return;

       var friends = await _db.Friend
          .AsNoTracking()
          .Where(f => f.UserName == username)
          .ToListAsync();
       // 默认分组
var defaultGroups = new[]
{
    "我的爱人",
    "我的舰娘",
    "家人"
};

// 空分组名统一归入“我的舰娘”
string GetGroupName(Friend f) =>
    string.IsNullOrWhiteSpace(f.GroupName)
        ? "我的舰娘"
        : f.GroupName.Trim();

// 默认分组 + 数据库中已有的自定义分组
var groupNames = defaultGroups
    .Concat(friends.Select(GetGroupName))
    .Distinct()
    .ToList();

ContactGroupsContainer.Children.Clear();

if (_isGroupMode)
{
    foreach (var groupName in groupNames)
    {
        var groupFriends = friends
            .Where(f => GetGroupName(f) == groupName)
            .OrderBy(f => string.IsNullOrWhiteSpace(f.NickName)
                ? f.FriendName
                : f.NickName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // 分组标题
        var arrow = new Label
        {
            Text = "›",
            FontSize = 24,
            TextColor = Color.FromArgb("#666666"),
            WidthRequest = 20,
            VerticalTextAlignment = TextAlignment.Center
        };

        var title = new Label
        {
            Text = groupName,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333333"),
            VerticalTextAlignment = TextAlignment.Center
        };

        var count = new Label
        {
            Text = groupFriends.Count.ToString(),
            FontSize = 12,
            TextColor = Color.FromArgb("#999999"),
            VerticalTextAlignment = TextAlignment.Center
        };

        var header = new Grid
        {
            HeightRequest = 40,
            Padding = new Thickness(10, 0),
            BackgroundColor = Colors.White,
            HorizontalOptions = LayoutOptions.Fill,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 20 },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        header.Add(arrow, 0, 0);
        header.Add(title, 1, 0);
        header.Add(count, 2, 0);

        // 分组好友列表
        var content = new VerticalStackLayout
        {
            Spacing = 0,
            Padding = new Thickness(0),
            IsVisible = false
        };

        foreach (var f in groupFriends)
        {
            var item = new ContactItem
            {
                Id = f.Id,
                Name = string.IsNullOrWhiteSpace(f.NickName)
                    ? f.FriendName
                    : f.NickName,
                Avatar = f.Avatar
            };

            content.Children.Add(CreateContactRow(item));
        }

        // 裁剪容器，用于展开动画
        var container = new Grid
        {
            IsClippedToBounds = true,
            HeightRequest = 0
        };

        container.Children.Add(content);

        bool expanded = false;
        bool animating = false;

        var tap = new TapGestureRecognizer();

        tap.Tapped += async (s, e) =>
        {
            if (animating)
                return;

            animating = true;

            try
            {
                expanded = !expanded;

                if (expanded)
                {
                    content.IsVisible = true;

                    double width = container.Width > 0
                        ? container.Width
                        : ContactGroupsContainer.Width;

                    if (width <= 0)
                        width = 300;

                    double targetHeight = content
                        .Measure(width, double.PositiveInfinity)
                        .Height;

                    await Task.WhenAll(
                        AnimateContactHeight(container, 0, targetHeight),
                        arrow.RotateTo(90, 200, Easing.CubicOut)
                    );

                    container.HeightRequest = -1;
                }
                else
                {
                    double currentHeight = container.Height;

                    container.HeightRequest = currentHeight;

                    await Task.WhenAll(
                        AnimateContactHeight(container, currentHeight, 0),
                        arrow.RotateTo(0, 200, Easing.CubicOut)
                    );

                    content.IsVisible = false;
                }
            }
            finally
            {
                animating = false;
            }
        };

        header.GestureRecognizers.Add(tap);

        ContactGroupsContainer.Children.Add(header);
        ContactGroupsContainer.Children.Add(container);
    }
}
else
{
    // 好友模式：全部好友平铺显示
    foreach (var f in friends)
    {
        var item = new ContactItem
        {
            Id = f.Id,
            Name = string.IsNullOrWhiteSpace(f.NickName)
                ? f.FriendName
                : f.NickName,
            Avatar = f.Avatar
        };

        ContactGroupsContainer.Children.Add(CreateContactRow(item));
    }
}
    }

private Border CreateContactRow(ContactItem friend)
{
    var avatar = new Image
    {
        Source = string.IsNullOrWhiteSpace(friend.Avatar)
            ? "head.jpg"
            : friend.Avatar,
        WidthRequest = 44,
        HeightRequest = 44,
        Aspect = Aspect.AspectFill,
        Clip = new EllipseGeometry
        {
            Center = new Point(22, 22),
            RadiusX = 22,
            RadiusY = 22
        }
    };

    var name = new Label
    {
        Text = friend.Name,
        FontSize = 15,
        TextColor = Color.FromArgb("#222222"),
        VerticalOptions = LayoutOptions.Center,
        LineBreakMode = LineBreakMode.TailTruncation
    };

    var row = new HorizontalStackLayout
    {
        Spacing = 12,
        Padding = new Thickness(12, 8),
        Children = { avatar, name }
    };

    return new Border
    {
        BackgroundColor = Colors.White,
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle
        {
            CornerRadius = new CornerRadius(8)
        },
        Content = row
    };
}

private Task AnimateContactHeight(
    VisualElement element,
    double from,
    double to)
{
    var tcs = new TaskCompletionSource<bool>();

    var animation = new Animation(
        value => element.HeightRequest = value,
        from,
        to,
        Easing.CubicOut);

    animation.Commit(
        element,
        "ContactGroupExpand",
        rate: 16,
        length: 200,
        finished: (value, cancelled) =>
        {
            element.HeightRequest = to;
            tcs.TrySetResult(true);
        });

    return tcs.Task;
}
    private async void GroupMode_Tapped(object sender, TappedEventArgs e)
    {
       SetContactMode(isGroupMode: true);
       await LoadFriendsAsync();
    }

    private async void FriendMode_Tapped(object sender, TappedEventArgs e)
    {
       SetContactMode(isGroupMode: false);
       await LoadFriendsAsync();
    }

    private void SetContactMode(bool isGroupMode)
    {
       _isGroupMode = isGroupMode;
       GroupModeLabel.FontAttributes = isGroupMode
          ? FontAttributes.Bold
          : FontAttributes.None;

       FriendModeLabel.FontAttributes = isGroupMode
          ? FontAttributes.None
          : FontAttributes.Bold;

       GroupModeLabel.TextColor = isGroupMode
          ? Color.FromArgb("#009FE3")
          : Color.FromArgb("#333333");

       FriendModeLabel.TextColor = isGroupMode
          ? Color.FromArgb("#333333")
          : Color.FromArgb("#009FE3");

       // 只有激活的标签才显示下划线
       GroupModeIndicator.IsVisible = isGroupMode;
       FriendModeIndicator.IsVisible = !isGroupMode;

       GroupModeIndicator.Color = Color.FromArgb("#009FE3");
       FriendModeIndicator.Color = Color.FromArgb("#009FE3");
    }
    private async void AddFriend_Tapped(object sender, TappedEventArgs e)
    {
       await Navigation.PushAsync(new AddFriendPage(_authService, _db, _serviceProvider));
    }
    
    
    
    private async void AzurButton_Tapped(
       object sender,
       TappedEventArgs e)
    {

    }

    private async void SettingButton_Tapped(
       object sender,
       TappedEventArgs e)
    {
       
    }

    private void My_Tapped(object sender, TappedEventArgs e)
    {
       SwitchTab("my");
    }

    private void SwitchTab(string tab)
    {
       // 切换中间内容
       ChatContent.IsVisible = tab == "chat";
       ContactContent.IsVisible = tab == "contact";
       MyContent.IsVisible = tab == "my";

       // 重置底部导航
       ChatIcon.Source = "talk.png";
       ContactIcon.Source = "phone.png";
       MyIcon.Source = "my.png";

       ChatLabel.TextColor = Colors.Black;
       ContactLabel.TextColor = Colors.Black;
       MyLabel.TextColor = Colors.Black;

       switch (tab)
       {
          case "chat":
             ChatIcon.Source = "talk_select.png";
             ChatLabel.TextColor = Color.FromArgb("#009FE3");
             break;

          case "contact":
             ContactIcon.Source = "phone_select.png";
             ContactLabel.TextColor = Color.FromArgb("#009FE3");
             break;

          case "my":
             MyIcon.Source = "my_select.png";
             MyLabel.TextColor = Color.FromArgb("#009FE3");
             break;
       }
    }

    private async void UserAvatarButton_Clicked(object? sender, EventArgs e)
    {
       await Shell.Current.GoToAsync("person");
    }
}
