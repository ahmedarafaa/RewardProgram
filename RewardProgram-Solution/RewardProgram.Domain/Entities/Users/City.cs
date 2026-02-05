using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Domain.Entities.Users;

public class City : TrackableEntity
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<District> Districts { get; set; } = [];
}
