using System;
using System.Diagnostics;
using System.Threading;
using NOTGS;

// Exits 0 when the mod's own gaze reader delivers a fresh gaze point within 5 s. Needs eye
// tracking on and someone looking at the screen. Optional argument: path to tobii_stream_engine.dll.

static void Check(bool ok, string what)
{
    if (!ok) throw new Exception("FAIL: " + what);
}

TobiiGaze.ToScreen(0f, 0f, 1920, 1080, out float px, out float py);
Check(px == 0f && py == 1080f, "gaze at the top-left must map to Unity's top-left pixel (0, height)");
TobiiGaze.ToScreen(1f, 1f, 1920, 1080, out px, out py);
Check(px == 1920f && py == 0f, "gaze at the bottom-right must map to Unity's bottom-right pixel (width, 0)");

TobiiGaze.Start(args.Length > 0 ? args[0] : TobiiGaze.DefaultDllPath, Console.WriteLine);
var clock = Stopwatch.StartNew();
while (clock.Elapsed.TotalSeconds < 5)
{
    if (TobiiGaze.TryGetLatest(200, out float x, out float y))
    {
        Console.WriteLine($"OK: fresh gaze at x={x:F3} y={y:F3} (0..1 across the display, origin top-left)");
        return 0;
    }
    Thread.Sleep(50);
}
Console.WriteLine("FAIL: no fresh gaze within 5 s. Is eye tracking on, and are you looking at the screen?");
return 1;
