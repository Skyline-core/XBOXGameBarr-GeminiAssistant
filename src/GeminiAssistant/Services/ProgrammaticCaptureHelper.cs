using System;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.Graphics.Capture;
using Windows.Security.Authorization.AppCapabilityAccess;

namespace GeminiAssistant.Services
{
    internal static class ProgrammaticCaptureHelper
    {
        public static async Task<AppCapabilityAccessStatus> RequestCaptureAccessAsync()
        {
            var status = AppCapabilityAccessStatus.UserPromptRequired;

            if (ApiInformation.IsTypePresent("Windows.Graphics.Capture.GraphicsCaptureAccess"))
            {
                status = await GraphicsCaptureAccess
                    .RequestAccessAsync(GraphicsCaptureAccessKind.Programmatic)
                    .AsTask()
                    .ConfigureAwait(true);

                if (status == AppCapabilityAccessStatus.Allowed)
                {
                    return status;
                }
            }

            if (ApiInformation.IsTypePresent(
                    "Windows.Security.Authorization.AppCapabilityAccess.AppCapability"))
            {
                try
                {
                    var capability = AppCapability.Create("graphicsCaptureProgrammatic");
                    status = await capability.RequestAccessAsync().AsTask().ConfigureAwait(true);
                }
                catch
                {
                }
            }

            return status;
        }

        public static GraphicsCaptureItem TryCreateForWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return GraphicsCaptureItemInterop.CreateForWindow(hwnd);
            }
            catch
            {
            }

            if (ApiInformation.IsMethodPresent(
                    typeof(GraphicsCaptureItem).FullName,
                    nameof(GraphicsCaptureItem.TryCreateFromWindowId)))
            {
                try
                {
                    var windowId = WindowIdFromHwnd(hwnd);
                    return GraphicsCaptureItem.TryCreateFromWindowId(windowId);
                }
                catch
                {
                }
            }

            return null;
        }

        public static GraphicsCaptureItem TryCreateForMonitor(IntPtr monitor)
        {
            if (monitor == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return GraphicsCaptureItemInterop.CreateForMonitor(monitor);
            }
            catch
            {
            }

            if (ApiInformation.IsMethodPresent(
                    typeof(GraphicsCaptureItem).FullName,
                    nameof(GraphicsCaptureItem.TryCreateFromDisplayId)))
            {
                try
                {
                    var displayId = DisplayIdFromHandle(monitor);
                    return GraphicsCaptureItem.TryCreateFromDisplayId(displayId);
                }
                catch
                {
                }
            }

            return null;
        }

        public static string DescribeAccessStatus(AppCapabilityAccessStatus status)
        {
            switch (status)
            {
                case AppCapabilityAccessStatus.Allowed:
                    return "Permitido";
                case AppCapabilityAccessStatus.DeniedByUser:
                    return "Denegado por el usuario";
                case AppCapabilityAccessStatus.DeniedBySystem:
                    return "Denegado por el sistema";
                case AppCapabilityAccessStatus.NotDeclaredByApp:
                    return "No declarado en la app";
                case AppCapabilityAccessStatus.UserPromptRequired:
                    return "Se requiere confirmacion";
                default:
                    return status.ToString();
            }
        }

        private static Windows.UI.WindowId WindowIdFromHwnd(IntPtr hwnd)
        {
            var value = unchecked((ulong)hwnd.ToInt64());
            var id = default(Windows.UI.WindowId);
            var type = typeof(Windows.UI.WindowId);

            foreach (var field in type.GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic))
            {
                if (field.FieldType == typeof(ulong))
                {
                    var boxed = (ValueType)id;
                    field.SetValue(boxed, value);
                    return (Windows.UI.WindowId)boxed;
                }
            }

            throw new InvalidOperationException("WindowId no compatible en este SDK.");
        }

        private static Windows.Graphics.DisplayId DisplayIdFromHandle(IntPtr handle)
        {
            var value = unchecked((ulong)handle.ToInt64());
            var id = default(Windows.Graphics.DisplayId);
            var type = typeof(Windows.Graphics.DisplayId);

            foreach (var field in type.GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic))
            {
                if (field.FieldType == typeof(ulong))
                {
                    var boxed = (ValueType)id;
                    field.SetValue(boxed, value);
                    return (Windows.Graphics.DisplayId)boxed;
                }
            }

            throw new InvalidOperationException("DisplayId no compatible en este SDK.");
        }
    }
}
