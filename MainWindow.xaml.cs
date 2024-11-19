using AutoGenerateContent.Event;
using AutoGenerateContent.ViewModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Serilog;
using Serilog.Core;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows;

namespace AutoGenerateContent
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            DataContext = App.AppHost.Services.GetRequiredService<MainWindowViewModel>();

            InitializeComponent();
        }

        private new void PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = NumberRegex().IsMatch(e.Text);
        }

        [GeneratedRegex("[^0-9]+")]
        private static partial Regex NumberRegex();
    }
}