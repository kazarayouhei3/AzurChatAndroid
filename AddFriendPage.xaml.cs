using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzurChat.Data;
using AzurChat.Model;
using AzurChat.Services;
using AzurChat.ViewModel;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace AzurChat;

public class FriendGroup
{
    public string Name { get; set; } = "";
    public List<AllFriend> Items { get; set; } = new();
}

public class SuggestItem
{
    public string Id { get; set; } = "";
    public string FriendName { get; set; } = "";
    public string Avatar { get; set; } = "";

    public bool CanAdd { get; set; }

    public string ButtonText =>
        CanAdd ? "添加" : "已添加";

    public Color ButtonColor =>
        CanAdd
            ? Color.FromArgb("#409EFF")
            : Color.FromArgb("#E4E7ED");
}

public partial class AddFriendPage : ContentPage
{
    private readonly IAuthService _authService;
    private readonly UserContext _db;
    
    private Grid? _selectedItem;
    private Color _normalItemColor = Colors.Transparent;
    private Color _selectedItemColor = Color.FromArgb("#409EFF");
    private readonly IServiceProvider _serviceProvider;

    public AddFriendPage(
        IAuthService authService,
        UserContext db,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();

        _authService = authService;
        _db = db;
        _serviceProvider = serviceProvider;
    }
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadFriendsAsync();
    }
    
    private async Task OpenConfirmAsync(string friendId)
    {
        if (_addedIds.Contains(friendId))
            return;

        var friend = _groups
            .SelectMany(g => g.Items)
            .FirstOrDefault(f => f.Id == friendId);

        if (friend == null)
            return;

        SuggestPopup.IsVisible = false;
        SuggestDismissLayer.IsVisible = false;

        var page = _serviceProvider.GetRequiredService<FriendPage>();

        var vm = (FriendViewModel)page.BindingContext;

        vm.SetUserInfo(
            friend.Id,
            friend.FriendName,
            friend.Avatar
        );

        await Navigation.PushAsync(page);
    }
    
    private void SearchEntry_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        string text = e.NewTextValue?.Trim() ?? "";

        if (string.IsNullOrEmpty(text))
        {
            SuggestPopup.IsVisible = false;
            SuggestDismissLayer.IsVisible = false;
            return;
        }

        var results = _groups
            .SelectMany(g => g.Items)
            .Where(f => f.FriendName.Contains(
                text, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .Select(f => new SuggestItem
            {
                Id = f.Id,
                FriendName = f.FriendName,
                Avatar = f.Avatar,
                CanAdd = !_addedIds.Contains(f.Id)
            })
            .ToList();

        if (results.Count == 0)
        {
            SuggestPopup.IsVisible = false;
            SuggestDismissLayer.IsVisible = false;
            return;
        }

        SuggestList.ItemsSource = results;

        // 每行 50，最多显示 5 行
        SuggestList.HeightRequest =
            Math.Min(results.Count, 5) * 50;

        SuggestDismissLayer.IsVisible = true;
        SuggestPopup.IsVisible = true;
    }
    
    private async void SuggestAdd_Clicked(
        object sender,
        EventArgs e)
    {
        if (sender is not Button button ||
            button.CommandParameter is not string friendId)
            return;

        await OpenConfirmAsync(friendId);
    }
    

    private async void BackButton_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
    
    
    
    private async Task<List<FriendGroup>> GetGroupsAsync()
    {
        var friends = await _db.AllFriend
            .AsNoTracking()
            .ToListAsync();

        return friends
            .GroupBy(f => f.Faction)
            .Select(g => new FriendGroup
            {
                Name = g.Key,
                Items = g.ToList()
            })
            .ToList();
    }
    private List<FriendGroup> _groups = new();
    
    private HashSet<string> _addedIds = new();

    private async Task LoadFriendsAsync()
    {
        var username = _authService.CurrentUser?.Username;

        if (string.IsNullOrEmpty(username))
            return;

        // 获取全部 AI 角色，按阵营分组
        _groups = await GetGroupsAsync();

        // 查询当前用户已添加的角色 ID
        var addedIds = await _db.Friend
            .AsNoTracking()
            .Where(f => f.UserName == username)
            .Select(f => f.Id)
            .ToListAsync();

        // 保存已添加的角色 ID
        _addedIds = addedIds.ToHashSet();
        BuildGroupList();
    }
    
    private void BuildGroupList()
{
    _selectedItem = null;
    GroupList.Children.Clear();

    foreach (var group in _groups)
    {
        // 整个分组
        var groupLayout = new VerticalStackLayout
        {
            Spacing = 0
        };

        // ===== 分组标题 =====
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
            Text = group.Name,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333333"),
            VerticalTextAlignment = TextAlignment.Center
        };

        var count = new Label
        {
            Text = group.Items.Count.ToString(),
            FontSize = 12,
            TextColor = Color.FromArgb("#999999"),
            VerticalTextAlignment = TextAlignment.Center
        };

        var headerGrid = new Grid
        {
            HeightRequest = 40,
            Padding = new Thickness(10, 0),
            BackgroundColor = Colors.White,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 20 },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        headerGrid.Add(arrow, 0, 0);
        headerGrid.Add(title, 1, 0);
        headerGrid.Add(count, 2, 0);

        // ===== 分组内容 =====
        var content = new VerticalStackLayout
        {
            Spacing = 0,
            Padding = new Thickness(30, 0, 0, 0)
        };

        foreach (var friend in group.Items)
        {
            bool isAdded = _addedIds.Contains(friend.Id);

            var avatar = new Image
            {
                Source = string.IsNullOrWhiteSpace(friend.Avatar)
                    ? null
                    : ImageSource.FromFile(friend.Avatar),
                WidthRequest = 40,
                HeightRequest = 40,
                Aspect = Aspect.AspectFill
            };

            var avatarBorder = new Border
            {
                WidthRequest = 40,
                HeightRequest = 40,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle
                {
                    CornerRadius = new CornerRadius(20)
                },
                Content = avatar
            };

            var name = new Label
            {
                Text = friend.FriendName,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#333333"),
                VerticalTextAlignment = TextAlignment.Center
            };

            var addButton = new Button
            {
                Text = isAdded ? "已添加" : "添加",
                IsEnabled = !isAdded,
                FontSize = 12,
                HeightRequest = 32,
                MinimumWidthRequest = 65,
                CornerRadius = 16,
                Padding = new Thickness(12, 0),
                BackgroundColor = isAdded
                    ? Color.FromArgb("#DDDDDD")
                    : Color.FromArgb("#409EFF"),
                TextColor = Colors.White
            };
            
            addButton.Clicked += async (sender, e) =>
            {
                await OpenConfirmAsync(friend.Id);
            };

            var itemGrid = new Grid
            {
                HeightRequest = 60,
                Padding = new Thickness(10, 6),
                ColumnSpacing = 10,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = 40 },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };

            itemGrid.Add(avatarBorder, 0, 0);
            itemGrid.Add(name, 1, 0);
            itemGrid.Add(addButton, 2, 0);
            
            var itemTap = new TapGestureRecognizer();

            itemTap.Tapped += async (sender, e) =>
            {
                // 清除之前的选中状态
                if (_selectedItem != null)
                {
                    _selectedItem.BackgroundColor = _normalItemColor;
                }

                // 当前角色变成蓝色
                itemGrid.BackgroundColor = _selectedItemColor;

                _selectedItem = itemGrid;

                // 打开添加好友申请页面
                await OpenConfirmAsync(friend.Id);
            };
            
            itemGrid.GestureRecognizers.Add(itemTap);
            
            content.Children.Add(itemGrid);
        }

        // 默认收起
        content.IsVisible = false;

        // 使用容器裁剪展开动画
        var contentContainer = new Grid
        {
            IsClippedToBounds = true,
            HeightRequest = 0
        };

        contentContainer.Children.Add(content);

        bool isExpanded = false;
        bool isAnimating = false;

        var tap = new TapGestureRecognizer();

        tap.Tapped += async (sender, e) =>
        {
            if (isAnimating)
                return;

            isAnimating = true;

            try
            {
                isExpanded = !isExpanded;

                if (isExpanded)
                {
                    content.IsVisible = true;

                    // 测量展开后的实际高度
                    double width = contentContainer.Width;

                    if (width <= 0)
                        width = GroupList.Width;

                    if (width <= 0)
                        width = 300;

                    double targetHeight = content
                        .Measure(width, double.PositiveInfinity)
                        .Height;

                    await Task.WhenAll(
                        AnimateHeight(contentContainer, 0, targetHeight),
                        arrow.RotateTo(90, 200, Easing.CubicOut)
                    );

                    // 展开后恢复自动高度
                    contentContainer.HeightRequest = -1;
                }
                else
                {
                    double currentHeight = contentContainer.Height;

                    contentContainer.HeightRequest = currentHeight;

                    await Task.WhenAll(
                        AnimateHeight(contentContainer, currentHeight, 0),
                        arrow.RotateTo(0, 200, Easing.CubicOut)
                    );

                    content.IsVisible = false;
                }
            }
            finally
            {
                isAnimating = false;
            }
        };

        headerGrid.GestureRecognizers.Add(tap);

        groupLayout.Children.Add(headerGrid);
        groupLayout.Children.Add(contentContainer);

        GroupList.Children.Add(groupLayout);
    }
    
    
}
    private Task AnimateHeight(
        VisualElement element,
        double from,
        double to)
    {
        var tcs = new TaskCompletionSource<bool>();

        var animation = new Animation(
            value => element.HeightRequest = value,
            from,
            to,
            Easing.CubicOut
        );

        animation.Commit(
            element,
            "ExpandCollapse",
            rate: 16,
            length: 200,
            finished: (value, cancelled) =>
            {
                element.HeightRequest = to;
                tcs.TrySetResult(true);
            }
        );

        return tcs.Task;
    }

    private void SuggestItem_Tapped(
        object sender,
        TappedEventArgs e)
    {
        SuggestPopup.IsVisible = false;
        SuggestDismissLayer.IsVisible = false;
    }
    
    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        SuggestPopup.IsVisible = false;
        SuggestDismissLayer.IsVisible = false;
        SearchEntry.Text = string.Empty;
    }


    private void HideSuggest_Tapped(object sender, TappedEventArgs e)
    {
        SuggestPopup.IsVisible = false;
        SuggestDismissLayer.IsVisible = false;
    }
}



