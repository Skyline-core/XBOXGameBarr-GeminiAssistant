using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.Graphics.Capture;

namespace GeminiAssistant.Services
{
    internal static class GraphicsCapturePickerService
    {
        public static async Task<byte[]> TryPickAndCaptureAsync()
        {
            if (!ApiInformation.IsTypePresent("Windows.Graphics.Capture.GraphicsCapturePicker"))
            {
                WidgetFileLog.Write("Captura elegir ventana: API no disponible");
                return null;
            }

            try
            {
                var picker = new GraphicsCapturePicker();
                if (WindowHandleInterop.TryGetWindowHandle(out var hwnd))
                {
                    WindowHandleLog(hwnd);
                    WindowHandleInterop.TrySetOwnerWindow(picker, hwnd);
                }
                else
                {
                    WidgetFileLog.Write("Captura elegir ventana: sin HWND del widget");
                }

                var item = await picker.PickSingleItemAsync().AsTask().ConfigureAwait(true);
                if (item == null)
                {
                    WidgetFileLog.Write("Captura elegir ventana: cancelado");
                    return null;
                }

                WidgetFileLog.Write(
                    "Captura elegir ventana OK: " + item.DisplayName + " " + item.Size.Width + "x" + item.Size.Height);
                return await GraphicsCaptureJpegService.TryCaptureGraphicsItemAsync(item).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Captura elegir ventana: " + WidgetExceptionFormatter.Format(ex));
                return null;
            }
        }

        private static void WindowHandleLog(IntPtr hwnd)
        {
            WidgetFileLog.Write("Captura elegir ventana HWND=" + hwnd.ToInt64());
        }
    }
}
