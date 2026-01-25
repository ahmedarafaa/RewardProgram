using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums.UserEnums;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Domain.Entities;

public class ApprovalRecord : TrackableEntity
{
    public required string UserId { get; set; }           // المستخدم المراد الموافقة عليه
    public required string ApproverId { get; set; }       // من قام بالموافقة/الرفض

    public ApprovalAction Action { get; set; }        
    public string? RejectionReason { get; set; }       // سبب الرفض (إن وجد)

    public RegistrationStatus FromStatus { get; set; } // الحالة السابقة
    public RegistrationStatus ToStatus { get; set; }   // الحالة الجديدة

    // Navigation
    public ApplicationUser User { get; set; } = null!;
    public ApplicationUser Approver { get; set; } = null!;
}
