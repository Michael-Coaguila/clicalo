using Clicalo.Domain.Primitives;
using Clicalo.Domain.Search;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>
/// The search filter of «Elegir programa» (EDI-014): a program stays on show when its name has every word of the
/// filter, without case and without accents («camara» finds «Cámara»), as the search of the panel compares text
/// (<see cref="SearchText"/>). Pure; an empty filter shows everything.
/// </summary>
public static class ProgramFilter
{
    private static readonly char[] Separators = [' ', '\t'];

    /// <summary>The comparable form of a name or of the filter.</summary>
    /// <param name="text">The text; null counts as empty.</param>
    public static string Fold(string? text) => SearchText.Fold(text).Trim();

    /// <summary>Whether <paramref name="program"/> stays on show with <paramref name="filter"/>.</summary>
    /// <param name="program">The program.</param>
    /// <param name="filter">What the person typed or dictated, as it is.</param>
    public static bool Matches(ProgramChip program, string? filter)
    {
        ArgumentNullException.ThrowIfNull(program);
        return MatchesFolded(program, Fold(filter));
    }

    /// <summary>How many programs of <paramref name="programs"/> stay on show with <paramref name="filter"/>.</summary>
    /// <param name="programs">The installed programs.</param>
    /// <param name="filter">What the person typed or dictated, as it is.</param>
    public static int Count(ValueList<ProgramChip> programs, string? filter)
    {
        var folded = Fold(filter);
        if (folded.Length == 0)
        {
            return programs.Count;
        }

        var count = 0;
        foreach (var program in programs)
        {
            if (MatchesFolded(program, folded))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Whether <paramref name="program"/> has every word of a filter that is already folded.</summary>
    /// <param name="program">The program.</param>
    /// <param name="foldedFilter">The filter after <see cref="Fold"/>.</param>
    public static bool MatchesFolded(ProgramChip program, string foldedFilter)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(foldedFilter);
        if (foldedFilter.Length == 0)
        {
            return true;
        }

        var name = program.SearchName.Length > 0 ? program.SearchName : Fold(program.Name);
        foreach (var word in foldedFilter.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!name.Contains(word, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
