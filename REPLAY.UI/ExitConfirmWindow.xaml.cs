using System.Windows;
using System.Windows.Input;

namespace REPLAY.UI
{
    public partial class ExitConfirmWindow : Window
    {
        public bool IsConfirmed { get; private set; }

        public ExitConfirmWindow()
        {
            InitializeComponent();
            this.MouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                    DragMove();
            };
        }

        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            Close();
        }

        private void No_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }
    }
}