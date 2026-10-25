using Microsoft.Extensions.DependencyInjection;

namespace AzurChat;

public partial class SettingPage : ContentPage
{
    private readonly IServiceProvider _serviceProvider;

    public SettingPage(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
    }

    private async void BackButton_Clicked(
        object sender,
        TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void CharaSetting_Tapped(
        object sender,
        TappedEventArgs e)
    {
        await DisplayAlert("舰娘设置", "功能暂未实现", "确定");
    }
    
    private void Feedback_Tapped(object sender, TappedEventArgs e)
    {
        FeedbackPopup.IsVisible = true;
    }
    
    private async void CommanderSetting_Tapped(
        object sender,
        TappedEventArgs e)
    {
        var page = _serviceProvider.GetRequiredService<CommanderSettingPage>();

        await Navigation.PushAsync(page);
    }

    private async void Notification_Tapped(
        object sender, TappedEventArgs e)
    {
        await DisplayAlert("通知", "功能暂未开放", "确定");
    }

    private void OnCloseFeedbackClicked(object sender, EventArgs e)
    {
        FeedbackPopup.IsVisible = false;
    }

    private async void OnQqGroupTapped(object sender, TappedEventArgs e)
    {
        await Clipboard.Default.SetTextAsync("1080606836");
        await DisplayAlert("提示", "QQ群号已复制", "确定");
    }

}