using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using Windows.Storage;
using Windows.Storage.Pickers;

using WinRT.Interop;

namespace Uni.EducationalPractice4;

public sealed partial class ChartExportDialogContentPage : Page, INotifyPropertyChanged
{
    private readonly ContentDialog _contentDialog;
    private string? _imagePath;
    private int _imageWidth = 800;
    private int _imageHeight = 300;

    public string? ImagePath
    {
        get => _imagePath;
        set => SetField(ref _imagePath, value);
    }

    public int ImageWidth
    {
        get => _imageWidth;
        set => SetField(ref _imageWidth, value);
    }

    public int ImageHeight
    {
        get => _imageHeight;
        set => SetField(ref _imageHeight, value);
    }

    public StorageFile? SelectedFile { get; private set; }

    public ChartExportDialogContentPage(ContentDialog contentDialog)
    {
        InitializeComponent();

        _contentDialog = contentDialog;
        _contentDialog.IsPrimaryButtonEnabled = false;
    }

    private async void BrowseFileButton_Click(object sender, RoutedEventArgs e)
    {
        FileSavePicker fileSavePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "chart",
            FileTypeChoices = { ["Изображение"] = [".png", ".jpg"] }
        };
        InitializeWithWindow.Initialize(fileSavePicker, WindowNative.GetWindowHandle(App.MainWindow));

        SelectedFile = await fileSavePicker.PickSaveFileAsync();

        if (SelectedFile is not null)
        {
            ImagePath = SelectedFile.Path;
            _contentDialog.IsPrimaryButtonEnabled = true;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
