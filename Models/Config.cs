using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace AutoGenerateContent.Models
{
    public partial class Config : ObservableObject
    {
        [ObservableProperty]
        public int id;

        [ObservableProperty]
        public string? name;

        [ObservableProperty]
        public string? searchText;

        [ObservableProperty]
        public string? promptText;

        [ObservableProperty]
        public string? promptComplete;

        [ObservableProperty]
        public string? searchImageText;

        public bool IsChanged = false;

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            IsChanged = true;
            base.OnPropertyChanged(e);
        }
    }
}
