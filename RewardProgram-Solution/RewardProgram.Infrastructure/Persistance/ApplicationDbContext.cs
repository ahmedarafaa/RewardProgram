using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.OTP;
using RewardProgram.Domain.Entities.Users;
using System.Linq.Expressions;
using System.Reflection;

namespace RewardProgram.Infrastructure.Persistance;


public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor) :
    IdentityDbContext<ApplicationUser, ApplicationRole, string>(options), IApplicationDbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    // DbSets
    public DbSet<City> Cities { get; set; }
    public DbSet<District> Districts { get; set; }
    public DbSet<ShopOwnerProfile> ShopOwnerProfiles { get; set; }
    public DbSet<SellerProfile> SellerProfiles { get; set; }
    public DbSet<TechnicianProfile> TechnicianProfiles { get; set; }
    public DbSet<ApprovalRecord> ApprovalRecords { get; set; }
    public DbSet<OtpCode> OtpCodes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);  // Call base FIRST for Identity

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Apply soft delete query filter to all TrackableEntity derived types
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(TrackableEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(TrackableEntity.IsDeleted));
                var falseConstant = Expression.Constant(false);
                var condition = Expression.Equal(property, falseConstant);
                var lambda = Expression.Lambda(condition, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }

        // Disable cascade delete
        var cascadeFKs = modelBuilder.Model
            .GetEntityTypes()
            .SelectMany(t => t.GetForeignKeys())
            .Where(fk => fk.DeleteBehavior == DeleteBehavior.Cascade && !fk.IsOwnership);

        foreach (var fk in cascadeFKs)
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = _httpContextAccessor.HttpContext?.User.GetUserId();
        var entries = ChangeTracker.Entries<TrackableEntity>();

        foreach (var entityEntry in entries)
        {
            switch (entityEntry.State)
            {
                case EntityState.Added:
                    entityEntry.Property(x => x.CreatedBy).CurrentValue = currentUserId;
                    entityEntry.Property(x => x.CreatedAt).CurrentValue = DateTime.UtcNow;
                    break;

                case EntityState.Modified:
                    entityEntry.Property(x => x.UpdatedBy).CurrentValue = currentUserId;
                    entityEntry.Property(x => x.UpdatedAt).CurrentValue = DateTime.UtcNow;
                    break;

                case EntityState.Deleted:
                    // Convert hard delete to soft delete
                    entityEntry.State = EntityState.Modified;
                    entityEntry.Property(x => x.IsDeleted).CurrentValue = true;
                    entityEntry.Property(x => x.DeletedAt).CurrentValue = DateTime.UtcNow;
                    entityEntry.Property(x => x.DeletedBy).CurrentValue = currentUserId;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}