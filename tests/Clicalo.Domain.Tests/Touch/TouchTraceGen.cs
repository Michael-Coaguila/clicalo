using System.Collections.Immutable;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;
using CsCheck;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>
/// CsCheck generators of pointer traces (blueprint §10.1, <c>TouchTrace</c>): a grid of targets of every kind,
/// the four filter configurations or random slider values (TAC-005), several monitor scales and overlapping contacts
/// with tremor, long rests, swipes, palms and cancellations, plus timer ticks at random moments.
/// </summary>
internal static class TouchTraceGen
{
    /// <summary>Largest contact area of a finger; palms use <see cref="PalmSize"/>.</summary>
    public const int FingerSize = TouchScript.FingerSize;

    /// <summary>A contact area well above every palm threshold of the scales used here.</summary>
    public const int PalmSize = 400;

    private static readonly TouchSettings[] Configurations =
    [
        TouchPresetSettings.Get("standard"),
        TouchPresetSettings.Get("mild-tremor"),
        TouchPresetSettings.Get("strong-tremor"),
        TouchPresetSettings.PersonalSettings,
    ];

    /// <summary>A preset, or random values on the steps of the Touch precision sliders (TAC-005).</summary>
    public static readonly Gen<TouchSettings> Settings = Gen.Frequency(
        (3, Gen.OneOfConst(Configurations)),
        (
            1,
            Gen.Select(
                Gen.Int[0, 20],
                Gen.Int[0, 20],
                Gen.Int[0, 16],
                Gen.Int[0, 30],
                (debounce, slop, cancel, minimum) =>
                    new TouchSettings(
                        TimeSpan.FromMilliseconds(debounce * 50),
                        slop * 2,
                        cancel * 5,
                        TimeSpan.FromMilliseconds(minimum * 10)
                    )
            )
        )
    );

    /// <summary>Scales of common monitors.</summary>
    public static readonly Gen<double> DpiScale = Gen.OneOfConst(1.0, 1.25, 1.5, 2.0);

    /// <summary>Any trace: up to 12 targets and 8 contacts over about 3 s.</summary>
    public static Gen<TouchTrace> Trace { get; } =
        Traces(maxColumns: 4, maxContacts: 8, startWindowMs: 3_000, quiet: false);

    /// <summary>
    /// Busy traces on few targets whose contacts barely move (no swipe, so no swipe lock links the targets): many
    /// contacts per target within the debounce windows.
    /// </summary>
    public static Gen<TouchTrace> QuietTrace { get; } =
        Traces(maxColumns: 3, maxContacts: 12, startWindowMs: 1_500, quiet: true);

    /// <summary>The contacts of <paramref name="trace"/> as steps, keeping only those that <paramref name="keep"/> selects.</summary>
    public static ImmutableArray<TraceStep> Steps(
        TouchTrace trace,
        Func<ContactPlan, bool> keep,
        IEnumerable<int> ticks
    ) => Build([.. trace.Contacts.Where(keep)], ticks);

    private static Gen<TouchTrace> Traces(
        int maxColumns,
        int maxContacts,
        int startWindowMs,
        bool quiet
    ) =>
        Gen.Select(Settings, DpiScale, Targets(maxColumns))
            .SelectMany(
                (settings, dpi, targets) =>
                    Gen.Select(
                        Contact(targets, startWindowMs, quiet).Array[1, maxContacts],
                        Gen.Int[0, startWindowMs + 2_000].Array[0, 6],
                        (plans, ticks) =>
                        {
                            ImmutableArray<ContactPlan> numbered =
                            [
                                .. plans.Select(
                                    (plan, i) => plan with { PointerId = (uint)(i + 1) }
                                ),
                            ];
                            return new TouchTrace(
                                settings,
                                dpi,
                                targets,
                                Build(numbered, ticks),
                                numbered
                            )
                            {
                                Ticks = [.. ticks],
                            };
                        }
                    )
            );

    private static Gen<ImmutableArray<TouchTarget>> Targets(int maxColumns) =>
        Gen.Select(
            Gen.Int[1, maxColumns],
            Gen.Int[1, 3],
            Gen.Int[44, 140],
            Gen.Int[0, 40],
            Gen.Enum<TouchTargetKind>().Array[12, 12],
            (columns, rows, size, gap, kinds) =>
            {
                var targets = ImmutableArray.CreateBuilder<TouchTarget>(columns * rows);
                for (var row = 0; row < rows; row++)
                {
                    for (var column = 0; column < columns; column++)
                    {
                        var index = (row * columns) + column;
                        targets.Add(
                            new TouchTarget(
                                new TouchTargetId(index + 1),
                                new PhysicalRect(
                                    200 + (column * (size + gap)),
                                    200 + (row * (size + gap)),
                                    size,
                                    size
                                ),
                                kinds[index]
                            )
                        );
                    }
                }

                return targets.MoveToImmutable();
            }
        );

    private static Gen<ContactPlan> Contact(
        ImmutableArray<TouchTarget> targets,
        int startWindowMs,
        bool quiet
    )
    {
        var left = targets.Min(t => t.Bounds.Left) - 60;
        var top = targets.Min(t => t.Bounds.Top) - 60;
        var right = targets.Max(t => t.Bounds.Right) + 60;
        var bottom = targets.Max(t => t.Bounds.Bottom) + 60;
        var onTarget = Gen.Select(
            Gen.Int[0, targets.Length - 1],
            Gen.Double[0, 0.999],
            Gen.Double[0, 0.999],
            (i, fx, fy) =>
            {
                var bounds = targets[i].Bounds;
                return new PhysicalPoint(
                    bounds.Left + (int)(fx * bounds.Width),
                    bounds.Top + (int)(fy * bounds.Height)
                );
            }
        );
        var anywhere = Gen.Select(
            Gen.Int[left, right],
            Gen.Int[top, bottom],
            (x, y) => new PhysicalPoint(x, y)
        );
        var start = quiet ? onTarget : Gen.Frequency((7, onTarget), (3, anywhere));

        var tremor = Gen.Select(Gen.Int[-3, 3], Gen.Int[-3, 3]);
        var drift = Gen.Select(Gen.Int[-40, 40], Gen.Int[-40, 40]);
        var sweep = Gen.Select(Gen.Int[-150, 150], Gen.Int[-30, 30]);
        var displacement = quiet ? tremor : Gen.Frequency((5, tremor), (3, drift), (2, sweep));
        var move = Gen.Select(
            Gen.Int[1, 250],
            displacement,
            (delay, d) => (DelayMs: delay, Dx: d.Item1, Dy: d.Item2)
        );
        var end = Gen.Frequency((3, Gen.Int[1, 150]), (2, Gen.Int[150, 1_200]));
        var canceled = Gen.FrequencyConst((9, false), (1, true));
        var size = quiet
            ? Gen.Const(FingerSize)
            : Gen.FrequencyConst((9, FingerSize), (1, PalmSize));

        return Gen.Select(
            Gen.Int[0, startWindowMs],
            start,
            move.Array[0, 6],
            end,
            canceled,
            size,
            (startMs, at, moves, endDelay, cancel, contactSize) =>
                new ContactPlan(0, startMs, at, [.. moves], endDelay, cancel, contactSize)
        );
    }

    private static ImmutableArray<TraceStep> Build(
        ImmutableArray<ContactPlan> plans,
        IEnumerable<int> ticks
    )
    {
        var events = new List<(int Ms, PointerSample Sample)>();
        foreach (var plan in plans)
        {
            var ms = plan.StartMs;
            var at = plan.Start;
            events.Add((ms, Sample(plan, PointerPhase.Down, at, ms)));
            foreach (var (delay, dx, dy) in plan.Moves)
            {
                ms += delay;
                at = new PhysicalPoint(at.X + dx, at.Y + dy);
                events.Add((ms, Sample(plan, PointerPhase.Move, at, ms)));
            }

            ms += plan.EndDelayMs;
            events.Add(
                (ms, Sample(plan, plan.Canceled ? PointerPhase.Cancel : PointerPhase.Up, at, ms))
            );
        }

        var steps = new List<(int Ms, int Order, TraceStep Step)>();
        uint frameId = 0;
        foreach (var group in events.GroupBy(e => e.Ms))
        {
            ImmutableArray<PointerSample> samples =
            [
                .. group.Select(e => e.Sample).OrderBy(s => s.PointerId),
            ];
            var at = TouchScript.At(group.Key);
            steps.Add((group.Key, 0, new TraceStep(at, new PointerFrame(++frameId, at, samples))));
        }

        foreach (var tick in ticks)
        {
            steps.Add((tick, 1, new TraceStep(TouchScript.At(tick), null)));
        }

        return [.. steps.OrderBy(s => s.Ms).ThenBy(s => s.Order).Select(s => s.Step)];
    }

    private static PointerSample Sample(
        ContactPlan plan,
        PointerPhase phase,
        PhysicalPoint at,
        int ms
    ) =>
        new(
            plan.PointerId,
            PointerKind.Finger,
            phase,
            at,
            new PhysicalRect(
                at.X - (plan.ContactSize / 2),
                at.Y - (plan.ContactSize / 2),
                plan.ContactSize,
                plan.ContactSize
            ),
            TouchScript.At(ms),
            PointerInputOrigin.Hardware
        );
}
