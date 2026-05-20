using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class ExportErrors
{
    // 422 (not 400): the request itself is well-formed; the *result set* is
    // too large for a single export. The fix is for the admin to refine
    // filters (date range, status, region) so the matching rows fit under the cap.
    public static readonly Error TooManyRows =
        new("Export.TooManyRows", "نتائج كثيرة جداً للتصدير، يرجى تقليل النطاق وإعادة المحاولة", 422);
}
