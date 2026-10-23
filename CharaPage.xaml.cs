using Microsoft.Maui.Controls.Shapes;

namespace AzurChat;

public partial class CharaPage : ContentPage
{
    public CharaPage()
    {
        InitializeComponent();

        // 64 × 64 的圆形头像
        AvatarImage.Clip = new EllipseGeometry
        {
            Center = new Point(32, 32),
            RadiusX = 32,
            RadiusY = 32
        };
    }

    // 设置好友昵称和头像
    public void SetCharaInfo(string name, string avatar)
    {
        NameLabel.Text = name;

        if (!string.IsNullOrWhiteSpace(avatar))
        {
            AvatarImage.Source = avatar;
        }
    }

    // 回忆往昔
    private async void MemButton_Tapped(
        object sender,
        TappedEventArgs e)
    {
        await DisplayAlert("提示", "等等吧", "确定");
    }

    // 设为置顶
    private async void TopButton_Tapped(
        object sender,
        TappedEventArgs e)
    {
        await DisplayAlert("提示", "等等吧", "确定");
    }

    // 删除回忆
    private async void DelButton_Tapped(
        object sender,
        TappedEventArgs e)
    {
        await DisplayAlert("提示", "等等吧", "确定");
    }
}