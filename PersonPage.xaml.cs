
using AzurChat.Services;

namespace AzurChat.Pages;

public partial class PersonPage : ContentPage
{
    private readonly IAuthService _authService;

    private bool _avatarOptionsExpanded;
    private bool _avatarOptionsAnimating;
    private readonly IServiceProvider _serviceProvider;

    public PersonPage(
        IAuthService authService,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();

        _authService = authService;
        _serviceProvider = serviceProvider;
    }

    // 页面显示时刷新用户信息
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _authService.RefreshCurrentUserAsync();
        LoadUserInfo();
    }

    // 加载用户资料
    private void LoadUserInfo()
    {
        var user = _authService.CurrentUser;

        if (user == null)
            return;

        // 昵称
        name.Text = user.Nickname;

        // 等级
        rank.Text = $"Lv.{user.Level}";

        // 个性签名
        lineEdit.Text = string.IsNullOrWhiteSpace(user.Signature)
            ? "指挥官，请写下你的誓言吧！"
            : user.Signature;

        lineEdit.TextColor = string.IsNullOrWhiteSpace(user.Signature)
            ? Colors.Gray
            : Color.FromArgb("#2F3542");

        // 头像
        if (!string.IsNullOrWhiteSpace(user.Avatar))
        {
            head.Source = ImageSource.FromFile(user.Avatar);
        }

        // 生日
        BirthdayLabel.Text =
            user.Birthday?.ToString("yyyy-MM-dd") ?? "未设置";

        // 地区
        RegionLabel.Text = string.IsNullOrWhiteSpace(user.Region)
            ? "未设置"
            : user.Region;
    }

    // 返回
    private async void BackButton_Clicked(
        object sender,
        TappedEventArgs e)
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            await Navigation.PopAsync();
        }
    }

    // 保留原来的个人资料入口
    private async void OnPersoncordClicked(
        object sender,
        TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("personrecord");
    }

    // 使用原来的编辑页面
    private async Task OpenEditAsync(string field, string value)
    {
        await Shell.Current.GoToAsync(
            "editprofile",
            new Dictionary<string, object>
            {
                ["field"] = field,
                ["value"] = value
            });
    }

    // 修改昵称
    private async void OnNameClicked(
        object sender,
        TappedEventArgs e)
    {
        await OpenEditAsync(
            "nickname",
            _authService.CurrentUser?.Nickname ?? "");
    }

    // 修改签名
    private async void OnSignatureClicked(
        object sender,
        TappedEventArgs e)
    {
        await OpenEditAsync(
            "signature",
            _authService.CurrentUser?.Signature ?? "");
    }

    // 修改生日
    private async void OnBirthdayClicked(
        object sender,
        TappedEventArgs e)
    {
        await OpenEditAsync(
            "birthday",
            _authService.CurrentUser?.Birthday?
                .ToString("yyyy-MM-dd") ?? "");
    }

    // 修改地区
    private async void OnRegionClicked(
        object sender,
        TappedEventArgs e)
    {
        await OpenEditAsync(
            "region",
            _authService.CurrentUser?.Region ?? "");
    }

    // 点击头像，展开或收起选项
    private async void OnAvatarTapped(
        object sender,
        TappedEventArgs e)
    {
        if (_avatarOptionsAnimating)
            return;

        _avatarOptionsAnimating = true;

        try
        {
            const double expandedHeight = 97;

            bool expanding = !_avatarOptionsExpanded;

            if (expanding)
            {
                AvatarOptions.HeightRequest = 0;
                AvatarOptions.Opacity = 0;
                AvatarOptions.IsVisible = true;
            }

            double start = expanding
                ? 0
                : expandedHeight;

            double end = expanding
                ? expandedHeight
                : 0;

            var completion =
                new TaskCompletionSource<bool>();

            var animation = new Animation(
                v =>
                {
                    AvatarOptions.HeightRequest = v;
                    AvatarOptions.Opacity =
                        Math.Clamp(v / expandedHeight, 0, 1);
                },
                start,
                end);

            animation.Commit(
                AvatarOptions,
                "AvatarExpandAnimation",
                16,
                250,
                expanding
                    ? Easing.CubicOut
                    : Easing.CubicIn,
                (value, cancelled) =>
                {
                    completion.TrySetResult(true);
                });

            await completion.Task;

            if (!expanding)
            {
                AvatarOptions.IsVisible = false;
                AvatarOptions.HeightRequest = 0;
            }

            _avatarOptionsExpanded = expanding;
        }
        finally
        {
            _avatarOptionsAnimating = false;
        }
    }

    // 从相册选择
    private async void SelectPhoto_Tapped(
        object sender,
        TappedEventArgs e)
    {
        var page = _serviceProvider.GetRequiredService<PhotoPickerPage>();

        await Navigation.PushAsync(page);
    }
// 拍摄照片
    private async void TakePhoto_Tapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlert("提示", "当前设备不支持拍照", "确定");
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync();

            if (photo == null)
                return;

            var cropPage = new AvatarCropPage(photo, _authService);

            await Navigation.PushAsync(cropPage);
        }
        catch (Exception ex)
        {
            await DisplayAlert("更换头像失败", ex.Message, "确定");
        }
    }

// 保存头像到本地，并更新数据库
    private async Task SaveAvatarAsync(FileResult photo)
    {
        string folder = Path.Combine(
            FileSystem.AppDataDirectory,
            "avatars");

        Directory.CreateDirectory(folder);

        string extension = Path.GetExtension(photo.FileName);

        if (string.IsNullOrWhiteSpace(extension))
            extension = ".jpg";

        string fileName = $"{Guid.NewGuid():N}{extension}";

        string filePath = Path.Combine(folder, fileName);

        await using (var source = await photo.OpenReadAsync())
        await using (var destination = File.Create(filePath))
        {
            await source.CopyToAsync(destination);
        }

        // 写入数据库
        try
        {
            await _authService.UpdateAvatarAsync(filePath);
        }
        catch
        {
            File.Delete(filePath);
            throw;
        }

        // 更新页面头像
        head.Source = ImageSource.FromFile(filePath);
    }
}
