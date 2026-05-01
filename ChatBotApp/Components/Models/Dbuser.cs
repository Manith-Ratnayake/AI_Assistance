using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlintecChatBotApp.Components.Models
{
    public class DbUser
    {
        public int Id { get; set; }

        public string UserName { get; set; } = "";

        public string? Email { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public List<DbConversation> Conversations { get; set; } = new();
    }
}
