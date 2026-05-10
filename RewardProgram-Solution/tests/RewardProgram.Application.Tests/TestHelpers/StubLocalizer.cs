using Microsoft.Extensions.Localization;

namespace RewardProgram.Application.Tests.TestHelpers;

/// <summary>
/// Minimal IStringLocalizer stub: returns the key itself as the value (with format args
/// applied if provided). Sufficient for service tests that only need a non-null result.
/// </summary>
public class StubLocalizer<T> : IStringLocalizer<T>
{
    public LocalizedString this[string name] => new(name, name);

    public LocalizedString this[string name, params object[] arguments] =>
        new(name, arguments.Length == 0 ? name : string.Format(name + " " + string.Join(" ", arguments), arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        Enumerable.Empty<LocalizedString>();
}
