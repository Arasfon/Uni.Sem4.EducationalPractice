using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts;

using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.OdeSolvers;
using MathNet.Numerics.RootFinding;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using MoreLinq;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using SkiaSharp;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Windows.Storage;
using Windows.Storage.Pickers;

using WinRT.Interop;

using WinUIEx;

using Window = Microsoft.UI.Xaml.Window;

namespace Uni.EducationalPractice4;

public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

    private CancellationTokenSource? _currentTaskCts;
    private List<Point>? _allCycloidalOdePoints;
    private List<Point>? _allLinearOdePoints;

    private int _stepsBetweenWholeNumbers = 100;
    private int _iterationLimit = 10000;
    private double _cycloidalPath;
    private double _cycloidalTime;
    private double _linearPath;
    private double _linearTime;
    private bool _isCalculationInProgress;
    private bool _hasCalculationErrors;
    private ObservableCollection<ISeries> _chartSeries = [];
    private bool _isExportInProgress;

    public int StepsBetweenWholeNumbers
    {
        get => _stepsBetweenWholeNumbers;
        set => SetField(ref _stepsBetweenWholeNumbers, value);
    }

    public int IterationLimit
    {
        get => _iterationLimit;
        set => SetField(ref _iterationLimit, value);
    }

    public double CycloidalPath
    {
        get => _cycloidalPath;
        set
        {
            SetField(ref _cycloidalPath, value);
            OnPropertyChanged(nameof(PathDifference));
        }
    }

    public double CycloidalTime
    {
        get => _cycloidalTime;
        set => SetField(ref _cycloidalTime, value);
    }

    public double LinearPath
    {
        get => _linearPath;
        set
        {
            SetField(ref _linearPath, value);
            OnPropertyChanged(nameof(PathDifference));
        }
    }

    public double LinearTime
    {
        get => _linearTime;
        set => SetField(ref _linearTime, value);
    }

    public double PathDifference => LinearPath - CycloidalPath;

    public bool IsCalculationInProgress
    {
        get => _isCalculationInProgress;
        set => SetField(ref _isCalculationInProgress, value);
    }

    public bool HasCalculationErrors
    {
        get => _hasCalculationErrors;
        set => SetField(ref _hasCalculationErrors, value);
    }

    public bool IsExportInProgress
    {
        get => _isExportInProgress;
        set => SetField(ref _isExportInProgress, value);
    }

    public ObservableCollection<ISeries> ChartSeries
    {
        get => _chartSeries;
        set => SetField(ref _chartSeries, value);
    }

    public ICartesianAxis[] XAxes =>
    [
        new Axis
        {
            Name = "t, с",
            CrosshairLabelsBackground = SKColors.DarkOrange.AsLvcColor(),
            CrosshairLabelsPaint = new SolidColorPaint(SKColors.DarkRed, 1),
            CrosshairPaint = new SolidColorPaint(SKColors.DarkOrange, 1)
        }
    ];

    public ICartesianAxis[] YAxes =>
    [
        new Axis
        {
            Name = "S(t), м",
            CrosshairLabelsBackground = SKColors.DarkOrange.AsLvcColor(),
            CrosshairLabelsPaint = new SolidColorPaint(SKColors.DarkRed, 1),
            CrosshairPaint = new SolidColorPaint(SKColors.DarkOrange, 1)
        }
    ];

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        WindowManager windowManager = WindowManager.Get(this);
        windowManager.MinHeight = 700;
        windowManager.MinWidth = 700;

        CartesianChart.LegendTextPaint = new SolidColorPaint(new SKColor(255, 255, 255));
    }

    private async void ParameterNumberBox_OnValueChanged(NumberBox numberBox, NumberBoxValueChangedEventArgs args)
    {
        await Task.Delay(100); // Handle race condition between property value change and ValueChanged event

        const double g = 9.81;

        double a = ANumberBox.Value;

        if (a <= 0)
            return;

        if (_currentTaskCts is not null)
            await _currentTaskCts.CancelAsync();

        CancellationTokenSource cancellationTokenSource = new();
        _currentTaskCts = cancellationTokenSource;

        IsCalculationInProgress = true;
        HasCalculationErrors = false;

        CycloidalPath = 8 * a;
        LinearPath = Math.Sqrt(Math.Pow(2 * a, 2) + Math.Pow(Math.PI * a, 2));

        try
        {
            ((CycloidalTime, _), _allCycloidalOdePoints) =
                await FindXByYFromOdeApproximating(CycloidalPath, CycloidalOdeFunc, IterationLimit, StepsBetweenWholeNumbers,
                    cancellationTokenSource.Token);

            ChartSeries.Clear();

            if (_allCycloidalOdePoints is not null)
            {
                ChartSeries.Add(new LineSeries<Point>
                {
                    Values = _allCycloidalOdePoints.TakeEvery(Math.Max(1, _allCycloidalOdePoints.Count / 100)),
                    GeometryFill = null,
                    GeometryStroke = null,
                    Name = "Путь по циклоиде"
                });
            }
            else
                HasCalculationErrors = true;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            ((LinearTime, _), _allLinearOdePoints) =
                await FindXByYFromOdeApproximating(LinearPath, LinearOdeFunc, IterationLimit, StepsBetweenWholeNumbers,
                    cancellationTokenSource.Token);

            if (_allLinearOdePoints is not null)
            {
                ChartSeries.Add(new LineSeries<Point>
                {
                    Values = _allLinearOdePoints.TakeEvery(Math.Max(1, _allLinearOdePoints.Count / 100)),
                    GeometryFill = null,
                    GeometryStroke = null,
                    Name = "Путь по прямой"
                });
            }
            else
                HasCalculationErrors = true;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _currentTaskCts = null;
        IsCalculationInProgress = false;

        Vector<double> CycloidalOdeFunc(double t, Vector<double> y) =>
            Vector<double>.Build.DenseOfArray([y[1], (4 * a * g - g * y[0]) / (4 * a)]);

        Vector<double> LinearOdeFunc(double t, Vector<double> y) =>
            Vector<double>.Build.DenseOfArray([y[1], 2 / Math.Sqrt(4 + Math.PI * Math.PI)]);
    }

    /// <summary>Finds X by Y from ODE using Runge-Kutta fourth order method, second order polynomial approximation and Brent's root finding method</summary>
    /// <param name="targetY">Y for which X should be found</param>
    /// <param name="func">ODE function</param>
    /// <param name="iterationsLimit">Limit of iterations (max X)</param>
    /// <param name="stepsBetweenWholeNumbers">Count of steps between two whole numbers</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search result</returns>
    private static async Task<OdeRootSearchResult> FindXByYFromOdeApproximating(double targetY,
        Func<double, Vector<double>, Vector<double>> func, int iterationsLimit = 10000, int stepsBetweenWholeNumbers = 100,
        CancellationToken cancellationToken = default) =>
        await Task.Run(() =>
            {
                // We're moving in batches of two offsetting each time by one, i.e. [0, 2], [1, 3], etc.

                int upperRangeLimit = 2;
                // Step is constant throughout one calculation
                // Examples assume that stepsBetweenWholeNumbers is 100.
                double step = 1.0 / stepsBetweenWholeNumbers;

                List<double> xValues = [];
                List<double> yValues = [];

                Vector<double> initialValues = Vector<double>.Build.DenseOfArray([0.0, 0.0]);

                cancellationToken.ThrowIfCancellationRequested();

                while (upperRangeLimit < iterationsLimit)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int lowerRangeLimit = upperRangeLimit - 2;

                    // Calculating what times and paths we need to store.
                    // For the first iteration we're storing all 2 * 100 points,
                    // later we're storing only last 100 points as we're moving by one to the left every iteration
                    int recordingXStart = lowerRangeLimit * stepsBetweenWholeNumbers;
                    int recordingXAmount = 2 * stepsBetweenWholeNumbers;

                    if (lowerRangeLimit != 0)
                    {
                        // Skipping first 100
                        recordingXStart += stepsBetweenWholeNumbers;
                        recordingXAmount -= stepsBetweenWholeNumbers;
                    }

                    Vector<double>[] resultPoints = RungeKutta.FourthOrder(initialValues, lowerRangeLimit, upperRangeLimit,
                        2 * stepsBetweenWholeNumbers, func);

                    xValues.AddRange(Enumerable.Range(recordingXStart, recordingXAmount).Select(x => x * step));
                    // Here we take 100 if recordingTimeAmount is 100, else 200
                    yValues.AddRange(resultPoints.Skip(Math.Abs(recordingXAmount - 2 * stepsBetweenWholeNumbers)).Select(x => x[0]));

                    if (!(resultPoints.Min(x => x[0]) - 1e-3 <= targetY) || !(targetY <= resultPoints.Max(x => x[0]) + 1e-3))
                    {
                        upperRangeLimit++;
                        // First point of second 100 points is the first for next iteration
                        initialValues = resultPoints[stepsBetweenWholeNumbers];
                        continue;
                    }

                    Polynomial polynomial = Polynomial.Fit(xValues.ToArray(), yValues.ToArray(), 2);

                    List<Point> odeSolutionPoints = xValues.Zip(yValues)
                        .Select(x => new Point(x.First, x.Second))
                        .ToList();

                    cancellationToken.ThrowIfCancellationRequested();

                    return new OdeRootSearchResult(new Point(
                            Brent.FindRootExpand(DiffFunc, lowerRangeLimit, upperRangeLimit, maxIterations: 1000),
                            targetY),
                        odeSolutionPoints);

                    double DiffFunc(double t) =>
                        polynomial.Evaluate(t) - targetY;
                }

                return new OdeRootSearchResult(new Point(Double.NaN, targetY), null);
            }, cancellationToken)
            .ConfigureAwait(false);

    private async void ShowTaskInfoButton_Click(object sender, RoutedEventArgs e) =>
        await new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Title = "Информация о решаемой задаче",
            CloseButtonText = "Ок",
            Content = new TaskInfoDialogContentPage()
        }.ShowAsync();

    private async void TxtExportMenuItem_Click(object sender, RoutedEventArgs e)
    {
        IsExportInProgress = true;

        FileSavePicker fileSavePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "path_data",
            FileTypeChoices = { ["Текстовый файл"] = [".txt"] }
        };
        InitializeWithWindow.Initialize(fileSavePicker, WindowNative.GetWindowHandle(App.MainWindow));

        StorageFile? selectedFile = await fileSavePicker.PickSaveFileAsync();

        if (selectedFile is null)
        {
            IsExportInProgress = false;
            return;
        }

        await using Stream stream = await selectedFile.OpenStreamForWriteAsync();

        stream.SetLength(0);

        await using StreamWriter streamWriter = new(stream);

        await streamWriter.WriteLineAsync("Движение по циклоиде");
        await streamWriter.WriteLineAsync($"Время: {CycloidalTime}");
        await streamWriter.WriteLineAsync($"Путь: {CycloidalPath}");
        await streamWriter.WriteLineAsync("Точки:");

        if (_allCycloidalOdePoints is not null)
        {
            foreach (Point point in _allCycloidalOdePoints)
                await streamWriter.WriteLineAsync($"X: {point.X}; Y: {point.Y}");
        }

        await streamWriter.WriteLineAsync();
        await streamWriter.WriteLineAsync("Движение по прямой");
        await streamWriter.WriteLineAsync($"Время: {LinearTime}");
        await streamWriter.WriteLineAsync($"Путь: {LinearPath}");
        await streamWriter.WriteLineAsync("Точки:");

        if (_allLinearOdePoints is not null)
        {
            foreach (Point point in _allLinearOdePoints)
                await streamWriter.WriteLineAsync($"X: {point.X}; Y: {point.Y}");
        }

        await streamWriter.WriteLineAsync();
        await streamWriter.WriteLineAsync($"Разница между длиной пути: {PathDifference}");

        IsExportInProgress = false;
    }

    private async void JsonExportMenuItem_Click(object sender, RoutedEventArgs e)
    {
        IsExportInProgress = true;

        FileSavePicker fileSavePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "path_data",
            FileTypeChoices = { ["JSON"] = [".json"] }
        };
        InitializeWithWindow.Initialize(fileSavePicker, WindowNative.GetWindowHandle(App.MainWindow));

        StorageFile? selectedFile = await fileSavePicker.PickSaveFileAsync();

        if (selectedFile is null)
        {
            IsExportInProgress = false;
            return;
        }

        await using Stream stream = await selectedFile.OpenStreamForWriteAsync();

        stream.SetLength(0);

        await JsonSerializer.SerializeAsync(stream, new
        {
            cycloidal = new
            {
                time = CycloidalTime,
                path = CycloidalPath,
                points = _allCycloidalOdePoints
            },
            linear = new
            {
                time = LinearTime,
                path = LinearPath,
                points = _allLinearOdePoints
            },
            pathDifference = PathDifference
        }, DefaultJsonSerializerOptions);

        IsExportInProgress = false;
    }

    private async void PdfExportMenuItem_Click(object sender, RoutedEventArgs e)
    {
        IsExportInProgress = true;

        FileSavePicker fileSavePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "path_data",
            FileTypeChoices = { ["PDF"] = [".pdf"] }
        };
        InitializeWithWindow.Initialize(fileSavePicker, WindowNative.GetWindowHandle(App.MainWindow));

        StorageFile? selectedFile = await fileSavePicker.PickSaveFileAsync();

        if (selectedFile is null)
        {
            IsExportInProgress = false;
            return;
        }

        await using Stream stream = await selectedFile.OpenStreamForWriteAsync();

        stream.SetLength(0);

        Document document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(2, Unit.Centimetre);
                page.Content()
                    .Column(column =>
                    {
                        column.Spacing(10);

                        column.Item().Text("Движение по циклоиде").FontSize(16).Bold();
                        column.Item().Text($"Время: {CycloidalTime}");
                        column.Item().Text($"Путь: {CycloidalPath}");

                        column.Item().Text("");
                        column.Item().Text("Движение по прямой").FontSize(16).Bold();
                        column.Item().Text($"Время: {LinearTime}");
                        column.Item().Text($"Путь: {LinearPath}");

                        column.Item().Text("");
                        column.Item().Text($"Разница между прямым путём и путём по циклоиде: {PathDifference}");

                        using MemoryStream memoryStream = new();
                        ExportChartToStream(memoryStream, 900, 300, SKEncodedImageFormat.Png);
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        column.Item().Image(memoryStream);
                    });
            });
        });

        document.GeneratePdf(stream);

        IsExportInProgress = false;
    }

    private async void ChartExportButton_Click(object sender, RoutedEventArgs e)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = RootGrid.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Title = "Параметры экспорта",
            PrimaryButtonText = "Экспортировать",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
            CloseButtonText = "Отмена"
        };
        ChartExportDialogContentPage dialogContentPage = new(dialog);
        dialog.Content = dialogContentPage;

        ContentDialogResult result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return;

        IsExportInProgress = true;

        Debug.Assert(dialogContentPage.SelectedFile is not null);

        string fileExtensionWithoutDot = dialogContentPage.SelectedFile.FileType[1..];
        SKEncodedImageFormat imageFormat =
            Enum.Parse<SKEncodedImageFormat>(Char.ToUpperInvariant(fileExtensionWithoutDot[0]) + fileExtensionWithoutDot[1..]);

        // CachedFileManager throws ERROR_INTERNAL_ERROR HRESULT here if used, reason unknown

        try
        {
            await using (Stream stream = await dialogContentPage.SelectedFile.OpenStreamForWriteAsync())
            {
                stream.SetLength(0);

                ExportChartToStream(stream, dialogContentPage.ImageWidth, dialogContentPage.ImageHeight, imageFormat);
            }

            IsExportInProgress = false;
        }
        catch
        {
            IsExportInProgress = false;

            await new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Title = "Ошибка",
                Content = "Не удалось сохранить файл.",
                CloseButtonText = "Ок"
            }.ShowAsync();
        }
    }

    private void ExportChartToStream(Stream stream, int width, int height, SKEncodedImageFormat imageFormat)
    {
        List<ISeries> series = [];

        if (_allCycloidalOdePoints is not null)
        {
            series.Add(new LineSeries<Point>
            {
                Values = _allCycloidalOdePoints.TakeEvery(Math.Max(1, _allCycloidalOdePoints.Count / 100)),
                GeometryFill = null,
                GeometryStroke = null,
                Name = "Путь по циклоиде"
            });
        }

        if (_allLinearOdePoints is not null)
        {
            series.Add(new LineSeries<Point>
            {
                Values = _allLinearOdePoints.TakeEvery(Math.Max(1, _allLinearOdePoints.Count / 100)),
                GeometryFill = null,
                GeometryStroke = null,
                Name = "Путь по прямой"
            });
        }

        SKCartesianChart skCartesianChart = new()
        {
            Width = width,
            Height = height,
            XAxes = XAxes,
            YAxes = YAxes,
            Series = series,
            LegendPosition = LegendPosition.Right
        };

        skCartesianChart.SaveImage(stream, imageFormat, 100);
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
