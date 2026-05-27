using REPLAY.Infrastructure;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static REPLAY.UI.MainViewModel;
// ⚠️「using static System.Windows.Forms...」は不要（誤作動の元）なので削除しました

namespace REPLAY.UI // ⚠️これが無いとXAMLの「x:Class="REPLAY.UI.SettingsWindow"」と繋がりません！
{
    public partial class SettingsWindow : Window
    {
        private readonly MainViewModel _vm;
        private readonly SettingsMode _mode;

        public SettingsWindow(MainViewModel vm, SettingsMode mode = SettingsMode.All)
        {
            InitializeComponent();

            this.MouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                    DragMove();
            };

            _vm = vm;
            DataContext = _vm;
            _mode = mode;
            ApplyMode();

            // ▼ CSV保存時などはトースト（下からフワッと出る通知）を表示
            _vm.OnExportCompleted += ShowExportSuccess;
            _vm.OnExportFailed += ShowExportError;

            // ▼ AI要約が完了したらSummaryWindow（要約画面）を開く
            _vm.OnSummaryCompleted += ShowSummaryWindow;
            _vm.OnSummaryFailed += ShowSummaryError;

            Closed += (s, e) =>
            {
                // ▼ 画面を閉じる時に登録を解除
                _vm.OnExportCompleted -= ShowExportSuccess;
                _vm.OnExportFailed -= ShowExportError;
                _vm.OnSummaryCompleted -= ShowSummaryWindow;
                _vm.OnSummaryFailed += ShowSummaryError;
            };
        }

        private void ShowSuccess(string message)
        {
            ToastText.Foreground = Brushes.White;
            Toast.Background = new SolidColorBrush(Color.FromArgb(220, 0, 150, 200));
            ShowToast(message);
        }

        private void ShowError(string message)
        {
            ToastText.Foreground = Brushes.White;
            Toast.Background = new SolidColorBrush(Color.FromArgb(220, 200, 50, 50));
        }

        private void ShowSummaryError(string message)
        {
            ShowError(message); // ←赤トースト
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var vm = (MainViewModel)DataContext;

            SettingsService.Save(new AppSettings
            {
                ApiKey = vm.ApiKey,
                AiModeIndex = vm.AiModeIndex,
                MicThreshold = vm.MicThreshold,
                NoiseReduction = vm.NoiseReduction,
                SilenceTimeout = vm.SilenceTimeout,
                MaxSentenceLength = vm.MaxSentenceLength,

                LogKeepDays = vm.LogKeepDays

            });

            vm.UpdateAiService(); // ←これ🔥

            ShowSuccess("APIキーを保存しました");
        }

        private void ShowExportSuccess(string message)
        {
            ShowToast(message);
        }

        private void ShowExportError(string message)
        {
            ShowToast(message);
        }

        private void ShowToast(string message)
        {
            // TextBlockのテキストを書き換えてOpacityをアニメーション
            ToastText.Text = message ?? "";

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400))
            {
                BeginTime = TimeSpan.FromMilliseconds(800)
            };

            Storyboard.SetTarget(fadeIn, Toast);
            Storyboard.SetTarget(fadeOut, Toast);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));

            var sb = new Storyboard();
            sb.Children.Add(fadeIn);
            sb.Children.Add(fadeOut);
            sb.Begin();
        }

        private void ShowSummaryWindow(string text)
        {
            var win = new SummaryWindow(text);

            win.Owner = Application.Current.MainWindow; // ←ここに変更

            win.Show();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ApplyMode()
        {
            SettingsSection.Visibility = Visibility.Collapsed;
            CsvSection.Visibility = Visibility.Collapsed;
            AiSection.Visibility = Visibility.Collapsed;

            switch (_mode)
            {
                case SettingsMode.SettingsOnly:
                    SettingsSection.Visibility = Visibility.Visible;
                    break;

                case SettingsMode.CsvOnly:
                    CsvSection.Visibility = Visibility.Visible;
                    break;

                case SettingsMode.AiOnly:
                    AiSection.Visibility = Visibility.Visible;
                    break;

                default:
                    SettingsSection.Visibility = Visibility.Visible;
                    CsvSection.Visibility = Visibility.Visible;
                    AiSection.Visibility = Visibility.Visible;
                    break;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // ① イベントの繋がりを解除（メモリリークを防ぐ！）
            if (_vm != null)
            {
                _vm.OnExportCompleted -= ShowExportSuccess;
                _vm.OnExportFailed -= ShowExportError;
                _vm.OnSummaryCompleted -= ShowSummaryWindow;
                _vm.OnSummaryFailed -= ShowSummaryError;
            }

            // ② もしストーリーボードが動いていればここで止める
            // （※もしToastという名前のBorderでアニメーションしているなら、以下のように書きます）
            Toast.BeginAnimation(OpacityProperty, null);

            base.OnClosed(e);
        }
    }
}