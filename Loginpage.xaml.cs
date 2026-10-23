using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzurChat.Services;

namespace AzurChat;

public partial class LoginPage : ContentPage
{
    private readonly IAuthService _authService;
    public LoginPage(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }
    
    private void OnMenuClicked(object sender, EventArgs e)
    {
        MenuPopup.IsVisible = true;
    }

    private void OnMenuBackgroundTapped(object sender, TappedEventArgs e)
    {
        MenuPopup.IsVisible = false;
    }

    private async void OnAboutClicked(object sender, EventArgs e)
    {
        MenuPopup.IsVisible = false;

        await DisplayAlert(
            "关于",
            "AzurChat v1.0\n\n基于 .NET MAUI 开发",
            "确定"
        );
    }
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var username = UserEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert(
                "提示",
                "用户名或密码不能为空",
                "确定");

            return;
        }

        if (username.Length < 8 || password.Length < 8)
        {
            await DisplayAlert(
                "提示",
                "用户名或密码长度不能少于8位",
                "确定");

            return;
        }

        var result = await _authService.LoginAsync(
            username,
            password);

        if (result == "not_exist")
        {
            await DisplayAlert(
                "提示",
                "用户不存在",
                "确定");

            return;
        }

        if (result == "wrong_password")
        {
            await DisplayAlert(
                "提示",
                "密码错误",
                "确定");

            return;
        }

        if (result == "success")
        {
            await Shell.Current.GoToAsync("//main");
        }
    }
    private void OnExitClicked(object sender, EventArgs e)
    {
        Application.Current?.Quit();
    }
    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(
            new RegisterPage(_authService)
        );
    }
    private void OnFeedbackClicked(object sender, EventArgs e)
    {
        FeedbackPopup.IsVisible = true;
    }

    private void OnCloseFeedbackClicked(object sender, EventArgs e)
    {
        FeedbackPopup.IsVisible = false;
    }

    private async void OnQqGroupTapped(object sender, TappedEventArgs e)
    {
        await Launcher.Default.OpenAsync(
            new Uri("https://qm.qq.com/q/bvGQ9RxsYw")
        );
    }
}