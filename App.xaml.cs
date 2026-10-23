using AzurChat.Data;
using AzurChat.Services;

namespace AzurChat;

public partial class App : Application
{
    private readonly IAuthService _authService;
    private readonly UserContext _userContext;
    private readonly AppUpgradeService _appUpgradeService;

    public App(
        IAuthService authService,
        UserContext userContext,
        AppUpgradeService appUpgradeService)
    {
        InitializeComponent();

        _authService = authService;
        _userContext = userContext;
        _appUpgradeService = appUpgradeService;
    }
    
    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Window 创建的时候必须已经有 Page
        var loadingPage = new ContentPage
        {
            Content = new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                Spacing = 15,

                Children =
                {
                    new ActivityIndicator
                    {
                        IsRunning = true
                    },

                    new Label
                    {
                        Text = "正在初始化..."
                    }
                }
            }
        };

        var window = new Window(loadingPage);

        window.Created += async (_, _) =>
        {
            try
            {
                // 1. 确保数据库存在
                await _userContext.Database.EnsureCreatedAsync();

                // 2. 首次安装 / APK升级
                await _appUpgradeService.RunAsync();

                // 3. 恢复登录
                _authService.RestoreLogin();

                // 4. 初始化完成，进入正式界面
                window.Page = new AppShell(_authService);
            }
            catch (Exception ex)
            {
                // 初始化失败
                window.Page = new ContentPage
                {
                    Content = new VerticalStackLayout
                    {
                        Padding = 30,
                        Spacing = 20,
                        VerticalOptions = LayoutOptions.Center,

                        Children =
                        {
                            new Label
                            {
                                Text = "应用初始化失败",
                                FontSize = 22,
                                HorizontalOptions = LayoutOptions.Center
                            },

                            new Label
                            {
                                Text = ex.Message,
                                HorizontalOptions = LayoutOptions.Center
                            }
                        }
                    }
                };
            }
        };

        return window;
    }
}