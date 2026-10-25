using AzurChat.Services;
using AzurChat.Pages;
namespace AzurChat;

public partial class AppShell : Shell
{
	public AppShell(IAuthService authService)
	{
		InitializeComponent();
		Routing.RegisterRoute("person", typeof(PersonPage));
	
		Routing.RegisterRoute("editprofile", typeof(ProfilePage));
		if(authService.IsLoggedIn())
		{
			GoToAsync("//main");
		}
		else
		{
			GoToAsync("//login");
		}
	}
}
