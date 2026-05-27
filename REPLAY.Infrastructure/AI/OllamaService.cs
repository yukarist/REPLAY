using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

public class OllamaService : IAiService
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    // OllamaService.cs の修正イメージ

    public async Task<string> SummarizeAsync(string text, int mode)
    {
        try
        {
            var prompt = BuildPrompt(text, mode);

            var request = new
            {
                // 💡 2bをやめて、リアルタイム補正(mode=1)に賢いqwen2.5:7bを指定
                model = mode == 1 ? "qwen2.5:7b" : "gemma2:9b",
                prompt = prompt,
                stream = false
            };

            var res = await _http.PostAsJsonAsync(
                "http://localhost:11434/api/generate",
                request
            );

            // 💡 通信エラー（500エラー等）をキャッチして例外に流す
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadFromJsonAsync<OllamaResponse>();

            if (json == null || string.IsNullOrWhiteSpace(json.response))
            {
                return "要約失敗: レスポンスが空です";
            }

            return json.response.Trim();

        }
        catch (Exception ex)
        {
            // 💡 Ollamaの起動忘れや、タイムアウト時の対策
            return $"【要約エラー】\n{ex.Message}\n(Ollamaが起動しているか確認してください)";
        }
    }

    public async Task<string> CorrectAsync(string text)
    {
        // 軽補正として流用
        return await SummarizeAsync(text, 1);
    }

    private string BuildPrompt(string text, int mode)
    {
        var cleanedText = string.Join("\n",
            text.Split('\n')
                .Where(line =>
                    !line.StartsWith("流れ：") &&
                    !line.StartsWith("雰囲気：")
                )
        );

        // 🟢 軽補正モード
        if (mode == 1)
        {
            return $@"あなたは優秀な編集者です。配信の音声認識テキストを読み込み、意味を変えずに読みやすく修正してください。

【制約】
・文章の要約や削除はしない。
・誤字脱字、明らかな聞き間違いのみを補正する。
・「えー」「あー」などのフィラーは削除する。
・出力は修正したテキストのみとする。

入力: あーえっと、今日のゲームは、バイオハザードをやります
出力: 今日のゲームはバイオハザードをやります。

入力: {text}
出力:";
        }

        return $@"# 配信ログの要約タスク

配信者本人の備忘録を作成します。

【ルール】
・出力は「タイトル：」「流れ：」「雰囲気：」の3項目のみ。
・「流れ：」は箇条書きまたは短い文章で、時系列に沿ってまとめる。
・事実のみを記載し、感情的な修飾語は最小限にする。
・ログに記載がないことは補完しない。

### 理想的な出力例
タイトル：ホラーゲーム初挑戦
流れ：
・冒頭：ホラーゲームのプレイ開始
・中盤：独特な操作方法に戸惑う
・終盤：謎解きで詰まり、リスナーと相談してクリア
雰囲気：真剣だが笑いもあり

### 対象のログ
{cleanedText}

### 出力
タイトル：";
        // 💡 ポイント：最後の行を「タイトル：」で終わらせることで、AIに強制的にタイトルの続きから書かせる
    }
}