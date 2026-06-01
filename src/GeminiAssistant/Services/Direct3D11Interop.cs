using System;
using System.Runtime.InteropServices;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Crea y libera un dispositivo D3D por captura para no interferir con el host de Game Bar.
    /// </summary>
    internal sealed class CaptureD3DDevice : IDisposable
    {
        private const uint D3D11CreateDeviceBgraSupport = 0x20;
        private const uint D3D11SdkVersion = 7;
        private static readonly Guid DxgiDeviceGuid = new Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C");

        private IntPtr _nativeD3dDevice;
        private bool _disposed;

        public IDirect3DDevice Device { get; private set; }

        [DllImport("d3d11.dll", ExactSpelling = true, PreserveSig = true)]
        private static extern int D3D11CreateDevice(
            IntPtr adapter,
            D3DDriverType driverType,
            IntPtr software,
            uint flags,
            IntPtr featureLevels,
            uint featureLevelCount,
            uint sdkVersion,
            out IntPtr device,
            out IntPtr featureLevel,
            out IntPtr immediateContext);

        [DllImport("d3d11.dll", ExactSpelling = true, PreserveSig = true)]
        private static extern int CreateDirect3D11DeviceFromDXGIDevice(
            IntPtr dxgiDevice,
            out IntPtr graphicsDevice);

        public static CaptureD3DDevice Create()
        {
            var lease = new CaptureD3DDevice();
            lease.Initialize();
            return lease;
        }

        private void Initialize()
        {
            var hr = D3D11CreateDevice(
                IntPtr.Zero,
                D3DDriverType.Hardware,
                IntPtr.Zero,
                D3D11CreateDeviceBgraSupport,
                IntPtr.Zero,
                0,
                D3D11SdkVersion,
                out var d3dDevice,
                out _,
                out var immediateContext);

            if (hr < 0)
            {
                throw new COMException("D3D11CreateDevice failed.", hr);
            }

            if (immediateContext != IntPtr.Zero)
            {
                Marshal.Release(immediateContext);
            }

            _nativeD3dDevice = d3dDevice;

            var dxgiDevice = GetDxgiDevicePointer(d3dDevice);
            try
            {
                hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out var inspectable);
                if (hr < 0)
                {
                    throw new COMException("CreateDirect3D11DeviceFromDXGIDevice failed.", hr);
                }

                Device = MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
                Marshal.Release(inspectable);
            }
            finally
            {
                Marshal.Release(dxgiDevice);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Device = null;

            if (_nativeD3dDevice != IntPtr.Zero)
            {
                Marshal.Release(_nativeD3dDevice);
                _nativeD3dDevice = IntPtr.Zero;
            }
        }

        private static IntPtr GetDxgiDevicePointer(IntPtr d3dDevice)
        {
            var hr = Marshal.QueryInterface(d3dDevice, in DxgiDeviceGuid, out var dxgiDevice);
            if (hr < 0)
            {
                throw new COMException("QueryInterface IDXGIDevice failed.", hr);
            }

            return dxgiDevice;
        }

        private enum D3DDriverType
        {
            Hardware = 1
        }
    }
}
