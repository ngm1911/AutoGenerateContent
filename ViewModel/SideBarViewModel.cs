using AutoGenerateContent.DatabaseContext;
using AutoGenerateContent.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AutoGenerateContent.ViewModel
{
    public partial class SideBarViewModel : ObservableObject
    {
        private readonly SQLiteContext _context;

        private ObservableCollection<Config> _Configs;
        public ObservableCollection<Config> Configs {
            get => _Configs;
            set
            {
                SetProperty(ref _Configs, value);
                OnPropertyChanged(nameof(Configs));
            }
        }

        private Config _selectedConfig;

        public Config SelectedConfig
        {
            get => _selectedConfig;
            set
            {
                SetProperty(ref _selectedConfig, value);
                OnPropertyChanged(nameof(IsSelectedConfig));
            }
        }

        public bool IsSelectedConfig => SelectedConfig != null;

        public SideBarViewModel(SQLiteContext context) : base()
        {
            _context = context;

            Configs = new ObservableCollection<Config>(_context.configs.ToList());

            var newConfig = new Config()
            {
                Id = -1,
                Name = "Create New Config"
            };

            Configs.Insert(0, newConfig);
        }

        public async Task<Config> ResetNewConfig()
        {
            var newConfig = new Config()
            {
                Id = -1,
                Name = "Create New Config"
            };

            return newConfig;
        }

        [RelayCommand]
        public async Task SaveConfig()
        {
            if (SelectedConfig.Id == -1)
            {
                var newConfig = new Config();

                newConfig.Name = SelectedConfig.Name;
                newConfig.SearchText = SelectedConfig.SearchText;
                newConfig.PromptText = SelectedConfig.PromptText;
                newConfig.PromptComplete = SelectedConfig.PromptComplete;
                newConfig.SearchImageText = SelectedConfig.SearchImageText;

                _context.configs.Add(newConfig);
                _context.SaveChanges();
            }
            else
            {
                _context.configs.Update(SelectedConfig);
                _context.SaveChanges();
               
            }

            Configs = new ObservableCollection<Config>(_context.configs.ToList());
            SelectedConfig = await ResetNewConfig();
            Configs.Insert(0, SelectedConfig);
        }

        [RelayCommand]
        public async Task DeleteConfig()
        {
            if (SelectedConfig.Id != -1)
            {
                _context.configs.Remove(SelectedConfig);
                _context.SaveChanges();
                Configs.Remove(SelectedConfig);
                SelectedConfig = null;
            }
        }

        [RelayCommand]
        public async Task CreateNewConfig()
        {
            SelectedConfig = new Config();
        }
    }
}
