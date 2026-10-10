using Clicalo.Application.Ports;
using Clicalo.Application.Store;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;
using Clicalo.Domain.Timing;

namespace Clicalo.Application.UseCases.Ai;

/// <summary>
/// «Crear con IA» of Plantillas (PLA-002 to PLA-008, ADR-0014, user decision D5): the AI works only with the person's
/// own key, saved in the Credential Manager, and after an explicit consent. A generation checks, in this order
/// (PLA-005): AI off → «off»; no consent → ask for it; no key → «nokey»; then it sends only the four values of
/// <see cref="TemplateRequest"/> and waits at most <c>Timings.Ai.AiRequestTimeout</c>; the answer passes the structural
/// check of the adapter and the semantic one of <see cref="TemplateSchema"/>. Used by the Workspace role only.
/// </summary>
public sealed class AiAssistant
{
    private readonly DocumentStore _store;
    private readonly ITemplateGenerator _generator;
    private readonly IAiKeyStore _keys;
    private readonly TimeProvider _time;

    /// <summary>Creates it.</summary>
    /// <param name="store">The document, with the AI settings.</param>
    /// <param name="generator">The provider adapter.</param>
    /// <param name="keys">The person's key.</param>
    /// <param name="time">The clock of the time limit.</param>
    public AiAssistant(
        DocumentStore store,
        ITemplateGenerator generator,
        IAiKeyStore keys,
        TimeProvider time
    )
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(time);
        _store = store;
        _generator = generator;
        _keys = keys;
        _time = time;
    }

    /// <summary>The error card in view.</summary>
    public AiError Error { get; private set; }

    /// <summary>Whether the consent card waits for an answer (PLA-004).</summary>
    public bool AskingConsent { get; private set; }

    /// <summary>Whether a generation is running («Generando…»).</summary>
    public bool Generating { get; private set; }

    /// <summary>The program name of the last generation: [Reintentar] repeats it, «Perfil vacío» starts with it.</summary>
    public string LastApp { get; private set; } = string.Empty;

    /// <summary>Whether a key is saved.</summary>
    public bool HasKey => _keys.HasKey();

    private AiSettings Settings => _store.Current.Settings.Ai;

    /// <summary>
    /// [Generar con IA] (PLA-005): the AI proposal, validated, or null with <see cref="Error"/> or
    /// <see cref="AskingConsent"/> set.
    /// </summary>
    /// <param name="appName">The program name; an empty one does nothing (Generar stays disabled).</param>
    /// <param name="layout">The layout of the keyboard line.</param>
    /// <param name="uiLanguage">The interface language.</param>
    /// <param name="isBlocked">Whether a combination is blocked (EJE-014).</param>
    /// <param name="isKnownIcon">Whether an icon exists in the icon library.</param>
    /// <param name="cancellationToken">Cancels the generation.</param>
    public async Task<AiTemplate?> GenerateAsync(
        string appName,
        string layout,
        LangCode uiLanguage,
        Func<KeyChord, bool> isBlocked,
        Func<string, bool> isKnownIcon,
        CancellationToken cancellationToken
    )
    {
        var app = (appName ?? string.Empty).Trim();
        if (app.Length > Timings.Ai.AiAppNameMaxLength)
        {
            app = app[..Timings.Ai.AiAppNameMaxLength].Trim();
        }

        if (app.Length == 0 || Generating)
        {
            return null;
        }

        LastApp = app;
        AskingConsent = false;
        if (Settings.Disabled)
        {
            Error = AiError.Off;
            return null;
        }

        if (!Settings.Consent)
        {
            Error = AiError.None;
            AskingConsent = true;
            return null;
        }

        if (!_keys.HasKey())
        {
            Error = AiError.NoKey;
            return null;
        }

        Error = AiError.None;
        Generating = true;
        try
        {
            var request = new TemplateRequest(
                app,
                layout,
                _store.Current.Settings.Keyboard.AppsLanguage,
                uiLanguage
            );
            var generation = await RunAsync(request, cancellationToken).ConfigureAwait(true);
            if (generation.Proposal is not { } proposal)
            {
                Error = ErrorOf(generation.Failure);
                return null;
            }

            var template = TemplateSchema.Validate(proposal, app, isBlocked, isKnownIcon);
            Error = template is null ? AiError.Invalid : AiError.None;
            return template;
        }
        finally
        {
            Generating = false;
        }
    }

    /// <summary>[Aceptar y generar] (PLA-004): the consent is saved; the caller generates again.</summary>
    public void AcceptConsent()
    {
        AskingConsent = false;
        _ = _store.Dispatch(new SetSetting(SettingPaths.AiConsent, true));
    }

    /// <summary>[No usar IA] (PLA-004): the AI is turned off and the «off» card shows.</summary>
    public void Decline()
    {
        AskingConsent = false;
        _ = _store.Dispatch(new SetSetting(SettingPaths.AiDisabled, true));
        Error = AiError.Off;
    }

    /// <summary>[Activar IA] of the «off» card: the AI is on again and the consent is asked before generating.</summary>
    public void Enable()
    {
        _ = _store.Dispatch(new SetSetting(SettingPaths.AiDisabled, false));
        Error = AiError.None;
        AskingConsent = !Settings.Consent;
    }

    /// <summary>
    /// General › IA (GEN-015, PLA-004): turns the AI on or off. Turning it on from General does not ask for the
    /// consent there: the card of Plantillas asks before the first attempt.
    /// </summary>
    /// <param name="enabled">Whether the AI can be used.</param>
    /// <returns>Whether the setting was written.</returns>
    public bool SetEnabled(bool enabled)
    {
        AskingConsent = false;
        if (Error == AiError.Off)
        {
            Error = AiError.None;
        }

        return _store.Dispatch(new SetSetting(SettingPaths.AiDisabled, !enabled)).IsSuccess;
    }

    /// <summary>General › IA (GEN-015, PLA-004): gives the consent, or revokes it.</summary>
    /// <param name="given">Whether the person consents to sending the four data of [consentD4].</param>
    /// <returns>Whether the setting was written.</returns>
    public bool SetConsent(bool given)
    {
        AskingConsent = false;
        return _store.Dispatch(new SetSetting(SettingPaths.AiConsent, given)).IsSuccess;
    }

    /// <summary>Saves the pasted key (PLA-003, ADR-0008): the document only keeps where it is.</summary>
    /// <param name="key">The key; an empty one is ignored.</param>
    /// <returns>Whether it was saved.</returns>
    public bool SaveKey(Sensitive<string> key)
    {
        if (string.IsNullOrWhiteSpace(key.Value) || !_keys.Save(key))
        {
            return false;
        }

        if (!string.Equals(Settings.ApiKeyRef, _keys.Target, StringComparison.Ordinal))
        {
            _ = _store.Dispatch(new SetSetting(SettingPaths.AiApiKeyRef, _keys.Target));
        }

        if (Error is AiError.NoKey or AiError.BadKey)
        {
            Error = AiError.None;
        }

        return true;
    }

    /// <summary>Deletes the saved key.</summary>
    public void DeleteKey()
    {
        _ = _keys.Delete();
        if (Settings.ApiKeyRef is not null)
        {
            _ = _store.Dispatch(new SetSetting(SettingPaths.AiApiKeyRef, null));
        }
    }

    /// <summary>Hides the error card (Perfil vacío, Ver plantillas, or a new attempt).</summary>
    public void Dismiss()
    {
        Error = AiError.None;
        AskingConsent = false;
    }

    private static AiError ErrorOf(AiFailure failure) =>
        failure switch
        {
            AiFailure.Offline => AiError.Offline,
            AiFailure.NoKey => AiError.NoKey,
            AiFailure.BadKey => AiError.BadKey,
            AiFailure.Unavailable => AiError.Unavailable,
            _ => AiError.Invalid,
        };

    private async Task<TemplateGeneration> RunAsync(
        TemplateRequest request,
        CancellationToken cancellationToken
    )
    {
        using var abort = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            return await _generator
                .GenerateAsync(request, abort.Token)
                .AsTask()
                .WaitAsync(Timings.Ai.AiRequestTimeout, _time, cancellationToken)
                .ConfigureAwait(true);
        }
        catch (TimeoutException)
        {
            await abort.CancelAsync().ConfigureAwait(true);
            return TemplateGeneration.Fail(AiFailure.Offline);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TemplateGeneration.Fail(AiFailure.Offline);
        }
    }
}
