using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSX.Core.Enums;
using DSX.Core.Models;

namespace DSX.Core.ViewModels;

public partial class HelpCenterViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty]
    private ObservableCollection<FAQItem> _fAQItems = new()
    {
        new FAQItem { Question = "How do I connect my controller?", Answer = "Connect via USB, Bluetooth, or the official wireless adapter." },
        new FAQItem { Question = "How do I set up adaptive triggers?", Answer = "Navigate to the Adaptive Triggers tab and configure trigger modes." },
        new FAQItem { Question = "How do I create a virtual controller?", Answer = "Go to the Virtual Device tab and select the desired controller type." },
        new FAQItem { Question = "Why is my controller not detected?", Answer = "Ensure the controller is properly connected and drivers are installed." },
        new FAQItem { Question = "How do I import/export profiles?", Answer = "Use the Controller Mapping tab import/export buttons." },
        new FAQItem { Question = "What is HidHide?", Answer = "HidHide hides physical controllers from games so only virtual devices are seen." },
        new FAQItem { Question = "How do I enable audio haptics?", Answer = "Go to Haptics & Rumble, enable Audio Haptics, and select an audio device." },
        new FAQItem { Question = "How do I use per-game profiles?", Answer = "Navigate to Installed Games, scan for games, and assign profiles." }
    };

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<FAQItem> FilteredFAQ =>
        string.IsNullOrWhiteSpace(SearchText)
            ? FAQItems
            : new ObservableCollection<FAQItem>(
                FAQItems.Where(f =>
                    f.Question.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    f.Answer.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));

    public HelpCenterViewModel(MainViewModel main)
    {
        _main = main;
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredFAQ));
    }

    [RelayCommand]
    private void OpenDiscord()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://discord.gg/dualsensex",
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void OpenReddit()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://reddit.com/r/DualSenseX",
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void OpenYouTube()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://youtube.com/@DualSenseX",
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void OpenTroubleshooting()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://dualsensex.com/troubleshooting",
            UseShellExecute = true
        });
    }
}

public class FAQItem
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}
