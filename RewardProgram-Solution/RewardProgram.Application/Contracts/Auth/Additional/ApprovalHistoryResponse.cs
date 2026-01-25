using RewardProgram.Domain.Enums.UserEnums;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.DTOs.Auth.Additional;

public record ApprovalHistoryResponse
(
     string ApproverName,
     UserType ApproverType,
     ApprovalAction Action,
     string? RejectionReason,
     RegistrationStatus FromStatus,
     RegistrationStatus ToStatus, 
     DateTime ActionDate 
);
