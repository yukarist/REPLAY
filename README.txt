🛠️ REPLAY
配信特化型リアルタイムログ支援ツール

REPLAY は、配信中の会話や雑談をリアルタイムで記録し、
あとから振り返りや切り抜き確認をしやすくするために制作した、
配信者向けリアルタイムログ支援アプリです。

Whisper を利用した音声認識と、
Gemini / Ollama による AI文章補正・要約に対応しています。

✨ Features
🎤 リアルタイム音声認識
🤖 AI文章補正（Gemini / Ollama）
📝 AI配信要約
⭐ お気に入りログ管理
📅 日付別ログ表示
📁 CSV / テキスト出力
🌊 リアルタイム波形アニメーション
🔇 ノイズ軽減 / フィラー抑制
🎛 マイク感度調整
💾 SQLiteローカル保存

🔰 Quick Start
Gemini モード
REPLAY を起動
設定画面を開く
AIモードで Gemini を選択
APIキーを入力
マイクを選択
Start を押す
Ollama モード

事前に Ollama をインストールしてください。

推奨モデル：

ollama pull qwen2.5:7b

その後：

Ollama を起動
REPLAY を起動
AIモードで Ollama を選択
Start を押す

🤖 AI Modes
Gemini

Google Gemini API を利用したクラウドAI。

特徴
高精度な補正・要約
PC負荷が軽い
セットアップが簡単
注意点
API利用回数制限あり
長時間連続トーク時は制限に到達する場合があります
Ollama

ローカル環境で動作するAI。

特徴
完全ローカル処理
API不要
利用制限なし
注意点
PCスペック依存
初回セットアップが必要

🔒 Security
ログデータはローカル (speech.db) に保存
Gemini利用時のみAPI通信
Ollama利用時は完全ローカル処理

📦 Whisper Model

REPLAY では Whisper small model を使用しています。

ggml-small.bin はGitHubには含まれていません。
利用時は別途配置してください。

🧠 Tech Stack
C#
WPF (.NET 8)
SQLite
NAudio
Whisper
Gemini API
Ollama

📌 Roadmap
OBS Overlay 連携
AIクリップ抽出
ログ検索強化
配信別ログ管理
モバイル連携

❤️ About

「配信後、“何を話したか思い出せない”を減らしたい」

そんな思いから制作した、
配信向けリアルタイムログ支援ツールです。

👤 Author

Created by YukarI
