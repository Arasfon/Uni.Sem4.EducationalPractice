using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;

using Microsoft.UI.Xaml;

using QuestPDF.Infrastructure;

using SkiaSharp;

namespace Uni.EducationalPractice4;

/// <summary>Provides application-specific behavior to supplement the default Application class.</summary>
public partial class App : Application
{
    public static Window MainWindow { get; private set; } = null!;

    /// <summary>Initializes the singleton application object.  This is the first line of authored code executed, and as such is the logical equivalent of main() or WinMain().</summary>
    public App() =>
        InitializeComponent();

    /// <summary>Invoked when the application is launched.</summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();

        QuestPDF.Settings.License = LicenseType.Community;

        LiveCharts.Configure(config =>
        {
            switch (RequestedTheme)
            {
                case ApplicationTheme.Light:
                {
                    config.AddLightTheme();
                    break;
                }
                case ApplicationTheme.Dark:
                {
                    config.AddDarkTheme();
                    break;
                }
            }

            config.HasGlobalSKTypeface(SKFontManager.Default.MatchCharacter('Ж'));
            config.HasMap<Point>((point, _) => new Coordinate(point.X, point.Y));
        });
    }
}
