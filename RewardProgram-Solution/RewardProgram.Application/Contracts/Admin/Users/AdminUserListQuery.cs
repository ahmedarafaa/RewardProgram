using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminUserListQuery(
    string? Search,
    UserType? UserType,
    RegistrationStatus? RegistrationStatus,
    string? RegionId,
    bool? IsDisabled,
    int Page = 1,
    int PageSize = 20
);
