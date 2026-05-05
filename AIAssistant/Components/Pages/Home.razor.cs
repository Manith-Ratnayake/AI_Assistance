using FlintecAIAssistant.Components.Data;
using FlintecAIAssistant.Components.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

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
        private CancellationTokenSource? currentGenerationCts;

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

        private bool isSourcesPanelOpen = false;
        private int? selectedSourcesMessageIndex = null;

        private ElementReference messagesScrollAreaRef;
        private bool showScrollDownButton = false;
        private DotNetObjectReference<Home>? scrollDotNetReference;

        private sealed class ResponseSource
        {
            public string DocumentName { get; set; } = string.Empty;

            public int PageNumber { get; set; }

            public string Snippet { get; set; } = string.Empty;
        }

        private Dictionary<int, List<ResponseSource>> assistantMessageSources = new();

        private List<ResponseSource> GetSelectedSources()
        {
            if (selectedSourcesMessageIndex is int messageIndex &&
                assistantMessageSources.TryGetValue(messageIndex, out var sources))
            {
                return sources;
            }

            return new List<ResponseSource>();
        }

        private void EnsureDemoSources(int assistantMessageIndex)
        {
            if (assistantMessageSources.ContainsKey(assistantMessageIndex))
            {
                return;
            }

            assistantMessageSources[assistantMessageIndex] = new List<ResponseSource>
            {
                new ResponseSource
                {
                    DocumentName = "Password Policy.pdf",
                    PageNumber = 2,
                    Snippet = "Password requirements, password expiry rules, and account protection guidelines."
                },
                new ResponseSource
                {
                    DocumentName = "Backup Policy.pdf",
                    PageNumber = 5,
                    Snippet = "Backup frequency, storage location, retention period, and recovery responsibilities."
                },
                new ResponseSource
                {
                    DocumentName = "Microsoft 365 Policy.pdf",
                    PageNumber = 3,
                    Snippet = "Microsoft 365 usage rules, user responsibilities, and access control guidance."
                }
            };
        }





        private async Task HandleSendOrStopButtonClick()
        {
            if (isAssistantStreaming)
            {
                StopAssistantResponse();
                return;
            }

            await UserSubmitQuestion();
        }

        private void StopAssistantResponse()
        {
            currentGenerationCts?.Cancel();

            isAssistantStreaming = false;
            backgroundGeneratingConversationIndex = null;

            StateHasChanged();
        }

        public async Task UserSubmitQuestion()
        {
            if (string.IsNullOrWhiteSpace(userQuestion))
            {
                return;
            }

            currentGenerationCts?.Cancel();
            currentGenerationCts?.Dispose();
            currentGenerationCts = new CancellationTokenSource();
            var cancellationToken = currentGenerationCts.Token;

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

            int conversationIndex = AddOrSyncConversationImmediately(question);
            backgroundGeneratingConversationIndex = conversationIndex;

            await InvokeAsync(StateHasChanged);

            _ = Task.Run(async () =>
            {
                await GenerateAssistantResponseInBackground(question, assistantMessage, conversationIndex, cancellationToken);
            }, cancellationToken);
        }

        private int AddOrSyncConversationImmediately(string question)
        {
            if (isChnagedAfterCreation == false)
            {
                userConversation.UpdateConversation(question, string.Empty);
            }

            int conversationIndex = userConversation.pointingTab;

            if (conversationIndex >= 0 && conversationIndex < userConversation.conversations.Count)
            {
                userConversation.conversations[conversationIndex] = messages;
            }

            return conversationIndex;
        }

        private void SyncConversationMessages(int conversationIndex)
        {
            if (conversationIndex >= 0 && conversationIndex < userConversation.conversations.Count)
            {
                userConversation.conversations[conversationIndex] = messages;
            }
        }








        private async Task GenerateAssistantResponseInBackground(
            string question,
            DbMessage assistantMessage,
            int conversationIndex,
            CancellationToken cancellationToken)
        {
            try
            {
                await GenerateAnswerFromApiStreaming(question, assistantMessage, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                string finalAnswer = assistantMessage.Content;

                SyncConversationMessages(conversationIndex);

                if (IsConnectionErrorMessage(finalAnswer))
                {
                    return;
                }

                await SaveConversationToDatabase(question, finalAnswer);

                if (userConversation.pointingTab != conversationIndex || isSavedResponsesView)
                {
                    unreadConversationIndexes.Add(conversationIndex);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    connectionErrorMessage = "Unable to connect. Check your internet connection and try again.";
                    assistantMessage.Content = connectionErrorMessage;

                    SyncConversationMessages(conversationIndex);

                    if (userConversation.pointingTab != conversationIndex || isSavedResponsesView)
                    {
                        unreadConversationIndexes.Add(conversationIndex);
                    }
                }
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    isAssistantStreaming = false;
                    backgroundGeneratingConversationIndex = null;
                }

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














        private async Task GenerateAnswerFromApiStreaming(
            string question,
            DbMessage assistantMessage,
            CancellationToken cancellationToken)
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
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                connectionErrorMessage = "Unable to connect. Check your internet connection and try again.";
                assistantMessage.Content = connectionErrorMessage;
                await InvokeAsync(StateHasChanged);
                return;
            }

            connectionErrorMessage = null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            char[] buffer = new char[1024];
            DateTime lastUiUpdate = DateTime.Now;

            while (!cancellationToken.IsCancellationRequested)
            {
                int count = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);

                if (count <= 0)
                {
                    break;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                string chunk = new string(buffer, 0, count);
                assistantMessage.Content += chunk;

                if ((DateTime.Now - lastUiUpdate).TotalMilliseconds >= 120)
                {
                    lastUiUpdate = DateTime.Now;
                    await InvokeAsync(StateHasChanged);
                }
            }

            await InvokeAsync(StateHasChanged);
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

            currentGenerationCts?.Cancel();
            currentGenerationCts?.Dispose();
            currentGenerationCts = new CancellationTokenSource();
            var cancellationToken = currentGenerationCts.Token;

            await InvokeAsync(StateHasChanged);

            try
            {
                await GenerateAnswerFromApiStreaming(previousUserMessage.Content, newAssistantMessage, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    connectionErrorMessage = "Unable to connect. Check your internet connection and try again.";
                    newAssistantMessage.Content = connectionErrorMessage;
                }
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    isAssistantStreaming = false;
                }

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
            showScrollDownButton = false;

            userConversation.PrepareNewConversation();

            StateHasChanged();
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
            showScrollDownButton = false;

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

            if (messages?.Count > 0 && !isSavedResponsesView)
            {
                scrollDotNetReference ??= DotNetObjectReference.Create(this);

                await JSRuntime.InvokeVoidAsync(
                    "flintecChatScroll.register",
                    messagesScrollAreaRef,
                    scrollDotNetReference
                );
            }
        }

        [JSInvokable]
        public async Task SetScrollDownButtonVisible(bool visible)
        {
            if (showScrollDownButton != visible)
            {
                showScrollDownButton = visible;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task ScrollChatToBottom()
        {
            await JSRuntime.InvokeVoidAsync(
                "flintecChatScroll.scrollToBottom",
                messagesScrollAreaRef
            );

            showScrollDownButton = false;
            await InvokeAsync(StateHasChanged);
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
            showScrollDownButton = false;
        }

        private void ShowSources(int assistantMessageIndex)
        {
            if (isSourcesPanelOpen && selectedSourcesMessageIndex == assistantMessageIndex)
            {
                CloseSourcesPanel();
                return;
            }

            selectedSourcesMessageIndex = assistantMessageIndex;
            EnsureDemoSources(assistantMessageIndex);
            isSourcesPanelOpen = true;
        }

        private void CloseSourcesPanel()
        {
            isSourcesPanelOpen = false;
            selectedSourcesMessageIndex = null;
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