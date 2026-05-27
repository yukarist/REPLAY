using System;
using System.IO;
using System.Threading.Tasks;
using Whisper.net;

namespace REPLAY.Infrastructure.AI
{
    public class WhisperService
    {
        private readonly WhisperProcessor _processor;

        public WhisperService()
        {
            var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ggml-small.bin");

            if (!File.Exists(modelPath))
                throw new Exception("モデルファイルが見つかりません");

            var factory = WhisperFactory.FromPath(modelPath);

            _processor = factory.CreateBuilder()
                .WithLanguage("ja")          // 日本語固定
                .WithThreads(Environment.ProcessorCount)
                .Build();
        }

        // WhisperService.cs の TranscribeAsync メソッド内
        public async Task<string> TranscribeAsync(byte[] wavData)
        {
            using var stream = new MemoryStream(wavData);
            string result = "";

            await foreach (var segment in _processor.ProcessAsync(stream))
            {
                // 🔍 ここでフィルタリングを行います
                string text = segment.Text;

                // (音楽) などの不要な文字列を削除・置換します
                // 日本語モデルや英語モデルでタグ名が異なる可能性があるため、いくつか指定しておくと安全です
                text = text.Replace("(音楽)", "")
                           .Replace("(music)", "")
                           .Replace("[Music]", "")
                           .Trim(); // 前後の空白を消す

                // 削除した結果、中身が空でなければ追加する
                if (!string.IsNullOrEmpty(text))
                {
                    result += text;
                }
            }

            return result.Trim();
        }
    }
}