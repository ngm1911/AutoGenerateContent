using AutoGenerateContent.DatabaseContext;
using AutoGenerateContent.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace AutoGenerateContent.ViewModel
{
    public partial class SideBarViewModel : ObservableObject
    {
        private readonly SQLiteContext _context;

        [ObservableProperty]
        public ObservableCollection<Config> _Configs;

        [ObservableProperty]
        public ObservableCollection<ConfigViewModel> _ConfigViewModels;

        [ObservableProperty]
        private int selectedConfigId;

        partial void OnSelectedConfigIdChanged(int value)
        {
            LoadSelectedConfig();
        }

        [ObservableProperty]
        Config _selectedConfig;

        public SideBarViewModel(SQLiteContext context)
        {
            _context = context;

            LoadConfigs();
        }

        private void LoadConfigs() {
            ConfigViewModels = new ObservableCollection<ConfigViewModel>(
                _context.configs.Select(c => new ConfigViewModel
                {
                    Id = c.Id,
                    Name = c.Name
                }).ToList()
            );

            var defaultConfig = new ConfigViewModel()
            {
                Id = -1,
                Name = "Create New Configs",
            };
            ConfigViewModels.Insert(0, defaultConfig);
        }

        private void LoadSelectedConfig()
        {
            SelectedConfig = _context.configs.FirstOrDefault(c => c.Id == SelectedConfigId);
            if(SelectedConfig == null)
            {
                SelectedConfig = new Config()
                {
                    Id = -1,
                }; 
            }
        }

        [RelayCommand]
        public async Task SaveConfig()
        { 
            var result = _context.configs.Any(x => x.Id == SelectedConfig.Id);

            if (result == true)
            {
                _context.configs.Update(SelectedConfig);
            }
            else
            {
                var newConfig = new Config()
                {
                    Name = SelectedConfig.Name,
                    SearchText = SelectedConfig.SearchText,
                    PromptText = SelectedConfig.PromptText,
                    PromptComplete = SelectedConfig.PromptComplete,
                    SearchImageText = SelectedConfig.SearchImageText,
                };
                _context.configs.Add(newConfig);
            }

            SelectedConfig = null;
            _context.SaveChanges();
            LoadConfigs();
        }

        [RelayCommand]
        public async Task DeleteConfig()
        {
            var result = _context.configs.Any(x => x.Id == SelectedConfig.Id);
            if (result == true)
            {
                _context.configs.Remove(SelectedConfig);
                _context.SaveChanges();
            }
            SelectedConfig = null;
            LoadConfigs();
        }

        [RelayCommand]
        public async Task CreateNewConfig()
        {
            SelectedConfig = new Config();
        }
    }
}
