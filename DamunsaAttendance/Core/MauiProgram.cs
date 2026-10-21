using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui;
using DamunsaAttendance.Pages;

namespace DamunsaAttendance
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            builder.Services.AddSingleton<Db>(sp =>
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "damunsa.db3");
                return new Db(dbPath);
            });

            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<StaffPage>();
            builder.Services.AddTransient<AdminPage>();
            builder.Services.AddTransient<OwnerPage>();
            builder.Services.AddTransient<LeavePage>();

            builder.Services.AddSingleton<AppShell>();

            return builder.Build();
        }
    }
}