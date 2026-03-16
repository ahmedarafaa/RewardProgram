using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Dashboard;

namespace RewardProgram.Application.Interfaces;

public interface IDashboardService
{
    Task<Result<DashboardResponse>> GetDashboardAsync(string userId, CancellationToken ct = default);
}
