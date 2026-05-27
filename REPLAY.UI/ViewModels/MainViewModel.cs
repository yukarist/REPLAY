using NAudio.Wave;
using REPLAY.Core.Audio;
using REPLAY.Domain.Models;
using REPLAY.Infrastructure;
using REPLAY.Infrastructure.AI;
using REPLAY.Infrastructure.Database;
using REPLAY.UI.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization; // 💡 重複を排除して1つに
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using static REPLAY.UI.MainWindow;

namespace REPLAY.UI
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<SpeechLog> Logs { get; set; } = new();
        public ObservableCollection<SpeechLog> FilteredLogs { get; set; } = new();
        public ObservableCollection<SpeechLog> SearchResults { get; set; } = new();
        public ObservableCollection<string> Devices { get; set; } = new();
        public ObservableCollection<SpeechLog> DateFilteredLogs { get; set; } = new();
        public ObservableCollection<string> Dates { get; set; } = new();

        public event PropertyChangedEventHandler PropertyChanged;

        // ▼ 各種イベント
        public event Action<SpeechLog> OnSearchResultSelected;
        public event Action<int> OnDeviceChanged;
        public event Action<bool> OnAiToggled;
        public event Action<string, bool> OnSettingsSaved;
        public event Action OnLogAdded;
        public event Action OnLogDeleted;
        public event Action<string> OnExportCompleted;
        public event Action<string> OnExportFailed;
        public event Action<string> OnSummaryCompleted;
        public event Action<string> OnSummaryFailed;

        private readonly AudioService _audioService;
        private readonly SpeechRepository _repo;
        private readonly OllamaService _ollama = new();
        private GeminiService _gemini;
        private IAiService _aiService = new OllamaService();
        private List<SpeechLog> _loadedLogsForDate = new();

        public IAiService AiService => _aiService;

        // ==========================================
        // 🔹 変更通知（Notification）付きプロパティ
        // ==========================================

        private bool _isSummarizing;
        public bool IsSummarizing
        {
            get => _isSummarizing;
            set
            {
                _isSummarizing = value;
                OnPropertyChanged(nameof(IsSummarizing));
            }
        }

        private string _summaryStatusText = string.Empty;
        public string SummaryStatusText
        {
            get => _summaryStatusText;
            set
            {
                _summaryStatusText = value;
                OnPropertyChanged(nameof(SummaryStatusText));
            }
        }

        private string _liveTextDisplay = string.Empty;
        public string LiveTextDisplay
        {
            get => _liveTextDisplay;

            set
            {
                if (_liveTextDisplay == value) return;
                _liveTextDisplay = value;
                OnPropertyChanged(nameof(LiveTextDisplay)); // 💡 共通メソッド呼び出しに統一
            }
        }

        private bool _isRaw;
        public bool IsRaw
        {
            get => _isRaw;
            set { _isRaw = value; OnPropertyChanged(nameof(IsRaw)); }
        }

        private int _logKeepDays = 7;
        public int LogKeepDays
        {
            get => _logKeepDays;
            set
            {
                if (_logKeepDays == value) return;
                _logKeepDays = value;
                OnPropertyChanged(nameof(LogKeepDays));
                SaveSettings(); // 値が変わったら自動保存
            }
        }

        private float _micThreshold = 0.08f;
        public float MicThreshold
        {
            get => _micThreshold;
            set
            {
                if (_micThreshold == value) return;
                _micThreshold = value;
                OnPropertyChanged(nameof(MicThreshold));
                _audioService?.SetMicThreshold(value);
                SaveSettings();
            }
        }

        private int _silenceTimeout = 1500;
        public int SilenceTimeout
        {
            get => _silenceTimeout;
            set
            {
                if (_silenceTimeout == value) return; // 🔥 超重要ガード
                _silenceTimeout = value;
                OnPropertyChanged(nameof(SilenceTimeout));
                _audioService?.SetSilenceTimeout(value);
            }
        }

        private int _maxSentenceLength = 25;
        public int MaxSentenceLength
        {
            get => _maxSentenceLength;
            set
            {
                if (_maxSentenceLength == value) return;
                _maxSentenceLength = value;
                OnPropertyChanged(nameof(MaxSentenceLength));
                _audioService?.SetMaxSentenceLength(value);
            }
        }

        private float _noiseReduction = 0.02f;
        public float NoiseReduction
        {
            get => _noiseReduction;
            set
            {
                if (_noiseReduction == value) return;
                _noiseReduction = value;
                OnPropertyChanged(nameof(NoiseReduction));
                _audioService?.SetNoiseReduction(_noiseReduction);
            }
        }

        private int _fillerLevel = 1;
        public int FillerLevel
        {
            get => _fillerLevel;
            set
            {
                if (_fillerLevel == value) return;
                _fillerLevel = value;
                OnPropertyChanged(nameof(FillerLevel));
                OnPropertyChanged(nameof(FillerLabel)); // 🔥 連動更新
                _audioService?.SetFillerLevel(value);
            }
        }

        public string FillerLabel => FillerLevel switch
        {
            0 => "OFF",
            1 => "弱",
            2 => "強",
            _ => ""
        };

        private string _activeButton = "";
        public string ActiveButton
        {
            get => _activeButton;
            set { _activeButton = value; OnPropertyChanged(nameof(ActiveButton)); }
        }

        private int _aiModeIndex;
        public int AiModeIndex
        {
            get => _aiModeIndex;
            set
            {
                if (_aiModeIndex == value) return;
                _aiModeIndex = value;
                OnPropertyChanged(nameof(AiModeIndex));
                IsAiEnabled = value != 0; // 💡 AIの有効/無効フラグを連動
                OnPropertyChanged(nameof(AiModeLabel));
                UpdateAiService();
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                ApplySearch(); // 🔥 リアルタイムフィルター
            }
        }

        private bool _isRecording;
        public bool IsRecording
        {
            get => _isRecording;
            set { _isRecording = value; OnPropertyChanged(nameof(IsRecording)); }
        }

        private bool _isAiEnabled;
        public bool IsAiEnabled
        {
            get => _isAiEnabled;
            set
            {
                if (_isAiEnabled == value) return;
                _isAiEnabled = value;
                OnPropertyChanged(nameof(IsAiEnabled));
                OnPropertyChanged(nameof(AiModeLabel));
                OnAiToggled?.Invoke(value);
                UpdateAiService();
            }
        }

        private bool _isAutoScrollEnabled = true;
        public bool IsAutoScrollEnabled
        {
            get => _isAutoScrollEnabled;
            set { _isAutoScrollEnabled = value; OnPropertyChanged(nameof(IsAutoScrollEnabled)); }
        }

        private bool _showFavoritesOnly;
        public bool ShowFavoritesOnly
        {
            get => _showFavoritesOnly;
            set
            {
                if (_showFavoritesOnly == value) return;
                _showFavoritesOnly = value;
                OnPropertyChanged(nameof(ShowFavoritesOnly));
                OnPropertyChanged(nameof(FavoriteFilterLabel));
                ApplySearch(); // 🔥 お気に入りフィルター即時適用
            }
        }

        public string FavoriteFilterLabel => ShowFavoritesOnly ? "★のみ表示中" : "すべて表示";

        private int _selectedDeviceIndex;
        public int SelectedDeviceIndex
        {
            get => _selectedDeviceIndex;
            set
            {
                if (_selectedDeviceIndex == value) return;
                _selectedDeviceIndex = value;
                OnPropertyChanged(nameof(SelectedDeviceIndex));

                // 録音中（配信中）にマイクが変更されたら、即座に切り替える
                if (IsRecording)
                {
                    _audioService?.ChangeDevice(value);
                }
            }
        }

        private string _ollamaResult = string.Empty;
        public string OllamaResult
        {
            get => _ollamaResult;
            set { _ollamaResult = value; OnPropertyChanged(nameof(OllamaResult)); }
        }

        private string _selectedDate = string.Empty;
        public string SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (_selectedDate == value) return;
                _selectedDate = value;
                OnPropertyChanged(nameof(SelectedDate));
                LoadLogsByDate(value);
                ApplySearch();
            }
        }

        private string _liveText = "";
        public string LiveText
        {
            get => _liveText;
            set
            {
                if (_liveText == value) return;
                _liveText = value;
                OnPropertyChanged(nameof(LiveText));
                OnPropertyChanged(nameof(LiveTextDisplay));
            }
        }

        private bool _removeFillerEnabled = true;
        public bool RemoveFillerEnabled
        {
            get => _removeFillerEnabled;
            set
            {
                if (_removeFillerEnabled == value) return;
                _removeFillerEnabled = value;
                _audioService?.SetRemoveFiller(value);
                OnPropertyChanged(nameof(RemoveFillerEnabled));
            }
        }

        private string _apiKey = "";
        public string ApiKey
        {
            get => _apiKey;
            set
            {
                if (_apiKey == value) return;
                _apiKey = value;
                OnPropertyChanged(nameof(ApiKey));
            }
        }

        // ==========================================
        // 🔹 コマンド (Commands)
        // ==========================================
        public ICommand ToggleFavoriteFilterCommand => new RelayCommand<object>(_ =>
        {
            ShowFavoritesOnly = !ShowFavoritesOnly;
        });

        public ICommand DeleteLogCommand => new RelayCommand<SpeechLog>(log =>
        {
            if (log == null) return;

            // 1. UI・メモリ上のコレクションから削除
            Logs.Remove(log);
            _loadedLogsForDate.Remove(log); // ✨【重要】この1行を追加！表示元からも消します

            // 2. 🔥 DBからも削除 
            _repo.Delete(log.Id);

            // 3. 日付再構築
            InitializeDates();

            // 4. 再フィルタ（これで画面が本当に綺麗になります）
            ApplySearch();
            OnLogDeleted?.Invoke();
        });

        public ICommand ToggleFavoriteCommand => new RelayCommand<SpeechLog>(log =>
        {
            if (log == null) return;
            log.IsFavorite = !log.IsFavorite;
            _repo.UpdateFavoriteStatus(log.Id, log.IsFavorite); // DB状態の同期
            ApplySearch();
        });

        public ICommand ExportAllCommand => new RelayCommand<object>(_ => {
            SaveToCsv(_repo.GetAllLogs(), "all_logs");
        });

        public ICommand ExportFavoritesCommand => new RelayCommand<object>(_ => {
            SaveToCsv(_repo.GetLogsByFilter(true), "favorites");
        });

        public ICommand ExportSelectedDateCommand => new RelayCommand<object>(_ => {
            if (DateTime.TryParse(SelectedDate, out DateTime dt))
            {
                SaveToCsv(_repo.GetLogsByDate(dt.ToString("yyyy-MM-dd")), $"logs_{dt:yyyyMMdd}");
            }
        });

        public ICommand GenerateSummaryCommand => new RelayCommand<object>(async _ =>
        {
            try
            {
                // 🌟 処理中状態にする
                IsSummarizing = true;
                SummaryStatusText = "🤖 AI要約を作成中しています...（数十秒かかる場合があります）";

                var result = await GenerateSummaryText();
                if (string.IsNullOrWhiteSpace(result))
                {
                    OnSummaryFailed?.Invoke("ログがありません");
                    return;
                }
                OnSummaryCompleted?.Invoke(result);
            }
            catch
            {
                OnSummaryFailed?.Invoke("要約に失敗しました");
            }
            finally
            {
                // 🌟 終わったら（成功でも失敗でも）必ず元に戻す
                IsSummarizing = false;
                SummaryStatusText = string.Empty;
            }
        }, _ => DateFilteredLogs.Any() && !IsSummarizing); // 💡 要約中はボタンを押せなくするガード

        public ICommand ExportSummaryCommand => new RelayCommand<object>(async _ =>
        {
            try
            {
                // 🌟 処理中状態にする
                IsSummarizing = true;
                SummaryStatusText = "💾 AI要約を生成してテキスト出力しています...";

                var result = await GenerateSummaryText();
                if (string.IsNullOrWhiteSpace(result))
                {
                    OnExportFailed?.Invoke("ログがありません");
                    return;
                }

                var folder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var path = Path.Combine(folder, $"summary_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(path, result, Encoding.UTF8);
                OnExportCompleted?.Invoke($"要約を保存しました\n{path}");
            }
            catch
            {
                OnExportFailed?.Invoke("要約の出力に失敗しました");
            }
            finally
            {
                // 🌟 元に戻す
                IsSummarizing = false;
                SummaryStatusText = string.Empty;
            }
        }, _ => !IsSummarizing); // 💡 要約中はボタンを押せなくするガード

        // ==========================================
        // ✨【修正】外枠を光らせる処理を追加したStart / Stopコマンド
        // ==========================================
        public ICommand StartCommand => new RelayCommand<object>(_ =>
        {
            if (!IsRecording)
            {
                IsRecording = true;
                ActiveButton = "Start"; // 🌟 これでStartボタンの外枠が光ります！

                // 🎙️ 録音を開始
                _audioService?.Start(SelectedDeviceIndex);
            }
        });

        public ICommand StopCommand => new RelayCommand<object>(_ =>
        {
            if (IsRecording)
            {
                IsRecording = false;
                ActiveButton = "Stop"; // 🌟 これでStopボタンの外枠が光ります！

                // 🛑 録音を停止
                _audioService?.Stop();

                // 📝 古いコードにあった「停止時に自動で要約を作る処理」もここで行います
                if (GenerateSummaryCommand.CanExecute(null))
                {
                    GenerateSummaryCommand.Execute(null);
                }
            }
        });

        // ==========================================
        // 🔹 コンストラクタ (Constructor)
        // ==========================================
        public MainViewModel(AudioService audioService, SpeechRepository repo)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            ArchiveLegacyLogs();
            LoadLogsFromDatabase();
            InitializeDates(); // 💡 起動時に確実に日付リストを構築

            // リアルタイムな日付変更検知タイマー
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMinutes(1);
            timer.Tick += (s, e) =>
            {
                var today = DateTime.Now.ToString("M/d (ddd)");
                if (!Dates.Contains(today))
                {
                    Dates.Insert(0, today);
                }
                if (SelectedDate != today)
                {
                    SelectedDate = today;
                }
            };
            timer.Start();

            SelectedDate = _repo.GetLatestDate();

            // 設定の復元ロード
            var settings = SettingsService.Load();
            ApiKey = settings.ApiKey;
            AiModeIndex = settings.AiModeIndex;
            MicThreshold = settings.MicThreshold;
            NoiseReduction = settings.NoiseReduction;
            SilenceTimeout = settings.SilenceTimeout;
            MaxSentenceLength = settings.MaxSentenceLength;

            UpdateAiService();
            DeleteOldLogs(LogKeepDays);

            // デバイス一覧取得
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var caps = WaveInEvent.GetCapabilities(i);
                Devices.Add(caps.ProductName);

                if (caps.ProductName.Contains("USB") || caps.ProductName.Contains("マイク"))
                {
                    SelectedDeviceIndex = i;
                }
            }
        }

        // ==========================================
        // 🔹 各種ロジックメソッド
        // ==========================================
        public void AddLog(string text)
        {
            var log = new SpeechLog
            {
                CorrectedText = text,
                Timestamp = DateTime.Now
            };

            log.Id = _repo.Add(log); // DB保存してID確定
            Logs.Add(log);

            var date = log.Timestamp.ToString("M/d (ddd)");
            if (!Dates.Contains(date))
            {
                Dates.Add(date);
                var sortedDates = Dates.OrderByDescending(x => x).ToList();
                Dates.Clear();
                foreach (var d in sortedDates) Dates.Add(d);
            }

            if (string.IsNullOrEmpty(SelectedDate))
            {
                SelectedDate = date;
            }

            if (SelectedDate == date)
            {
                DateFilteredLogs.Add(log);
                _loadedLogsForDate.Add(log);
            }

            ApplySearch();
            OnLogAdded?.Invoke();
        }

        public void InitializeDates()
        {
            Dates.Clear();
            var sortedDates = Logs
                .OrderByDescending(x => x.Timestamp)
                .Select(x => x.Timestamp.ToString("M/d (ddd)"))
                .Distinct()
                .ToList();

            foreach (var date in sortedDates)
            {
                Dates.Add(date);
            }
        }

        public void UpdateDates() => InitializeDates();

        private void ApplySearch()
        {
            var target = _loadedLogsForDate.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                target = target.Where(x => x.CorrectedText.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            if (ShowFavoritesOnly)
            {
                target = target.Where(x => x.IsFavorite);
            }

            DateFilteredLogs.Clear();
            foreach (var log in target)
            {
                DateFilteredLogs.Add(log);
            }

            if (!DateFilteredLogs.Any())
            {
                DateFilteredLogs.Add(new SpeechLog { CorrectedText = "該当するログがありません", Timestamp = DateTime.Now });
            }
        }

        private void LoadLogsFromDatabase()
        {
            var allLogs = _repo.GetAllLogs();
            _loadedLogsForDate.Clear();
            _loadedLogsForDate.AddRange(allLogs);

            // 💡 内部の同期用コレクション(Logs)にも反映
            Logs.Clear();
            foreach (var log in allLogs) Logs.Add(log);
        }

        private void SaveToCsv(List<SpeechLog> logs, string fileNamePrefix)
        {
            if (logs == null || !logs.Any())
            {
                OnExportFailed?.Invoke("対象ログがありません");
                return;
            }
            try
            {
                var folder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var path = Path.Combine(folder, $"{fileNamePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                var lines = new List<string> { "Time,Text" };
                lines.AddRange(logs.Select(x => $"{x.Timestamp:HH:mm:ss},\"{x.CorrectedText.Replace("\"", "\"\"")}\""));

                File.WriteAllLines(path, lines, Encoding.UTF8);
                OnExportCompleted?.Invoke($"出力完了: {path}");
            }
            catch (Exception ex)
            {
                OnExportFailed?.Invoke($"エクスポートに失敗しました: {ex.Message}");
            }
        }

        private async Task<string> GenerateSummaryText()
        {
            if (!DateTime.TryParse(SelectedDate, out DateTime dt))
                return "日付を選択してください";

            var allLogsForDate = _repo.GetLogsByDate(dt.ToString("yyyy-MM-dd"));

            var favoriteLogs = allLogsForDate
                .Where(x => x.IsFavorite)
                .OrderBy(x => x.Timestamp)
                .Select(x => x.CorrectedText)
                .Where(x => x.Length > 10)
                .Distinct()
                .Take(20)
                .ToList();

            var finalLogs = new List<string>(favoriteLogs);

            if (finalLogs.Count < 3)
            {
                var extraLogs = allLogsForDate
                    .Where(x => !x.IsFavorite)
                    .OrderBy(x => x.Timestamp)
                    .Select(x => x.CorrectedText)
                    .Where(x => x.Length > 10)
                    .Distinct()
                    .Take(5)
                    .ToList();

                finalLogs.AddRange(extraLogs);
            }

            if (finalLogs.Count < 3)
                return "要約するにはログが少なすぎます";

            var text = string.Join("\n", finalLogs);
            text = string.Join("\n", text.Split('\n')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x) && !x.Contains("(音楽)") && x.Length > 8));

            if (_aiService == null)
                return "AIがOFFです";

            var summary = await _aiService.SummarizeAsync(text, AiModeIndex);
            return $"【{SelectedDate} のまとめ】\n\n" + summary;
        }

        public void UpdateAiService()
        {
            if (!IsAiEnabled)
            {
                _aiService = null;
            }
            else
            {
                switch (AiModeIndex)
                {
                    case 0:
                        _aiService = null;
                        break;
                    case 1:
                        _aiService = _ollama;
                        break;
                    case 2:
                        if (!string.IsNullOrEmpty(ApiKey))
                        {
                            _gemini = new GeminiService(ApiKey);
                        }
                        _aiService = _gemini;
                        break;
                }
            }
            _audioService?.SetAiService(_aiService);
        }

        public string AiModeLabel
        {
            get
            {
                if (!IsAiEnabled) return "AI: OFF";
                return AiModeIndex switch
                {
                    1 => "AI: 軽い（高速）",
                    2 => "AI: 高精度（遅延あり）",
                    _ => "AI: OFF"
                };
            }
        }

        private void LoadLogsByDate(string date)
        {
            if (string.IsNullOrEmpty(date)) return;

            string searchDateString = DateTime.Today.ToString("yyyy-MM-dd");
            var datePart = date.Split(' ')[0];

            if (DateTime.TryParse($"{DateTime.Today.Year}/{datePart}", out DateTime parsedDate))
            {
                searchDateString = parsedDate.ToString("yyyy-MM-dd");
            }

            var logs = _repo.GetLogsByDate(searchDateString);
            _loadedLogsForDate = logs.ToList();
            ApplySearch();
        }

        private void DeleteOldLogs(int keepDays)
        {
            try
            {
                _repo.DeleteOldLogs(keepDays);
                System.Diagnostics.Debug.WriteLine($"🧹 データ管理：{keepDays}日より古いログをDBから削除しました。");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ ログ削除中にエラーが発生しました: {ex.Message}");
            }
        }

        private void ArchiveLegacyLogs()
        {
            string logDir = "logs";
            string backupDir = "logs_backup_old";

            if (Directory.Exists(logDir))
            {
                try
                {
                    if (Directory.Exists(backupDir)) return;
                    Directory.Move(logDir, backupDir);
                    System.Diagnostics.Debug.WriteLine("🧹 移行完了：旧logsフォルダを logs_backup_old に退避しました。");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 旧ログの退避に失敗しました: {ex.Message}");
                }
            }
        }

        private void SaveSettings()
        {
            SettingsService.Save(
                ApiKey,
                AiModeIndex,
                MicThreshold,
                NoiseReduction,
                SilenceTimeout,
                MaxSentenceLength,
                LogKeepDays // 👈 ✨ここにViewModelのLogKeepDaysを渡す！
            );
        }

        public void SelectLog(SpeechLog log)
        {
            OnSearchResultSelected?.Invoke(log);
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public enum SettingsMode
    {
        All,
        SettingsOnly,
        CsvOnly,
        AiOnly
    }

    public class OllamaResponse
    {
        [JsonPropertyName("model")]
        public string model { get; set; }

        [JsonPropertyName("created_at")]
        public string created_at { get; set; }

        [JsonPropertyName("response")]
        public string response { get; set; }

        [JsonPropertyName("done")]
        public bool done { get; set; }
    }
}