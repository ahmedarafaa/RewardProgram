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
[Authorize(Roles = $"{UserRoles.ShopOwner},{UserRoles.Seller},{UserRoles.Technician}")]
public class WalletController : ControllerBase
{
    private readonly IWalletService _walletService;

    public WalletController(IWalletService walletService)
    {
        _walletService = walletService;
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
    [ProducesResponseType(typeof(PaginatedResult<WalletTransactionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions([FromQuery] WalletTransactionListQuery query, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _walletService.GetTransactionsAsync(userId, query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
