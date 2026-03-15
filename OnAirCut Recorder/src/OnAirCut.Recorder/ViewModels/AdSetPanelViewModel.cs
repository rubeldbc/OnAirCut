using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Models;
using Serilog;

namespace OnAirCut.Recorder.ViewModels;

public partial class AdSetPanelViewModel : ObservableObject
{
    private readonly IAdSetProvider _adSetProvider;

    public AdSetPanelViewModel(IAdSetProvider adSetProvider)
    {
        _adSetProvider = adSetProvider;
        _adSetProvider.AdSetsChanged += OnAdSetsChanged;
    }

    [ObservableProperty]
    private ObservableCollection<AdSetConfig> _availableAdSets = [];

    [ObservableProperty]
    private AdSetConfig? _selectedAdSet;

    [ObservableProperty]
    private bool _noAdSelected = true;

    public string? SelectedAdSetName => NoAdSelected ? null : SelectedAdSet?.Name;

    partial void OnSelectedAdSetChanged(AdSetConfig? value)
    {
        if (value is not null)
        {
            NoAdSelected = false;
        }
        OnPropertyChanged(nameof(SelectedAdSetName));
    }

    partial void OnNoAdSelectedChanged(bool value)
    {
        if (value)
        {
            SelectedAdSet = null;
        }
        OnPropertyChanged(nameof(SelectedAdSetName));
    }

    [RelayCommand]
    private void SelectAdSet(AdSetConfig? adSet)
    {
        SelectedAdSet = adSet;
    }

    [RelayCommand]
    public async Task LoadAdSetsAsync()
    {
        try
        {
            // Only show configured ad sets (with config.json) on the recording page
            var adSets = await _adSetProvider.GetAvailableAdSetsAsync();
            AvailableAdSets = new ObservableCollection<AdSetConfig>(adSets);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load ad sets");
        }
    }

    private async void OnAdSetsChanged(object? sender, EventArgs e)
    {
        try
        {
            await LoadAdSetsAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to refresh ad sets");
        }
    }
}
