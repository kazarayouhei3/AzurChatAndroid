namespace AzurChat.Services;

public class AppUpgradeService
{
    private readonly AllFriendService _allFriendService;

    public AppUpgradeService(AllFriendService allFriendService)
    {
        _allFriendService = allFriendService;
    }

    public async Task RunAsync()
    {
        // 1. 获取当前 APK 的版本号
        var currentVersion = int.Parse(AppInfo.Current.BuildString);

        // 2. 获取上一次已经处理过的版本号
        var lastVersion = Preferences.Get("LastAppVersion", 0);

        // 3. 当前版本没有提高，不需要执行升级任务
        if (currentVersion <= lastVersion)
            return;

        // 4. 新版本：执行角色数据更新
        await _allFriendService.SyncFromJsonAsync();

        // 5. 所有升级任务成功后，记录当前版本
        Preferences.Set("LastAppVersion", currentVersion);
    }
}