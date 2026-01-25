using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Domain.Entities.Users;

public abstract class UserProfile : TrackableEntity
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
}