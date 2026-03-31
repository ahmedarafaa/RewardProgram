using Microsoft.EntityFrameworkCore;
using RewardProgram.Application.Abstractions;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.OTP;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<ShopOwnerProfile> ShopOwnerProfiles { get; }
    DbSet<SellerProfile> SellerProfiles { get; }
    DbSet<TechnicianProfile> TechnicianProfiles { get; }
    DbSet<ApprovalRecord> ApprovalRecords { get; }
    DbSet<OtpCode> OtpCodes { get; }
    DbSet<Region> Regions { get; }
    DbSet<City> Cities { get; }
    DbSet<District> Districts { get; }
    DbSet<ErpCustomer> ErpCustomers { get; }
    DbSet<ShopData> ShopData { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductBarcode> ProductBarcodes { get; }
    DbSet<ScanRecord> ScanRecords { get; }
    DbSet<Wallet> Wallets { get; }
    DbSet<WalletTransaction> WalletTransactions { get; }
    DbSet<RewardSettings> RewardSettings { get; }
    DbSet<RedemptionRequest> RedemptionRequests { get; }
    DbSet<RedemptionApproval> RedemptionApprovals { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<ContactUsContent> ContactUsContents { get; }
    DbSet<AboutAppContent> AboutAppContents { get; }

    Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
