using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

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
    }
}
