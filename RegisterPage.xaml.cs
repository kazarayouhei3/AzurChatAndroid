using AzurChat.Services;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AzurChat;

public partial class RegisterPage : ContentPage
{
    private readonly IAuthService _authService;

    public RegisterPage(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        var username = UserEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text?.Trim() ?? "";
        var url = UrlEntry.Text?.Trim() ?? "";
        var api = ApiEntry.Text?.Trim() ?? "";
        var model = ModelPicker.SelectedItem?.ToString() ?? "";

        // 每次点击注册，先隐藏上一次提示
        MessageLabel.IsVisible = false;

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            MessageLabel.Text = "用户名或密码不能为空";
            MessageLabel.IsVisible = true;
            return;
        }

        if (username.Length < 8 || password.Length < 8)
        {
            MessageLabel.Text = "用户名或密码长度不能少于8位";
            MessageLabel.IsVisible = true;
            return;
        }
        if (string.IsNullOrWhiteSpace(url) ||
            string.IsNullOrWhiteSpace(api))
        {
            MessageLabel.Text = "API或URL不能为空";
            MessageLabel.IsVisible = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            MessageLabel.Text = "请先选择模型";
            MessageLabel.IsVisible = true;
            return;
        }

        var result = await _authService.RegisterAsync(
            username,
            password,
            url,
            api,
            model);

        if (result == "created")
        {
            MessageLabel.Text = "注册成功";
            MessageLabel.IsVisible = true;

            await Task.Delay(1000);

            await Navigation.PopAsync();
        }
        else if (result == "exist")
        {
            MessageLabel.Text = "用户名已存在";
            MessageLabel.IsVisible = true;
        }

        UserEntry.Text = "";
        PasswordEntry.Text = "";
    }
    private async Task<List<string>?> GetModelsAsync(
        string api,
        string baseUrl)
    {
        try
        {
            using var client = new HttpClient();

            client.Timeout = TimeSpan.FromSeconds(10);

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", api);

            var url = baseUrl.TrimEnd('/') + "/models";

            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(json);

            var models = document.RootElement
                .GetProperty("data")
                .EnumerateArray()
                .Select(x => x.GetProperty("id").GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList();

            return models;
        }
        catch
        {
            return null;
        }
    }
    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
    private async void OnModelPickerFocused(object sender, FocusEventArgs e)
    {
        var url = UrlEntry.Text?.Trim() ?? "";
        var api = ApiEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(url) ||
            string.IsNullOrWhiteSpace(api))
        {
            await DisplayAlert("提示", "请先填写URL和API", "确定");
            ModelPicker.Unfocus();
            return;
        }

        var models = await GetModelsAsync(api, url);

        if (models == null || models.Count == 0)
        {
            await DisplayAlert("提示", "API或URL错误", "确定");
            ModelPicker.Unfocus();
            return;
        }

        ModelPicker.ItemsSource = models;
    }
}