using Microsoft.AspNetCore.Http;
using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class LookupErrors
{
    public static readonly Error CityNotFound =
        new("Lookup.CityNotFound", "المدينة غير موجودة", StatusCodes.Status404NotFound);

    public static readonly Error DistrictNotFound =
        new("Lookup.DistrictNotFound", "الحي غير موجود", StatusCodes.Status404NotFound);
}