using AzurChat.Model;
using Microsoft.EntityFrameworkCore;

namespace AzurChat.Data;

public class UserContext : DbContext
{
    public DbSet<User> User { get; set; }
    public DbSet<Friend> Friend { get; set; }
    public DbSet<AllFriend> AllFriend { get; set; }
    public DbSet<Conversation> Conversation { get; set; }
    public DbSet<Message> Message { get; set; }

    public UserContext(DbContextOptions<UserContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 用户名不能重复
        modelBuilder.Entity<User>()
            .HasIndex(x => x.Username)
            .IsUnique();

        // 同一个用户不能重复添加同一个好友
        modelBuilder.Entity<Friend>()
            .HasKey(x => new { x.UserName, x.Id });

        // 同一个用户和同一个好友只能有一个聊天会话
        modelBuilder.Entity<Conversation>()
            .HasIndex(x => new { x.UserName, x.FriendId })
            .IsUnique();

        // 优化：根据会话查询聊天记录，并按时间排列
        modelBuilder.Entity<Message>()
            .HasIndex(x => new { x.ConvId, x.Time });
    }
}