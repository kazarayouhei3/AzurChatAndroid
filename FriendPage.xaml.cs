using AzurChat.ViewModel;

namespace AzurChat;

public partial class FriendPage : ContentPage
{
    public FriendPage(FriendViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.SetNavigation(Navigation);
    }
}