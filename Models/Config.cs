using AutoGenerateContent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoGenerateContent.Models
{
    public partial class Config : ObservableObject
    {
        [ObservableProperty]
        int id;

        [ObservableProperty]
        string? name;

        [ObservableProperty]
        string? searchText;

        [ObservableProperty]
        int numberUrls;

        [ObservableProperty]
        string? promptIntro;

        [ObservableProperty]
        string? promptText;

        [ObservableProperty]
        string? promptAskNewContent;

        [ObservableProperty]
        string? searchImageText;
        
        [ObservableProperty]
        string? promptTitle;
        
        [ObservableProperty]
        string? promptHeading;

        [NotMapped]
        public bool IsChanged = false;

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            IsChanged = true;
            base.OnPropertyChanged(e);
        }
    }
}
