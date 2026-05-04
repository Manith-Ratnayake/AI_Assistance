using System;
using System.Collections.Generic;

namespace FlintecAIAssistant.Components.Models
{
    public class DbConversation
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public DbUser? User { get; set; }

        public string? Title { get; set; } = "New Chat";

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? UpdatedDate { get; set; }

        public List<DbMessage> Messages { get; set; } = new();
    }
}