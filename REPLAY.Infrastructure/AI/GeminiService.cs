using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
namespace REPLAY.Infrastructure.AI
{
    public class GeminiService : IAiService
    {
        private static readonly HttpClient _http = new HttpClient();
        private readonly string _apiKey;

        // 💡 制限が緩い 1.5-flash にしておくと開発がとても快適になります
        // もし 2.5 を使いたい場合は "gemini-2.5-flash" に書き換えてください
        private const string ModelName = "gemini-2.5-flash";

        public GeminiService(string apiKey)
        {
            _apiKey = apiKey;
        }

        public async Task<string> SummarizeAsync(string input, int mode)
        {
            try
            {
                // 💡 「流れ：」「雰囲気：」というコロン付きの項目名で始まる行だけを除去
                var cleanedText = string.Join("\n",
                    input.Split('\n')
                        .Where(line =>
                            !string.IsNullOrWhiteSpace(line) &&
                            !line.TrimStart().StartsWith("流れ：") && // 💡 「：」を付けた
                            !line.TrimStart().StartsWith("流れ:") &&  // 💡 半角コロンもケア
                            !line.TrimStart().StartsWith("雰囲気：") &&
                            !line.TrimStart().StartsWith("雰囲気:")
                        )
                );

                // 念のため、削りすぎて空っぽになったら元の入力を使う
                if (string.IsNullOrWhiteSpace(cleanedText) || cleanedText.Length < 10)
                {
                    cleanedText = input;
                }

                // --- 以降のプロンプトや通信処理はそのまま ---

                // おすすめの「配信者振り返り用」プロンプト構成
                var prompt = $@"
あなたは配信者の優秀なアシスタントです。以下の配信ログから、配信者自身が「何をしたか」「何を話したか」を思い出すための振り返りログを作成してください。

【出力形式】
■ 配信のハイライト
・○○についての話
・○○の出来事

■ 話したトピック
・(トピックA)：〇〇という結論/感想
・(トピックB)：〇〇という話

■ 配信者へのメモ
・次に活かせるポイントや、視聴者の反応で良かったこと

【ルール】
・丁寧な文章よりも、事実を箇条書きで分かりやすく整理する
・「流れ」だけでなく「個人的な気づき」があれば含める
・要約の文体は、配信者本人が読んでしっくりくる自然なメモ形式にする

ログ：{cleanedText}
";

                var body = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    }
                };

                var json = JsonSerializer.Serialize(body);
                var response = await _http.PostAsync(
                   $"https://generativelanguage.googleapis.com/v1/models/{ModelName}:generateContent?key={_apiKey}",
                    new StringContent(json, Encoding.UTF8, "application/json")
                );

                if (!response.IsSuccessStatusCode)
                {
                    // 💡 nullを返すのをやめて、エラーコードを画面に叩きつける
                    return $"【Gemini 通信エラー: {(int)response.StatusCode}】\nGoogle API側がリクエストを拒否しました。少し時間を置くか、Ollamaに切り替えてください。";
                }

                var resJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(resJson);

                if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
    candidates.GetArrayLength() == 0)
                {
                    return "要約失敗: レスポンス形式が不正です";
                }

                return candidates[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString()
                    ?.Trim() ?? "要約失敗: 空レスポンス";
            }
            catch (Exception ex)
            {
                return $"【Gemini システムエラー】\n{ex.Message}";
            }
        }

        public async Task<string> CorrectAsync(string input)
        {
            try
            {
                // 推奨：振り返り用文字起こし整形プロンプト
                var prompt = $@"
あなたはプロの速記・編集アシスタントです。
以下の配信テキストを、配信者本人が後から読み返して内容を把握しやすいように整形してください。

【目的】
・誤変換や不要な口癖を削除し、読みやすくする
・ただし、元の話者の個性や雰囲気は極力残す

【ルール】
・「えー」「あの」「その」などの無意味なフィラーを徹底的に削除する
・同じ単語の無意識な繰り返しを整理する
・明らかな誤変換（聞き間違い）は文脈から推測して修正する
・読点「、」や句点「。」を適切に追加して、文章の区切りを明確にする
・箇条書きにせず、元の会話の流れを維持したまま文章として整える
・「です・ます」調などの語尾は、元のテキストの雰囲気に合わせる

入力テキスト：
{input}
";

                var body = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    }
                };

                var json = JsonSerializer.Serialize(body);
                var response = await _http.PostAsync(
                    $"https://generativelanguage.googleapis.com/v1/models/{ModelName}:generateContent?key={_apiKey}",
                    new StringContent(json, Encoding.UTF8, "application/json")
                );

                if (!response.IsSuccessStatusCode)
                {
                    return $"【Gemini 整形エラー: {(int)response.StatusCode}】リクエスト上限です。";
                }

                var resJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(resJson);

                return doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString()
                    .Trim();
            }
            catch (Exception ex)
            {
                return $"【Gemini システムエラー】\n{ex.Message}";
            }
        }
    }
}