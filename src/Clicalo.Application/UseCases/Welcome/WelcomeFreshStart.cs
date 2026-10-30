using Clicalo.Domain.Document;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;

namespace Clicalo.Application.UseCases.Welcome;

/// <summary>
/// «Empezar de cero» of the welcome that opens on the first start of a new installation that found data from before
/// (proposal P6, NFR-010): how the empty document is built and the clock of its two taps (REG-04).
/// </summary>
/// <param name="Create">
/// The document of a new installation for the person of the current one, or <see langword="null"/> when it cannot be
/// built (nothing is replaced then).
/// </param>
/// <param name="Time">The clock of the two-tap confirmation.</param>
public sealed record WelcomeFreshStart(Func<UserDocument, UserDocument?> Create, TimeProvider Time)
{
    /// <summary>
    /// The fresh start of the product: General and Siempre visible empty (<see cref="FirstDocument"/>), the default
    /// settings and the interface language the person is using, so the welcome goes on in it.
    /// </summary>
    /// <param name="content">The kit, the seed and the templates, read when asked.</param>
    /// <param name="ids">New ids.</param>
    /// <param name="time">The clock.</param>
    public static WelcomeFreshStart Of(
        Func<StarterContent?> content,
        IIdGenerator ids,
        TimeProvider time
    )
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(ids);
        return new WelcomeFreshStart(
            current =>
                content() is { } starter
                && FirstDocument
                    .Create(
                        starter,
                        StarterSelection.Empty,
                        SettingsSchema.Defaults with
                        {
                            Language = current.Settings.Language,
                        },
                        ids
                    )
                    .TryGetValue(out var document)
                    ? document
                    : null,
            time
        );
    }

    /// <summary>
    /// Whether the start must ask (P6): the installer has just installed this copy and the data folder already held a
    /// document that had finished its welcome. A new installation without data, a start Sentinel relaunched and any
    /// later start never ask.
    /// </summary>
    /// <param name="firstRunAfterInstall">The installer started this process right after installing.</param>
    /// <param name="newData">The start created the document (there was no data).</param>
    /// <param name="document">The document of the start.</param>
    public static bool ShouldAsk(bool firstRunAfterInstall, bool newData, UserDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return firstRunAfterInstall && !newData && document.Onboarding.Completed;
    }
}
