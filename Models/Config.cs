using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoGenerateContent.Models
{
    public partial class Config : ObservableObject
    {

        [ObservableProperty]
        public int id;//{ get; set; }
        [ObservableProperty]
        public string? name;//{ get; set; }
        [ObservableProperty]
        public string? searchText;// { get; set; }
        [ObservableProperty]
        public string? promptText;// { get; set; }
        [ObservableProperty]
        public string? promptComplete; // { get; set; }
        [ObservableProperty]
        public string? searchImageText; // { get; set; }
    }
}
