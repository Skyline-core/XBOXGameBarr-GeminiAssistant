using System;
using System.Runtime.InteropServices;
using Windows.UI.Xaml;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// HWND interop for Game Bar widgets (direct casts to ICoreWindowInterop often fail).
    /// </summary>
    internal static class WindowHandleInterop
    {
        private static readonly Guid IidCoreWindowInterop = new Guid("BBE47E6A-366C-4844-B349-EA5EDD4BEACF");
        private static readonly Guid IidInitializeWithWindow = new Guid("3E68D4BD-7135-4D10-8018-9FB6D469F33B");

        [ComImport]
        [Guid("BBE47E6A-366C-4844-B349-EA5EDD4BEACF")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ICoreWindowInterop
        {
            IntPtr WindowHandle { get; }
        }

        [ComImport]
        [Guid("3E68D4BD-7135-4D10-8018-9FB6D469F33B")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IInitializeWithWindow
        {
            void Initialize(IntPtr hwnd);
        }

        public static bool TryGetWindowHandle(out IntPtr hwnd)
        {
            hwnd = IntPtr.Zero;
            var coreWindow = Window.Current?.CoreWindow;
            if (coreWindow == null)
            {
                return false;
            }

            var handle = IntPtr.Zero;
            var ok = TryQueryInterface(
                coreWindow,
                IidCoreWindowInterop,
                ptr =>
                {
                    var interop = (ICoreWindowInterop)Marshal.GetObjectForIUnknown(ptr);
                    handle = interop.WindowHandle;
                    return handle != IntPtr.Zero;
                });

            if (ok)
            {
                hwnd = handle;
            }

            return ok;
        }

        public static bool TrySetOwnerWindow(object winRtObject, IntPtr hwnd)
        {
            if (winRtObject == null || hwnd == IntPtr.Zero)
            {
                return false;
            }

            return TryQueryInterface(
                winRtObject,
                IidInitializeWithWindow,
                ptr =>
                {
                    var init = (IInitializeWithWindow)Marshal.GetObjectForIUnknown(ptr);
                    init.Initialize(hwnd);
                    return true;
                });
        }

        private static bool TryQueryInterface(object obj, Guid iid, Func<IntPtr, bool> useInterface)
        {
            var unknown = IntPtr.Zero;
            var iface = IntPtr.Zero;
            try
            {
                unknown = Marshal.GetIUnknownForObject(obj);
                var hr = Marshal.QueryInterface(unknown, ref iid, out iface);
                if (hr != 0 || iface == IntPtr.Zero)
                {
                    return false;
                }

                return useInterface(iface);
            }
            catch
            {
                return false;
            }
            finally
            {
                if (iface != IntPtr.Zero)
                {
                    Marshal.Release(iface);
                }

                if (unknown != IntPtr.Zero)
                {
                    Marshal.Release(unknown);
                }
            }
        }
    }
}
