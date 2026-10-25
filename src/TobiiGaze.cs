using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace NOTGS
{
    // Reads the Tobii gaze point through Stream Engine on a background thread. Kept Unity-free so
    // tools/GazeCheck can compile and run it against a live tracker outside the game.
    internal static class TobiiGaze
    {
        // Tobii Experience installs Stream Engine here, so the mod loads the user's own copy
        // instead of redistributing Tobii's DLL.
        internal const string DefaultDllPath = @"C:\Program Files\Tobii\Tobii EyeX\tobii_stream_engine.dll";

        private const string Dll = "tobii_stream_engine";
        private const int FieldOfUseInteractive = 1;   // TOBII_FIELD_OF_USE_INTERACTIVE
        private const int ErrorConnectionFailed = 5;   // TOBII_ERROR_CONNECTION_FAILED
        private const int ErrorTimedOut = 6;           // TOBII_ERROR_TIMED_OUT: no data yet, not a failure
        private const int ValidityValid = 1;           // TOBII_VALIDITY_VALID

        [StructLayout(LayoutKind.Sequential)]
        private struct GazePoint { public long TimestampUs; public int Validity; public float X, Y; }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void UrlReceiver(IntPtr url, IntPtr userData);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GazeCallback(ref GazePoint gaze, IntPtr userData);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string path);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int tobii_api_create(out IntPtr api, IntPtr customAlloc, IntPtr customLog);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int tobii_enumerate_local_device_urls(IntPtr api, UrlReceiver receiver, IntPtr userData);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int tobii_device_create(IntPtr api, [MarshalAs(UnmanagedType.LPStr)] string url, int fieldOfUse, out IntPtr device);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int tobii_gaze_point_subscribe(IntPtr device, GazeCallback callback, IntPtr userData);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int tobii_wait_for_callbacks(int deviceCount, IntPtr[] devices);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int tobii_device_process_callbacks(IntPtr device);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int tobii_device_reconnect(IntPtr device);

        private static readonly object Gate = new object();
        private static float lastX, lastY;
        private static long lastStamp;   // Stopwatch timestamp of the last valid sample; 0 = none yet
        private static Thread? thread;
        // Native code holds this delegate for the life of the subscription; a static keeps it from
        // being garbage-collected.
        private static readonly GazeCallback OnGazeCallback = OnGaze;

        internal static void Start(string dllPath, Action<string> log)
        {
            if (thread != null) return;
            thread = new Thread(() => Run(dllPath, log)) { IsBackground = true, Name = "NOTGS Tobii gaze" };
            thread.Start();
        }

        // Latest valid gaze, normalized to the tracker's display (0..1, origin top-left), when it is
        // no older than maxAgeMs. Blinks and glances away leave no valid sample, so they go stale.
        internal static bool TryGetLatest(int maxAgeMs, out float x, out float y)
        {
            long stamp;
            lock (Gate) { x = lastX; y = lastY; stamp = lastStamp; }
            return stamp != 0 && Stopwatch.GetTimestamp() - stamp <= maxAgeMs * Stopwatch.Frequency / 1000;
        }

        // Unity screen pixels start bottom-left; Stream Engine's normalized gaze starts top-left.
        // offsetX/offsetY are the player's correction, in screen pixels (+x right, +y up), for a
        // tracker that reads consistently off to one side.
        // ponytail: maps onto the whole game screen, which holds only when the game runs fullscreen on
        // the monitor the tracker is set up for. Windowed play would need the window rect relative to
        // that display (GetWindowRect) folded in.
        internal static void ToScreen(float x, float y, int width, int height, float offsetX, float offsetY, out float px, out float py)
        {
            px = x * width + offsetX;
            py = (1f - y) * height + offsetY;
        }

        private static void OnGaze(ref GazePoint gaze, IntPtr userData)
        {
            if (gaze.Validity != ValidityValid) return;
            lock (Gate) { lastX = gaze.X; lastY = gaze.Y; lastStamp = Stopwatch.GetTimestamp(); }
        }

        // ponytail: the API and device handles are never destroyed; the thread lives as long as the
        // game process and Windows releases them on exit.
        private static void Run(string dllPath, Action<string> log)
        {
            try
            {
                // Loading by full path first makes the DllImports below bind to this already-loaded module.
                if (LoadLibrary(dllPath) == IntPtr.Zero)
                {
                    log($"Tobii Stream Engine not found at '{dllPath}' (Win32 error {Marshal.GetLastWin32Error()}). Is Tobii Experience installed? Gaze select is off.");
                    return;
                }
                Check(tobii_api_create(out IntPtr api, IntPtr.Zero, IntPtr.Zero), "tobii_api_create");

                string? url = null;
                UrlReceiver receiver = (u, _) => url ??= Marshal.PtrToStringAnsi(u);
                Check(tobii_enumerate_local_device_urls(api, receiver, IntPtr.Zero), "tobii_enumerate_local_device_urls");
                GC.KeepAlive(receiver);
                if (url == null) { log("No Tobii eye tracker found. Gaze select is off."); return; }

                Check(tobii_device_create(api, url, FieldOfUseInteractive, out IntPtr device), "tobii_device_create");
                Check(tobii_gaze_point_subscribe(device, OnGazeCallback, IntPtr.Zero), "tobii_gaze_point_subscribe");
                log($"Reading gaze from {url}.");

                var devices = new[] { device };
                int loggedErr = 0;   // a persistent error is logged once, not every retry
                while (true)
                {
                    int err = tobii_wait_for_callbacks(1, devices);
                    if (err == 0 || err == ErrorTimedOut) err = tobii_device_process_callbacks(device);
                    if (err == 0 || err == ErrorTimedOut) { loggedErr = 0; continue; }

                    if (err == ErrorConnectionFailed)
                    {
                        log("Tobii tracker connection lost. Reconnecting.");
                        while (tobii_device_reconnect(device) != 0) Thread.Sleep(1000);
                        log("Tobii tracker reconnected.");
                    }
                    else
                    {
                        if (err != loggedErr) log($"Tobii Stream Engine error {err}. Retrying every second.");
                        loggedErr = err;
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception e)
            {
                log($"Gaze reader stopped: {e.Message}. Gaze select is off.");
            }
        }

        private static void Check(int err, string call)
        {
            if (err != 0) throw new InvalidOperationException($"{call} failed with Tobii error {err}");
        }
    }
}
