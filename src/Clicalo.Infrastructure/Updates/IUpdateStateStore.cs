namespace Clicalo.Infrastructure.Updates;

/// <summary>Where <see cref="UpdateState"/> lives; tests keep it in memory.</summary>
internal interface IUpdateStateStore
{
    /// <summary>The saved state, or null when there is none or it cannot be read.</summary>
    UpdateState? Load();

    /// <summary>Saves <paramref name="state"/>; a failure is ignored (the worst case is a missed «Actualizado»).</summary>
    /// <param name="state">The state.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task SaveAsync(UpdateState state, CancellationToken cancellationToken);
}
