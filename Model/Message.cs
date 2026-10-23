namespace AzurChat.Model;

public class Message
{
    public int Id { get; set; }
    public int ConvId { get; set; }
    public string Sender { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime? Time { get; set; } = DateTime.Now;
}