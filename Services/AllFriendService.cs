using System.Text.Json;
using AzurChat.Data;
using AzurChat.Model;
using Microsoft.EntityFrameworkCore;

namespace AzurChat.Services;

public class AllFriendService
{
    private readonly UserContext _context;

    public AllFriendService(UserContext context)
    {
        _context = context;
    }

    public async Task SyncFromJsonAsync()
    {
        // 1. 读取新版 info.json
        await using var stream =
            await FileSystem.OpenAppPackageFileAsync("info.json");

        var jsonFriends =
            await JsonSerializer.DeserializeAsync<List<AllFriend>>(stream)
            ?? new List<AllFriend>();

        // 2. 读取数据库中旧的 all_friends
        var dbFriends =
            await _context.AllFriend.ToListAsync();

        // 3. 删除整张表的旧数据
        _context.AllFriend.RemoveRange(dbFriends);

        // 4. 加入新版 JSON 的全部数据
        _context.AllFriend.AddRange(jsonFriends);

        // 5. 保存
        await _context.SaveChangesAsync();
    }
}