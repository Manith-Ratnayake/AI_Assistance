using Microsoft.Extensions.Logging;
using FlintecChatBotApp.Components.Models;
using Microsoft.JSInterop;
using System.Globalization;
using FlintecChatBotApp.Components.Pages;

using FlintecChatBotApp.Data;
using Microsoft.EntityFrameworkCore;

namespace FlintecChatBotApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

   

            builder.Services.AddMauiBlazorWebView();

            builder.Logging.AddDebug();

            builder.Services.AddLocalization();
            builder.Services.AddSingleton<Conversation>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();



#endif
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(
                    "Server=localhost\\SQLEXPRESS;Database=FlintecAIAssistant;Trusted_Connection=True;TrustServerCertificate=True;"
                ));

            return builder.Build();
        }
    }
}
