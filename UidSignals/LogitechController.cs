using System.Windows.Interop;

namespace UidSignals
{
    /// <summary>
    /// WPF adapter that wires up the LogiMX library to a WPF window.
    /// All logic lives in <see cref="LogiMX.LogitechController"/>.
    /// </summary>
    public static class LogitechController
    {
        /// <inheritdoc cref="LogiMX.LogitechController.ThumbWheelScrolled"/>
        public static event EventHandler<int>? ThumbWheelScrolled
        {
            add    => LogiMX.LogitechController.ThumbWheelScrolled += value;
            remove => LogiMX.LogitechController.ThumbWheelScrolled -= value;
        }

        /// <summary>
        /// Registers the WPF window to receive raw input and hooks the WndProc.
        /// </summary>
        public static void Initialize(HwndSource? source)
        {
            if (source == null) return;

            LogiMX.LogitechController.Initialize(source.Handle);
            source.AddHook(WndProcHook);
        }

        /// <inheritdoc cref="LogiMX.LogitechController.ResetCache"/>
        public static void ResetCache() => LogiMX.LogitechController.ResetCache();

        /// <inheritdoc cref="LogiMX.LogitechController.TriggerFeedbackAsync"/>
        public static Task TriggerFeedbackAsync(byte patternId = 0x00)
            => LogiMX.LogitechController.TriggerFeedbackAsync(patternId);

        private static IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
            => LogiMX.LogitechController.ProcessMessage(msg, wParam, lParam, ref handled);
    }
}