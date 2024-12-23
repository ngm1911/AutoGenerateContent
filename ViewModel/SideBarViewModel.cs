﻿using AutoGenerateContent.DatabaseContext;
using AutoGenerateContent.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace AutoGenerateContent.ViewModel
{
    public partial class SideBarViewModel : ObservableObject
    {
        private readonly SQLiteContext _context;

        [ObservableProperty]
        ObservableCollection<KeyValuePair<int, string>> configs;

        [ObservableProperty]
        int selectedConfigId;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanModified))]
        [NotifyCanExecuteChangedFor(nameof(DeleteConfigCommand))]
        [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
        Config selectedConfig;

        public SideBarViewModel(SQLiteContext context)
        {
            _context = context;
            LoadConfigs();
        }

        partial void OnSelectedConfigChanged(Config value)
        {
            if (value != null)
            {
                value.IsChanged = false;

                value.PropertyChanged -= Value_PropertyChanged;
                value.PropertyChanged += Value_PropertyChanged;

                void Value_PropertyChanged(object? sender, PropertyChangedEventArgs e)
                {
                    SaveConfigCommand.NotifyCanExecuteChanged();
                }
            }
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
                                             }, TaskContinuationOptions.ExecuteSynchronously);
        }

        private bool canSaveClick => SelectedConfig.IsChanged;

        [RelayCommand(CanExecute = nameof(canSaveClick))]
        private async Task SaveConfig()
        {
            var recentId = SelectedConfig.Id;
            if (SelectedConfig.Id == -1)
            {
                var newConfig = new Config()
                {
                    Name = SelectedConfig.Name,
                    SearchText = SelectedConfig.SearchText,
                    NumberUrls = SelectedConfig.NumberUrls,
                    PromptIntro = SelectedConfig.PromptIntro,
                    PromptText = SelectedConfig.PromptText,
                    PromptAskNewContent = SelectedConfig.PromptAskNewContent,
                    SearchImageText = SelectedConfig.SearchImageText,
                    PromptTitle = SelectedConfig.PromptTitle,
                    PromptHeading = SelectedConfig.PromptHeading,
                };
                await _context.Configs.AddAsync(newConfig);
            }

            await _context.SaveChangesAsync();
            LoadConfigs(recentId);
        }

        private bool canDeleteClick => SelectedConfigId != -1;

        public bool CanModified => !canDeleteClick;

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

        private void LoadConfigs(int? recentId = null)
        {
            _context.Configs.Select(c => new KeyValuePair<int, string>(c.Id, c.Name))
                            .ToListAsync()
                            .ContinueWith(t =>
                            {
                                SelectedConfigId = 0;
                                Configs = new ObservableCollection<KeyValuePair<int, string>>(t.Result);
                                Configs.Insert(0, new KeyValuePair<int, string>(-1, "[Create configs]"));
                                SelectedConfigId = recentId ?? Configs.LastOrDefault().Key;
                            }, TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}
