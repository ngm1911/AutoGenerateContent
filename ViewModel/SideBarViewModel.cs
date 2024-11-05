using AutoGenerateContent.DatabaseContext;
using AutoGenerateContent.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;

namespace AutoGenerateContent.ViewModel
{
    public partial class SideBarViewModel : ObservableObject
    {
        private readonly SQLiteContext _context;

        [ObservableProperty]
        ObservableCollection<KeyValuePair<int, string>> configs;

        [ObservableProperty]
        int selectedConfigId;

        private Config selectedConfig;

        public Config SelectedConfig
        {
            get => selectedConfig;
            set
            {
                if (selectedConfig != value)
                {
                    if (selectedConfig != null)
                        selectedConfig.PropertyChanged -= SelectedConfig_PropertyChanged;

                    selectedConfig = value;
                    OnPropertyChanged(nameof(SelectedConfig));

                    if (selectedConfig != null)
                        selectedConfig.PropertyChanged += SelectedConfig_PropertyChanged;
                }
            }
        }

        private void SelectedConfig_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            HasUsername = true;

            SaveConfigCommand.NotifyCanExecuteChanged();
        }

        public SideBarViewModel(SQLiteContext context)
        {
            _context = context; 
            LoadConfigs();
        }

        partial void OnSelectedConfigIdChanged(int value)
        {
            _context.Configs.FirstOrDefaultAsync(c => c.Id == SelectedConfigId)
                            .ContinueWith(t =>
                            {
                                SelectedConfig = t.Result ?? new Config()
                                {
                                    Id = -1
                                };

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    HasUsername = false;
                                    DeleteConfigCommand.NotifyCanExecuteChanged();
                                    SaveConfigCommand.NotifyCanExecuteChanged();
                                });
                            });
            
        }

        public bool canSaveClick => HasUsername;

        private bool HasUsername = false;

        [RelayCommand(CanExecute = nameof(canSaveClick))]
        private async Task SaveConfig()
        { 
            if (SelectedConfig.Id == -1)
            {
                var newConfig = new Config()
                {
                    Name = SelectedConfig.Name,
                    SearchText = SelectedConfig.SearchText,
                    PromptText = SelectedConfig.PromptText,
                    PromptComplete = SelectedConfig.PromptComplete,
                    SearchImageText = SelectedConfig.SearchImageText,
                };
                await _context.Configs.AddAsync(newConfig);
            }

            await _context.SaveChangesAsync();
            LoadConfigs();
        }

        private bool canDeleteClick() => selectedConfigId != null && selectedConfigId != -1;

        [RelayCommand(CanExecute = nameof(canDeleteClick))]
        public async Task DeleteConfig()
        {
            if (SelectedConfig.Id != -1)
            {
                _context.Configs.Remove(SelectedConfig);
                await _context.SaveChangesAsync();
            }
            LoadConfigs();
        }

        private void LoadConfigs()
        {
            _context.Configs.Select(c => new KeyValuePair<int, string>(c.Id, c.Name))
                            .ToListAsync()
                            .ContinueWith(t =>
                            {
                                SelectedConfigId = 0;
                                Configs = new ObservableCollection<KeyValuePair<int, string>>(t.Result);
                                Configs.Insert(0, new KeyValuePair<int, string>(-1, "Create New Configs"));
                                SelectedConfigId = -1;
                            });
        }
    }
}
