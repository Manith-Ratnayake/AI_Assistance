using FlintecChatBotApp.Components.Models;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using Microsoft.AspNetCore.Components;
using FlintecChatBotApp.Data;
 
namespace FlintecChatBotApp.Components.Pages
{
    partial class Home
    {




        //public void EnterKeyPressed(KeyboardEventArgs e)
        //{
        //    if (e.Key == "Enter")
        //    {

        //        JSRuntime.InvokeVoidAsync("console.log", $"Enter Pressed userQuestion: {userQuestion}");
        //        UserSubmitQuestion();
        //    }
        //}



        public List<string> PossibleReplies = new List<string>
        {
            "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's."
        };



        public void GenerateAnswer()
        {
            var random = new Random();
            int index = random.Next(PossibleReplies.Count);
            userAnswer = PossibleReplies[index];
        }



        public void EditButtonClick(int index)
        {

        }





        //public void CreateConversationTab()
        //{
        //    userConversation = new Conversation();
        //    userAccount.CreateConversation(userConversation);


        //}


        //public void UpdateConversationTab()
        //{
        //    userConversation.userAnswer = "//////////";
        //    JSRuntime.InvokeVoidAsync("console.log", "******222**** : " + userConversation.userQuestion);

        //    userAccount.UpdateConversation(userConversation.userQuestion);
        //    userAccount.UpdateConversation(userConversation.userAnswer);
        //    JSRuntime.InvokeVoidAsync("console.log", "************ : " + userConversation.userQuestion);


        //}



        //public async Task GetConversationTab(int conversationId)
        //{
        //    await JSRuntime.InvokeVoidAsync("console.log", "requested id" + conversationId);
        //    userConversation = userAccount.GetConversation(conversationId);
        //}


        //public async Task DeleteConversationTab(int conversationId)
        //{
        //    await JSRuntime.InvokeVoidAsync("console.log", "requested delete" + conversationId);
        //    userAccount.DeleteConversation(conversationId);
        //}


    }
}
