#if ANDROID
using AzurChat.Platforms.Android;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.Content;
#endif
using AzurChat.Services;
using AzurChat.Model;

namespace AzurChat.Pages;

public partial class PhotoPickerPage : ContentPage
{
    private PhotoItem? _selectedPhoto;
    private bool _isOpeningCropPage = false;
    private readonly IAuthService _authService;

    public PhotoPickerPage(IAuthService authService)
    {
        InitializeComponent();

        _authService = authService;
    }

    // 每次进入页面检查权限
    protected override async void OnAppearing()
    {
        base.OnAppearing();

#if ANDROID
        try
        {
            await CheckPhotoPermissionAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "读取相册失败",
                ex.Message,
                "确定");
        }
#endif
    }

#if ANDROID
    // 检查 Android 相册权限
    private async Task CheckPhotoPermissionAsync()
    {
        var context = Platform.AppContext;

        bool fullAccess =
            ContextCompat.CheckSelfPermission(
                context,
                "android.permission.READ_MEDIA_IMAGES")
            == Permission.Granted;

        bool partialAccess =
            Build.VERSION.SdkInt >= BuildVersionCodes.UpsideDownCake &&
            ContextCompat.CheckSelfPermission(
                context,
                "android.permission.READ_MEDIA_VISUAL_USER_SELECTED")
            == Permission.Granted;

        bool hasAccess = fullAccess || partialAccess;

        // 没有权限才显示授权界面
        PermissionView.IsVisible = !hasAccess;
        PhotosView.IsVisible = hasAccess;

        if (!hasAccess)
        {
            PhotosView.ItemsSource = null;
            return;
        }

        await LoadPhotosAsync();
    }

    // 加载照片
    private async Task LoadPhotosAsync()
    {
        var photos = await Task.Run(AndroidPhotoService.GetPhotos);

        PhotosView.ItemsSource = photos;
    }
#endif

    // 点击授权相册
    private async void RequestPermission_Clicked(
        object sender,
        EventArgs e)
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.Photos>();

#if ANDROID
        await CheckPhotoPermissionAsync();

        if (!PhotosView.IsVisible)
        {
            bool goSettings = await DisplayAlert(
                "相册权限",
                "未获得相册权限，是否前往系统设置手动授权？",
                "前往设置",
                "取消");

            if (goSettings)
                AppInfo.ShowSettingsUI();
        }
#endif
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "权限申请失败",
                ex.Message,
                "确定");
        }
    }

    // 前往系统设置
    private void OpenSettings_Tapped(
        object sender,
        TappedEventArgs e)
    {
        AppInfo.ShowSettingsUI();
    }

    // 返回
    private async void Back_Tapped(
        object sender,
        TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }

    // 手机相册
    private async void Album_Clicked(
        object sender,
        EventArgs e)
    {
#if ANDROID
        try
        {
            await CheckPhotoPermissionAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "读取相册失败",
                ex.Message,
                "确定");
        }
#endif
    }

    // 手机文件
    private async void Files_Clicked(
        object sender,
        EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(
                new PickOptions
                {
                    PickerTitle = "选择头像图片",
                    FileTypes = FilePickerFileType.Images
                });

            if (file == null)
                return;

            var cropPage = new AvatarCropPage(file, _authService);

            await Navigation.PushAsync(cropPage);
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "选择文件失败",
                ex.Message,
                "确定");
        }
    }

    // 选择照片
    

    private async void Photos_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isOpeningCropPage)
            return;

        var photo = e.CurrentSelection.FirstOrDefault() as PhotoItem;

        if (photo == null)
            return;

        _isOpeningCropPage = true;

        try
        {
            var cropPage = new AvatarCropPage(photo.Uri, _authService);

            // 第一次点击直接进入裁剪页面
            await Navigation.PushAsync(cropPage);
        }
        catch (Exception ex)
        {
            await DisplayAlert("打开裁剪页面失败", ex.Message, "确定");
        }
        finally
        {
            // 返回后可以再次选择同一张照片
            PhotosView.SelectedItem = null;
            _isOpeningCropPage = false;
        }
    }

    // 下一步
    private async void Next_Clicked(
        object sender,
        EventArgs e)
    {
        if (_selectedPhoto == null)
            return;

        // 下一阶段：读取所选 URI，进入 AvatarCropPage
        await DisplayAlert(
            "已选择照片",
            "下一步进入头像裁剪页面",
            "确定");
    }
}