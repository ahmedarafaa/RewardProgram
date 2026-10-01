using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RewardProgram.Application.Contracts.Admin.Imports;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.Application.Tests.Helpers;

/// <summary>
/// An import template's instructions sheet is user-facing text, so every key it asks
/// for must resolve in both shipped cultures — a typo'd or unadded key would
/// otherwise print the raw resource name into the workbook the client downloads.
/// </summary>
public class ImportTemplateLocalizationTests
{
    public static TheoryData<string, string> TemplatesAndCultures => new()
    {
        { "products", "en" },
        { "products", "ar" },
        { "erp-customers", "en" },
        { "erp-customers", "ar" },
    };

    [Theory]
    [MemberData(nameof(TemplatesAndCultures))]
    public void Template_ShouldResolveEveryStringItAsksFor(string template, string culture)
    {
        var definition = template == "products"
            ? ImportTemplates.Products
            : ImportTemplates.ErpCustomers;

        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            var factory = new ResourceManagerStringLocalizerFactory(
                Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance);
            var recorder = new RecordingLocalizer(new StringLocalizer<ErrorMessages>(factory));

            // Drives the real builder: whatever text it requests is what gets checked,
            // so a key added to a template later is covered without editing this test.
            ImportTemplateBuilder.Build(new NullWorkbookBuilder(), recorder, definition);

            recorder.Missing.Should().BeEmpty(
                "every {0} template string must exist in the {1} resources", template, culture);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private sealed class RecordingLocalizer(IStringLocalizer<ErrorMessages> inner)
        : IStringLocalizer<ErrorMessages>
    {
        public List<string> Missing { get; } = [];

        public LocalizedString this[string name] => Record(inner[name]);

        public LocalizedString this[string name, params object[] arguments] =>
            Record(inner[name, arguments]);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            inner.GetAllStrings(includeParentCultures);

        private LocalizedString Record(LocalizedString value)
        {
            if (value.ResourceNotFound)
                Missing.Add(value.Name);

            return value;
        }
    }

    // Consumes the sheets without writing a workbook — enumerating the rows is all
    // that is needed to force every localized string to be resolved.
    private sealed class NullWorkbookBuilder : IExcelWorkbookBuilder
    {
        public IExcelWorkbookBuilder AddSheet<T>(
            string sheetName, IEnumerable<T> rows, IReadOnlyList<ExcelColumn<T>> columns)
        {
            foreach (var row in rows)
            {
                foreach (var column in columns)
                    _ = column.ValueSelector(row);
            }

            return this;
        }
    }
}
