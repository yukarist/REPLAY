using System.IO;
using System.Text.Json;
using System.Windows;

namespace REPLAY.Infrastructure;

public class AppSettings
{
    // 🔑 APIキー
    public string ApiKey { get; set; } = "";

    // 🤖 AIのモード
    public int AiModeIndex { get; set; } = 0;

    // 🎙️ マイク設定関係
    public float MicThreshold { get; set; } = 0.08f;
    public float NoiseReduction { get; set; } = 0.02f;
    public int SilenceTimeout { get; set; } = 1500;
    public int MaxSentenceLength { get; set; } = 25;

    // ✨ ユーザーが指定するログの保管日数（初期値: 7日）
    // 💡 ここは余計なものを入れず、シンプルなこの1行だけでOKです！
    public int LogKeepDays { get; set; } = 7;
}

public static class SettingsService
{
    private static readonly string PathFile = "settings.json";

    public static void Save(
    string apiKey,
    int aiModeIndex,
    float micThreshold,
    float noiseReduction,
    int silenceTimeout,
    int maxSentenceLength,
    int logKeepDays)
    {
        var settings = new AppSettings
        {
            ApiKey = apiKey,
            AiModeIndex = aiModeIndex,
            MicThreshold = micThreshold,
            NoiseReduction = noiseReduction,
            SilenceTimeout = silenceTimeout,
            MaxSentenceLength = maxSentenceLength,
            LogKeepDays = logKeepDays
        };

        var json = JsonSerializer.Serialize(settings);
        File.WriteAllText(PathFile, json);
    }

    public static AppSettings Load()
    {
        if (!File.Exists(PathFile))
            return new AppSettings();

        var json = File.ReadAllText(PathFile);

        if (string.IsNullOrWhiteSpace(json))
            return new AppSettings();

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(json)
                   ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings);
        File.WriteAllText(PathFile, json);
    }
}