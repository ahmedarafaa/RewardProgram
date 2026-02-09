using RewardProgram.Domain.Enums.UserEnums;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Contracts.Lookups;

public record DistrictResponse(
    string Id,
    string NameAr,
    string NameEn,
    string CityId,
    Zone Zone
);