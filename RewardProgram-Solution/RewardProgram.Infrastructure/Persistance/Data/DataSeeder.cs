using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums.UserEnums;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Infrastructure.Persistance.Data;

//public static class DataSeeder
//{
//    public static async Task SeedAsync(IServiceProvider serviceProvider)
//    {
//        using var scope = serviceProvider.CreateScope();
//        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
//        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
//        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

//        await SeedRolesAsync(roleManager);
//        await SeedUsersAsync(userManager);
//        await SeedCitySalesManMappingsAsync(context);
//    }

//    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
//    {
//        var roles = new[]
//        {
//            new ApplicationRole { Name = "SystemAdmin", IsDefault = false },
//            new ApplicationRole { Name = "DistrictManager", IsDefault = false },
//            new ApplicationRole { Name = "SalesMan", IsDefault = false },
//            new ApplicationRole { Name = "ShopOwner", IsDefault = true },
//            new ApplicationRole { Name = "Seller", IsDefault = true },
//            new ApplicationRole { Name = "Technician", IsDefault = true }
//        };

//        foreach (var role in roles)
//        {
//            if (!await roleManager.RoleExistsAsync(role.Name!))
//            {
//                await roleManager.CreateAsync(role);
//            }
//        }
//    }

//    private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager)
//    {
//        // System Admin
//        await CreateUserIfNotExists(userManager, new ApplicationUser
//        {
//            Name = "مدير النظام",
//            UserName = "0500000001",
//            MobileNumber = "0500000001",
//            PhoneNumber = "0500000001",
//            UserType = UserType.SystemAdmin,
//            RegistrationStatus = RegistrationStatus.Approved,
//            IsDisabled = false,
//            PhoneNumberConfirmed = true,
//            NationalAddress = new NationalAddress
//            {
//                City = "الرياض",
//                Neighborhood = "العليا",
//                Street = "شارع العليا",
//                BuildingNumber = 1000,
//                SubNumber = 1,
//                PostalCode = "12211"
//            }
//        }, "SystemAdmin");

//        // District Manager - Riyadh
//        var dmRiyadh = await CreateUserIfNotExists(userManager, new ApplicationUser
//        {
//            Name = "مدير منطقة الرياض",
//            UserName = "0500000002",
//            MobileNumber = "0500000002",
//            PhoneNumber = "0500000002",
//            UserType = UserType.DistrictManager,
//            RegistrationStatus = RegistrationStatus.Approved,
//            IsDisabled = false,
//            PhoneNumberConfirmed = true,
//            NationalAddress = new NationalAddress
//            {
//                City = "الرياض",
//                Neighborhood = "الملز",
//                Street = "شارع الضباب",
//                BuildingNumber = 2000,
//                SubNumber = 1,
//                PostalCode = "12629"
//            }
//        }, "DistrictManager");

//        // District Manager - Jeddah
//        var dmJeddah = await CreateUserIfNotExists(userManager, new ApplicationUser
//        {
//            Name = "مدير منطقة جدة",
//            UserName = "0500000003",
//            MobileNumber = "0500000003",
//            PhoneNumber = "0500000003",
//            UserType = UserType.DistrictManager,
//            RegistrationStatus = RegistrationStatus.Approved,
//            IsDisabled = false,
//            PhoneNumberConfirmed = true,
//            NationalAddress = new NationalAddress
//            {
//                City = "جدة",
//                Neighborhood = "الحمراء",
//                Street = "شارع فلسطين",
//                BuildingNumber = 3000,
//                SubNumber = 1,
//                PostalCode = "23326"
//            }
//        }, "DistrictManager");

//        // SalesMan - Riyadh
//        await CreateUserIfNotExists(userManager, new ApplicationUser
//        {
//            Name = "مندوب الرياض",
//            UserName = "0500000004",
//            MobileNumber = "0500000004",
//            PhoneNumber = "0500000004",
//            UserType = UserType.SalesMan,
//            RegistrationStatus = RegistrationStatus.Approved,
//            DistrictManagerId = dmRiyadh?.Id,
//            IsDisabled = false,
//            PhoneNumberConfirmed = true,
//            NationalAddress = new NationalAddress
//            {
//                City = "الرياض",
//                Neighborhood = "النخيل",
//                Street = "شارع الملك فهد",
//                BuildingNumber = 4000,
//                SubNumber = 1,
//                PostalCode = "12345"
//            }
//        }, "SalesMan");

//        // SalesMan - Jeddah
//        await CreateUserIfNotExists(userManager, new ApplicationUser
//        {
//            Name = "مندوب جدة",
//            UserName = "0500000005",
//            MobileNumber = "0500000005",
//            PhoneNumber = "0500000005",
//            UserType = UserType.SalesMan,
//            RegistrationStatus = RegistrationStatus.Approved,
//            DistrictManagerId = dmJeddah?.Id,
//            IsDisabled = false,
//            PhoneNumberConfirmed = true,
//            NationalAddress = new NationalAddress
//            {
//                City = "جدة",
//                Neighborhood = "الروضة",
//                Street = "شارع الأمير سلطان",
//                BuildingNumber = 5000,
//                SubNumber = 1,
//                PostalCode = "23434"
//            }
//        }, "SalesMan");

//        // SalesMan - Makkah (under Jeddah DM)
//        await CreateUserIfNotExists(userManager, new ApplicationUser
//        {
//            Name = "مندوب مكة",
//            UserName = "0500000006",
//            MobileNumber = "0500000006",
//            PhoneNumber = "0500000006",
//            UserType = UserType.SalesMan,
//            RegistrationStatus = RegistrationStatus.Approved,
//            DistrictManagerId = dmJeddah?.Id,
//            IsDisabled = false,
//            PhoneNumberConfirmed = true,
//            NationalAddress = new NationalAddress
//            {
//                City = "مكة",
//                Neighborhood = "العزيزية",
//                Street = "شارع الحج",
//                BuildingNumber = 6000,
//                SubNumber = 1,
//                PostalCode = "24231"
//            }
//        }, "SalesMan");
//    }

//    private static async Task<ApplicationUser?> CreateUserIfNotExists(
//        UserManager<ApplicationUser> userManager,
//        ApplicationUser user,
//        string role)
//    {
//        var existingUser = await userManager.FindByNameAsync(user.UserName!);
//        if (existingUser != null)
//            return existingUser;

//        var result = await userManager.CreateAsync(user);
//        if (result.Succeeded)
//        {
//            await userManager.AddToRoleAsync(user, role);
//            return user;
//        }

//        return null;
//    }

//    private static async Task SeedCitySalesManMappingsAsync(ApplicationDbContext context)
//    {
//        if (await context.CitySalesManMappings.AnyAsync())
//            return;

//        var salesManRiyadh = await context.Users
//            .FirstOrDefaultAsync(u => u.MobileNumber == "0500000004");
//        var salesManJeddah = await context.Users
//            .FirstOrDefaultAsync(u => u.MobileNumber == "0500000005");
//        var salesManMakkah = await context.Users
//            .FirstOrDefaultAsync(u => u.MobileNumber == "0500000006");

//        var mappings = new List<CitySalesManMapping>();

//        if (salesManRiyadh != null)
//        {
//            mappings.Add(new CitySalesManMapping
//            {
//                City = "الرياض",
//                SalesManId = salesManRiyadh.Id,
//                IsActive = true
//            });
//        }

//        if (salesManJeddah != null)
//        {
//            mappings.Add(new CitySalesManMapping
//            {
//                City = "جدة",
//                SalesManId = salesManJeddah.Id,
//                IsActive = true
//            });
//        }

//        if (salesManMakkah != null)
//        {
//            mappings.Add(new CitySalesManMapping
//            {
//                City = "مكة",
//                SalesManId = salesManMakkah.Id,
//                IsActive = true
//            });
//        }

//        if (mappings.Any())
//        {
//            await context.CitySalesManMappings.AddRangeAsync(mappings);
//            await context.SaveChangesAsync();
//        }
//    }
//}