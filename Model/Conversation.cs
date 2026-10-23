namespace AzurChat.Model;

public class Conversation
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string FriendId { get; set; } = "";
    public string FriendName { get; set; } = "";
    public string NickName { get; set; } = "";
    public string Avatar { get; set; } = "";
    public string LastMessage { get; set; } = "";
    public DateTime? LastTime { get; set; }
    
    public int Tag { get; set; } = 0;
}