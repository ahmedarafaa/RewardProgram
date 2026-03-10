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

        var conflict = await query
            .Where(sd => sd.VAT == vat || sd.CRN == crn || sd.ShortAddress == shortAddress)
            .Select(sd => new { sd.VAT, sd.CRN, sd.ShortAddress })
            .FirstOrDefaultAsync(ct);

        if (conflict is null)
            return Result.Success();

        if (conflict.VAT == vat)
            return Result.Failure(ShopDataErrors.VatAlreadyExists);

        if (conflict.CRN == crn)
            return Result.Failure(ShopDataErrors.CrnAlreadyExists);

        return Result.Failure(ShopDataErrors.ShortAddressAlreadyExists);
    }

    public static async Task<Result> ValidateUniqueFieldsPartialAsync(
        IApplicationDbContext context,
        string? vat, string? crn, string? shortAddress,
        string excludeCustomerCode,
        CancellationToken ct = default)
    {
        var hasVat = !string.IsNullOrEmpty(vat);
        var hasCrn = !string.IsNullOrEmpty(crn);
        var hasAddr = !string.IsNullOrEmpty(shortAddress);

        if (!hasVat && !hasCrn && !hasAddr)
            return Result.Success();

        var conflict = await context.ShopData
            .Where(sd => sd.CustomerCode != excludeCustomerCode
                && ((hasVat && sd.VAT == vat)
                    || (hasCrn && sd.CRN == crn)
                    || (hasAddr && sd.ShortAddress == shortAddress)))
            .Select(sd => new { sd.VAT, sd.CRN, sd.ShortAddress })
            .FirstOrDefaultAsync(ct);

        if (conflict is null)
            return Result.Success();

        if (hasVat && conflict.VAT == vat)
            return Result.Failure(ShopDataErrors.VatAlreadyExists);

        if (hasCrn && conflict.CRN == crn)
            return Result.Failure(ShopDataErrors.CrnAlreadyExists);

        return Result.Failure(ShopDataErrors.ShortAddressAlreadyExists);
    }
}
