using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace REPLAY.Infrastructure.AI
{
    public class TextCorrectionService
    {
        private readonly HttpClient _http = new();
        private readonly string _apiKey;

        public TextCorrectionService(string apiKey)
        {
            _apiKey = apiKey;

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task<string> CorrectAsync(string input, AiMode mode)
        {
            // OFFならそのまま返す
            if (mode == AiMode.Off)
                return input;

            string prompt;

            // ▼ 軽補正（おすすめ）
            if (mode == AiMode.Light)
            {
                prompt = $@"
以下は音声認識の結果です。
誤字や聞き間違いのみを修正してください。

【ルール】
・意味は絶対に変えない
・文章を作り直さない
・言い換えない
・補完しない
・句読点のみ追加
・不要なフィラー（えー、あのー等）は削除

入力:
{input}

出力（修正後の文章のみ）:
";
            }
            // ▼ しっかり整形
            else
            {
                prompt = $@"
以下は音声認識の結果です。
自然な日本語として読みやすく整形してください。

【ルール】
・意味は変えない
・読みやすくする
・句読点を適切に追加
・軽く補完OK
・話し言葉として自然にする

入力:
{input}

出力（整形後の文章のみ）:
";
            }

            var request = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0.2
            };

            var json = JsonSerializer.Serialize(request);

            var response = await _http.PostAsync(
                "https://api.openai.com/v1/chat/completions",
                new StringContent(json, Encoding.UTF8, "application/json")
            );

            response.EnsureSuccessStatusCode();

            var resJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(resJson);

            var result = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return result?.Trim() ?? input;
        }
    }
}