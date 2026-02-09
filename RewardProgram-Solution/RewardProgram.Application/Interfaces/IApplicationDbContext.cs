using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.OTP;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Application.Interfaces;

public interface IApplicationDbContext
{
    // DbSets
    DbSet<ApplicationUser> Users { get; }
    DbSet<ShopOwnerProfile> ShopOwnerProfiles { get; }
    DbSet<SellerProfile> SellerProfiles { get; }
    DbSet<TechnicianProfile> TechnicianProfiles { get; }
    DbSet<ApprovalRecord> ApprovalRecords { get; }
    DbSet<OtpCode> OtpCodes { get; }
    DbSet<City> Cities { get; set; }
    DbSet<District> Districts { get; set; }
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
