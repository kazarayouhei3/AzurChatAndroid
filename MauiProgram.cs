using AzurChat.Data;
using Microsoft.Extensions.Logging;
using AzurChat.Services;
using SkiaSharp.Views.Maui.Controls.Hosting;
using AzurChat.Model;
using AzurChat.Pages;
using AzurChat.ViewModel;
using Microsoft.EntityFrameworkCore;

namespace AzurChat;
using Microsoft.Maui.Handlers;

#if ANDROID
using Android.Graphics.Drawables;
#endif
public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>().
			UseSkiaSharp()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif
		var dbPath = Path.Combine(
			FileSystem.AppDataDirectory,
			"azurchat.db");
		builder.Services.AddDbContextFactory<UserContext>(options =>
		{
			options.UseSqlite($"Data Source={dbPath}");
		});
		builder.Services.AddSingleton<IAuthService, AuthService>();
		builder.Services.AddSingleton<EncryptionService>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddScoped<AllFriendService>();
		builder.Services.AddTransient<ProfilePage>();
		builder.Services.AddTransient<AddFriendPage>();
		builder.Services.AddTransient<ChatPage>();
		builder.Services.AddTransient<SettingPage>();
		builder.Services.AddTransient<FriendViewModel>();
		builder.Services.AddTransient<PersonPage>();
		builder.Services.AddTransient<CommanderSettingPage>();
		builder.Services.AddSingleton<MessageCacheService>();
		builder.Services.AddTransient<CharaPage>();
		builder.Services.AddTransient<PhotoPickerPage>();
		builder.Services.AddSingleton<ChatService>();
		builder.Services.AddTransient<FriendPage>();
		
		builder.Services.AddScoped<AppUpgradeService>();
		
#if ANDROID
		EntryHandler.Mapper.AppendToMapping("RemoveUnderline",
			(handler, view) => { handler.PlatformView.Background = null; });
#endif
		return builder.Build();
	}
}
