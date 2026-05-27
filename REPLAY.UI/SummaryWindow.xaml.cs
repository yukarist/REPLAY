using System.Windows.Media.Animation;
using System.Windows;
using System.Windows.Input;

namespace REPLAY.UI
{
    public partial class SummaryWindow : Window
    {
        public SummaryWindow(string text)
        {
            InitializeComponent();

            var formatted = text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("タイトル：", "【")
                .Replace("\n要約：", "】\n");

            SummaryText.Text = formatted;

            // 全体ドラッグ
            this.MouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                    DragMove();
            };

            // TextBox上でもドラッグ
            SummaryText.PreviewMouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                    DragMove();
            };
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(SummaryText.Text);
            ShowToast("コピーしました");
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ShowToast(string message)
        {
            ToastText.Text = message;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400))
            {
                BeginTime = TimeSpan.FromMilliseconds(1000)
            };

            Storyboard.SetTarget(fadeIn, Toast);
            Storyboard.SetTarget(fadeOut, Toast);

            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));

            var sb = new Storyboard();
            sb.Children.Add(fadeIn);
            sb.Children.Add(fadeOut);
            sb.Begin();
        }

        protected override void OnClosed(EventArgs e)
        {
            // ストーリーボードがもし動いていれば止める処理などをここに書くとより安全です
            base.OnClosed(e);
        }
    }
}