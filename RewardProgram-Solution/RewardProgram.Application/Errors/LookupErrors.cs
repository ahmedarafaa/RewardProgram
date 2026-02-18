using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class LookupErrors
{
    public static readonly Error RegionNotFound =
        new("Lookup.RegionNotFound", "المنطقة غير موجودة", 404);

    public static readonly Error CityNotFound =
        new("Lookup.CityNotFound", "المدينة غير موجودة", 404);

    public static readonly Error DistrictNotFound =
        new("Lookup.DistrictNotFound", "الحي غير موجود", 404);
}
