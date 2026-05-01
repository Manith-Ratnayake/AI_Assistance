using FlintecChatBotApp.Components.Models;
using FlintecChatBotApp.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using System.Globalization;

namespace FlintecChatBotApp.Components.Pages
{


   


    partial class Home
    {
        private int? currentDbConversationId = null;

        [Inject]
        public AppDbContext Db { get; set; }

        public string? randomAnswer;  
        Conversation userConversation = new();
        public List<string>? messages = [];

        public bool isChnagedAfterCreation = false;

        public string userQuestion = "";
        public string? userAnswer;








        public async Task UserSubmitQuestion()
        {
            if (string.IsNullOrWhiteSpace(userQuestion))
            {
                return;
            }

            GenerateAnswer();

            string question = userQuestion;
            string answer = userAnswer ?? "";

            messages ??= new List<string>();

            messages.Add(question);
            messages.Add(answer);

            if (isChnagedAfterCreation == false)
            {
                userConversation.UpdateConversation(question, answer);
            }

            int defaultUserId = await GetDefaultUserId();

            DbConversation dbConversation;

            if (currentDbConversationId == null)
            {
                dbConversation = new DbConversation
                {
                    UserId = defaultUserId,
                    Title = question.Length > 50 ? question.Substring(0, 50) : question,
                    CreatedDate = DateTime.Now,
                    UpdatedDate = DateTime.Now
                };

                Db.Conversations.Add(dbConversation);
                await Db.SaveChangesAsync();

                currentDbConversationId = dbConversation.Id;
            }
            else
            {
                dbConversation = await Db.Conversations
                    .FirstAsync(c => c.Id == currentDbConversationId.Value);

                dbConversation.UpdatedDate = DateTime.Now;
            }

            Db.Messages.Add(new DbMessage
            {
                ConversationId = dbConversation.Id,
                Role = "user",
                Content = question,
                CreatedDate = DateTime.Now
            });

            Db.Messages.Add(new DbMessage
            {
                ConversationId = dbConversation.Id,
                Role = "assistant",
                Content = answer,
                CreatedDate = DateTime.Now
            });

            await Db.SaveChangesAsync();

            await AnimateText();

            userQuestion = string.Empty;
            userAnswer = string.Empty;
        }
















        private async Task<int> GetDefaultUserId()
        {
            var user = await Db.Users.FirstOrDefaultAsync(u => u.UserName == "Default User");

            if (user == null)
            {
                user = new DbUser
                {
                    UserName = "Default User",
                    Email = null,
                    CreatedDate = DateTime.Now
                };

                Db.Users.Add(user);
                await Db.SaveChangesAsync();
            }

            return user.Id;
        }


        public void CreateNewConversation()
        {
            isChnagedAfterCreation = false;
            messages = new List<string>();
            currentDbConversationId = null;

            userConversation.PrepareNewConversation();
        }

        public void UpdateConversation()
        {
            userConversation.UpdateConversation(userQuestion, userAnswer);
        }


        public void GetConversation(int tabNumber)
        {
            isChnagedAfterCreation = true;
            JSRuntime.InvokeVoidAsync("console.log", $"{tabNumber}");
            messages = userConversation.GetConversation(tabNumber);
            JSRuntime.InvokeVoidAsync("console.log", $"Getting Tab : {messages}");

        }




        private async Task AnimateText()
        {
            try
            {
                // Show all elements with the class "AnimationText"
                await JSRuntime.InvokeVoidAsync("showTextElements", "AnimationText");

                // Then start the text animation on all elements with the class "AnimationText"
                await JSRuntime.InvokeVoidAsync("AnimateTextTyping", "AnimationText");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}"); // Log the error message to the console
                await JSRuntime.InvokeVoidAsync("alert", "An error occurred: " + ex.Message);
            }
        }








        private const int DefaultUserId = 1;


        private async Task EnsureDefaultUserExists()
        {
            bool userExists = await Db.Users.AnyAsync(u => u.Id == DefaultUserId);

            if (!userExists)
            {
                Db.Users.Add(new DbUser
                {
                    Id = DefaultUserId,
                    UserName = "Default User",
                    Email = null,
                    CreatedDate = DateTime.Now
                });

                await Db.SaveChangesAsync();
            }
        }






    }
}


//userConversation.CreateNewConversation(new Conversation());
//userConversation.messages.Clear();

