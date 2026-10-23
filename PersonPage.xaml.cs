using AzurChat.Services;

namespace AzurChat.Pages;

public partial class PersonPage : ContentPage
{
    private readonly IAuthService _authService;

    public PersonPage(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    /// <summary>
    /// 页面显示时加载用户信息
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _authService.RefreshCurrentUserAsync();
        LoadUserInfo();
    }

    /// <summary>
    /// 显示当前登录用户的资料
    /// </summary>
    private void LoadUserInfo()
    {
        var user = _authService.CurrentUser;
        if (user == null) return;

        name.Text = user.Nickname;
        rank.Text = $"Lv.{user.Level}";
        lineEdit.Text = string.IsNullOrWhiteSpace(user.Signature)
            ? "指挥官，请写下你的誓言吧！"
            : user.Signature;

        lineEdit.TextColor = string.IsNullOrWhiteSpace(user.Signature)
            ? Colors.Gray
            : Color.FromArgb("#2F3542");

        if (!string.IsNullOrWhiteSpace(user.Avatar))
        {
            head.Source = ImageSource.FromFile(user.Avatar);
        }
    }
    
    private async void OnPersoncordClicked(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("personrecord");
    }

    private async void OnSignatureClicked(
        object sender,
        TappedEventArgs e)
    {
        await Navigation.PushAsync(
            new ProfilePage(_authService, "signature")
        );
    }
}