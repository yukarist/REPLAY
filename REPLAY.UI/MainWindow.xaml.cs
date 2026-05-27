using Microsoft.VisualBasic.Logging;
using REPLAY.Core.Audio;
using REPLAY.Domain.Models;
using REPLAY.Infrastructure;
using REPLAY.Infrastructure.AI;
using REPLAY.Infrastructure.Database;
using REPLAY.UI.Commands;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms.Design;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using static REPLAY.UI.MainViewModel;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace REPLAY.UI
{
    public partial class MainWindow : Window
    {
        private readonly AudioService _audioService;
        private readonly MainViewModel _vm;

        private readonly Random _rand = new();
        private List<Rectangle> _bars;
        private DateTime _lastWaveUpdate = DateTime.Now;

        private IAiService _aiService;
        private SpeechRepository _repo = new();

        public MainWindow()
        {
            InitializeComponent();

            // =========================
            // 🔹 初期化
            // =========================
            _repo = new SpeechRepository();

            _audioService = new AudioService();
            _vm = new MainViewModel(_audioService, _repo);

            DataContext = _vm;

            _aiService = _vm.AiService;

            // =========================
            // 🔹 設定読み込み
            // =========================
            var settings = SettingsService.Load();

            _vm.ApiKey = settings.ApiKey;
            _vm.AiModeIndex = settings.AiModeIndex;
            _vm.LogKeepDays = settings.LogKeepDays;
            _vm.UpdateDates();

            _audioService.UpdateSettings(
                settings.ApiKey,
                (AiMode)settings.AiModeIndex
            );

            Console.WriteLine("🔥 起動時に設定反映");

            _audioService.OnLevelChanged += (level) =>
            {
                if ((DateTime.Now - _lastWaveUpdate).TotalMilliseconds < 50)
                    return;

                _lastWaveUpdate = DateTime.Now;

                Dispatcher.InvokeAsync(() =>
                {
                    AnimateBars(level);
                });
            };

            // =========================
            // 🔥 音声認識イベント（リアルタイムプレビュー＆確定・完成版）
            // =========================

            // ✨ 生ログ（確定前のうっすらプレビュー）イベントの処理
            _audioService.OnLiveText += (liveText) =>
            {
                if (string.IsNullOrWhiteSpace(liveText)) return;

                // UIスレッドで安全にViewModelのプロパティを更新する
                Dispatcher.Invoke(() =>
                {
                    // 💡 XAMLの {Binding LiveTextDisplay} にリアルタイムな生文字をセット！
                    _vm.LiveTextDisplay = liveText;
                });
            };

            // 🔥 音声認識イベント（確定版）
            _audioService.OnRecognized += async (raw, corrected) =>
            {
                // 🔹 ① 空白・短文チェック
                // corrected は AudioService ですでにAI補正済みなので、これを使います
                if (string.IsNullOrWhiteSpace(corrected) || corrected.Length < 2)
                    return;

                // UIスレッドで更新
                await Dispatcher.InvokeAsync(async () =>
                {
                    var log = new SpeechLog
                    {
                        Timestamp = DateTime.Now,
                        CorrectedText = corrected
                    };

                    // UI表示用リストに追加
                    _vm.Logs.Add(log);

                    // データベースに保存
                    _repo.Add(log);
                });
            };

            // =========================
            // 🔹 起動時ログ読み込み
            // =========================
            LoadLogs();

            // =========================
            // 🔹 自動スクロール
            // =========================
            _vm.OnLogAdded += () =>
            {
                if (!_vm.IsAutoScrollEnabled) return;

                Dispatcher.Invoke(() =>
                {
                    if (LogListBox.Items.Count > 0)
                    {
                        LogListBox.ScrollIntoView(LogListBox.Items[^1]);
                    }
                });
            };

            _vm.OnLogDeleted += () =>
            {
                if (!_vm.IsAutoScrollEnabled) return;

                Dispatcher.Invoke(() =>
                {
                    if (LogListBox.Items.Count > 0)
                    {
                        LogListBox.ScrollIntoView(LogListBox.Items[^1]);
                    }
                });
            };

            DataContext = _vm;

            // =========================
            // 🔹 マイク自動開始
            // =========================
            //if (_vm.SelectedDeviceIndex >= 0)
            // {
            // try
            // {
            // _audioService.Start(_vm.SelectedDeviceIndex);
            //}
            //        catch (Exception ex)
            // {
            // MessageBox.Show(
            //$"マイクの起動に失敗しました\n\n{ex.Message}",
            //"エラー",
            // MessageBoxButton.OK,
            //MessageBoxImage.Warning
            //        );
            //}
            //}

            _bars = new List<Rectangle>
{
    Bar1, Bar2, Bar3, Bar4, Bar5, Bar6, Bar7, Bar8
};

            _vm.OnLogDeleted += () =>
            {
                if (!_vm.IsAutoScrollEnabled) return;

                Dispatcher.Invoke(() =>
                {
                    if (LogListBox.Items.Count > 0)
                    {
                        LogListBox.ScrollIntoView(LogListBox.Items[^1]);
                    }
                });
            };
        }

        // 💡 MainWindow.xaml.cs 内
        private void LoadLogs()
        {
            try
            {
                var allLogs = _repo.GetAllLogs();

                _vm.Logs.Clear();
                foreach (var log in allLogs)
                {
                    _vm.Logs.Add(log);
                }

                // 🔥 ここで定義したメソッドを呼ぶ
                _vm.InitializeDates();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DBからのログ読み込み失敗: {ex.Message}");
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ExitConfirmWindow();
            dialog.Owner = this;

            dialog.ShowDialog();

            if (dialog.IsConfirmed)
            {
                Application.Current.Shutdown();
            }
        }

        // MainViewModel.cs に追加
        // MainViewModel.cs に追加
        // MainViewModel.cs に追加してください
        // MainViewModel.cs に以下のメソッドを追加してください

        public void UpdateDates() => _vm.InitializeDates();

        // ▼ 設定画面を開く
        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(_vm, SettingsMode.SettingsOnly);
            win.Owner = this;
            win.ShowDialog();
        }

        private void OpenCsv_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(_vm, SettingsMode.CsvOnly);
            win.Owner = this;
            win.ShowDialog();
        }

        private void OpenAi_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(_vm, SettingsMode.AiOnly);
            win.Owner = this;
            win.ShowDialog();
        }

        // ▼ 波形アニメーション
        private void AnimateBars(float level)
        {
            if (_bars == null || _bars.Count == 0)
                return;

            if (double.IsNaN(level) || level <= 0)
                return;

            level = (float)Math.Pow(level, 0.5);

            double boost = Math.Min(level * 10, 2.0);

            foreach (var bar in _bars)
            {
                double baseHeight = 6;
                double rand = _rand.NextDouble();

                int index = _bars.IndexOf(bar);

                double center = _bars.Count / 2.0;
                double centerBoost = 1.0 - Math.Abs(index - center) / center;

                centerBoost = Math.Pow(centerBoost, 1.2);

                double target = baseHeight + (rand * 80 * boost * centerBoost);

                // 🔥 ガード
                if (double.IsNaN(target) || double.IsInfinity(target))
                    target = baseHeight;

                if (boost < 0.05)
                    target = baseHeight + rand * 6;

                // 🔥 現在の高さを安全に取得
                double current = bar.ActualHeight;

                if (double.IsNaN(current) || current <= 0)
                    current = baseHeight;

                // 🔥 アニメーション
                var anim = new DoubleAnimation
                {
                    From = current,
                    To = target,
                    Duration = TimeSpan.FromMilliseconds(300)
                };

                bar.BeginAnimation(HeightProperty, anim);
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        // 💡 過去ログ表示から、今のリアルタイムなログ表示に画面を戻す処理
        private void ReturnToCurrent_Click(object sender, RoutedEventArgs e)
        {
            // 💡 修正：リストを無理やり書き換えるのではなく、SelectedDateを「今日」にするだけ！
            _vm.SelectedDate = DateTime.Now.ToString("M/d (ddd)");

            if (LogListBox.Items.Count > 0)
            {
                LogListBox.ScrollIntoView(LogListBox.Items[^1]);
            }
        }
    }
}