using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Domain.Entities;

public abstract class TrackableEntity
{
    public string Id { get; set; } = Guid.CreateVersion7().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
