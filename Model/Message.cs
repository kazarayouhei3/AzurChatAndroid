namespace AzurChat.Model;

public class Message
{
    // 原来的字段，全部保留
    public int Id { get; set; }
    public int ConvId { get; set; }
    public string Sender { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime? Time { get; set; } = DateTime.Now;

    // 消息类型
    public string MessageType { get; set; } = "text";

    // 媒体文件地址
    public string? MediaPath { get; set; }

    // 媒体文件 MIME 类型
    public string? MimeType { get; set; }

    // 语音、视频时长（毫秒）
    public long? DurationMs { get; set; }

    // 扩展数据（JSON）
    public string? ExtraData { get; set; }

    // 回复的消息 ID
    public int? ReplyToMessageId { get; set; }

    // 消息状态
    public string Status { get; set; } = "sent";
}