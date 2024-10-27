using AutoGenerateContent.DatabaseContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Configuration;
using System.Data;
using System.Windows;

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
                    services.AddDbContext<SQLiteContext>(options => options.UseSqlite("Data Source=SQLiteDb.db"));

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

            base.OnStartup(e);
        }
    }

}
