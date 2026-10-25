
using AzurChat.Services;

namespace AzurChat;

public partial class CommanderSettingPage : ContentPage
{
    private readonly IAuthService _authService;

    private bool _urlExpanded;
    private bool _apiExpanded;
    private bool _animating;

    public CommanderSettingPage(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    // 进入页面时读取数据库
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await _authService.RefreshCurrentUserAsync();

            var user = _authService.CurrentUser;

            if (user == null)
                return;

            UrlEntry.Text = user.Url ?? "";

            // API 在数据库中是加密存储的
            ApiEntry.Text = await _authService.GetDecryptedApiAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", ex.Message, "确定");
        }
    }

    // URL 展开或收起
    private async void UrlSetting_Tapped(
        object sender,
        TappedEventArgs e)
    {
        if (_animating)
            return;

        bool expand = !_urlExpanded;

        await AnimatePanelAsync(
            UrlPanel,
            UrlArrow,
            expand);

        _urlExpanded = expand;
    }

    // API 展开或收起
    private async void ApiSetting_Tapped(
        object sender,
        TappedEventArgs e)
    {
        if (_animating)
            return;

        bool expand = !_apiExpanded;

        await AnimatePanelAsync(
            ApiPanel,
            ApiArrow,
            expand);

        _apiExpanded = expand;
    }

    // 展开和收起动画
    private async Task AnimatePanelAsync(
        Grid panel,
        Label arrow,
        bool expand)
    {
        if (_animating)
            return;

        _animating = true;

        try
        {
            double width = Width > 56 ? Width - 56 : 300;

            // 暂时取消高度限制，获取实际内容高度
            panel.HeightRequest = -1;
            panel.IsVisible = true;

            double height = panel.Measure(
                width,
                double.PositiveInfinity).Height;

            if (height <= 0)
                height = 110;

            // 设置动画起始状态
            panel.HeightRequest = expand ? 0 : height;
            panel.Opacity = expand ? 0 : 1;

            if (expand)
                panel.InputTransparent = false;

            var completion = new TaskCompletionSource<bool>();

            var animation = new Animation(
                value =>
                {
                    panel.HeightRequest = value;
                    panel.Opacity = Math.Clamp(
                        value / height, 0, 1);
                },
                expand ? 0 : height,
                expand ? height : 0);

            animation.Commit(
                panel,
                "ExpandAnimation",
                16,
                250,
                expand ? Easing.CubicOut : Easing.CubicIn,
                (value, cancelled) =>
                {
                    completion.TrySetResult(true);
                });

            await completion.Task;

            await arrow.RotateTo(
                expand ? 180 : 0,
                150);

            if (expand)
            {
                panel.HeightRequest = -1;
                panel.Opacity = 1;
            }
            else
            {
                panel.IsVisible = false;
                panel.InputTransparent = true;
                panel.HeightRequest = 0;
                panel.Opacity = 0;
            }
        }
        finally
        {
            _animating = false;
        }
    }
    // 保存 URL
    private async void SaveUrl_Clicked(
        object sender,
        EventArgs e)
    {
        string url = UrlEntry.Text?.Trim() ?? "";
        string api = ApiEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(url))
        {
            await DisplayAlert("提示", "URL 不能为空", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(api))
        {
            await DisplayAlert("提示", "API Key 不能为空", "确定");
            return;
        }

        try
        {
            bool success =
                await _authService.UpdateAiSettingsAsync(url, api);

            if (!success)
            {
                await DisplayAlert("失败", "保存失败", "确定");
                return;
            }

            await AnimatePanelAsync(
                UrlPanel,
                UrlArrow,
                false);

            _urlExpanded = false;

            await DisplayAlert("成功", "URL 已保存", "确定");
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", ex.Message, "确定");
        }
    }

    // 保存 API
    private async void SaveApi_Clicked(
        object sender,
        EventArgs e)
    {
        string url = UrlEntry.Text?.Trim() ?? "";
        string api = ApiEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(api))
        {
            await DisplayAlert("提示", "API Key 不能为空", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            await DisplayAlert("提示", "URL 不能为空", "确定");
            return;
        }

        try
        {
            bool success =
                await _authService.UpdateAiSettingsAsync(url, api);

            if (!success)
            {
                await DisplayAlert("失败", "保存失败", "确定");
                return;
            }

            await AnimatePanelAsync(
                ApiPanel,
                ApiArrow,
                false);

            _apiExpanded = false;

            await DisplayAlert("成功", "API Key 已保存", "确定");
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", ex.Message, "确定");
        }
    }

    // 返回
    private async void BackButton_Clicked(
        object sender,
        TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }
}
