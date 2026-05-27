using Microsoft.Data.Sqlite;
using Microsoft.VisualBasic.Logging;
using REPLAY.Domain.Models;
using System;
using System.Collections.Generic;

namespace REPLAY.Infrastructure.Database
{
    public class SpeechRepository
    {
        private readonly string _connectionString = "Data Source=speech.db";

        public SpeechRepository()
        {
            Initialize();
        }

        private void Initialize()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();

            // 🧹 RawText を削除
            cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS SpeechLogs (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Timestamp TEXT,
            CorrectedText TEXT,
            IsFavorite INTEGER
        );
        CREATE INDEX IF NOT EXISTS idx_timestamp ON SpeechLogs(Timestamp);
    ";
            cmd.ExecuteNonQuery();
        }

        // 🧹 引数を SpeechLog だけに変更！
        // SpeechRepository.cs の Add メソッド内
        // SpeechRepository.cs

        // 1. Add メソッドから RawText を排除
        // 既に存在する Add メソッドをこのように書き換えてください
        public int Add(SpeechLog log)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            // トランザクションにして安全にIDを取得します
            using var transaction = conn.BeginTransaction();

            var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;

            cmd.CommandText = @"
        INSERT INTO SpeechLogs (Timestamp, CorrectedText, IsFavorite) 
        VALUES ($time, $text, $fav);
        SELECT last_insert_rowid();"; // 💡 ここで挿入したIDを取得

            cmd.Parameters.AddWithValue("$time", log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("$text", log.CorrectedText);
            cmd.Parameters.AddWithValue("$fav", log.IsFavorite ? 1 : 0);

            var newId = Convert.ToInt32(cmd.ExecuteScalar()); // ExecuteScalar で ID を受け取る
            transaction.Commit();

            return newId; // 正しいIDを返す
        }

        // SpeechRepository.cs に追加
        public List<SpeechLog> GetLogsByFilter(bool onlyFavorites)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            var sql = "SELECT Id, Timestamp, CorrectedText, IsFavorite FROM SpeechLogs";
            if (onlyFavorites) sql += " WHERE IsFavorite = 1";
            sql += " ORDER BY Timestamp DESC";

            cmd.CommandText = sql;
            var list = new List<SpeechLog>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new SpeechLog
                {
                    Id = reader.GetInt32(0),
                    Timestamp = DateTime.Parse(reader.GetString(1)),
                    CorrectedText = reader.GetString(2),
                    IsFavorite = reader.GetBoolean(3)
                });
            }
            return list;
        }

        // 2. 日付で検索するメソッドを追加 (これがJSON読み込みの代わりになります)
        public List<SpeechLog> GetLogsByDate(string dateString)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            // yyyy-MM-dd で検索
            cmd.CommandText = "SELECT Id, Timestamp, CorrectedText, IsFavorite FROM SpeechLogs WHERE Timestamp LIKE $date ORDER BY Timestamp DESC";
            cmd.Parameters.AddWithValue("$date", dateString + "%");

            var list = new List<SpeechLog>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new SpeechLog
                {
                    Id = reader.GetInt32(0),
                    Timestamp = DateTime.Parse(reader.GetString(1)),
                    CorrectedText = reader.GetString(2),
                    IsFavorite = reader.GetBoolean(3)
                });
            }
            return list;
        }

        // SpeechRepository.cs に追加
        // SpeechRepository.cs に追加
        public string GetLatestDate()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            // 最新の日付を1件取得
            cmd.CommandText = "SELECT Timestamp FROM SpeechLogs ORDER BY Timestamp DESC LIMIT 1";

            var result = cmd.ExecuteScalar();
            if (result != null && DateTime.TryParse(result.ToString(), out DateTime dt))
            {
                return dt.ToString("M/d (ddd)");
            }
            return DateTime.Now.ToString("M/d (ddd)"); // DBが空なら今日の日付を返す
        }

        // SpeechRepository.cs に追加してください
        // 変更後：正しい GetAllLogs メソッド
        public List<SpeechLog> GetAllLogs()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Timestamp, CorrectedText, IsFavorite FROM SpeechLogs ORDER BY Timestamp DESC";

            using var reader = cmd.ExecuteReader();
            var list = new List<SpeechLog>();

            while (reader.Read())
            {
                list.Add(new SpeechLog
                {
                    Id = reader.GetInt32(0),
                    Timestamp = DateTime.Parse(reader.GetString(1)),
                    CorrectedText = reader.GetString(2),
                    IsFavorite = reader.GetBoolean(3)
                });
            }

            // ❌ ここにあった「cmd.Parameters.AddWithValue...」という行を削除しました
            return list;
        }

        // SpeechRepository.cs に追加
        public void UpdateFavoriteStatus(int id, bool isFavorite)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE SpeechLogs SET IsFavorite = $isFavorite WHERE Id = $id";
            cmd.Parameters.AddWithValue("$isFavorite", isFavorite ? 1 : 0); // bool -> int(1/0)
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        // SpeechRepository.cs に追加してください
        public void Delete(int id)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            // IDを指定してログを削除するSQL
            cmd.CommandText = "DELETE FROM SpeechLogs WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteOldLogs(int keepDays)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();

            // keepDays 前より古い日時を持つレコードを削除するSQL
            // Timestampが文字列（"yyyy-MM-dd..."）で保存されている前提です
            var limitDate = DateTime.Today.AddDays(-keepDays).ToString("yyyy-MM-dd");

            cmd.CommandText = "DELETE FROM SpeechLogs WHERE Timestamp < $limitDate";
            cmd.Parameters.AddWithValue("$limitDate", limitDate);

            cmd.ExecuteNonQuery();
        }
    }
}