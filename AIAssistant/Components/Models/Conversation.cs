using Microsoft.JSInterop;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;




namespace FlintecAIAssistant.Components.Models
{
    public class Conversation
    {
        public List<List<DbMessage>> conversations = new();

        public int pointingTab = -1;

        public List<DbMessage> GetConversation(int tabNumber)
        {
            pointingTab = tabNumber;
            return conversations[tabNumber];
        }

        public void PrepareNewConversation()
        {
            pointingTab = -1;
        }

        public void CreateNewConversation()
        {
            List<DbMessage> newConversation = new();
            conversations.Add(newConversation);
            pointingTab = conversations.Count - 1;
        }

        public void UpdateConversation(string userQuestion, string userQuestionAnswer)
        {
            if (pointingTab < 0 || pointingTab >= conversations.Count)
            {
                CreateNewConversation();
            }

            conversations[pointingTab].Add(new DbMessage
            {
                Role = "user",
                Content = userQuestion,
                CreatedDate = DateTime.Now
            });

            conversations[pointingTab].Add(new DbMessage
            {
                Role = "assistant",
                Content = userQuestionAnswer,
                CreatedDate = DateTime.Now
            });
        }

        public void DeleteConversation(int tabNumber)
        {
            conversations.RemoveAt(tabNumber);

            if (conversations.Count == 0)
            {
                pointingTab = -1;
            }
            else
            {
                pointingTab = conversations.Count - 1;
            }
        }
    }
}



//public List<string> GetMessages()
//{
//    return messages;
//}


//public Dictionary<int, Conversation> GetConversations()
//{
//    return conversations;
//}

//string combinedMessages = string.Join("", conversations);
//return combinedMessages.Length > 1000 ? combinedMessages.Substring(0, 1000) : combinedMessages;


//public List<string> GetMessages()
//{
//    return messages;
//}


//public Dictionary<int, Conversation> GetConversations()
//{
//    return conversations;
//}


/*
 * 
 * 
 *  //// Check if the conversation has messages
                //if (conversations[i] == null || conversations[i].Count == 0)
                //{
                //    result += "  No messages.\n";
                //}
                //else
                //{
                //    // Add each message in the conversation
                //    foreach (var message in conversations[i])
                //    {
                //        result += $"  - {message}\n";
                //    }
                //}*/