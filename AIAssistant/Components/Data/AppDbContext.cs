using FlintecAIAssistant.Components.Models;
using Microsoft.EntityFrameworkCore;

namespace FlintecAIAssistant.Components.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<DbUser> Users { get; set; }

        public DbSet<DbConversation> Conversations { get; set; }

        public DbSet<DbMessage> Messages { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbUser>()
                .HasMany(u => u.Conversations)
                .WithOne(c => c.User)
                .HasForeignKey(c => c.UserId);

            modelBuilder.Entity<DbConversation>()
                .HasMany(c => c.Messages)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}