using Ookii.Dialogs.Wpf;
using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Thumbnail_Generator_Library;

namespace Thumbnail_Generator_GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource cts = null;

        public MainWindow()
        {
            InitializeComponent();
            MaxThreadsCount.Maximum = Convert.ToInt32(Environment.ProcessorCount);
            MaxThreadsCount.Value = Convert.ToInt32(Environment.ProcessorCount);
        }

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog folderBrowser = new();
            if (!folderBrowser.ShowDialog().GetValueOrDefault())
            {
                return;
            }
            TargetFolder.Text = folderBrowser.SelectedPath;
        }

        private async void StartStopBtn_Click(object sender, RoutedEventArgs e)
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
                EnableControls();
                return;
            }
            cts = new CancellationTokenSource();
            DisableControls();
            ResetProgress();

            try
            {
                if (TargetFolder.Text.Length <= 0)
                {
                    _ = ModernWpf.MessageBox.Show("You didn't choose a folder!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                    EnableControls();
                    return;
                } else if (!Directory.Exists(TargetFolder.Text))
                {
                    _ = ModernWpf.MessageBox.Show("The directory you chose does not exist!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                    EnableControls();
                    return;
                }

                Progress<InitProgressInfo> initializationProgress = new(ipi => SetInitializationProgress(ipi));
                Progress<float> generationProgress = new(percentage => SetProgress(percentage));

                CancellationToken cancellationToken = cts.Token;

                Observable
                    .Create<string>(observable =>
                    {
                        var start = DateTimeOffset.Now;

                        return
                            Observable
                            .Interval(TimeSpan.FromSeconds(0.1))
                            .TakeUntil(aa => cancellationToken.IsCancellationRequested)
                            .Select(x => DateTimeOffset.Now.Subtract(start).ToString(@"hh\:mm\:ss"))
                            .DistinctUntilChanged()
                            .Subscribe(observable);
                    })
                    .SubscribeOn(TaskPoolScheduler.Default)
                    .ObserveOn(SynchronizationContext.Current)
                    .Subscribe(x => StatusLbl.Text = $"Generating... {x}");

                    string targetFolder = TargetFolder.Text;
                    int maxThumbCount = (int)MaxThumbCount.Value;
                    int maxThreadsCount = (int)MaxThreadsCount.Value;
                    bool recursive = RecursiveChk.IsChecked.GetValueOrDefault();
                    bool clean = CleanChk.IsChecked.GetValueOrDefault();
                    bool skipExisting = SkipExistingChk.IsChecked.GetValueOrDefault();
                    bool useShort = UseShortChk.IsChecked.GetValueOrDefault();
                    bool stacked = StackedChk.IsChecked.GetValueOrDefault();
                    long elapsedMillis = await Task.Run(() => ProcessHandler.GenerateThumbnailsForFolder(
                        initializationProgress,
                        generationProgress,
                        targetFolder,
                        maxThumbCount,
                        maxThreadsCount,
                        recursive,
                        clean,
                        skipExisting,
                        useShort,
                        stacked,
                        cancellationToken
                    ));

                SetLastDurationTime(TargetFolder.Text, elapsedMillis, PathHandler.ProgressInfo);
            } catch (OperationCanceledException)
            {
                SetCanceledMessage(TargetFolder.Text);
            }
            finally
            {
                cts?.Cancel();
                cts?.Dispose();
                EnableControls();
                cts = null;
            }
        }

        private void CleanChk_Checked(object sender, RoutedEventArgs e)
        {
            _ = ModernWpf.MessageBox.Show("Choosing this option will restart explorer!\nSave your work before proceeding!", "Warning!", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }

        public void EnableControls()
        {
            StartStopBtn.Content = "Start";
            CurrentProgress.Value = 0;
            ProgressLabel.Content = "";

            TargetFolder.IsEnabled = true;
            BrowseBtn.IsEnabled = true;
            RecursiveChk.IsEnabled = true;
            CleanChk.IsEnabled = true;
            SkipExistingChk.IsEnabled = true;
            UseShortChk.IsEnabled = true;
            MaxThumbCount.IsEnabled = true;
            MaxThreadsCount.IsEnabled = true;
        }

        public void DisableControls()
        {
            StartStopBtn.Content = "Stop";

            TargetFolder.IsEnabled = false;
            BrowseBtn.IsEnabled = false;
            RecursiveChk.IsEnabled = false;
            CleanChk.IsEnabled = false;
            SkipExistingChk.IsEnabled = false;
            UseShortChk.IsEnabled = false;
            MaxThumbCount.IsEnabled = false;
            MaxThreadsCount.IsEnabled = false;
        }

        private static long nextProgressLabelUpdateTicks = 0L;
        private const long TICKS_PER_MILLISECOND = 10000L;
        private const long PROGRESS_LABEL_DELTA_MILLIS = 100L * TICKS_PER_MILLISECOND;

        public void SetInitializationProgress(InitProgressInfo initProgressInfo)
        {
            long now = DateTime.Now.Ticks;
            if (now > nextProgressLabelUpdateTicks)
            {
                nextProgressLabelUpdateTicks = now + PROGRESS_LABEL_DELTA_MILLIS;
                ProgressLabel.Content = $"Initializing directory tree: max depth {initProgressInfo.MaxLevel} - {initProgressInfo.DirCount} directories";
            }
        }

        public void SetProgress(float percentage)
        {
            CurrentProgress.Value = percentage;
            ProgressLabel.Content = string.Format("{0:0.##}", percentage) + "%";
        }

        public void SetLastDurationTime(string path, long elapsedMillis, InitProgressInfo progressInfo)
        {
            StatusLbl.Text = $"Generation completed in {TimeSpan.FromMilliseconds(elapsedMillis).ToString(@"hh\:mm\:ss\.fff")} (path {path} - {progressInfo.DirCount} directories, max depth {progressInfo.MaxLevel})";
        }

        public void SetCanceledMessage(string path)
        {
            StatusLbl.Text = "Generation canceled (path " + path + ")";
        }

        public void ResetProgress()
        {
            CurrentProgress.Value = 0;
            ProgressLabel.Content = "Initializing...";
        }
    }
}
