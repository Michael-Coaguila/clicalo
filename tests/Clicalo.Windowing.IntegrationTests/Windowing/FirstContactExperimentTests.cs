using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.TestKit;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Automation;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// TEMPORARY diagnostic (m2/fix-desk): where does the first contact of a synthetic finger land? A catcher surface of this
/// process covers the primary monitor, so a contact that misses its target lands on it and nowhere else. Only on the CI
/// runner. Records outcomes; asserts nothing.
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
public sealed class FirstContactExperimentTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromMilliseconds(1500);
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [DesktopFact]
    public async Task Where_first_contacts_land()
    {
        Assert.SkipUnless(SystemContrastSwitch.IsAllowed, "Diagnostic: CI runner only.");
        var origin = Stopwatch.GetTimestamp();
        var results = new List<Dictionary<string, string>>();
        using var foreground = ForegroundLog.Start();
        using var lab = SurfaceLab.Create();
        var (monitor, center) = PrimaryMonitor();
        var catcher = lab.CreateSurface(SurfaceKind.Panel, 90, 100, 100);
        var target = lab.CreateSurface(SurfaceKind.Bubble, 90, 64, 64);
        WpfThread.Invoke(() =>
        {
            catcher.MovePassive(monitor);
            catcher.ShowPassive();
            target.MovePassive(new PhysicalRect(center.X - 40, center.Y - 40, 80, 80));
            target.ShowPassive();
        });
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // E: when does a created device appear in GetPointerDevices (no injection)?
        for (var i = 0; i < 5; i++)
        {
            results.Add(DeviceArrival(i));
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        // A: a fresh device for every tap on a long-lived window.
        for (var i = 0; i < 25; i++)
        {
            using var finger = new SyntheticPointer(
                SyntheticPointerKind.Finger,
                [Environment.ProcessId]
            );
            results.Add(await TapAsync("A-fresh-device", i, finger, target, catcher, center));
            await Task.Delay(250, TestContext.Current.CancellationToken);
        }

        // B: one device, warmed up, on the same long-lived window.
        using (
            var warmed = new SyntheticPointer(SyntheticPointerKind.Finger, [Environment.ProcessId])
        )
        {
            for (var i = 0; i < 25; i++)
            {
                results.Add(await TapAsync("B-same-device", i, warmed, target, catcher, center));
                await Task.Delay(250, TestContext.Current.CancellationToken);
            }

            // C: a window shown right before the tap, with the warmed device.
            for (var i = 0; i < 15; i++)
            {
                var fresh = ShowFresh(lab, 100 + i, center, i);
                results.Add(
                    await TapAsync("C-fresh-window", i, warmed, fresh, catcher, CenterOf(fresh))
                );
                lab.Close(fresh);
                await Task.Delay(250, TestContext.Current.CancellationToken);
            }
        }

        // D: a window shown right before the tap and a fresh device.
        for (var i = 0; i < 15; i++)
        {
            var fresh = ShowFresh(lab, 200 + i, center, i);
            using var finger = new SyntheticPointer(
                SyntheticPointerKind.Finger,
                [Environment.ProcessId]
            );
            results.Add(
                await TapAsync(
                    "D-fresh-window-fresh-device",
                    i,
                    finger,
                    fresh,
                    catcher,
                    CenterOf(fresh)
                )
            );
            lab.Close(fresh);
            await Task.Delay(250, TestContext.Current.CancellationToken);
        }

        var trace = new List<string>();
        trace.AddRange(PointerFrameTrace.Since(origin));
        trace.AddRange(foreground.Relative(origin));
        trace.AddRange(catcher.PointerLogSince(origin));
        trace.AddRange(target.PointerLogSince(origin));
        var folder = RepoPaths.Combine("artifacts", "cl", "test-results");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "first-contact-experiment.json"),
            JsonSerializer.Serialize(new { results, trace }, Indented)
        );
        foreach (var group in results.GroupBy(r => r["phase"], StringComparer.Ordinal))
        {
            TestContext.Current.TestOutputHelper?.WriteLine(
                group.Key
                    + ": "
                    + string.Join(
                        ", ",
                        group
                            .GroupBy(
                                r => r.GetValueOrDefault("outcome", "-"),
                                StringComparer.Ordinal
                            )
                            .Select(o => o.Key + "=" + o.Count())
                    )
            );
        }
    }

    private static TestSurface ShowFresh(SurfaceLab lab, int instance, NativePoint center, int i)
    {
        var fresh = lab.CreateSurface(SurfaceKind.Bubble, instance, 64, 64);
        WpfThread.Invoke(() =>
        {
            fresh.MovePassive(
                new PhysicalRect(center.X - 300 + ((i % 5) * 120), center.Y + 150, 80, 80)
            );
            fresh.ShowPassive();
        });
        return fresh;
    }

    private static NativePoint CenterOf(TestSurface surface)
    {
        var bounds = NativeSurface.Bounds(surface.Handle);
        return new NativePoint(bounds.CenterX, bounds.CenterY);
    }

    private static async Task<Dictionary<string, string>> TapAsync(
        string phase,
        int index,
        SyntheticPointer finger,
        TestSurface target,
        TestSurface catcher,
        NativePoint point
    )
    {
        var targetDowns = target.PointerDowns;
        var targetUps = target.PointerUps;
        var catcherDowns = catcher.PointerDowns;
        var catcherUps = catcher.PointerUps;
        var before = Stopwatch.GetTimestamp();
        string outcome;
        try
        {
            finger.Tap(point.X, point.Y);
            var deadline = Stopwatch.GetTimestamp();
            while (
                target.PointerUps == targetUps
                && catcher.PointerUps == catcherUps
                && Stopwatch.GetElapsedTime(deadline) < Wait
            )
            {
                await Task.Delay(5, TestContext.Current.CancellationToken);
            }

            outcome =
                target.PointerUps > targetUps ? "target"
                : catcher.PointerUps > catcherUps ? "catcher"
                : "none";
        }
        catch (InjectionRefusedException ex)
        {
            outcome = "refused: " + ex.Message;
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["phase"] = phase,
            ["index"] = index.ToString(CultureInfo.InvariantCulture),
            ["outcome"] = outcome,
            ["ms"] = Stopwatch
                .GetElapsedTime(before)
                .TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture),
            ["targetDowns"] = (target.PointerDowns - targetDowns).ToString(
                CultureInfo.InvariantCulture
            ),
            ["catcherDowns"] = (catcher.PointerDowns - catcherDowns).ToString(
                CultureInfo.InvariantCulture
            ),
            ["point"] = string.Create(CultureInfo.InvariantCulture, $"{point.X},{point.Y}"),
            ["devices"] = DeviceSummary(),
        };
    }

    private static Dictionary<string, string> DeviceArrival(int index)
    {
        var before = DeviceSummary();
        var created = Stopwatch.GetTimestamp();
        var device = CreateSyntheticPointerDevice(2, 1, 3);
        var seen = before;
        double? changedAt = null;
        while (Stopwatch.GetElapsedTime(created) < TimeSpan.FromMilliseconds(500))
        {
            var now = DeviceSummary();
            if (!string.Equals(now, before, StringComparison.Ordinal))
            {
                seen = now;
                changedAt = Stopwatch.GetElapsedTime(created).TotalMilliseconds;
                break;
            }

            Thread.Sleep(1);
        }

        DestroySyntheticPointerDevice(device);
        Thread.Sleep(50);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["phase"] = "E-device-arrival",
            ["index"] = index.ToString(CultureInfo.InvariantCulture),
            ["device"] = string.Create(CultureInfo.InvariantCulture, $"0x{device:X}"),
            ["before"] = before,
            ["after"] = seen,
            ["changedAfterMs"] =
                changedAt?.ToString("0.0", CultureInfo.InvariantCulture) ?? "never (500 ms)",
            ["afterDestroy"] = DeviceSummary(),
        };
    }

    private static string DeviceSummary()
    {
        uint count = 0;
        if (!GetPointerDevices(ref count, null))
        {
            return "GetPointerDevices failed " + Marshal.GetLastPInvokeError();
        }

        var devices = new PointerDeviceInfo[count];
        if (count > 0 && !GetPointerDevices(ref count, devices))
        {
            return "GetPointerDevices failed " + Marshal.GetLastPInvokeError();
        }

        return string.Join(
            "; ",
            devices
                .Take((int)count)
                .Select(d =>
                {
                    var rects = GetPointerDeviceRects(d.Device, out var deviceRect, out var display)
                        ? string.Create(
                            CultureInfo.InvariantCulture,
                            $"display ({display.Left},{display.Top},{display.Right},{display.Bottom}) device ({deviceRect.Left},{deviceRect.Top},{deviceRect.Right},{deviceRect.Bottom})"
                        )
                        : "no rects";
                    return string.Create(
                        CultureInfo.InvariantCulture,
                        $"type {d.Type} handle 0x{d.Device:X} monitor 0x{d.Monitor:X} cursor {d.StartingCursorId} max {d.MaxActiveContacts} '{d.ProductString}' {rects}"
                    );
                })
        );
    }

    private static (PhysicalRect Monitor, NativePoint Center) PrimaryMonitor()
    {
        var (work, _) = NativeSurface.PrimaryWorkArea();
        var width = GetSystemMetrics(0);
        var height = GetSystemMetrics(1);
        return (new PhysicalRect(0, 0, width, height), new NativePoint(work.CenterX, work.CenterY));
    }

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint CreateSyntheticPointerDevice(int type, uint maxCount, int mode);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern void DestroySyntheticPointerDevice(nint device);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPointerDevices(
        ref uint count,
        [In, Out] PointerDeviceInfo[]? devices
    );

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPointerDeviceRects(
        nint device,
        out NativeSurface.Rect deviceRect,
        out NativeSurface.Rect displayRect
    );

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetSystemMetrics(int index);

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct NativePoint(int X, int Y);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PointerDeviceInfo
    {
        public uint DisplayOrientation;
        public nint Device;
        public int Type;
        public nint Monitor;
        public uint StartingCursorId;
        public ushort MaxActiveContacts;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 520)]
        public string ProductString;
    }
}
