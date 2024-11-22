using AutoGenerateContent.DatabaseContext;
using AutoGenerateContent.ViewModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Configuration;
using System.Data;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace AutoGenerateContent
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IHost AppHost { get; private set; } = default!;

        public App()
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices((HostBuilderContext, services) =>
                {
                    services.AddDbContext<SQLiteContext>(options => options.UseSqlite("Data Source=SQLiteDb.db"))
                            .AddScoped<MainWindowViewModel>()
                            .AddScoped<SideBarViewModel>();

                    Log.Logger = new LoggerConfiguration()
                            .WriteTo.File("Logs/.txt", rollingInterval: RollingInterval.Day)
                            .CreateLogger();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await AppHost!.StartAsync();

            using (var scope = AppHost.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SQLiteContext>();
                db.Database.EnsureCreated();
                db.Database.Migrate();
            }

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            base.OnStartup(e);
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            if (e.Exception is OperationCanceledException canceledException)
            {
                Log.Logger.Error($"{canceledException.Message}");
                Log.Logger.Error($"{canceledException.StackTrace}");
                e.Handled = true;
            }
            else
            {
                Log.Logger.Error($"{e.Exception.Message}");
                Log.Logger.Error($"{e.Exception.StackTrace}");
                e.Handled = true;
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Logger.Error("TaskScheduler_UnobservedTaskException");
            Log.Logger.Error(e.Exception.StackTrace);
            e.SetObserved();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Logger.Error($"{e?.ExceptionObject}");
        }
    }
}
