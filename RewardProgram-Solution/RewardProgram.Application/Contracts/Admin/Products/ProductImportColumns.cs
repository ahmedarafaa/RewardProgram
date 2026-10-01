using RewardProgram.Application.Contracts.Admin.Imports;

namespace RewardProgram.Application.Contracts.Admin.Products;

/// <summary>
/// The product-import column map. Each set covers this app's export header plus the
/// ERP file's Arabic headers, and the sets are disjoint so a header matches exactly
/// one column. See <see cref="ImportColumns"/> for the matching rules.
/// </summary>
public static class ProductImportColumns
{
    public static readonly ImportColumn Name = new(
        canonicalHeader: "Name",
        isRequired: true,
        missingLabel: "Name / الاسم",
        descriptionKey: "ProductImport.Template.Description.Name",
        example: "مصباح LED",
        // "search name" / "اسم البحث" are the dashboard grid's labels for this column,
        // accepted so a sheet headed the way the screen reads still imports.
        aliases: new HashSet<string>(StringComparer.Ordinal)
        {
            "name", "product name", "search name", "الاسم", "اسم الصنف",
            "اسم المنتج", "اسم البحث", "اسم"
        });

    // English-name aliases are deliberately explicit ("english name", "name (en)"…)
    // rather than the bare "product name" the ERP catalogue sheet uses for it —
    // that header is already claimed above for the Arabic name, and re-pointing it
    // would silently remap every file the existing importer accepts today.
    // Canonical header is "Product Name (EN)" — the dashboard grid calls this column
    // "Product Name", but that bare header is an alias of the Arabic name below, so
    // emitting it would silently map the English name onto the wrong column. The
    // "(EN)" suffix keeps the familiar wording without the collision.
    public static readonly ImportColumn NameEn = new(
        canonicalHeader: "Product Name (EN)",
        isRequired: false,
        missingLabel: "Name (EN) / الاسم بالإنجليزية",
        descriptionKey: "ProductImport.Template.Description.NameEn",
        example: "LED Lamp",
        aliases: new HashSet<string>(StringComparer.Ordinal)
        {
            "name (en)", "name en", "nameen", "english name", "product name (en)",
            "product name en", "الاسم بالإنجليزية", "الاسم الانجليزي",
            "اسم المنتج بالإنجليزية", "الاسم بالانجليزية"
        });

    public static readonly ImportColumn ProductCode = new(
        canonicalHeader: "Product Code",
        isRequired: true,
        missingLabel: "Product Code / كود المنتج",
        descriptionKey: "ProductImport.Template.Description.ProductCode",
        example: "P001",
        aliases: new HashSet<string>(StringComparer.Ordinal)
        {
            "product code", "productcode", "code", "كود المنتج", "كود الصنف",
            "رمز المنتج", "رمز الصنف", "الكود", "كود"
        });

    public static readonly ImportColumn Category = new(
        canonicalHeader: "Category",
        isRequired: false,
        missingLabel: "Category / الفئة",
        descriptionKey: "ProductImport.Template.Description.Category",
        example: "إضاءة",
        aliases: new HashSet<string>(StringComparer.Ordinal)
        {
            "category", "الفئة", "المجموعة", "التصنيف"
        });

    public static readonly ImportColumn PointValue = new(
        canonicalHeader: "Point Value",
        isRequired: true,
        missingLabel: "Point Value / قيمة النقاط",
        descriptionKey: "ProductImport.Template.Description.PointValue",
        example: "10",
        aliases: new HashSet<string>(StringComparer.Ordinal)
        {
            "point value", "pointvalue", "points", "قيمة النقاط", "النقاط", "نقاط"
        });

    public static readonly ImportColumn Price = new(
        canonicalHeader: "Price (SAR)",
        isRequired: true,
        missingLabel: "Price / السعر",
        descriptionKey: "ProductImport.Template.Description.Price",
        example: "25.50",
        aliases: new HashSet<string>(StringComparer.Ordinal)
        {
            "price (sar)", "price", "السعر (ر.س)", "السعر (ريال)", "السعر", "سعر"
        });

    /// <summary>
    /// Every column, in the order the template writes them: the dashboard's Products
    /// grid order (code, English name, Arabic/search name, points, price, category),
    /// so the sheet an admin fills in reads like the screen they filled it from.
    /// Order is presentation only — the parser matches by header text, never by
    /// position, so reordering this list cannot change how a file is read.
    /// </summary>
    public static readonly IReadOnlyList<ImportColumn> All =
        [ProductCode, NameEn, Name, PointValue, Price, Category];
}
