namespace AzurChat.Model;

public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    public string Nickname { get; set; } = "";

    public string Avatar { get; set; } = "";
    
    public DateTime? Birthday { get; set; }
    public string Region { get; set; } = "";
    public string Signature { get; set; } = "";
    public int Level { get; set; } = 1;
    public int Exp { get; set; } = 0;
    public string Url { get; set; } = "";
    public string Api { get; set; } = "";
    
    public string Model { get; set; } = "";
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
}