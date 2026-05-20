using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Wallet;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers;

[ApiController]
[Route("api/wallet")]
[Authorize(Roles = $"{UserRoles.Seller},{UserRoles.Technician}")]
public class WalletController : ControllerBase
{
    private readonly IWalletService _walletService;
    private readonly IRedemptionService _redemptionService;

    public WalletController(IWalletService walletService, IRedemptionService redemptionService)
    {
        _walletService = walletService;
        _redemptionService = redemptionService;
    }

    [HttpGet("balance")]
    [ProducesResponseType(typeof(WalletBalanceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBalance(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _walletService.GetBalanceAsync(userId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("transactions")]
    [ProducesResponseType(typeof(WalletHistoryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions([FromQuery] WalletTransactionListQuery query, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var txResult = await _walletService.GetTransactionsAsync(userId, query, ct);
        if (txResult.IsFailure)
            return txResult.ToProblem();

        // Pin the user's in-flight redemption (if any) above the ledger so its
        // status and cash OTP stay visible in the wallet history screen.
        var activeResult = await _redemptionService.GetActiveRequestAsync(userId, ct);
        if (activeResult.IsFailure)
            return activeResult.ToProblem();

        return Ok(new WalletHistoryResponse(activeResult.Value, txResult.Value));
    }
}
