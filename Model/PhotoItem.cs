using Microsoft.Maui.Controls;

namespace AzurChat.Model;

public class PhotoItem
{
    // Android 图片的 URI，用于读取原图
    public string Uri { get; set; } = "";

    // 用于在相册列表中显示图片
    public ImageSource? Thumbnail { get; set; }
}