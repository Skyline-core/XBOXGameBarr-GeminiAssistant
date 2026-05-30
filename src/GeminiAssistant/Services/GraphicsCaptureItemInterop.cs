using System;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;

namespace GeminiAssistant.Services
{
    internal static class GraphicsCaptureItemInterop
    {
        private static readonly Guid GraphicsCaptureItemIid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
        private static readonly Guid InteropIid = new Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");

        private static readonly Lazy<IGraphicsCaptureItemInterop> Factory =
            new Lazy<IGraphicsCaptureItemInterop>(CreateFactory, true);

        [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int WindowsCreateString(
            [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
            int length,
            out IntPtr hstring);

        [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("combase.dll", PreserveSig = true)]
        private static extern int RoGetActivationFactory(
            IntPtr activatableClassId,
            ref Guid iid,
            out IntPtr factory);

        [ComImport]
        [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IGraphicsCaptureItemInterop
        {
            IntPtr CreateForWindow(IntPtr window, ref Guid iid);
            IntPtr CreateForMonitor(IntPtr monitor, ref Guid iid);
        }

        public static GraphicsCaptureItem CreateForWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                throw new ArgumentException("HWND invalido.", nameof(hwnd));
            }

            return CreateItem((interop, iid) => interop.CreateForWindow(hwnd, ref iid));
        }

        public static GraphicsCaptureItem CreateForMonitor(IntPtr monitor)
        {
            if (monitor == IntPtr.Zero)
            {
                throw new ArgumentException("Monitor invalido.", nameof(monitor));
            }

            return CreateItem((interop, iid) => interop.CreateForMonitor(monitor, ref iid));
        }

        private static GraphicsCaptureItem CreateItem(Func<IGraphicsCaptureItemInterop, Guid, IntPtr> create)
        {
            var interop = Factory.Value;
            var iid = GraphicsCaptureItemIid;
            var ptr = create(interop, iid);
            if (ptr == IntPtr.Zero)
            {
                throw new InvalidOperationException("No se pudo crear el objeto de captura.");
            }

            try
            {
                return (GraphicsCaptureItem)Marshal.GetObjectForIUnknown(ptr);
            }
            finally
            {
                Marshal.Release(ptr);
            }
        }

        private static IGraphicsCaptureItemInterop CreateFactory()
        {
            IntPtr hstring = IntPtr.Zero;
            IntPtr factoryPtr = IntPtr.Zero;
            try
            {
                const string className = "Windows.Graphics.Capture.GraphicsCaptureItem";
                var hr = WindowsCreateString(className, className.Length, out hstring);
                if (hr < 0)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                var iid = InteropIid;
                hr = RoGetActivationFactory(hstring, ref iid, out factoryPtr);
                if (hr < 0)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                return (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "No se pudo inicializar la captura de pantalla: " + ex.Message, ex);
            }
            finally
            {
                if (factoryPtr != IntPtr.Zero)
                {
                    Marshal.Release(factoryPtr);
                }

                if (hstring != IntPtr.Zero)
                {
                    WindowsDeleteString(hstring);
                }
            }
        }
    }
}
