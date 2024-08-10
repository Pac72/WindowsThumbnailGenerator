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

        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
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

            Progress<float> progress = new(percentage => SetProgress(percentage));

            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken cancellationToken = cts.Token;

            Observable
                .Create<string>(observable =>
                {
                    var start = DateTimeOffset.Now;

                    return
                        Observable
                        .Interval(TimeSpan.FromSeconds(0.1))
                        .TakeUntil(aa => cts.IsCancellationRequested)
                        .Select(x => DateTimeOffset.Now.Subtract(start).ToString(@"hh\:mm\:ss"))
                        .DistinctUntilChanged()
                        .Subscribe(observable);
                })
                .SubscribeOn(TaskPoolScheduler.Default)
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(x => ElapsedLabel.Content = x);

                string targetFolder = TargetFolder.Text;
                int maxThumbCount = (int)MaxThumbCount.Value;
                int maxThreadsCount = (int)MaxThreadsCount.Value;
                bool recursive = RecursiveChk.IsChecked.GetValueOrDefault();
                bool clean = CleanChk.IsChecked.GetValueOrDefault();
                bool skipExisting = SkipExistingChk.IsChecked.GetValueOrDefault();
                bool useShort = UseShortChk.IsChecked.GetValueOrDefault();
                bool stacked = StackedChk.IsChecked.GetValueOrDefault();
                long elapsedMillis = await Task.Run(() => ProcessHandler.GenerateThumbnailsForFolder(
                progress,
                    targetFolder,
                    maxThumbCount,
                    maxThreadsCount,
                    recursive,
                    clean,
                    skipExisting,
                    useShort,
                    stacked
                ));

            cts.Cancel();
                SetLastDurationTime(TargetFolder.Text, elapsedMillis);
            }
            finally
            {
            EnableControls();
        }
        }

        private void CleanChk_Checked(object sender, RoutedEventArgs e)
        {
            _ = ModernWpf.MessageBox.Show("Choosing this option will restart explorer!\nSave your work before proceeding!", "Warning!", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }

        public void EnableControls()
        {
            StartBtn.IsEnabled = true;
            StartBtn.Visibility = Visibility.Visible;
            CurrentProgress.Visibility = Visibility.Hidden;
            ProgressLabel.Visibility = Visibility.Hidden;
            CurrentProgress.Value = 0;
            ProgressLabel.Content = "0%";

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
            StartBtn.IsEnabled = false;
            StartBtn.Visibility = Visibility.Hidden;
            CurrentProgress.Visibility = Visibility.Visible;
            ProgressLabel.Visibility = Visibility.Visible;

            TargetFolder.IsEnabled = false;
            BrowseBtn.IsEnabled = false;
            RecursiveChk.IsEnabled = false;
            CleanChk.IsEnabled = false;
            SkipExistingChk.IsEnabled = false;
            UseShortChk.IsEnabled = false;
            MaxThumbCount.IsEnabled = false;
            MaxThreadsCount.IsEnabled = false;
        }

        public void SetProgress(float percentage)
        {
            CurrentProgress.Value = percentage;
            ProgressLabel.Content = string.Format("{0:0.##}", percentage) + "%";
        }

        public void SetLastDurationTime(string path, long elapsedMillis)
        {
            StatusLbl.Text = "Last run duration: " + TimeSpan.FromMilliseconds(elapsedMillis).ToString(@"hh\:mm\:ss\.fff") + " (path " + path + ")";
        }

        public void ResetProgress()
        {
            CurrentProgress.Value = 0;
            ProgressLabel.Content = "Initializing...";
            ElapsedLabel.Content = "00:00:00";
        }
    }
}
