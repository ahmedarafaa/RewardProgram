using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class LookupErrors
{
    public static readonly Error RegionNotFound =
        new("Lookup.RegionNotFound", "المنطقة غير موجودة", 404);

    public static readonly Error CityNotFound =
        new("Lookup.CityNotFound", "المدينة غير موجودة", 404);

    public static readonly Error CustomerCodeNotFound =
        new("Lookup.CustomerCodeNotFound", "كود العميل غير موجود", 404);
}
