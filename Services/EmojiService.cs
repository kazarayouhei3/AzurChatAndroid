using System.Text.Json;

namespace AzurChat.Services;

public class EmojiCategory
{
    public string Name { get; set; } = "";
    public List<string> Emojis { get; set; } = new();
}

public class EmojiData
{
    public List<EmojiCategory> Categories { get; set; } = new();
}

public static class EmojiService
{
    public static async Task<List<EmojiCategory>> LoadAsync()
    {
        using var stream =
            await FileSystem.OpenAppPackageFileAsync("emojis.json");

        var data = await JsonSerializer.DeserializeAsync<EmojiData>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return data?.Categories ?? new();
    }
    
}