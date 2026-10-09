using Clicalo.Application.Confirmation;
using Clicalo.Application.Localization;
using Clicalo.Application.Store;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Messages;
using Clicalo.Presentation.Panel.EditMode;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel.ContextMenu;

/// <summary>
/// «Reiniciar Frecuentes» (FRE-004, GEN-013, REG-04): the first tap arms the button, which says [delConfirm] for 3.5 s;
/// the second empties usage, pins and hidden, says [resetFreqT] and offers undo, which restores the three. It lives with
/// the menu that pins and hides because the three curate Frequents; the General page of the control center shows it.
/// </summary>
/// <remarks>
/// The two taps are <see cref="TwoStepConfirm"/>'s, the only issuer of a <see cref="ConfirmationToken"/> (CLC0010);
/// the reset is the destructive <see cref="ResetFrequents"/>, which keeps a backup first (DAT-006). Lives on the thread
/// of the window that shows it; the timer that disarms it comes back through <c>post</c>.
/// </remarks>
public sealed class ResetFrequentsViewModel : ObservableObject
{
    private const string ResetIcon = "restart_alt";

    private static readonly ConfirmationSubject Subject = new(nameof(ResetFrequents), "frequents");

    private readonly DocumentStore _store;
    private readonly TwoStepConfirm _confirm;
    private readonly ILocalizationContext _localization;
    private readonly IPanelNoticeSink _notices;
    private readonly TimeProvider _time;
    private readonly Action<Action> _post;
    private ITimer? _disarm;
    private bool _isArmed;
    private string _label = string.Empty;
    private string _description = string.Empty;
    private string _buttonText = string.Empty;

    /// <summary>Creates the button, disarmed.</summary>
    /// <param name="store">The document.</param>
    /// <param name="confirm">The two taps of destructive operations (one per role).</param>
    /// <param name="localization">The interface language.</param>
    /// <param name="notices">Where [resetFreqT] and its undo show.</param>
    /// <param name="time">The clock of the armed state.</param>
    /// <param name="post">Runs an action on the window's thread without waiting.</param>
    public ResetFrequentsViewModel(
        DocumentStore store,
        TwoStepConfirm confirm,
        ILocalizationContext localization,
        IPanelNoticeSink notices,
        TimeProvider time,
        Action<Action> post
    )
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(confirm);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(notices);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(post);
        _store = store;
        _confirm = confirm;
        _localization = localization;
        _notices = notices;
        _time = time;
        _post = post;
        Relocalize();
    }

    /// <summary>Whether the first tap armed it (danger fill, [delConfirm]).</summary>
    public bool IsArmed
    {
        get => _isArmed;
        private set
        {
            if (SetProperty(ref _isArmed, value))
            {
                ApplyButtonText();
            }
        }
    }

    /// <summary>[resetFreq] «Reiniciar Frecuentes», the row's title and the button's accessible name.</summary>
    public string Label
    {
        get => _label;
        private set => SetProperty(ref _label, value);
    }

    /// <summary>[resetFreqD], the row's description.</summary>
    public string Description
    {
        get => _description;
        private set => SetProperty(ref _description, value);
    }

    /// <summary>What the button says: [resetFreq], or [delConfirm] while armed.</summary>
    public string ButtonText
    {
        get => _buttonText;
        private set => SetProperty(ref _buttonText, value);
    }

    /// <summary>A tap on the button.</summary>
    public void Tap()
    {
        switch (_confirm.Tap(Subject))
        {
            case TwoStepResult.Armed armed:
                IsArmed = true;
                _disarm?.Dispose();
                var wait = armed.Until - _time.GetUtcNow();
                _disarm = _time.CreateTimer(
                    _ => _post(() => IsArmed = false),
                    null,
                    wait > TimeSpan.Zero ? wait : TimeSpan.Zero,
                    Timeout.InfiniteTimeSpan
                );
                break;
            case TwoStepResult.Confirmed confirmed:
                _disarm?.Dispose();
                IsArmed = false;
                if (_store.Dispatch(new ResetFrequents(), confirmed.Token).IsSuccess)
                {
                    _notices.Notify(
                        new PanelNotice(
                            L.ResetFreqT,
                            new IconRef(ResetIcon),
                            NoticeTone.Notice,
                            CanUndo: _store.CanUndo
                        )
                    );
                }

                break;
        }
    }

    /// <summary>Formats every text again in the current language (IDI-001).</summary>
    public void Relocalize()
    {
        var localizer = _localization.Current;
        Label = localizer.Format(L.ResetFreq);
        Description = localizer.Format(L.ResetFreqD);
        ApplyButtonText();
    }

    private void ApplyButtonText() =>
        ButtonText = _localization.Current.Format(_isArmed ? L.DelConfirm : L.ResetFreq);
}
