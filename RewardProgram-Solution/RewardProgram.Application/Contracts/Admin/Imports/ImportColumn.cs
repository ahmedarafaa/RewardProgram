namespace RewardProgram.Application.Contracts.Admin.Imports;

/// <summary>
/// One column of an .xlsx import file: the header its downloadable template emits,
/// every header alias the parser accepts for it, and whether a file must contain it.
/// Template and parser read the same column list, so a column added to one cannot
/// drift out of the other.
/// </summary>
/// <remarks>
/// <c>CanonicalHeader</c> is the English header written into the template; it is
/// always one of <c>Aliases</c> once normalized, so a template downloaded and
/// re-uploaded unchanged always parses. <c>MissingLabel</c> is the bilingual label
/// listed back to the caller when a required column is absent. <c>DescriptionKey</c>
/// is the resource key for the column's guidance line on the template's instructions
/// sheet, and <c>Example</c> a sample value shown on that sheet only — never on the
/// data sheet, where a leftover sample row would be imported as real data.
/// </remarks>
// A class rather than a record: columns are singletons and are used as dictionary
// keys while resolving a header row, so identity — not structural equality over an
// alias set — is the comparison that should apply.
public sealed class ImportColumn(
    string canonicalHeader,
    bool isRequired,
    string missingLabel,
    string descriptionKey,
    string example,
    IReadOnlySet<string> aliases)
{
    public string CanonicalHeader { get; } = canonicalHeader;
    public bool IsRequired { get; } = isRequired;
    public string MissingLabel { get; } = missingLabel;
    public string DescriptionKey { get; } = descriptionKey;
    public string Example { get; } = example;
    public IReadOnlySet<string> Aliases { get; } = aliases;
}

/// <summary>
/// Header-matching rules shared by every .xlsx importer. Aliases are compared after
/// normalization (trim + lower-invariant + single-spaced), so the column ORDER in an
/// uploaded file does not matter: a file exported from this app and a code-first ERP
/// export both import correctly, because each column is matched by header name rather
/// than by position. Within one import, alias sets are kept disjoint so a header can
/// only ever match one column.
/// </summary>
public static class ImportColumns
{
    /// <summary>
    /// Normalizes a header cell for alias matching: trim, lower-case (invariant) and
    /// collapse internal whitespace runs, so matching tolerates casing and stray
    /// spaces. Every alias is stored in this normalized form.
    /// </summary>
    public static string NormalizeHeader(string raw)
    {
        var trimmed = raw.Trim().ToLowerInvariant();
        if (trimmed.Length == 0)
            return string.Empty;

        return string.Join(' ', trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Returns the column a normalized header belongs to, or null when none claims it.
    /// </summary>
    public static ImportColumn? Match(IReadOnlyList<ImportColumn> columns, string normalizedHeader)
    {
        foreach (var column in columns)
        {
            if (column.Aliases.Contains(normalizedHeader))
                return column;
        }

        return null;
    }

    /// <summary>
    /// Maps header text to its 1-based worksheet column number for every column the
    /// header row carries. A repeated column keeps its leftmost occurrence.
    /// </summary>
    public static Dictionary<ImportColumn, int> Resolve(
        IReadOnlyList<ImportColumn> columns, IEnumerable<(string Header, int ColumnNumber)> headerCells)
    {
        var resolved = new Dictionary<ImportColumn, int>();

        foreach (var (header, columnNumber) in headerCells)
        {
            var normalized = NormalizeHeader(header);
            if (normalized.Length == 0)
                continue;

            if (Match(columns, normalized) is { } column)
                resolved.TryAdd(column, columnNumber);
        }

        return resolved;
    }

    /// <summary>
    /// Bilingual labels of the required columns the header row did not provide —
    /// empty when the file's layout is usable.
    /// </summary>
    public static List<string> MissingRequired(
        IReadOnlyList<ImportColumn> columns, IReadOnlyDictionary<ImportColumn, int> resolved)
        => columns
            .Where(c => c.IsRequired && !resolved.ContainsKey(c))
            .Select(c => c.MissingLabel)
            .ToList();
}
