using AzurChat.Data;
using AzurChat.Model;
using Microsoft.EntityFrameworkCore;

namespace AzurChat.Services;

public interface IAuthService
{
    User? CurrentUser { get; }
    Task<string> LoginAsync(string username, string password);
    Task<string> RegisterAsync(
        string username,
        string password,
        string url,
        string api,
        string model);
    Task RefreshCurrentUserAsync();
    Task UpdateSignatureAsync(string signature);
    Task<bool> UpdateAiSettingsAsync(string url, string api);
    Task UpdateNicknameAsync(string nickname);
    Task<string> GetDecryptedApiAsync();
    Task UpdateRegionAsync(string region);
    Task UpdateBirthdayAsync(DateTime birthday);
    Task UpdateAvatarAsync(string avatar);
    bool IsLoggedIn();

    void RestoreLogin();
}

public class AuthService: IAuthService
{
    private readonly UserContext _userContext;
    private readonly EncryptionService _encryptionService;
    public User? CurrentUser {get; private set;}
    
    public AuthService(
        UserContext userContext,
        EncryptionService encryptionService)
    {
        _userContext = userContext;
        _encryptionService = encryptionService;
    }
    /// <summary>
    /// 异步刷新当前登录用户的信息。
    /// 从数据库重新读取用户资料，并更新 CurrentUser。
    /// </summary>
    /// <returns>表示异步刷新操作的任务。</returns> 
    public async Task RefreshCurrentUserAsync()
    {
        int userId = CurrentUser?.Id
                     ?? Preferences.Default.Get("CurrentUserId", 0);

        if (userId == 0)
        {
            CurrentUser = null;
            return;
        }

        CurrentUser = await _userContext.User
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId);
    }
    public async Task<string> GetDecryptedApiAsync()
    {
        if (CurrentUser == null ||
            string.IsNullOrWhiteSpace(CurrentUser.Api))
            return "";

        return await _encryptionService.DecryptAsync(CurrentUser.Api);
    }
    public async Task UpdateAvatarAsync(string avatar)
    {
        if (CurrentUser == null)
            throw new InvalidOperationException("用户未登录");

        var user = await _userContext.User.FindAsync(CurrentUser.Id);

        if (user == null)
            throw new InvalidOperationException("用户不存在");

        user.Avatar = avatar;

        await _userContext.SaveChangesAsync();

        CurrentUser.Avatar = avatar;
    }
    
    public async Task UpdateSignatureAsync(string signature)
    {
        if (CurrentUser == null)
            return;

        var user = await _userContext.User
            .FindAsync(CurrentUser.Id);

        if (user == null)
            return;

        user.Signature = signature;
        await _userContext.SaveChangesAsync();

        CurrentUser.Signature = signature;
    }
    public async Task<bool> UpdateAiSettingsAsync(string url, string api)
    {
        if (CurrentUser == null)
            return false;

        var user = await _userContext.User.FindAsync(CurrentUser.Id);

        if (user == null)
            return false;

        // API Key 加密后保存
        string encryptedApi = await _encryptionService.EncryptAsync(api);

        user.Url = url;
        user.Api = encryptedApi;
        user.UpdatedAt = DateTime.Now;

        await _userContext.SaveChangesAsync();

        CurrentUser.Url = url;
        CurrentUser.Api = encryptedApi;
        CurrentUser.UpdatedAt = user.UpdatedAt;

        return true;
    }
    public async Task UpdateNicknameAsync(string nickname)
    {
        if (CurrentUser == null)
            throw new InvalidOperationException("用户未登录");

        var user = await _userContext.User.FindAsync(CurrentUser.Id);

        if (user == null)
            throw new InvalidOperationException("用户不存在");

        user.Nickname = nickname;

        await _userContext.SaveChangesAsync();

        CurrentUser.Nickname = nickname;
    }

    public async Task UpdateRegionAsync(string region)
    {
        if (CurrentUser == null)
            throw new InvalidOperationException("用户未登录");

        var user = await _userContext.User.FindAsync(CurrentUser.Id);

        if (user == null)
            throw new InvalidOperationException("用户不存在");

        user.Region = region;

        await _userContext.SaveChangesAsync();

        CurrentUser.Region = region;
    }

    public async Task UpdateBirthdayAsync(DateTime birthday)
    {
        if (CurrentUser == null)
            throw new InvalidOperationException("用户未登录");

        var user = await _userContext.User.FindAsync(CurrentUser.Id);

        if (user == null)
            throw new InvalidOperationException("用户不存在");

        user.Birthday = birthday;

        await _userContext.SaveChangesAsync();

        CurrentUser.Birthday = birthday;
    }
    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private static bool VerifyPassword(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }

    public void RestoreLogin()
    {
        int userId = Preferences.Default.Get("CurrentUserId", 0);
        if (userId == 0)
        {
            CurrentUser = null;
            return;
        }
        CurrentUser = _userContext.User
            .FirstOrDefault(x => x.Id == userId);
        if (CurrentUser == null)
        {
            Preferences.Default.Remove("CurrentUserId");
        }
    }
    public async Task<string> RegisterAsync(
        string username,
        string password,
        string url,
        string api,
        string model)
    {
        // 检查用户名是否存在
        var exists = await _userContext.User
            .AnyAsync(x => x.Username == username);

        if (exists)
            return "exist";

        var now = DateTime.Now;
        var encryptedApi =
            await _encryptionService.EncryptAsync(api);
        var user = new User
        {
            Username = username,
            Password = HashPassword(password),

            Nickname = username,
            Avatar = "head.jpg",
            Birthday = new DateTime(2000, 1, 1),
            Region = "",
            Signature = "",

            Level = 1,
            Exp = 0,

            Url = url,
            Api = encryptedApi,
            Model = model,

            CreatedAt = now,
            UpdatedAt = now,
            LastLogin = now
        };

        _userContext.User.Add(user);

        await _userContext.SaveChangesAsync();

        return "created";
    }
    public async Task<string> LoginAsync(
        string username,
        string password)
    {
        var user = await _userContext.User
            .FirstOrDefaultAsync(x => x.Username == username);
        
        if (user == null)
            return "not_exist";
        
        if (!VerifyPassword(password, user.Password))
            return "wrong_password";

        // 登录成功
        user.LastLogin = DateTime.Now;

        await _userContext.SaveChangesAsync();

        // 保存当前登录用户
        CurrentUser = user;

        // 保存登录状态，下次启动 RestoreLogin() 会使用
        Preferences.Default.Set("CurrentUserId", user.Id);

        return "success";
    }
    public bool IsLoggedIn()
    {
        return CurrentUser != null;
    }
    
    
}