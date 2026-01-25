using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Domain.Entities.Users;

public class CitySalesManMapping : TrackableEntity
{
    public string City { get; set; } = string.Empty;
    public string SalesManId { get; set; } = string.Empty;
    public ApplicationUser SalesMan { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
