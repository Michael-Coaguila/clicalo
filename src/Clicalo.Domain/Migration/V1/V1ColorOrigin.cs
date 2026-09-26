namespace Clicalo.Domain.Migration.V1;

/// <summary>Where the colour of a v1 button came from, which decides whether the report mentions it (PQ-08).</summary>
internal enum V1ColorOrigin
{
    /// <summary>No colour: the v1 default (<c>#2980B9</c>) applies.</summary>
    Default,

    /// <summary>One of the nine colours of the v1 palette: its category is its equivalent.</summary>
    Palette,

    /// <summary>A colour chosen with the colour picker: mapped to the closest category and reported.</summary>
    Custom,

    /// <summary>Not a colour (EC-MIG-05): the default category and a report line, without failing.</summary>
    Invalid,
}
