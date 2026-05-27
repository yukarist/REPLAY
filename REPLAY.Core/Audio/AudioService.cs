using NAudio.Wave;
using REPLAY.Infrastructure;
using REPLAY.Infrastructure.AI;
using REPLAY.Infrastructure.Audio;
using System;
using System.IO;
using System.Threading.Tasks;

namespace REPLAY.Core.Audio
{
    public class AudioService
    {
        private readonly AudioCaptureService _capture = new();
        private readonly WhisperService _whisper = new();

        private GeminiService _ai;
        private AiMode _mode = AiMode.Off;

        // UIの音量バー表示用イベント
        public event Action<float> OnLevelChanged;

        // ★ MainWindowが購読して画面にログを出す最重要イベント (生テキスト, 補正テキスト)
        public event Action<string, string> OnRecognized;
        public event Action<string>? OnLiveText;
        public event Action OnStreamStopped;

        private DateTime _lastSpeechTime = DateTime.Now;

        private IAiService _aiService;

        public void SetAiService(IAiService ai)
        {
            _aiService = ai;
        }

        public AudioService()
        {
            // 💡 解決の鍵：キャプチャサービスからの音声通知イベントをここで購読！
            _capture.OnAudioChunkReady += async (audioBytes) => await ProcessAudioChunkAsync(audioBytes);

            // 音量バーのイベントをそのまま上流へ中継
            _capture.OnLevelChanged += (level) => OnLevelChanged?.Invoke(level);
        }

        public void UpdateSettings(string apiKey, AiMode mode)
        {
            _mode = mode;

            if (!string.IsNullOrEmpty(apiKey))
            {
                _ai = new GeminiService(apiKey);
            }
        }

        public void Start(int deviceIndex)
        {
            if (deviceIndex < 0 || deviceIndex >= WaveInEvent.DeviceCount)
            {
                deviceIndex = 0; // fallback
            }

            _capture.SetDevice(deviceIndex);
            _capture.Start();
        }

        public void Stop()
        {
            _capture.Stop();
            OnStreamStopped?.Invoke();
        }

        public void ChangeDevice(int deviceIndex)
        {
            if (_capture == null) return;

            _capture.Stop();
            _capture.SetDevice(deviceIndex);
            _capture.Start();
        }

        private string _lastRaw = "";
        private DateTime _lastUpdateTime = DateTime.Now;

        private int _silenceTimeout = 1200;
        private int _maxSentenceLength = 25;

        public void SetSilenceTimeout(int value)
        {
            _silenceTimeout = value;
        }

        public void SetMaxSentenceLength(int value)
        {
            _maxSentenceLength = value;
        }

        private bool _removeFillerEnabled = true;

        public void SetRemoveFiller(bool value)
        {
            _removeFillerEnabled = value;
        }

        private int _fillerLevel = 1;

        public void SetFillerLevel(int level)
        {
            _fillerLevel = level;
        }

        // =========================
        // 🔹 重複除去
        // =========================
        private string RemoveDuplicateTail(string text)
        {
            for (int i = text.Length / 2; i > 5; i--)
            {
                var head = text.Substring(0, i);
                var tail = text.Substring(text.Length - i);

                if (head == tail)
                    return head;
            }
            return text;
        }

        // =========================
        // 🔹 フィラー除去（確定時のみ使う）
        // =========================
        private string CleanText(string text)
        {
            if (_fillerLevel > 0)
            {
                // えー系
                text = System.Text.RegularExpressions.Regex.Replace(
                    text,
                    @"えー+|えーと|えーっと|えっと",
                    ""
                );

                // 安全
                string[] safe =
                {
            "あのー", "そのー", "うーん", "いやー"
        };

                foreach (var f in safe)
                    text = text.Replace(f, "");

                // 強モード
                if (_fillerLevel >= 2)
                {
                    string[] strong =
                    {
                "なんか", "まあ", "ちょっと"
            };

                    foreach (var f in strong)
                        text = text.Replace(f, "");
                }

                // ▼ 句読点補正
                if (!text.EndsWith("。") &&
    !text.EndsWith("！") &&
    !text.EndsWith("？"))
                {
                    if (text.Length >= 15) // ←ここ伸ばす
                    {
                        text += "。";
                    }
                }

                // ▼ 改行調整
                if (text.Length > 20)
                {
                    int split = text.Length / 2;

                    // いい感じの区切り探す
                    int pos = text.LastIndexOf('、', split);
                    if (pos == -1)
                        pos = split;

                    text = text.Insert(pos + 1, "\n");
                }
            }

            // 空白整理
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

            return text;
        }

        private bool IsSimilar(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return false;

            // 完全包含
            if (a.Contains(b) || b.Contains(a))
                return true;

            // 前方一致（かなり重要）
            int min = Math.Min(a.Length, b.Length);
            int match = 0;

            for (int i = 0; i < min; i++)
            {
                if (a[i] == b[i])
                    match++;
                else
                    break;
            }

            // 7割以上一致なら同一扱い
            return (double)match / min > 0.7;
        }

        // =========================
        // 🔥 メイン処理
        // =========================
        private async Task ProcessAudioChunkAsync(byte[] audioData)
        {
            try
            {
                string finalText;

                // =========================
                // 🔹 無音確定
                // =========================
                bool IsIncomplete(string text)
                {
                    return text.EndsWith("て") ||
                           text.EndsWith("とっても") ||
                           text.EndsWith("けど") ||
                           text.EndsWith("が");
                }

                // ==========================================
                // 🛠️ AudioService.cs の音声認識・判定部分
                // ==========================================

                if (!string.IsNullOrEmpty(_lastRaw) &&
                    (DateTime.Now - _lastUpdateTime).TotalMilliseconds > _silenceTimeout)
                {
                    // 🔥 未完成 or 短い → 確定しないだけ（returnしない！）
                    if (_lastRaw.Length >= 20 && !IsIncomplete(_lastRaw))
                    {
                        _lastRaw = RemoveDuplicateTail(_lastRaw);

                        finalText = CleanText(_lastRaw);

                        if (!string.IsNullOrWhiteSpace(finalText))
                        {
                            await EmitFinalAsync(finalText);
                        }

                        _lastRaw = "";
                    }
                }

                System.Diagnostics.Debug.WriteLine($"audio size: {audioData.Length}");

                string recognizedText = await _whisper.TranscribeAsync(audioData);
                recognizedText = recognizedText?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(recognizedText))
                    return;

                System.Diagnostics.Debug.WriteLine("認識結果: " + recognizedText);

                if (recognizedText.Contains("(音楽)") || recognizedText.Contains("音楽"))
                    return;

                // ✨【追加】確定前のリアルタイムな生ログをUI（もうっすら表示用）に通知する
                OnLiveText?.Invoke(recognizedText);

                // =========================
                // 🔹 重複除去
                // =========================
                recognizedText = RemoveDuplicateTail(recognizedText);

                if (recognizedText.Length < 3)
                    return;

                _lastUpdateTime = DateTime.Now;

                // =========================
                // 🔹 初回
                // =========================
                if (string.IsNullOrEmpty(_lastRaw))
                {
                    _lastRaw = recognizedText;
                    return;
                }

                // =========================
                // 🔹 完全一致
                // =========================
                if (_lastRaw == recognizedText)
                    return;

                // =========================
                // 🔹 類似（←追加したやつ）
                // =========================
                if (IsSimilar(_lastRaw, recognizedText))
                {
                    // 🔥 逆流防止①：短くなったら無視
                    if (recognizedText.Length < _lastRaw.Length)
                        return;

                    // 🔥 逆流防止②：順序が崩れてたら無視
                    if (!recognizedText.StartsWith(_lastRaw) &&
                        !_lastRaw.StartsWith(recognizedText))
                        return;

                    _lastRaw = recognizedText;
                    return;
                }

                // =========================
                // 🔹 続き（自然な伸び）
                // =========================
                if (recognizedText.StartsWith(_lastRaw))
                {
                    _lastRaw = recognizedText;

                    // 句点で確定
                    if (_lastRaw.EndsWith("。") ||
                        _lastRaw.EndsWith("！") ||
                        _lastRaw.EndsWith("？"))
                    {
                        _lastRaw = RemoveDuplicateTail(_lastRaw);

                        finalText = CleanText(_lastRaw);

                        if (!string.IsNullOrWhiteSpace(finalText))
                        {
                            await EmitFinalAsync(finalText);
                        }

                        _lastRaw = "";
                        return;
                    }

                    // 長さで確定
                    if (_lastRaw.Length > _maxSentenceLength)
                    {
                        _lastRaw = RemoveDuplicateTail(_lastRaw);

                        finalText = CleanText(_lastRaw);

                        if (!string.IsNullOrWhiteSpace(finalText))
                        {
                            await EmitFinalAsync(finalText);
                        }

                        _lastRaw = "";
                        return;
                    }

                    return;
                }

                // =========================
                // 🔥 別文 → 前を確定
                // =========================

                // 続きの可能性があるならスキップ
                if (recognizedText.Length < _lastRaw.Length ||
                    IsSimilar(_lastRaw, recognizedText))
                {
                    _lastRaw = recognizedText;
                    return;
                }

                // 本当に別文
                _lastRaw = RemoveDuplicateTail(_lastRaw);

                finalText = CleanText(_lastRaw);

                if (!string.IsNullOrWhiteSpace(finalText))
                {
                    await EmitFinalAsync(finalText);
                }

                _lastRaw = recognizedText;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"音声処理エラー: {ex.Message}");
            }
        }


        public void SetNoiseReduction(float value)
        {
            _capture.SetNoiseReduction(value);
        }

        public void SetMicThreshold(float value)
        {
            _capture.SetThreshold(value);
        }

        private async Task EmitFinalAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 3)
                return;

            string correctedText = text;

            // =========================
            // 🔥 修正点：UI（ViewModel）から渡された AI に全て任せる！
            // _mode の分岐や new OllamaService() はもう不要です。
            // =========================
            if (_aiService != null)
            {
                try
                {
                    correctedText = await _aiService.CorrectAsync(text);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AI補正エラー]: {ex.Message}");
                    correctedText = text; // エラー時は元のテキストを維持して処理を止めない
                }
            }

            if (string.IsNullOrWhiteSpace(correctedText) ||
                correctedText.Contains("エラー") ||
                correctedText.Contains("404"))
            {
                correctedText = text;
            }

            // =========================
            // 🔥 ゆるいフィルター（まず動かす）
            // =========================

            if (string.IsNullOrWhiteSpace(correctedText))
                return;

            // これだけにする👇
            if (correctedText.Trim().Length < 4)
                return;

            // =========================

            OnRecognized?.Invoke(text, correctedText);
        }
    }
}