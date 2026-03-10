using Microsoft.EntityFrameworkCore;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.Application.Helpers;

public static class ShopDataValidationHelper
{
    public static async Task<Result> ValidateUniqueFieldsAsync(
        IApplicationDbContext context,
        string vat, string crn, string shortAddress,
        string? excludeCustomerCode = null,
        CancellationToken ct = default)
    {
        var query = context.ShopData.AsQueryable();

        if (excludeCustomerCode != null)
            query = query.Where(sd => sd.CustomerCode != excludeCustomerCode);

        if (await query.AnyAsync(sd => sd.VAT == vat, ct))
            return Result.Failure(ShopDataErrors.VatAlreadyExists);

        if (await query.AnyAsync(sd => sd.CRN == crn, ct))
            return Result.Failure(ShopDataErrors.CrnAlreadyExists);

        if (await query.AnyAsync(sd => sd.ShortAddress == shortAddress, ct))
            return Result.Failure(ShopDataErrors.ShortAddressAlreadyExists);

        return Result.Success();
    }

    public static async Task<Result> ValidateUniqueFieldsPartialAsync(
        IApplicationDbContext context,
        string? vat, string? crn, string? shortAddress,
        string excludeCustomerCode,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(vat)
            && await context.ShopData.AnyAsync(sd => sd.VAT == vat && sd.CustomerCode != excludeCustomerCode, ct))
            return Result.Failure(ShopDataErrors.VatAlreadyExists);

        if (!string.IsNullOrEmpty(crn)
            && await context.ShopData.AnyAsync(sd => sd.CRN == crn && sd.CustomerCode != excludeCustomerCode, ct))
            return Result.Failure(ShopDataErrors.CrnAlreadyExists);

        if (!string.IsNullOrEmpty(shortAddress)
            && await context.ShopData.AnyAsync(sd => sd.ShortAddress == shortAddress && sd.CustomerCode != excludeCustomerCode, ct))
            return Result.Failure(ShopDataErrors.ShortAddressAlreadyExists);

        return Result.Success();
    }
}
