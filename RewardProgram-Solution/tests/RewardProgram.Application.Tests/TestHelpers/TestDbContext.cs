using Microsoft.EntityFrameworkCore;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.OTP;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Application.Tests.TestHelpers;

public class TestDbContext : DbContext, IApplicationDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    public DbSet<ShopOwnerProfile> ShopOwnerProfiles => Set<ShopOwnerProfile>();
    public DbSet<SellerProfile> SellerProfiles => Set<SellerProfile>();
    public DbSet<TechnicianProfile> TechnicianProfiles => Set<TechnicianProfile>();
    public DbSet<ApprovalRecord> ApprovalRecords => Set<ApprovalRecord>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<ErpCustomer> ErpCustomers => Set<ErpCustomer>();
    public DbSet<ShopData> ShopData => Set<ShopData>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductBarcode> ProductBarcodes => Set<ProductBarcode>();
    public DbSet<ScanRecord> ScanRecords => Set<ScanRecord>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<RewardSettings> RewardSettings => Set<RewardSettings>();
    public DbSet<RedemptionRequest> RedemptionRequests => Set<RedemptionRequest>();
    public DbSet<RedemptionApproval> RedemptionApprovals => Set<RedemptionApproval>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ContactUsContent> ContactUsContents => Set<ContactUsContent>();
    public DbSet<AboutAppContent> AboutAppContents => Set<AboutAppContent>();

    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return new FakeTransaction();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ApplicationUser — configure as a standalone entity for InMemory
        modelBuilder.Entity<ApplicationUser>(b =>
        {
            b.HasKey(u => u.Id);
            b.OwnsOne(u => u.NationalAddress);
            b.OwnsMany(u => u.RefreshTokens);
            b.HasOne(u => u.AssignedSalesMan).WithMany(u => u.AssignedUsers).HasForeignKey(u => u.AssignedSalesManId);
        });

        // Profiles
        modelBuilder.Entity<ShopOwnerProfile>(b =>
        {
            b.HasKey(p => p.Id);
            b.HasOne(p => p.User).WithOne(u => u.ShopOwnerProfile).HasForeignKey<ShopOwnerProfile>(p => p.UserId);
        });
        modelBuilder.Entity<SellerProfile>(b =>
        {
            b.HasKey(p => p.Id);
            b.HasOne(p => p.User).WithOne(u => u.SellerProfile).HasForeignKey<SellerProfile>(p => p.UserId);
        });
        modelBuilder.Entity<TechnicianProfile>(b =>
        {
            b.HasKey(p => p.Id);
            b.HasOne(p => p.User).WithOne(u => u.TechnicianProfile).HasForeignKey<TechnicianProfile>(p => p.UserId);
        });

        // Core entities
        modelBuilder.Entity<Region>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasOne(e => e.ZoneManager).WithOne(u => u.ManagedRegion).HasForeignKey<Region>(e => e.ZoneManagerId);
            b.HasMany(e => e.Cities).WithOne(c => c.Region).HasForeignKey(c => c.RegionId);
        });

        modelBuilder.Entity<City>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasOne(e => e.ApprovalSalesMan).WithMany(u => u.ApprovalCities).HasForeignKey(e => e.ApprovalSalesManId);
            b.HasMany(e => e.Districts).WithOne(d => d.City).HasForeignKey(d => d.CityId);
        });

        modelBuilder.Entity<District>(b => b.HasKey(e => e.Id));
        modelBuilder.Entity<ErpCustomer>(b => b.HasKey(e => e.Id));

        modelBuilder.Entity<ShopData>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasOne(e => e.EnteredByUser).WithMany().HasForeignKey(e => e.EnteredByUserId);
            b.HasOne(e => e.City).WithMany().HasForeignKey(e => e.CityId);
            b.HasOne(e => e.ErpCustomer).WithMany().HasForeignKey(e => e.CustomerCode).HasPrincipalKey(ec => ec.CustomerCode);
        });

        modelBuilder.Entity<ApprovalRecord>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasOne(e => e.User).WithMany(u => u.ApprovalRecords).HasForeignKey(e => e.UserId);
            b.HasOne(e => e.Approver).WithMany().HasForeignKey(e => e.ApproverId);
        });

        modelBuilder.Entity<OtpCode>(b => b.HasKey(e => e.Id));

        // Product / Barcode / Scan
        modelBuilder.Entity<Product>(b => b.HasKey(e => e.Id));

        modelBuilder.Entity<ProductBarcode>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasOne(e => e.Product).WithMany(p => p.Barcodes).HasForeignKey(e => e.ProductId);
            b.HasMany(e => e.ScanRecords).WithOne(s => s.Barcode).HasForeignKey(s => s.BarcodeId);
            b.Property(e => e.RowVersion).IsConcurrencyToken();
        });

        modelBuilder.Entity<ScanRecord>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
        });

        // Wallet
        modelBuilder.Entity<Wallet>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            b.HasMany(e => e.Transactions).WithOne(t => t.Wallet).HasForeignKey(t => t.WalletId);
        });

        modelBuilder.Entity<WalletTransaction>(b => b.HasKey(e => e.Id));
        modelBuilder.Entity<RewardSettings>(b => b.HasKey(e => e.Id));

        // Redemption
        modelBuilder.Entity<RedemptionRequest>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            b.HasOne(e => e.CashHandoverBy).WithMany().HasForeignKey(e => e.CashHandoverById);
            b.HasOne(e => e.RejectedBy).WithMany().HasForeignKey(e => e.RejectedById);
            b.HasMany(e => e.Approvals).WithOne(a => a.RedemptionRequest).HasForeignKey(a => a.RedemptionRequestId);
        });

        modelBuilder.Entity<RedemptionApproval>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasOne(e => e.Approver).WithMany().HasForeignKey(e => e.ApproverId);
        });

        modelBuilder.Entity<Notification>(b => b.HasKey(e => e.Id));
        modelBuilder.Entity<ContactUsContent>(b => b.HasKey(e => e.Id));
        modelBuilder.Entity<AboutAppContent>(b => b.HasKey(e => e.Id));
    }

    public static TestDbContext Create()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
