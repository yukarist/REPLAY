using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using REPLAY.Infrastructure.Audio;

namespace REPLAY.Infrastructure.Audio
{
    public class LocalWhisperService
    {
        public async Task<string> TranscribeAsync(byte[] audioData)
        {
            // ▼ 一時wav作成
            var tempWav = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");
            await File.WriteAllBytesAsync(tempWav, audioData);

            // ✨ 修正：アプリの実行フォルダからの相対パスを取得する
            // プロジェクトの構成に合わせて調整してください
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "REPLAY_AI", "whisper_runner.py");

            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{scriptPath}\" \"{tempWav}\"", // 修正したパスを使用
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(psi);

            string result = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            Console.WriteLine("OUT: " + result);
            Console.WriteLine("ERR: " + error);

            process.WaitForExit();

            File.Delete(tempWav);

            return result.Trim();
        }
    }
}