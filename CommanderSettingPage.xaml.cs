
using AzurChat.Data;
using AzurChat.Services;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AzurChat;

public partial class CommanderSettingPage : ContentPage
{
    private readonly IAuthService _authService;
    private readonly IDbContextFactory<UserContext> _dbFactory;

    private bool _urlExpanded;
    private bool _apiExpanded;
    private bool _modelExpanded;
    private bool _animating;
    private bool _modelBusy;

    // 当前界面选择的模型
    private string _selectedModel = "";

    // 数据库中已保存的模型
    private string _savedModel = "";

    // API 返回的模型列表
    private List<string> _models = new();

    public CommanderSettingPage(
        IAuthService authService,
        IDbContextFactory<UserContext> dbFactory)
    {
        InitializeComponent();

        _authService = authService;
        _dbFactory = dbFactory;
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

            // API Key 在数据库中加密保存
            ApiEntry.Text =
                await _authService.GetDecryptedApiAsync();

            // 加载当前模型
            _savedModel = user.Model ?? "";
            _selectedModel = _savedModel;

            UpdateCurrentModelLabel();
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "错误",
                ex.Message,
                "确定");
        }
    }

    // 更新标题右侧的当前模型名称
    private void UpdateCurrentModelLabel()
    {
        CurrentModelLabel.Text =
            string.IsNullOrWhiteSpace(_selectedModel)
                ? "未选择"
                : _selectedModel;
    }

    // URL 展开或收起
    private async void UrlSetting_Tapped(
        object sender,
        TappedEventArgs e)
    {
        if (_animating || _modelBusy)
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
        if (_animating || _modelBusy)
            return;

        bool expand = !_apiExpanded;

        await AnimatePanelAsync(
            ApiPanel,
            ApiArrow,
            expand);

        _apiExpanded = expand;
    }

    // 通用展开和收起动画
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
            double width =
                Width > 56 ? Width - 56 : 300;

            panel.HeightRequest = -1;
            panel.IsVisible = true;

            double height = panel.Measure(
                width,
                double.PositiveInfinity).Height;

            if (height <= 0)
                height = 110;

            panel.HeightRequest = expand ? 0 : height;
            panel.Opacity = expand ? 0 : 1;

            if (expand)
                panel.InputTransparent = false;

            var completion =
                new TaskCompletionSource<bool>();

            var animation = new Animation(
                value =>
                {
                    panel.HeightRequest = value;

                    panel.Opacity = Math.Clamp(
                        value / height,
                        0,
                        1);
                },
                expand ? 0 : height,
                expand ? height : 0);

            animation.Commit(
                panel,
                "ExpandAnimation",
                16,
                250,
                expand
                    ? Easing.CubicOut
                    : Easing.CubicIn,
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
            await DisplayAlert(
                "提示",
                "URL 不能为空",
                "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(api))
        {
            await DisplayAlert(
                "提示",
                "API Key 不能为空",
                "确定");
            return;
        }

        try
        {
            bool success =
                await _authService.UpdateAiSettingsAsync(
                    url,
                    api);

            if (!success)
            {
                await DisplayAlert(
                    "失败",
                    "保存失败",
                    "确定");
                return;
            }

            await AnimatePanelAsync(
                UrlPanel,
                UrlArrow,
                false);

            _urlExpanded = false;

            await DisplayAlert(
                "成功",
                "URL 已保存",
                "确定");
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "错误",
                ex.Message,
                "确定");
        }
    }

    // 保存 API Key
    private async void SaveApi_Clicked(
        object sender,
        EventArgs e)
    {
        string url = UrlEntry.Text?.Trim() ?? "";
        string api = ApiEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(api))
        {
            await DisplayAlert(
                "提示",
                "API Key 不能为空",
                "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            await DisplayAlert(
                "提示",
                "URL 不能为空",
                "确定");
            return;
        }

        try
        {
            bool success =
                await _authService.UpdateAiSettingsAsync(
                    url,
                    api);

            if (!success)
            {
                await DisplayAlert(
                    "失败",
                    "保存失败",
                    "确定");
                return;
            }

            await AnimatePanelAsync(
                ApiPanel,
                ApiArrow,
                false);

            _apiExpanded = false;

            await DisplayAlert(
                "成功",
                "API Key 已保存",
                "确定");
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "错误",
                ex.Message,
                "确定");
        }
    }

    // 从 API 获取所有可用模型
    private async Task<List<string>> GetModelsAsync(
        string api,
        string baseUrl)
    {
        using var client = new HttpClient();

        client.Timeout = TimeSpan.FromSeconds(15);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                api);

        string url =
            baseUrl.TrimEnd('/') + "/models";

        using var response =
            await client.GetAsync(url);

        response.EnsureSuccessStatusCode();

        string json =
            await response.Content.ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        return document.RootElement
            .GetProperty("data")
            .EnumerateArray()
            .Select(x =>
                x.GetProperty("id").GetString())
            .Where(x =>
                !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct()
            .ToList();
    }

    // 从 SQLite 获取当前用户保存的模型
    private async Task<string> GetSavedModelAsync()
    {
        string username =
            _authService.CurrentUser?.Username ?? "";

        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException(
                "用户未登录");

        await using var db =
            await _dbFactory.CreateDbContextAsync();

        var user = await db.User
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Username == username);

        if (user == null)
            throw new InvalidOperationException(
                "用户不存在");

        return user.Model ?? "";
    }

    // 根据当前选择绘制模型列表及对勾
    private void RenderModelList()
    {
        ModelListContainer.Children.Clear();

        foreach (string model in _models)
        {
            bool isSelected =
                model == _selectedModel;

            var nameLabel = new Label
            {
                Text = model,
                FontSize = 14,
                TextColor =
                    Color.FromArgb("#222222"),
                VerticalOptions =
                    LayoutOptions.Center,
                LineBreakMode =
                    LineBreakMode.WordWrap
            };

            var checkLabel = new Label
            {
                Text = isSelected ? "✓" : "",
                FontSize = 20,
                FontAttributes =
                    FontAttributes.Bold,
                TextColor =
                    Color.FromArgb("#365A9D"),
                HorizontalOptions =
                    LayoutOptions.Center,
                VerticalOptions =
                    LayoutOptions.Center
            };

            var row = new Grid
            {
                Padding = new Thickness(12, 10),
                ColumnDefinitions =
                {
                    new ColumnDefinition(
                        GridLength.Star),

                    new ColumnDefinition(
                        new GridLength(30))
                },
                BackgroundColor = isSelected
                    ? Color.FromArgb("#EEF3FF")
                    : Colors.Transparent
            };

            row.Add(nameLabel, 0, 0);
            row.Add(checkLabel, 1, 0);

            string selectedValue = model;

            var tap = new TapGestureRecognizer();

            tap.Tapped += (sender, args) =>
            {
                if (_modelBusy || _animating)
                    return;

                // 切换当前选中模型
                _selectedModel = selectedValue;

                // 更新标题
                UpdateCurrentModelLabel();

                // 立即刷新对勾
                RenderModelList();
            };

            row.GestureRecognizers.Add(tap);

            ModelListContainer.Children.Add(row);

            ModelListContainer.Children.Add(
                new BoxView
                {
                    HeightRequest = 1,
                    BackgroundColor =
                        Color.FromArgb("#F0F0F0")
                });
        }
    }

    // 点击 AI 模型标题：获取列表、展开或保存收起
    private async void ModelSetting_Tapped(
        object sender,
        TappedEventArgs e)
    {
        if (_animating || _modelBusy)
            return;

        _modelBusy = true;

        try
        {
            // 已展开：先保存，再收起
            if (_modelExpanded)
            {
                bool success =
                    await SaveSelectedModelAsync();

                if (!success)
                    return;

                await AnimatePanelAsync(
                    ModelPanel,
                    ModelArrow,
                    false);

                _modelExpanded = false;
                return;
            }

            string url =
                UrlEntry.Text?.Trim() ?? "";

            string api =
                ApiEntry.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(url) ||
                string.IsNullOrWhiteSpace(api))
            {
                await DisplayAlert(
                    "提示",
                    "请先填写 URL 和 API Key",
                    "确定");
                return;
            }

            // 并行获取模型列表和数据库当前模型
            Task<List<string>> modelsTask =
                GetModelsAsync(api, url);

            Task<string> savedModelTask =
                GetSavedModelAsync();

            await Task.WhenAll(
                modelsTask,
                savedModelTask);

            _models = await modelsTask;
            _savedModel = await savedModelTask;
            _selectedModel = _savedModel;

            if (_models.Count == 0)
            {
                await DisplayAlert(
                    "提示",
                    "API 未返回任何模型",
                    "确定");
                return;
            }

            // 数据库中的模型不在新列表时仍保留显示
            if (!string.IsNullOrWhiteSpace(
                    _selectedModel) &&
                !_models.Contains(_selectedModel))
            {
                _models.Insert(
                    0,
                    _selectedModel);
            }

            UpdateCurrentModelLabel();

            RenderModelList();

            await AnimatePanelAsync(
                ModelPanel,
                ModelArrow,
                true);

            _modelExpanded = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "获取模型失败",
                ex.Message,
                "确定");
        }
        finally
        {
            _modelBusy = false;
        }
    }

    // 将选中的模型保存到 SQLite
    private async Task<bool> SaveSelectedModelAsync()
    {
        // 没选择或没有修改，不需要保存
        if (string.IsNullOrWhiteSpace(
                _selectedModel) ||
            _selectedModel == _savedModel)
        {
            return true;
        }

        try
        {
            string username =
                _authService.CurrentUser?.Username ?? "";

            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException(
                    "用户未登录");

            await using var db =
                await _dbFactory.CreateDbContextAsync();

            var user = await db.User
                .FirstOrDefaultAsync(
                    u => u.Username == username);

            if (user == null)
                throw new InvalidOperationException(
                    "用户不存在");

            user.Model = _selectedModel;

            await db.SaveChangesAsync();

            _savedModel = _selectedModel;

            await _authService.RefreshCurrentUserAsync();

            return true;
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "模型保存失败",
                ex.Message,
                "确定");

            return false;
        }
    }

    // 返回：先保存模型，再返回
    private async void BackButton_Clicked(
        object sender,
        TappedEventArgs e)
    {
        if (_modelBusy || _animating)
            return;

        _modelBusy = true;

        try
        {
            bool success =
                await SaveSelectedModelAsync();

            if (!success)
                return;

            await Navigation.PopAsync();
        }
        finally
        {
            _modelBusy = false;
        }
    }
}
