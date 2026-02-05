using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Contracts.Lookups;

public record CityResponse(
    int Id,
    string NameAr,
    string NameEn
);