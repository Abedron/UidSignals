# UidSignals

WPF application for controlling haptic feedback on Logitech device Master MX 4 and communicating with hardware through Raw Input API.

## 📋 Description

UidSignals is a desktop application in C# / WPF that:

- **Captures Thumb Wheel movements** (horizontal scroll wheel) on Logitech MX Master devices using Raw Input API
- **Sends haptic feedback** with 15 different vibration patterns (Pattern 0-14) to Logitech HID++ 2.0 compatible devices
- **Supports multiple connection types**: Logi Bolt dongle and direct Bluetooth/USB connection
- **Automatically detects** connected devices and manages their cache

## 🎯 Main Features

### 1. **Haptic Feedback (15 Patterns)**
Buttons (0-14) with visual pattern descriptions:
- `0 - 14`: Various vibration intensities and durations (dot, dash, pause)
- Graphical pattern representation below each button
- Monospace font for better symbol readability

#### Haptic Pattern Reference

| Pattern ID | Visual Representation | Description                         |
|:----------:|:--------------------:|-------------------------------------|
| **0** | `● ●` | Two strong pulses with pause        |
| **1** | `• •` | Two light pulses with pause         |
| **2** | `•` | Single light pulse                  |
| **3** | `–` | Single medium pulse                 |
| **4** | `–` | Single medium pulse (variant)       |
| **5** | `•• •• •••` | Multiple light pulses in groups     |
| **6** | `—— – – – – –` | Long pulse followed by short pulses |
| **7** | `–  ● ●` | Medium pulse with strong pulses     |
| **8** | `● ● ●   •` | Three strong pulses with light pulse |
| **9** | `● ● ●   —` | Three strong pulses with long pulse |
| **10** | `● ● – –` | Strong and medium pulses mixed      |
| **11** | `○ ○ ○` | Three intensive pulses              |
| **12** | `–   –   –` | Three medium pulses with      |
| **13** | `• •• • – –` | Mixed light and medium pulses       |
| **14** | `—— ——` | Two long vibrations                 |

**Legend:**
- `●` / `•` = Pulse (● = strong, • = light)
- `–` / `—` = Vibration (– = short, — = long)
- Spacing = Duration between pulses

### 2. **Thumb Wheel Detection**
- Real-time tracking of horizontal scroll wheel rotation
- Display rotation direction: **RIGHT** / **LEFT**
- Status label below buttons showing current state

### 3. **Device Management**
- Automatic Logitech device detection (VID: 046D)
- Support for Logi Bolt receivers (PID: C548)
- Support for direct Bluetooth/USB connection

## 🛠️ Technology Stack

| Component | Details |
|-----------|---------|
| **Language** | C# (.NET 10.0 Windows) |
| **Framework** | WPF (Windows Presentation Foundation) |
| **Raw Input** | Win32 API for real-time mouse detection |
| **HID API** | HID++ 2.0 for Logitech device communication |

## 📁 Project Structure

```
UidSignals/
├── UidSignals.sln              # Solution file
├── UidSignals/
│   ├── App.xaml                # WPF application settings
│   ├── App.xaml.cs             # Application code-behind
│   ├── MainWindow.xaml          # Main UI (15 buttons + label)
│   ├── MainWindow.xaml.cs       # Event handlers for buttons and Thumb Wheel
│   ├── LogitechHapticController.cs  # Core: Raw Input, HID++, cache
│   ├── AssemblyInfo.cs          # Project metadata
│   └── bin/Debug/               # Compiled output
├── README.md                    # This file
└── UidSignals.sln.DotSettings.user  # IDE settings
```

## 💻 Usage

### Application Initialization
```csharp
// MainWindow.xaml.cs
protected override void OnSourceInitialized(EventArgs e)
{
    base.OnSourceInitialized(e);
    LogitechController.Initialize(this);  // Initialize Raw Input
    LogitechController.ThumbWheelScrolled += OnThumbWheelScrolled;
}
```

### Sending Haptic Feedback
```csharp
// Example: Soft click (0x01)
await LogitechController.TriggerFeedbackAsync(0x01);

// Example: Harder click (0x04)
await LogitechController.TriggerFeedbackAsync(0x04);
```

### Receiving Thumb Wheel Events
```csharp
private void OnThumbWheelScrolled(object? sender, int delta)
{
    if (delta > 0)
        ThumbWheelDirectionLabel.Text = $"Thumb: RIGHT ({delta})";
    else if (delta < 0)
        ThumbWheelDirectionLabel.Text = $"Thumb: LEFT ({Math.Abs(delta)})";
}
```

## 📝 HID++ 2.0 Buffer Format

Haptic feedback is sent through a 20-byte HID++ Long Report:

```
Byte  Description              Value
─────────────────────────────────────────
 0    Report ID                0x11 (HID++ Long Report)
 1    Device Index             0x02 (Bolt) or 0xFF (Direct)
 2    Feature Index            0x0B (Haptic module)
 3    Command                  0x4C (HapticPlay)
 4    Pattern ID               0x00-0x0E (15 patterns)
5-19  Padding                  0x00
```

## 🐛 Troubleshooting

### Thumb Wheel Not Responding
1. Check if Logitech device is connected
2. Verify in Device Manager → Mice and other pointing devices
3. Application requires **admin rights** for Raw Input

### Haptic Not Working
1. Verify device supports HID++ 2.0
2. Check if device is in active mode (not sleeping)
3. Reset cache: `LogitechController.ResetCache()`

## 📚 Resources and References

- [Microsoft Raw Input API](https://docs.microsoft.com/en-us/windows/win32/inputdev/raw-input)
- [Logitech HID++ 2.0 Protocol](https://lekensteyn.nl/files/logitech_hidpp_2.0_specification_draft_2012-06-04.pdf)
- [Setup API - Device Discovery](https://docs.microsoft.com/en-us/windows/win32/setupapi/device-enumeration)
- [HID API Documentation](https://docs.microsoft.com/en-us/windows-hardware/drivers/hid/)

## ⚙️ Configuration

### Changing Haptic Patterns
Edit `MainWindow.xaml.cs` - `ButtonBase_OnClick` method:
```csharp
private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
{
    if (sender is Button button && byte.TryParse(button.Tag?.ToString(), out byte patternId))
    {
        await LogitechController.TriggerFeedbackAsync(patternId);
    }
}
```

### Changing Button Text
Edit `MainWindow.xaml` - `Button.Content` property in each StackPanel:
```xml
<Button Click="ButtonBase_OnClick" Tag="0" Width="60" Height="40">0</Button>
```

## 📄 License

This project is intended for educational and personal use.