using System;
using System.ComponentModel;

namespace REPLAY.Domain.Models
{
    public class SpeechLog : INotifyPropertyChanged
    {
        // 💡 privateフィールドを必ず宣言してください
        private bool _isFavorite;

        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string CorrectedText { get; set; }
        public bool IsSystemMessage { get; set; } = false;

        // 💡 1つにまとめたプロパティ
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value) // 値が変わった時だけ処理
                {
                    _isFavorite = value;
                    OnPropertyChanged(nameof(IsFavorite));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}