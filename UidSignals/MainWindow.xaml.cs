using System.Windows;
using System.Windows.Controls;

namespace UidSignals
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            LogitechController.Initialize(this);

            LogitechController.ThumbWheelScrolled += OnThumbWheelScrolled;
        }

        private void OnThumbWheelScrolled(object? sender, int delta)
        {
            // Update label on UI thread
            Dispatcher.Invoke(() =>
            {
                if (delta > 0)
                {
                    ThumbWheelDirectionLabel.Text = $"Thumb: RIGHT ({delta})";
                }
                else if (delta < 0)
                {
                    ThumbWheelDirectionLabel.Text = $"Thumb: LEFT ({delta})";
                }
                else
                {
                    ThumbWheelDirectionLabel.Text = "Thumb: -";
                }
            });
        }

        private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && byte.TryParse(button.Tag?.ToString(), out byte patternId))
            {
                Console.WriteLine($"Triggering haptic pattern: {patternId}");
                await LogitechController.TriggerFeedbackAsync(patternId);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            LogitechController.ThumbWheelScrolled -= OnThumbWheelScrolled;
            base.OnClosed(e);
        }
    }
}