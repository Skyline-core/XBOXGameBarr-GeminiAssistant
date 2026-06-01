using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Dispara Win+Alt+Impr Pant (atajo de captura de Xbox Game Bar) sin intervencion del usuario.
    /// </summary>
    internal static class GameBarHotkeyCaptureService
    {
        private const byte VkLWin = 0x5B;
        private const byte VkMenu = 0x12;
        private const byte VkSnapshot = 0x2C;
        private const uint KeyeventfKeyup = 0x0002;
        private const uint InputKeyboard = 1;

        private const int WaitAfterHotkeyMs = 400;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public static async Task<bool> TryTriggerScreenshotHotkeyAsync()
        {
            try
            {
                WidgetFileLog.Write("Captura: enviar Win+Alt+Impr Pant automatico");
                var sent = TrySendInputHotkey();
                if (!sent)
                {
                    WidgetFileLog.Write("Captura: SendInput fallo, usar keybd_event");
                    PressKeyLegacy(VkLWin);
                    PressKeyLegacy(VkMenu);
                    PressKeyLegacy(VkSnapshot);
                    ReleaseKeyLegacy(VkSnapshot);
                    ReleaseKeyLegacy(VkMenu);
                    ReleaseKeyLegacy(VkLWin);
                }

                if (WaitAfterHotkeyMs > 0)
                {
                    await Task.Delay(WaitAfterHotkeyMs).ConfigureAwait(false);
                }

                return true;
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Captura hotkey: " + WidgetExceptionFormatter.Format(ex));
                return false;
            }
        }

        private static bool TrySendInputHotkey()
        {
            try
            {
                SendVk(VkLWin, false);
                SendVk(VkMenu, false);
                SendVk(VkSnapshot, false);
                SendVk(VkSnapshot, true);
                SendVk(VkMenu, true);
                SendVk(VkLWin, true);
                return true;
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Captura SendInput: " + WidgetExceptionFormatter.Format(ex));
                return false;
            }
        }

        private static void SendVk(byte virtualKey, bool keyUp)
        {
            var inputs = new INPUT[1];
            inputs[0].type = InputKeyboard;
            inputs[0].u.ki.wVk = virtualKey;
            inputs[0].u.ki.dwFlags = keyUp ? KeyeventfKeyup : 0;
            var sent = SendInput(1, inputs, Marshal.SizeOf<INPUT>());
            if (sent == 0)
            {
                throw new InvalidOperationException("SendInput devolvio 0");
            }
        }

        private static void PressKeyLegacy(byte virtualKey)
        {
            keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
        }

        private static void ReleaseKeyLegacy(byte virtualKey)
        {
            keybd_event(virtualKey, 0, KeyeventfKeyup, UIntPtr.Zero);
        }
    }
}
