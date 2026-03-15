using CommunityToolkit.Mvvm.ComponentModel;

namespace OnAirCut.RenderServer.Models;

public partial class FolderValidationItem : ObservableObject
{
    [ObservableProperty]
    private string _folderName = "";

    [ObservableProperty]
    private bool _exists;
}
