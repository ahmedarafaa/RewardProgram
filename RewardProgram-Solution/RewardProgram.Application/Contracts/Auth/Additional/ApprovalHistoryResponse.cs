using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Auth.Additional;

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
