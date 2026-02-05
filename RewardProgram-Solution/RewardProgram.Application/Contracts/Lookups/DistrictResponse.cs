using RewardProgram.Domain.Enums.UserEnums;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Contracts.Lookups;

public record DistrictResponse(
    int Id,
    string NameAr,
    string NameEn,
    int CityId,
    Zone Zone
);