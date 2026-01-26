namespace RewardProgram.Application.Contracts.Auth;

public record RejectRequest
(
    string UserId,
    string Reason
);
