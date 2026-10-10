using Clicalo.Application.Interaction;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Timing;

namespace Clicalo.Application.Tests.Interaction;

/// <summary>
/// <see cref="NoticeQueue"/> (AVI-002): one notice at a time; a plain one is replaced by the next; a safety notice and
/// the one with [undo] are never lost; a fixed notice lasts while its state lasts.
/// </summary>
public sealed class NoticeQueueTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Short = Timings.Notices.NoticeDuration;
    private static readonly TimeSpan Long = Timings.Notices.UndoNoticeDuration;

    private static readonly Notice Sent = Plain(L.Released);
    private static readonly Notice Copied = Plain(L.CopiedEmail);
    private static readonly Notice Deleted = new(
        L.Deleted,
        new IconRef("delete"),
        Kind: NoticeKind.Undo
    );
    private static readonly Notice ReleasedOnSwitch = new(
        L.ReleasedSwitch,
        new IconRef("warning"),
        Warning: true,
        Kind: NoticeKind.Safety
    );
    private static readonly Notice ReleasedAlone = new(
        L.ReleasedAuto(count: 30),
        new IconRef("warning"),
        Warning: true,
        Kind: NoticeKind.Safety
    );

    [Fact]
    [Trait("Req", "AVI-002")]
    public void At_rest_nothing_shows()
    {
        NoticeQueue.Empty.Shown.ShouldBeNull();
        NoticeQueue.Empty.EndsAt.ShouldBeNull();
        NoticeQueue.Empty.Advance(Now).ShouldBeSameAs(NoticeQueue.Empty);
    }

    [Fact]
    [Trait("Req", "AVI-002")]
    public void A_notice_shows_for_its_duration_and_then_the_bar_rests()
    {
        var queue = NoticeQueue.Empty.Post(Sent, Short, Now);

        queue.Shown.ShouldBe(Sent);
        queue.EndsAt.ShouldBe(Now + Short);
        queue.Advance(Now + Short - TimeSpan.FromMilliseconds(1)).ShouldBeSameAs(queue);
        queue.Advance(Now + Short).Shown.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "AVI-002")]
    public void A_plain_notice_is_replaced_by_the_next_one()
    {
        var queue = NoticeQueue.Empty.Post(Sent, Short, Now).Post(Copied, Short, Now + Second(1));

        queue.Shown.ShouldBe(Copied);
        queue.Waiting.ShouldBeEmpty();
        queue.EndsAt.ShouldBe(Now + Second(1) + Short);
        queue.Advance(Now + Second(1) + Short).Shown.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "AVI-002")]
    [Trait("Req", "AVI-005")]
    public void A_notice_with_undo_is_not_lost_when_another_arrives()
    {
        var queue = NoticeQueue.Empty.Post(Deleted, Long, Now).Post(Sent, Short, Now + Second(1));

        // One at a time: the new one shows, the one with [undo] waits.
        queue.Shown.ShouldBe(Sent);
        queue.Waiting.ShouldHaveSingleItem().Notice.ShouldBe(Deleted);

        // Once the current one ends it shows again, for its whole 6 s.
        var back = queue.Advance(Now + Second(1) + Short);
        back.Shown.ShouldBe(Deleted);
        back.Shown!.CanUndo.ShouldBeTrue();
        back.EndsAt.ShouldBe(Now + Second(1) + Short + Long);
        back.Waiting.ShouldBeEmpty();
        back.Advance(back.EndsAt!.Value).Shown.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "AVI-002")]
    public void A_safety_notice_is_not_lost_when_another_arrives()
    {
        var queue = NoticeQueue
            .Empty.Post(ReleasedOnSwitch, Short, Now)
            .Post(Sent, Short, Now + Second(1))
            .Post(Copied, Short, Now + Second(2));

        queue.Shown.ShouldBe(Copied);
        queue.Waiting.ShouldHaveSingleItem().Notice.ShouldBe(ReleasedOnSwitch);
        queue.Advance(Now + Second(2) + Short).Shown.ShouldBe(ReleasedOnSwitch);
    }

    [Fact]
    [Trait("Req", "AVI-002")]
    public void Waiting_safety_notices_go_before_the_one_with_undo_and_then_in_order_of_arrival()
    {
        var queue = NoticeQueue
            .Empty.Post(Deleted, Long, Now)
            .Post(ReleasedOnSwitch, Short, Now)
            .Post(ReleasedAlone, Short, Now)
            .Post(Sent, Short, Now);
        queue.Shown.ShouldBe(Sent);

        var first = queue.Advance(queue.EndsAt!.Value);
        first.Shown.ShouldBe(ReleasedOnSwitch);
        var second = first.Advance(first.EndsAt!.Value);
        second.Shown.ShouldBe(ReleasedAlone);
        var third = second.Advance(second.EndsAt!.Value);
        third.Shown.ShouldBe(Deleted);
        third.Advance(third.EndsAt!.Value).Shown.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "AVI-002")]
    [Trait("Req", "AVI-003")]
    public void Only_the_newest_notice_with_undo_is_kept_because_its_button_undoes_that_operation()
    {
        var newer = new Notice(L.CtxUnpinT, new IconRef("push_pin"), Kind: NoticeKind.Undo);

        var onShow = NoticeQueue.Empty.Post(Deleted, Long, Now).Post(newer, Long, Now + Second(1));
        onShow.Shown.ShouldBe(newer);
        onShow.Waiting.ShouldBeEmpty();

        var waiting = NoticeQueue
            .Empty.Post(Deleted, Long, Now)
            .Post(Sent, Short, Now)
            .Post(newer, Long, Now + Second(1));
        waiting.Shown.ShouldBe(newer);
        waiting.Waiting.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "AVI-002")]
    public void The_same_notice_again_restarts_its_time_and_never_waits_twice()
    {
        var again = NoticeQueue
            .Empty.Post(ReleasedOnSwitch, Short, Now)
            .Post(ReleasedOnSwitch, Short, Now + Second(2));
        again.Shown.ShouldBe(ReleasedOnSwitch);
        again.Waiting.ShouldBeEmpty();
        again.EndsAt.ShouldBe(Now + Second(2) + Short);

        var whileWaiting = NoticeQueue
            .Empty.Post(ReleasedOnSwitch, Short, Now)
            .Post(Sent, Short, Now)
            .Post(ReleasedOnSwitch, Short, Now + Second(1));
        whileWaiting.Shown.ShouldBe(ReleasedOnSwitch);
        whileWaiting.Waiting.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "AVI-002")]
    public void A_fixed_notice_lasts_while_its_state_lasts_and_comes_back_after_a_posted_one()
    {
        var hold = new object();
        var holding = Plain(L.HoldingKeys(keys: "Ctrl"));
        var queue = NoticeQueue.Empty.ShowSticky(hold, holding);

        queue.Shown.ShouldBe(holding);
        queue.ShownOwner.ShouldBeSameAs(hold);
        queue.EndsAt.ShouldBeNull();
        queue.Advance(Now + TimeSpan.FromHours(1)).ShouldBeSameAs(queue);

        // Another notice covers it for its duration; the state still lasts, so the fixed notice comes back.
        var covered = queue.Post(Sent, Short, Now);
        covered.Shown.ShouldBe(Sent);
        covered.ShownOwner.ShouldBeNull();
        covered.Advance(Now + Short).Shown.ShouldBe(holding);

        // The state ends: it goes away, whatever shows.
        covered.ClearSticky(hold).Shown.ShouldBe(Sent);
        covered.ClearSticky(hold).Advance(Now + Short).Shown.ShouldBeNull();
        queue.ClearSticky(hold).Shown.ShouldBeNull();
        queue.ClearSticky(new object()).ShouldBeSameAs(queue);
    }

    [Fact]
    [Trait("Req", "AVI-002")]
    public void A_fixed_notice_replaces_a_plain_one_but_waits_for_a_safety_notice_or_an_undo()
    {
        var edit = new object();
        var hint = Plain(L.EditHint);

        NoticeQueue.Empty.Post(Sent, Short, Now).ShowSticky(edit, hint).Shown.ShouldBe(hint);

        var undo = NoticeQueue.Empty.Post(Deleted, Long, Now).ShowSticky(edit, hint);
        undo.Shown.ShouldBe(Deleted);
        undo.Advance(Now + Long).Shown.ShouldBe(hint);

        var safety = NoticeQueue.Empty.Post(ReleasedAlone, Short, Now).ShowSticky(edit, hint);
        safety.Shown.ShouldBe(ReleasedAlone);
        safety.Advance(Now + Short).Shown.ShouldBe(hint);
    }

    [Fact]
    [Trait("Req", "AVI-002")]
    public void With_several_fixed_notices_the_last_one_shown_is_on_top_and_the_others_return()
    {
        var edit = new object();
        var macro = new object();
        var hint = Plain(L.EditHint);
        var step1 = Plain(L.MacroRunning(name: "Informe", index: 1, total: 3));
        var step2 = Plain(L.MacroRunning(name: "Informe", index: 2, total: 3));

        var queue = NoticeQueue.Empty.ShowSticky(edit, hint).ShowSticky(macro, step1);
        queue.Shown.ShouldBe(step1);

        // The same owner updates its notice in place; the same notice again changes nothing.
        var next = queue.ShowSticky(macro, step2);
        next.Shown.ShouldBe(step2);
        next.Sticky.Length.ShouldBe(2);
        next.ShowSticky(macro, step2).ShouldBeSameAs(next);

        next.ClearSticky(macro).Shown.ShouldBe(hint);
        next.ClearSticky(macro).ShownOwner.ShouldBeSameAs(edit);
    }

    [Fact]
    [Trait("Req", "AVI-003")]
    public void An_undone_operation_takes_its_notice_away_on_show_or_waiting()
    {
        var onShow = NoticeQueue
            .Empty.Post(ReleasedOnSwitch, Short, Now)
            .Post(Deleted, Long, Now)
            .Dismiss(NoticeKind.Undo, Now + Second(1));
        onShow.Shown.ShouldBe(ReleasedOnSwitch);
        onShow.EndsAt.ShouldBe(Now + Second(1) + Short);

        var waiting = NoticeQueue.Empty.Post(Deleted, Long, Now).Post(Sent, Short, Now);
        waiting.Dismiss(NoticeKind.Undo, Now).Waiting.ShouldBeEmpty();
        waiting.Dismiss(NoticeKind.Undo, Now).Shown.ShouldBe(Sent);
        waiting.Dismiss(NoticeKind.Safety, Now).ShouldBeSameAs(waiting);
    }

    [Fact]
    [Trait("Req", "AVI-002")]
    public void No_protected_notice_is_ever_lost_whatever_arrives_meanwhile()
    {
        // Every safety notice and the newest undo posted are shown to their end at some point.
        var posted = new[] { Deleted, Sent, ReleasedOnSwitch, Copied, ReleasedAlone, Sent, Copied };
        var queue = NoticeQueue.Empty;
        var at = Now;
        foreach (var notice in posted)
        {
            queue = queue.Post(notice, notice.CanUndo ? Long : Short, at);
            at += TimeSpan.FromMilliseconds(200);
        }

        var completed = new List<Notice>();
        while (queue.EndsAt is { } end)
        {
            completed.Add(queue.Shown!);
            queue = queue.Advance(end);
        }

        completed.ShouldBe([Copied, ReleasedOnSwitch, ReleasedAlone, Deleted]);
        queue.Shown.ShouldBeNull();
    }

    private static Notice Plain(Message text) => new(text, new IconRef("info"));

    private static TimeSpan Second(int count) => TimeSpan.FromSeconds(count);
}
