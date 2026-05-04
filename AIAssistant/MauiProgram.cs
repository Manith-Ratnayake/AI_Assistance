using Microsoft.Extensions.Logging;
using FlintecAIAssistant.Components.Models;
using Microsoft.JSInterop;
using System.Globalization;
using FlintecAIAssistant.Components.Pages;
using Microsoft.EntityFrameworkCore;
using FlintecAIAssistant.Components.Data;

using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace FlintecAIAssistant
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("FlintecAIAssistant.appsettings.json");

            if (stream != null)
            {
                var config = new ConfigurationBuilder()
                    .AddJsonStream(stream)
                    .Build();

                builder.Configuration.AddConfiguration(config);
            }

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
            builder.Services.AddHttpClient();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection")
                ));

            return builder.Build();
        }
    }
}