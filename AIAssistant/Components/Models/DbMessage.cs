using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace FlintecAIAssistant.Components.Models
{
    public class DbMessage
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }


        public DbConversation? Conversation { get; set; }

        public string Role { get; set; } = "";

        public string Content { get; set; } = "";

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}