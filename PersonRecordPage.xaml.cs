using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzurChat.Services;

namespace AzurChat.Pages;

public partial class PersonRecordPage : ContentPage
{
    private readonly IAuthService _authService;

    public PersonRecordPage(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _authService.RefreshCurrentUserAsync();

        var user = _authService.CurrentUser;
        if (user == null) return;

        name.Text = user.Nickname;
        line.Text = user.Signature;
        region.Text = user.Region;
        level.Text = $"Lv.{user.Level}";
        birthday.Text = user.Birthday?.ToString("yyyy-MM-dd") ?? "";

        if (!string.IsNullOrWhiteSpace(user.Avatar))
            avatar.Source = ImageSource.FromFile(user.Avatar);
    }
    
    private async Task OpenEditAsync(string field, string value)
    {
        await Shell.Current.GoToAsync("editprofile",
            new Dictionary<string, object>
            {
                ["field"] = field,
                ["value"] = value
            });
    }

    private async void OnNameClicked(object sender, TappedEventArgs e)
    {
        await OpenEditAsync("nickname", name.Text ?? "");
    }

    private async void OnSignatureClicked(object sender, TappedEventArgs e)
    {
        await OpenEditAsync("signature", line.Text ?? "");
    }

    private async void OnRegionClicked(object sender, TappedEventArgs e)
    {
        await OpenEditAsync("region", region.Text ?? "");
    }

    private async void OnBirthdayClicked(object sender, TappedEventArgs e)
    {
        await OpenEditAsync("birthday", birthday.Text ?? "");
    }
}