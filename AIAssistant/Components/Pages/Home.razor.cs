using FlintecAIAssistant.Components.Data;
using FlintecAIAssistant.Components.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;

namespace FlintecAIAssistant.Components.Pages
{
    public partial class Home
    {
        [Inject]
        public HttpClient Http { get; set; } = default!;

        [Inject]
        public AppDbContext Db { get; set; } = default!;

        [Inject]
        public IConfiguration Configuration { get; set; } = default!;

        [Inject]
        public IHttpClientFactory HttpClientFactory { get; set; } = default!;

        private int? currentDbConversationId = null;

        public string? randomAnswer;
        public Conversation userConversation = new();

        public List<DbMessage> messages = new();

        public bool isChnagedAfterCreation = false;

        public string userQuestion = string.Empty;
        public string? userAnswer;

        private bool isSettingsPopupVisible = false;
        private bool isBVisible = true;
        private bool isSliderVisible = true;
        private bool isAssistantStreaming = false;

        private int popupIndex = -1;
        private int? activeIndex = null;
        private int? currentIndex = null;

        private string sendButtonColor { get; set; } = "gray";
        private int clickedTimes = 0;

        private List<string> suggestions = new List<string>
        {
            "Password Policy",
            "Backup Policy",
            "Microsoft 365 Policy",
        };

        private HashSet<int> savedConversationIndexes = new();
        private HashSet<int> saveTickMessageIndexes = new();
        private HashSet<int> copyTickMessageIndexes = new();
        private HashSet<int> copySavedTickIndexes = new();

        private List<string> savedResponses = new();

        private bool isSavedResponsesView = false;
        private string? connectionErrorMessage = null;
        private int? backgroundGeneratingConversationIndex = null;
        private HashSet<int> unreadConversationIndexes = new();

        private string personName = "Manith Ratnayake";





        public Task UserSubmitQuestion()
        {
            if (isAssistantStreaming)
            {
                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(userQuestion))
            {
                return Task.CompletedTask;
            }

            RemoveLastConnectionErrorMessage();

            string question = userQuestion.Trim();
            userQuestion = string.Empty;
            connectionErrorMessage = null;

            var userMessage = new DbMessage
            {
                Role = "user",
                Content = question,
                CreatedDate = DateTime.Now
            };

            var assistantMessage = new DbMessage
            {
                Role = "assistant",
                Content = string.Empty,
                CreatedDate = DateTime.Now
            };

            messages.Add(userMessage);
            messages.Add(assistantMessage);

            isSavedResponsesView = false;
            isAssistantStreaming = true;

            int conversationIndex = userConversation.pointingTab;
            backgroundGeneratingConversationIndex = conversationIndex;

            _ = GenerateAssistantResponseInBackground(question, assistantMessage, conversationIndex);

            return InvokeAsync(StateHasChanged);
        }







        private async Task GenerateAssistantResponseInBackground(string question, DbMessage assistantMessage, int conversationIndex)
        {
            try
            {
                await GenerateAnswerFromApiStreaming(question, assistantMessage);

                string finalAnswer = assistantMessage.Content;

                if (IsConnectionErrorMessage(finalAnswer))
                {
                    return;
                }

                if (isChnagedAfterCreation == false)
                {
                    userConversation.UpdateConversation(question, finalAnswer);
                }

                await SaveConversationToDatabase(question, finalAnswer);

                if (userConversation.pointingTab != conversationIndex || isSavedResponsesView)
                {
                    unreadConversationIndexes.Add(conversationIndex);
                }
            }
            catch (Exception)
            {
                connectionErrorMessage = "Unable to connect. Check your internet connection and try again.";
                assistantMessage.Content = connectionErrorMessage;

                if (userConversation.pointingTab != conversationIndex || isSavedResponsesView)
                {
                    unreadConversationIndexes.Add(conversationIndex);
                }
            }
            finally
            {
                isAssistantStreaming = false;
                backgroundGeneratingConversationIndex = null;
                await InvokeAsync(StateHasChanged);
            }
        }









        private async Task CopyUserMessage(string content, int messageIndex)
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", content);

            copyTickMessageIndexes.Add(messageIndex);
            await InvokeAsync(StateHasChanged);

            await Task.Delay(1200);

            copyTickMessageIndexes.Remove(messageIndex);
            await InvokeAsync(StateHasChanged);
        }

        private void DeleteUserMessage(int messageIndex)
        {
            if (messageIndex < 0 || messageIndex >= messages.Count)
            {
                return;
            }

            messages.RemoveAt(messageIndex);

            if (messageIndex < messages.Count && messages[messageIndex].Role == "assistant")
            {
                messages.RemoveAt(messageIndex);
            }

            StateHasChanged();
        }














        private async Task GenerateAnswerFromApiStreaming(string question, DbMessage assistantMessage)
        {
            var baseUrl = Configuration["LLM:BaseUrl"];
            var endpoint = Configuration["LLM:GenerateEndpoint"];

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                connectionErrorMessage = "Unable to connect. LLM BaseUrl is missing.";
                assistantMessage.Content = connectionErrorMessage;
                await InvokeAsync(StateHasChanged);
                return;
            }

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                connectionErrorMessage = "Unable to connect. LLM GenerateEndpoint is missing.";
                assistantMessage.Content = connectionErrorMessage;
                await InvokeAsync(StateHasChanged);
                return;
            }

            var apiUrl = $"{baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";

            var requestBody = new
            {
                question = question
            };

            var httpClient = HttpClientFactory.CreateClient();

            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = JsonContent.Create(requestBody)
            };

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead
            );

            if (!response.IsSuccessStatusCode)
            {
                connectionErrorMessage = "Unable to connect. Check your internet connection and try again.";
                assistantMessage.Content = connectionErrorMessage;
                await InvokeAsync(StateHasChanged);
                return;
            }

            connectionErrorMessage = null;

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            char[] buffer = new char[256];

            while (!reader.EndOfStream)
            {
                int count = await reader.ReadAsync(buffer, 0, buffer.Length);

                if (count > 0)
                {
                    string chunk = new string(buffer, 0, count);
                    assistantMessage.Content += chunk;
                    await InvokeAsync(StateHasChanged);
                }
            }
        }

        private bool IsCurrentStreamingAssistantMessage(int index, DbMessage message)
        {
            return isAssistantStreaming &&
                   index == messages.Count - 1 &&
                   message.Role == "assistant";
        }

        private bool IsConnectionErrorMessage(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            return content.StartsWith("Unable to connect", StringComparison.OrdinalIgnoreCase) ||
                   content.StartsWith("Sorry, something went wrong", StringComparison.OrdinalIgnoreCase);
        }

        private async Task ReloadAssistantMessage(int assistantMessageIndex)
        {
            if (assistantMessageIndex <= 0 || assistantMessageIndex >= messages.Count || isAssistantStreaming)
            {
                return;
            }

            var previousUserMessage = messages
                .Take(assistantMessageIndex)
                .LastOrDefault(message => message.Role == "user");

            if (previousUserMessage == null)
            {
                return;
            }

            messages.RemoveAt(assistantMessageIndex);

            var newAssistantMessage = new DbMessage
            {
                Role = "assistant",
                Content = string.Empty,
                CreatedDate = DateTime.Now
            };

            messages.Insert(assistantMessageIndex, newAssistantMessage);

            connectionErrorMessage = null;
            isAssistantStreaming = true;

            await InvokeAsync(StateHasChanged);

            try
            {
                await GenerateAnswerFromApiStreaming(previousUserMessage.Content, newAssistantMessage);
            }
            catch (Exception)
            {
                connectionErrorMessage = "Unable to connect. Check your internet connection and try again.";
                newAssistantMessage.Content = connectionErrorMessage;
            }
            finally
            {
                isAssistantStreaming = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task SaveConversationToDatabase(string question, string answer)
        {
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
            isSavedResponsesView = false;
            isChnagedAfterCreation = false;
            messages = new List<DbMessage>();
            currentDbConversationId = null;
            popupIndex = -1;
            connectionErrorMessage = null;

            userConversation.PrepareNewConversation();
        }

        public void UpdateConversation()
        {
            userConversation.UpdateConversation(userQuestion, userAnswer ?? string.Empty);
        }

        public void GetConversation(int tabNumber)
        {
            isChnagedAfterCreation = true;
            popupIndex = -1;
            isSavedResponsesView = false;

            unreadConversationIndexes.Remove(tabNumber);

            messages = userConversation.GetConversation(tabNumber);
        }

        private async Task AnimateText()
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("showTextElements", "AnimationText");
                await JSRuntime.InvokeVoidAsync("AnimateTextTyping", "AnimationText");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await JSRuntime.InvokeVoidAsync("AnimateTextTyping", "AnimationText");
            }
        }

        private void ShowSettingsPopup(bool show)
        {
            isSettingsPopupVisible = show;
        }

        private void ClosePopup()
        {
            isSettingsPopupVisible = false;
        }

        public void UpdateTheSearchBar(string suggestion)
        {
            userQuestion = suggestion;
        }

        private Task ResizeTextArea(ChangeEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task CopyTextToClipboard()
        {
            return Task.CompletedTask;
        }

        private void UpdateButtonColor()
        {
            sendButtonColor = string.IsNullOrWhiteSpace(userQuestion) ? "red" : "pink";
            clickedTimes++;
        }

        public void bOff()
        {
            isBVisible = !isBVisible;
        }

        public void Slider()
        {
            isSliderVisible = !isSliderVisible;
        }

        private void TogglePopup(int index)
        {
            popupIndex = popupIndex == index ? -1 : index;
        }

        private void CloseConversationPopup()
        {
            popupIndex = -1;
        }

        private void DeleteConversationAndClose(int listIndex)
        {
            userConversation.DeleteConversation(listIndex);
            popupIndex = -1;
        }

        private void RenameConversation(int listIndex)
        {
            popupIndex = -1;
        }

        public void EditButtonClick(int index)
        {
        }

        private void DoSomething(int index)
        {
        }

        private void SetActiveParagraph(int index)
        {
            activeIndex = index;
            currentIndex = index;
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "?";
            }

            var parts = name
                .Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
            {
                return parts[0].Substring(0, 1).ToUpper();
            }

            return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
        }

        private async Task CopyAssistantMessage(string content, int assistantMessageIndex)
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", content);

            copyTickMessageIndexes.Add(assistantMessageIndex);
            await InvokeAsync(StateHasChanged);

            await Task.Delay(1200);

            copyTickMessageIndexes.Remove(assistantMessageIndex);
            await InvokeAsync(StateHasChanged);
        }

        private async Task CopySavedResponse(string content, int savedResponseIndex)
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", content);

            copySavedTickIndexes.Add(savedResponseIndex);
            await InvokeAsync(StateHasChanged);

            await Task.Delay(1200);

            copySavedTickIndexes.Remove(savedResponseIndex);
            await InvokeAsync(StateHasChanged);
        }

        private async Task SaveCurrentConversation(int assistantMessageIndex)
        {
            if (assistantMessageIndex < 0 || assistantMessageIndex >= messages.Count)
            {
                return;
            }

            string response = messages[assistantMessageIndex].Content;

            if (string.IsNullOrWhiteSpace(response))
            {
                return;
            }

            if (!savedResponses.Contains(response))
            {
                savedResponses.Add(response);
            }

            int currentConversationIndex = userConversation.pointingTab;

            if (currentConversationIndex >= 0)
            {
                savedConversationIndexes.Add(currentConversationIndex);
            }

            saveTickMessageIndexes.Add(assistantMessageIndex);
            await InvokeAsync(StateHasChanged);

            await Task.Delay(1200);

            saveTickMessageIndexes.Remove(assistantMessageIndex);
            await InvokeAsync(StateHasChanged);
        }

        private void OpenSavedResponses()
        {
            isSavedResponsesView = true;
            popupIndex = -1;
        }

        private void ShowSources(int assistantMessageIndex)
        {
            JSRuntime.InvokeVoidAsync("console.log", $"Sources clicked for assistant message index: {assistantMessageIndex}");
        }



        private void RemoveLastConnectionErrorMessage()
        {
            if (messages.Count == 0)
            {
                return;
            }

            var lastMessage = messages.Last();

            if (lastMessage.Role == "assistant" && IsConnectionErrorMessage(lastMessage.Content))
            {
                messages.RemoveAt(messages.Count - 1);
            }
        }





    }







}
















//public void EnterKeyPressed(KeyboardEventArgs e)
//{
//    if (e.Key == "Enter")
//    {

//        JSRuntime.InvokeVoidAsync("console.log", $"Enter Pressed userQuestion: {userQuestion}");
//        UserSubmitQuestion();
//    }
//}








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





//userConversation.CreateNewConversation(new Conversation());
//userConversation.messages.Clear();

//@* window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', e => { *@
//@*     const newColorScheme = e.matches ? "dark" : "light"; *@
//@*     console.log('System theme changed to', newColorScheme); *@
//@* }); *@