using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RewardProgram.Domain.Constants;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;
using RewardProgram.Application.Helpers;

namespace RewardProgram.Infrastructure.Persistance.Data;

public static class DataSeeder
{
    private static int _mobileCounter;

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        await SeedRolesAsync(roleManager, logger);
        var users = await SeedUsersAsync(userManager, logger);
        await SeedRegionsAndCitiesAsync(context, users, logger);
        await SeedErpCustomersAsync(context, logger);
        await SeedProductsAsync(context, logger);
        await SeedRewardSettingsAsync(context, logger);
        try
        {
            await SeedDemoAnalyticsDataAsync(context, userManager, users, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed demo analytics data — app will continue without it");
        }
    }

    #region Roles

    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager, ILogger logger)
    {
        var roles = new (string Name, bool IsDefault)[]
        {
            (UserRoles.SystemAdmin, false),
            (UserRoles.ZoneManager, false),
            (UserRoles.SalesMan, false),
            (UserRoles.ShopOwner, true),
            (UserRoles.Seller, true),
            (UserRoles.Technician, true)
        };

        foreach (var (name, isDefault) in roles)
        {
            if (!await roleManager.RoleExistsAsync(name))
            {
                var result = await roleManager.CreateAsync(new ApplicationRole { Name = name, IsDefault = isDefault });
                if (result.Succeeded)
                    logger.LogInformation("Role '{Role}' created", name);
                else
                    logger.LogError("Failed to create role '{Role}': {Errors}", name,
                        string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    #endregion

    #region Users

    private static async Task<Dictionary<string, ApplicationUser>> SeedUsersAsync(
        UserManager<ApplicationUser> userManager, ILogger logger)
    {
        var users = new Dictionary<string, ApplicationUser>();
        _mobileCounter = 1;

        // === SystemAdmin ===
        await CreateUser(userManager, users, logger,
            name: "مدير النظام",
            userType: UserType.SystemAdmin,
            roles: [UserRoles.SystemAdmin],
            usernameOverride: "admin",
            password: "Admin@123");

        await CreateUser(userManager, users, logger,
            name: "مدير النظام - تست",
            userType: UserType.SystemAdmin,
            roles: [UserRoles.SystemAdmin],
            mobileOverride: "+201121007505",
            usernameOverride: "admin.test",
            password: "Admin@123");

        // === Pure ZoneManagers (not also salesmen) ===
        await CreateUser(userManager, users, logger,
            name: "فرحان ممدوح",
            userType: UserType.ZoneManager,
            roles: [UserRoles.ZoneManager]);

        await CreateUser(userManager, users, logger,
            name: "الطيب حسين",
            userType: UserType.ZoneManager,
            roles: [UserRoles.ZoneManager]);

        await CreateUser(userManager, users, logger,
            name: "محمد العجوز",
            userType: UserType.ZoneManager,
            roles: [UserRoles.ZoneManager]);

        await CreateUser(userManager, users, logger,
            name: "نيازي عمر",
            userType: UserType.ZoneManager,
            roles: [UserRoles.ZoneManager]);

        await CreateUser(userManager, users, logger,
            name: "محمد اسماعيل",
            userType: UserType.ZoneManager,
            roles: [UserRoles.ZoneManager]);

        // === Dual-role: ZoneManager + SalesMan ===
        await CreateUser(userManager, users, logger,
            name: "نعيم عوض",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan, UserRoles.ZoneManager]);

        await CreateUser(userManager, users, logger,
            name: "سيد بخيت",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan, UserRoles.ZoneManager]);

        await CreateUser(userManager, users, logger,
            name: "وليد السكري",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan, UserRoles.ZoneManager]);

        // === Pure Salesmen ===
        await CreateUser(userManager, users, logger,
            name: "محمود حجازي",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "احمد سمير",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "احمد جمال",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "عبد الرحمن خالد",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "محمد المشير",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "محمد اياد",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "سعيد عبد القادر",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "محمود الزين",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "عباس الفاضل",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "محمد خميس",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "شريف محسن",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "عمرو مدبولي",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "هشام كشك",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "محمد خطاب",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "احمد عاطف",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "احمد السيد",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "محمد جمال",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "محمد حسام",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "عادل امام",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "خالد حشيش",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "احمد عمر الزيات",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        await CreateUser(userManager, users, logger,
            name: "سليمان حبيب",
            userType: UserType.SalesMan,
            roles: [UserRoles.SalesMan]);

        return users;
    }

    private static async Task CreateUser(
        UserManager<ApplicationUser> userManager,
        Dictionary<string, ApplicationUser> users,
        ILogger logger,
        string name,
        UserType userType,
        string[] roles,
        string? mobileOverride = null,
        string? usernameOverride = null,
        string? password = null)
    {
        var trimmedName = name.Trim();

        // Check if already exists by name
        var existing = await userManager.Users.FirstOrDefaultAsync(u => u.Name == trimmedName);
        if (existing != null)
        {
            // Ensure password is set for existing admin users
            if (password != null && !await userManager.HasPasswordAsync(existing))
            {
                await userManager.AddPasswordAsync(existing, password);
                logger.LogInformation("Password added to existing user '{Name}'", trimmedName);
            }

            // Ensure username is updated for existing admin users
            if (usernameOverride != null && existing.UserName != usernameOverride)
            {
                await userManager.SetUserNameAsync(existing, usernameOverride);
                logger.LogInformation("Username updated for existing user '{Name}' to '{Username}'", trimmedName, usernameOverride);
            }

            users[trimmedName] = existing;
            return;
        }

        var rawMobile = mobileOverride ?? $"05{_mobileCounter:D8}";
        if (mobileOverride is null) _mobileCounter++;
        var mobile = MobileNumberHelper.Normalize(rawMobile);

        var user = new ApplicationUser
        {
            Name = trimmedName,
            UserName = usernameOverride ?? mobile,
            MobileNumber = mobile,
            PhoneNumber = mobile,
            PhoneNumberConfirmed = true,
            UserType = userType,
            RegistrationStatus = RegistrationStatus.Approved,
            IsDisabled = false
        };

        var result = password != null
            ? await userManager.CreateAsync(user, password)
            : await userManager.CreateAsync(user);

        if (!result.Succeeded)
        {
            logger.LogError("Failed to create user '{Name}': {Errors}", trimmedName,
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        foreach (var role in roles)
        {
            var roleResult = await userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
                logger.LogError("Failed to add role '{Role}' to user '{Name}'", role, trimmedName);
        }

        users[trimmedName] = user;
        logger.LogInformation("User '{Name}' created ({UserType}, Username: {Username}, Mobile: {Mobile}, Roles: {Roles})",
            trimmedName, userType, user.UserName, mobile, string.Join("+", roles));
    }

    #endregion

    #region Regions & Cities

    private static async Task SeedRegionsAndCitiesAsync(
        ApplicationDbContext context,
        Dictionary<string, ApplicationUser> users,
        ILogger logger)
    {
        // Region → ZoneManager mapping
        var regionData = new (string NameAr, string NameEn, string ZoneManagerName)[]
        {
            ("الرياض", "Riyadh", "فرحان ممدوح"),
            ("المنطقة الغربية", "Western Region", "نعيم عوض"),
            ("المدينة المنورة", "Madinah", "الطيب حسين"),
            ("الشرقية", "Eastern Region", "سيد بخيت"),
            ("جازان", "Jazan", "محمد العجوز"),
            ("المنطقة الجنوبية", "Southern Region", "نيازي عمر"),
            ("تبوك و الشمال", "Tabuk & Northern", "محمد اسماعيل"),
            ("القصيم", "Qassim", "وليد السكري"),
        };

        var regions = new Dictionary<string, Region>();
        var existingRegions = await context.Regions.IgnoreQueryFilters().ToDictionaryAsync(r => r.NameAr);

        foreach (var (nameAr, nameEn, zmName) in regionData)
        {
            var trimmedZm = zmName.Trim();
            var foundUser = users.TryGetValue(trimmedZm, out var zm);

            if (!foundUser)
                logger.LogWarning("ZoneManager '{ZM}' not found in users dictionary for region '{Region}'", trimmedZm, nameAr);

            if (existingRegions.TryGetValue(nameAr, out var existing))
            {
                // Update ZoneManagerId if missing
                if (existing.ZoneManagerId is null && zm is not null)
                {
                    existing.ZoneManagerId = zm.Id;
                    logger.LogInformation("Region '{Region}' updated ZoneManagerId → {ZM}", nameAr, trimmedZm);
                }
                regions[nameAr] = existing;
            }
            else
            {
                var region = new Region
                {
                    NameAr = nameAr,
                    NameEn = nameEn,
                    ZoneManagerId = zm?.Id,
                    CreatedBy = "DataSeeder"
                };

                context.Regions.Add(region);
                regions[nameAr] = region;
                logger.LogInformation("Region '{Region}' created (ZoneManager: {ZM}, Id: {ZmId})",
                    nameAr, trimmedZm, zm?.Id ?? "NULL");
            }
        }

        await context.SaveChangesAsync();

        // City data: (RegionNameAr, CityNameAr, CityNameEn, SalesManName)
        var cityData = new (string Region, string CityAr, string CityEn, string SalesMan)[]
        {
            // === الرياض ===
            ("الرياض", "الرياض", "Riyadh", "محمود حجازي"),
            ("الرياض", "الخرج", "Al Kharj", "احمد سمير"),
            ("الرياض", "الأفلاج", "Al Aflaj", "احمد سمير"),
            ("الرياض", "القويعية", "Al Quway'iyah", "احمد جمال"),
            ("الرياض", "عفيف", "Afif", "احمد جمال"),
            ("الرياض", "الدرعية", "Ad Diriyah", "عبد الرحمن خالد"),
            ("الرياض", "حريملاء", "Huraymila", "عبد الرحمن خالد"),
            ("الرياض", "رماح", "Rumah", "عبد الرحمن خالد"),
            ("الرياض", "السيح", "As Sih", "احمد سمير"),
            ("الرياض", "الدلم", "Ad Dilam", "احمد سمير"),
            ("الرياض", "ليلى", "Layla", "احمد سمير"),
            ("الرياض", "المزاحمية", "Al Muzahimiyah", "محمود حجازي"),

            // === المنطقة الغربية ===
            ("المنطقة الغربية", "مكة", "Makkah", "محمد المشير"),
            ("المنطقة الغربية", "جدة", "Jeddah", "محمد اياد"),
            ("المنطقة الغربية", "الطائف", "Taif", "محمد المشير"),
            ("المنطقة الغربية", "رابغ", "Rabigh", "سعيد عبد القادر"),
            ("المنطقة الغربية", "بحرة", "Bahrah", "محمد اياد"),
            ("المنطقة الغربية", "الجموم", "Al Jumum", "محمد المشير"),
            ("المنطقة الغربية", "ثول", "Thuwal", "نعيم عوض"),
            ("المنطقة الغربية", "ذهبان", "Dhahban", "نعيم عوض"),
            ("المنطقة الغربية", "المويه", "Al Muwayh", "نعيم عوض"),
            ("المنطقة الغربية", "تربة", "Turbah", "نعيم عوض"),
            ("المنطقة الغربية", "العرضيات", "Al Ardiyat", "نعيم عوض"),
            ("المنطقة الغربية", "غميقة", "Ghumayqah", "نعيم عوض"),
            ("المنطقة الغربية", "مستورة", "Masturah", "نعيم عوض"),

            // === المدينة المنورة ===
            ("المدينة المنورة", "المدينة", "Madinah", "محمود الزين"),
            ("المدينة المنورة", "ينبع", "Yanbu", "عباس الفاضل"),
            ("المدينة المنورة", "بدر", "Badr", "محمود الزين"),
            ("المدينة المنورة", "الحناكية", "Al Hanakiyah", "عباس الفاضل"),
            ("المدينة المنورة", "الصويدرة", "As Suwadirah", "محمود الزين"),
            ("المدينة المنورة", "ينبع النخل", "Yanbu Al Nakhl", "محمود الزين"),
            ("المدينة المنورة", "العيص", "Al Ays", "محمود الزين"),
            ("المدينة المنورة", "مغيراء", "Mughayra", "محمود الزين"),
            ("المدينة المنورة", "الرايس", "Ar Rayis", "محمود الزين"),

            // === الشرقية ===
            ("الشرقية", "الدمام", "Dammam", "سيد بخيت"),
            ("الشرقية", "الخبر", "Khobar", "محمد خميس"),
            ("الشرقية", "القطيف", "Qatif", "شريف محسن"),
            ("الشرقية", "الأحساء", "Al Ahsa", "عمرو مدبولي"),
            ("الشرقية", "حفر الباطن", "Hafar Al Batin", "احمد سمير"),
            ("الشرقية", "الجبيل", "Jubail", "شريف محسن"),
            ("الشرقية", "الظهران", "Dhahran", "محمد خميس"),
            ("الشرقية", "بقيق", "Buqayq", "شريف محسن"),
            ("الشرقية", "العزيزية", "Al Aziziyah", "محمد خميس"),
            ("الشرقية", "صفوى", "Safwa", "شريف محسن"),
            ("الشرقية", "تاروت", "Tarut", "شريف محسن"),
            ("الشرقية", "العمران", "Al Omran", "عمرو مدبولي"),
            ("الشرقية", "العيون", "Al Uyun", "عمرو مدبولي"),
            ("الشرقية", "قرية العليا", "Qaryat Al Ulya", "شريف محسن"),
            ("الشرقية", "الجبيل البلد", "Jubail Al Balad", "شريف محسن"),

            // === جازان ===
            ("جازان", "صبيا", "Sabya", "هشام كشك"),
            ("جازان", "بيش", "Baysh", "هشام كشك"),
            ("جازان", "العارضة", "Al Aridah", "هشام كشك"),
            ("جازان", "فيفاء", "Fifa", "هشام كشك"),
            ("جازان", "الطوال", "At Tuwal", "هشام كشك"),
            ("جازان", "الشقيق", "Ash Shuqayq", "هشام كشك"),
            ("جازان", "جازان", "Jazan", "هشام كشك"),
            ("جازان", "أبو عريش", "Abu Arish", "هشام كشك"),
            ("جازان", "صامطة", "Samtah", "هشام كشك"),
            ("جازان", "الدرب", "Ad Darb", "هشام كشك"),

            // === المنطقة الجنوبية ===
            ("المنطقة الجنوبية", "السودة", "As Sudah", "محمد خطاب"),
            ("المنطقة الجنوبية", "رجال ألمع", "Rijal Alma", "محمد خطاب"),
            ("المنطقة الجنوبية", "أحد رفيدة", "Ahad Rufaydah", "محمد خطاب"),
            ("المنطقة الجنوبية", "تنومة", "Tanumah", "محمد خطاب"),
            ("المنطقة الجنوبية", "بارق", "Bariq", "محمد خطاب"),
            ("المنطقة الجنوبية", "تثليث", "Tathlith", "محمد خطاب"),
            ("المنطقة الجنوبية", "أبها", "Abha", "محمد خطاب"),
            ("المنطقة الجنوبية", "خميس مشيط", "Khamis Mushait", "محمد خطاب"),
            ("المنطقة الجنوبية", "النماص", "An Namas", "محمد خطاب"),
            ("المنطقة الجنوبية", "محايل", "Muhayil", "محمد خطاب"),
            ("المنطقة الجنوبية", "بيشة", "Bisha", "محمد خطاب"),
            ("المنطقة الجنوبية", "نجران", "Najran", "احمد عاطف"),
            ("المنطقة الجنوبية", "شرورة", "Sharurah", "احمد عاطف"),
            ("المنطقة الجنوبية", "بدر الجنوب", "Badr Al Janoub", "محمد خطاب"),
            ("المنطقة الجنوبية", "الباحة", "Al Baha", "احمد السيد"),
            ("المنطقة الجنوبية", "بلجرشي", "Baljurashi", "احمد السيد"),
            ("المنطقة الجنوبية", "المندق", "Al Mandaq", "احمد السيد"),
            ("المنطقة الجنوبية", "القنفذة", "Al Qunfudhah", "احمد السيد"),
            ("المنطقة الجنوبية", "الليث", "Al Lith", "احمد السيد"),
            ("المنطقة الجنوبية", "وادي الدواسر", "Wadi Ad Dawasir", "محمد جمال"),
            ("المنطقة الجنوبية", "السليل", "As Sulayyil", "محمد جمال"),
            ("المنطقة الجنوبية", "يدمة", "Yadamah", "احمد عاطف"),
            ("المنطقة الجنوبية", "خباش", "Khabash", "احمد عاطف"),
            ("المنطقة الجنوبية", "حبونا", "Habuna", "احمد عاطف"),
            ("المنطقة الجنوبية", "ثار", "Thar", "احمد عاطف"),
            ("المنطقة الجنوبية", "العقيق", "Al Aqiq", "احمد السيد"),
            ("المنطقة الجنوبية", "القرى", "Al Qura", "احمد السيد"),
            ("المنطقة الجنوبية", "بني حسن", "Bani Hasan", "احمد السيد"),
            ("المنطقة الجنوبية", "غامد الزناد", "Ghamid Az Zinad", "احمد السيد"),

            // === تبوك و الشمال ===
            ("تبوك و الشمال", "تبوك", "Tabuk", "محمد حسام"),
            ("تبوك و الشمال", "الوجه", "Al Wajh", "محمد حسام"),
            ("تبوك و الشمال", "ضباء", "Duba", "محمد حسام"),
            ("تبوك و الشمال", "أملج", "Umluj", "محمد حسام"),
            ("تبوك و الشمال", "عرعر", "Arar", "عادل امام"),
            ("تبوك و الشمال", "رفحاء", "Rafha", "عادل امام"),
            ("تبوك و الشمال", "طريف", "Turaif", "عادل امام"),
            ("تبوك و الشمال", "سكاكا", "Sakaka", "عادل امام"),
            ("تبوك و الشمال", "القريات", "Al Qurayyat", "عادل امام"),
            ("تبوك و الشمال", "دومة الجندل", "Dumat Al Jandal", "عادل امام"),
            ("تبوك و الشمال", "البدع", "Al Bada", "محمد حسام"),
            ("تبوك و الشمال", "حقل", "Haql", "محمد حسام"),
            ("تبوك و الشمال", "شواق", "Shawaq", "محمد حسام"),
            ("تبوك و الشمال", "أبو راكة", "Abu Rakah", "محمد حسام"),
            ("تبوك و الشمال", "الخريبة", "Al Khuraybah", "محمد حسام"),
            ("تبوك و الشمال", "الشعفة", "Ash Sha'fah", "محمد حسام"),
            ("تبوك و الشمال", "العويقيلة", "Al Uwayqilah", "محمد حسام"),
            ("تبوك و الشمال", "جديدة عرعر", "Jadidah Arar", "محمد حسام"),
            ("تبوك و الشمال", "لينة", "Linah", "محمد حسام"),
            ("تبوك و الشمال", "أم خنصر", "Umm Khunsar", "محمد حسام"),
            ("تبوك و الشمال", "صوير", "Suwayr", "عادل امام"),
            ("تبوك و الشمال", "الحديثة", "Al Hadithah", "عادل امام"),
            ("تبوك و الشمال", "طبرجل", "Tabarjal", "عادل امام"),
            ("تبوك و الشمال", "الرديفة", "Ar Radifah", "محمد حسام"),
            ("تبوك و الشمال", "العلا", "Al Ula", "محمد حسام"),

            // === القصيم ===
            ("القصيم", "تمير", "Tumair", "خالد حشيش"),
            ("القصيم", "جلاجل", "Jalajil", "خالد حشيش"),
            ("القصيم", "مرات", "Marat", "خالد حشيش"),
            ("القصيم", "ثادق", "Thadiq", "خالد حشيش"),
            ("القصيم", "الأرطاوية", "Al Artawiyah", "خالد حشيش"),
            ("القصيم", "المجمعة", "Al Majma'ah", "خالد حشيش"),
            ("القصيم", "شقراء", "Shaqra", "خالد حشيش"),
            ("القصيم", "بريدة", "Buraydah", "وليد السكري"),
            ("القصيم", "عنيزة", "Unayzah", "خالد حشيش"),
            ("القصيم", "الرس", "Ar Rass", "احمد عمر الزيات"),
            ("القصيم", "البكيرية", "Al Bukayriyah", "احمد عمر الزيات"),
            ("القصيم", "المذنب", "Al Mithnab", "خالد حشيش"),
            ("القصيم", "الشماسية", "Ash Shimasiyah", "خالد حشيش"),
            ("القصيم", "عيون الجواء", "Uyun Al Jiwa", "خالد حشيش"),
            ("القصيم", "القصيم", "Qassim", "وليد السكري"),
            ("القصيم", "قبة", "Qubbah", "احمد عمر الزيات"),
            ("القصيم", "رياض الخبراء", "Riyadh Al Khabra", "احمد عمر الزيات"),
            ("القصيم", "خضراء", "Khadra", "احمد عمر الزيات"),
            ("القصيم", "حائل", "Hail", "سليمان حبيب"),
            ("القصيم", "بقعاء", "Buqa'a", "سليمان حبيب"),
            ("القصيم", "الغزالة", "Al Ghazalah", "سليمان حبيب"),
            ("القصيم", "الشنان", "Ash Shinan", "سليمان حبيب"),
            ("القصيم", "الشملي", "Ash Shamli", "سليمان حبيب"),
            ("القصيم", "موقق", "Mawqaq", "سليمان حبيب"),
            ("القصيم", "سميراء", "Samira", "سليمان حبيب"),
            ("القصيم", "الروضة", "Ar Rawdah", "سليمان حبيب"),
            ("القصيم", "الحائط", "Al Ha'it", "سليمان حبيب"),
        };

        if (await context.Cities.AnyAsync())
        {
            logger.LogInformation("Cities already seeded, skipping");
            return;
        }

        foreach (var (regionNameAr, cityAr, cityEn, salesManName) in cityData)
        {
            if (!regions.TryGetValue(regionNameAr, out var region))
            {
                logger.LogWarning("Region '{Region}' not found for city '{City}'", regionNameAr, cityAr);
                continue;
            }

            var trimmedSm = salesManName.Trim();
            users.TryGetValue(trimmedSm, out var salesMan);

            var city = new City
            {
                NameAr = cityAr,
                NameEn = cityEn,
                RegionId = region.Id,
                ApprovalSalesManId = salesMan?.Id,
                CreatedBy = "DataSeeder"
            };

            context.Cities.Add(city);
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {RegionCount} regions and {CityCount} cities",
            regionData.Length, cityData.Length);
    }

    #endregion

    #region ErpCustomers

    private static async Task SeedErpCustomersAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.ErpCustomers.AnyAsync())
        {
            logger.LogInformation("ErpCustomers already seeded, skipping");
            return;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("ErpCustomers.csv", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            logger.LogWarning("ErpCustomers.csv embedded resource not found, skipping ERP customer seeding");
            return;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);

        var content = await reader.ReadToEndAsync();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var customers = new List<ErpCustomer>();

        // Skip header line (index 0)
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            var parts = line.Split(';', 2);
            if (parts.Length < 2)
            {
                logger.LogWarning("Skipping malformed CSV line {LineNumber}: {Line}", i + 1, line);
                continue;
            }

            customers.Add(new ErpCustomer
            {
                CustomerCode = parts[0].Trim(),
                CustomerName = parts[1].Trim(),
                CreatedBy = "DataSeeder"
            });
        }

        context.ErpCustomers.AddRange(customers);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} ErpCustomers", customers.Count);
    }

    #endregion

    #region Products

    private static async Task SeedProductsAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.Products.AnyAsync())
        {
            logger.LogInformation("Products already seeded, skipping");
            return;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("Products.csv", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            logger.LogWarning("Products.csv embedded resource not found, skipping product seeding");
            return;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);

        var content = await reader.ReadToEndAsync();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var products = new List<Product>();

        // Skip header line (index 0): كود الصنف;اسم الصنف;المجموعة;السعر;النقاط
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            var parts = line.Split(';');
            if (parts.Length < 5)
            {
                logger.LogWarning("Skipping malformed Products CSV line {LineNumber}: {Line}", i + 1, line);
                continue;
            }

            if (!decimal.TryParse(parts[3].Trim(), out var price))
            {
                logger.LogWarning("Skipping invalid price on line {LineNumber}: {Value}", i + 1, parts[3].Trim());
                continue;
            }

            if (!int.TryParse(parts[4].Trim(), out var points))
            {
                logger.LogWarning("Skipping invalid points on line {LineNumber}: {Value}", i + 1, parts[4].Trim());
                continue;
            }

            products.Add(new Product
            {
                ProductCode = parts[0].Trim(),
                Name = parts[1].Trim(),
                Category = parts[2].Trim(),
                Price = price,
                PointValue = points,
                CreatedBy = "DataSeeder"
            });
        }

        context.Products.AddRange(products);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} Products", products.Count);
    }

    #endregion

    #region RewardSettings

    private static async Task SeedRewardSettingsAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.RewardSettings.AnyAsync())
        {
            logger.LogInformation("RewardSettings already seeded, skipping");
            return;
        }

        context.RewardSettings.Add(new RewardSettings
        {
            PointsToSarRate = 10m,
            InviterRewardPoints = 100m,
            InviteeRewardPoints = 50m,
            MinimumRedemptionPoints = 1000m,
            CreatedBy = "DataSeeder"
        });

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded default RewardSettings (PointsToSarRate: 10)");
    }

    #endregion

    #region Demo Analytics Data

    private static async Task SeedDemoAnalyticsDataAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        Dictionary<string, ApplicationUser> users,
        ILogger logger)
    {
        if (await context.Wallets.AnyAsync())
        {
            logger.LogInformation("Demo analytics data already seeded, skipping");
            return;
        }

        logger.LogInformation("Seeding demo analytics data...");

        // --- Load lookup data ---
        var riyadhCity = await context.Cities.FirstAsync(c => c.NameEn == "Riyadh");
        var jeddahCity = await context.Cities.FirstAsync(c => c.NameEn == "Jeddah");
        var dammamCity = await context.Cities.FirstAsync(c => c.NameEn == "Dammam");
        var buraydahCity = await context.Cities.FirstAsync(c => c.NameEn == "Buraydah");
        var abhaCity = await context.Cities.FirstAsync(c => c.NameEn == "Abha");
        var tabukCity = await context.Cities.FirstAsync(c => c.NameEn == "Tabuk");

        var erpCodes = await context.ErpCustomers
            .OrderBy(e => e.CustomerCode)
            .Take(10)
            .Select(e => e.CustomerCode)
            .ToListAsync();

        var products = await context.Products
            .OrderByDescending(p => p.PointValue)
            .Take(8)
            .ToListAsync();

        var settings = await context.RewardSettings.FirstAsync();
        var sarRate = settings.PointsToSarRate;

        // Lookup existing users by name
        users.TryGetValue("محمود حجازي", out var smRiyadh1);
        users.TryGetValue("احمد سمير", out var smRiyadh2);
        users.TryGetValue("محمد اياد", out var smJeddah);
        users.TryGetValue("محمد خميس", out var smDammam);
        users.TryGetValue("وليد السكري", out var smQassim);
        users.TryGetValue("محمد خطاب", out var smSouth);
        users.TryGetValue("محمد حسام", out var smTabuk);
        users.TryGetValue("فرحان ممدوح", out var zmRiyadh);
        users.TryGetValue("مدير النظام", out var admin);

        // --- 1. Create demo mobile users ---
        var demoUsers = new (string Name, UserType Type, string Role, string SalesManId, string CityId, string? CustomerCode)[]
        {
            ("بائع الرياض", UserType.Seller, UserRoles.Seller, smRiyadh1!.Id, riyadhCity.Id, erpCodes[0]),
            ("بائع الرياض ٢", UserType.Seller, UserRoles.Seller, smRiyadh2!.Id, riyadhCity.Id, erpCodes[1]),
            ("بائع جدة", UserType.Seller, UserRoles.Seller, smJeddah!.Id, jeddahCity.Id, erpCodes[2]),
            ("بائع الدمام", UserType.Seller, UserRoles.Seller, smDammam!.Id, dammamCity.Id, erpCodes[3]),
            ("بائع بريدة", UserType.Seller, UserRoles.Seller, smQassim!.Id, buraydahCity.Id, erpCodes[4]),
            ("بائع أبها", UserType.Seller, UserRoles.Seller, smSouth!.Id, abhaCity.Id, erpCodes[5]),
            ("فني الرياض", UserType.Technician, UserRoles.Technician, smRiyadh1.Id, riyadhCity.Id, null),
            ("فني الرياض ٢", UserType.Technician, UserRoles.Technician, smRiyadh2.Id, riyadhCity.Id, null),
            ("فني جدة", UserType.Technician, UserRoles.Technician, smJeddah.Id, jeddahCity.Id, null),
            ("فني الدمام", UserType.Technician, UserRoles.Technician, smDammam.Id, dammamCity.Id, null),
            ("فني تبوك", UserType.Technician, UserRoles.Technician, smTabuk!.Id, tabukCity.Id, null),
            ("صاحب محل الرياض", UserType.ShopOwner, UserRoles.ShopOwner, smRiyadh1.Id, riyadhCity.Id, erpCodes[6]),
            ("صاحب محل جدة", UserType.ShopOwner, UserRoles.ShopOwner, smJeddah.Id, jeddahCity.Id, erpCodes[7]),
        };

        var createdUsers = new Dictionary<string, ApplicationUser>();

        foreach (var (name, type, role, smId, cityId, custCode) in demoUsers)
        {
            await CreateUser(userManager, createdUsers, logger, name, type, [role]);

            if (!createdUsers.TryGetValue(name, out var user)) continue;

            // Set AssignedSalesManId and NationalAddress
            user.AssignedSalesManId = smId;
            user.NationalAddress = new NationalAddress
            {
                CityId = cityId,
                Street = "شارع التحلية",
                BuildingNumber = 1234,
                PostalCode = "12345",
                SubNumber = 1000,
                District = "العليا"
            };

            await userManager.UpdateAsync(user);
        }

        // Set invitation link: seller1 invited seller2
        var seller1 = createdUsers["بائع الرياض"];
        var seller2 = createdUsers["بائع الرياض ٢"];
        seller1.InvitationCode = "DEMO1234";
        seller2.InvitedByUserId = seller1.Id;
        await userManager.UpdateAsync(seller1);
        await userManager.UpdateAsync(seller2);

        // Create profiles
        foreach (var (name, _, _, _, _, custCode) in demoUsers)
        {
            if (!createdUsers.TryGetValue(name, out var user)) continue;

            if (user.UserType == UserType.Seller && custCode != null)
                context.SellerProfiles.Add(new SellerProfile { UserId = user.Id, CustomerCode = custCode, CreatedBy = "DataSeeder" });
            else if (user.UserType == UserType.ShopOwner && custCode != null)
                context.ShopOwnerProfiles.Add(new ShopOwnerProfile { UserId = user.Id, CustomerCode = custCode, CreatedBy = "DataSeeder" });
            else if (user.UserType == UserType.Technician)
                context.TechnicianProfiles.Add(new TechnicianProfile { UserId = user.Id, CreatedBy = "DataSeeder" });
        }

        await context.SaveChangesAsync();

        // --- 2. Create ShopData ---
        var shopDataEntries = new (string CustCode, string StoreName, string VAT, string CRN, string ShortAddr, string CityId, string EnteredBy)[]
        {
            (erpCodes[0], "محل الرائد - الرياض", "300000000000003", "1000000001", "SEED0001", riyadhCity.Id, createdUsers["بائع الرياض"].Id),
            (erpCodes[2], "محل الرائد - جدة", "300000000000023", "1000000002", "SEED0002", jeddahCity.Id, createdUsers["بائع جدة"].Id),
            (erpCodes[3], "محل الرائد - الدمام", "300000000000043", "1000000003", "SEED0003", dammamCity.Id, createdUsers["بائع الدمام"].Id),
            (erpCodes[6], "محل صاحب المحل - الرياض", "300000000000063", "1000000004", "SEED0004", riyadhCity.Id, createdUsers["صاحب محل الرياض"].Id),
            (erpCodes[7], "محل صاحب المحل - جدة", "300000000000083", "1000000005", "SEED0005", jeddahCity.Id, createdUsers["صاحب محل جدة"].Id),
        };

        foreach (var (custCode, storeName, vat, crn, shortAddr, cityId, enteredBy) in shopDataEntries)
        {
            context.ShopData.Add(new ShopData
            {
                CustomerCode = custCode,
                StoreName = storeName,
                VAT = vat,
                CRN = crn,
                ShortAddress = shortAddr,
                District = "حي النسيم",
                CityId = cityId,
                Street = "شارع الملك فهد",
                BuildingNumber = 2000,
                PostalCode = "11564",
                SubNumber = 1000,
                ShopImageUrl = "/uploads/demo-shop.png",
                EnteredByUserId = enteredBy,
                CreatedBy = "DataSeeder"
            });
        }

        await context.SaveChangesAsync();

        // --- 3. Create Wallets ---
        var wallets = new Dictionary<string, Wallet>();
        foreach (var (name, user) in createdUsers)
        {
            var wallet = new Wallet { UserId = user.Id, CreatedBy = "DataSeeder" };
            context.Wallets.Add(wallet);
            wallets[name] = wallet;
        }

        await context.SaveChangesAsync();

        // --- 4. Create Barcodes & Scan Records ---
        var sellers = new[] { "بائع الرياض", "بائع الرياض ٢", "بائع جدة", "بائع الدمام", "بائع بريدة", "بائع أبها" };
        var technicians = new[] { "فني الرياض", "فني الرياض ٢", "فني جدة", "فني الدمام", "فني تبوك" };

        var barcodeIndex = 0;
        var allScanRecords = new List<ScanRecord>();
        var baseDate = DateTime.UtcNow.AddDays(-90);

        // Helper to create barcodes with scan records
        void AddBarcode(Product product, BarcodeStatus status, string? sellerName, string? techName, int dayOffset)
        {
            var barcode = new ProductBarcode
            {
                Code = $"SEED{barcodeIndex:D8}",
                ProductId = product.Id,
                Status = status,
                CreatedBy = "DataSeeder"
            };
            context.ProductBarcodes.Add(barcode);
            barcodeIndex++;

            if (sellerName != null && createdUsers.TryGetValue(sellerName, out var sellerUser))
            {
                var pts = product.PointValue / 2.0m;
                var scan = new ScanRecord
                {
                    BarcodeId = barcode.Id,
                    UserId = sellerUser.Id,
                    ScannerRole = ScannerRole.Seller,
                    PointsAwarded = pts,
                    Latitude = 24.7136 + (Random.Shared.NextDouble() - 0.5) * 0.1,
                    Longitude = 46.6753 + (Random.Shared.NextDouble() - 0.5) * 0.1,
                    CreatedBy = "DataSeeder"
                };
                context.ScanRecords.Add(scan);
                allScanRecords.Add(scan);
            }

            if (techName != null && createdUsers.TryGetValue(techName, out var techUser))
            {
                var pts = (decimal)product.PointValue;
                var scan = new ScanRecord
                {
                    BarcodeId = barcode.Id,
                    UserId = techUser.Id,
                    ScannerRole = ScannerRole.Technician,
                    PointsAwarded = pts,
                    Latitude = 24.7136 + (Random.Shared.NextDouble() - 0.5) * 0.1,
                    Longitude = 46.6753 + (Random.Shared.NextDouble() - 0.5) * 0.1,
                    CreatedBy = "DataSeeder"
                };
                context.ScanRecords.Add(scan);
                allScanRecords.Add(scan);
            }
        }

        // 20 Available barcodes (no scans)
        for (var i = 0; i < 20; i++)
            AddBarcode(products[i % products.Count], BarcodeStatus.Available, null, null, 0);

        // 15 Consumed barcodes (seller + technician scan each)
        for (var i = 0; i < 15; i++)
            AddBarcode(products[i % products.Count], BarcodeStatus.Consumed,
                sellers[i % sellers.Length], technicians[i % technicians.Length], i * 5);

        // 10 SellerScanned only
        for (var i = 0; i < 10; i++)
            AddBarcode(products[i % products.Count], BarcodeStatus.SellerScanned,
                sellers[i % sellers.Length], null, i * 7);

        // 5 TechnicianScanned only
        for (var i = 0; i < 5; i++)
            AddBarcode(products[i % products.Count], BarcodeStatus.TechnicianScanned,
                null, technicians[i % technicians.Length], i * 10);

        await context.SaveChangesAsync();

        // --- 5. Create Wallet Transactions ---
        // Earned transactions from scans
        foreach (var scan in allScanRecords)
        {
            var userName = createdUsers.First(kv => kv.Value.Id == scan.UserId).Key;
            if (!wallets.TryGetValue(userName, out var wallet)) continue;

            context.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = wallet.Id,
                Amount = scan.PointsAwarded,
                Type = WalletTransactionType.Earned,
                ReferenceId = scan.Id,
                Description = "نقاط مكتسبة من مسح باركود",
                SarRate = sarRate,
                SarAmount = scan.PointsAwarded / sarRate,
                RemainingAmount = scan.PointsAwarded,
                CreatedBy = "DataSeeder"
            });

            wallet.Balance += scan.PointsAwarded;
            wallet.SarBalance += scan.PointsAwarded / sarRate;
        }

        // Invitation rewards
        var inviterWallet = wallets["بائع الرياض"];
        var inviteeWallet = wallets["بائع الرياض ٢"];

        context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = inviterWallet.Id,
            Amount = settings.InviterRewardPoints,
            Type = WalletTransactionType.InvitationReward,
            Description = "مكافأة دعوة — بائع الرياض ٢",
            SarRate = sarRate,
            SarAmount = settings.InviterRewardPoints / sarRate,
            RemainingAmount = settings.InviterRewardPoints,
            CreatedBy = "DataSeeder"
        });
        inviterWallet.Balance += settings.InviterRewardPoints;
        inviterWallet.SarBalance += settings.InviterRewardPoints / sarRate;

        context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = inviteeWallet.Id,
            Amount = settings.InviteeRewardPoints,
            Type = WalletTransactionType.InvitationReward,
            Description = "مكافأة تسجيل بدعوة",
            SarRate = sarRate,
            SarAmount = settings.InviteeRewardPoints / sarRate,
            RemainingAmount = settings.InviteeRewardPoints,
            CreatedBy = "DataSeeder"
        });
        inviteeWallet.Balance += settings.InviteeRewardPoints;
        inviteeWallet.SarBalance += settings.InviteeRewardPoints / sarRate;

        await context.SaveChangesAsync();

        // --- 6. Redemption Requests ---
        // Find the user with highest balance for a completed redemption (sellers or technicians)
        var redeemableUsers = sellers.Concat(technicians).ToArray();
        var topSeller = wallets.Where(kv => redeemableUsers.Contains(kv.Key))
            .OrderByDescending(kv => kv.Value.Balance)
            .First();

        // Completed bank transfer redemption (1000 pts = 100 SAR)
        RedemptionRequest? completedRedemption = null;
        if (topSeller.Value.Balance >= 1000m)
        {
            completedRedemption = new RedemptionRequest
            {
                UserId = createdUsers[topSeller.Key].Id,
                Method = RedemptionMethod.BankTransfer,
                Status = RedemptionRequestStatus.Completed,
                PointsAmount = 1000m,
                SarRate = sarRate,
                SarAmount = 100m,
                Iban = "SA4420000001234567891234",
                BankName = "الراجحي",
                AccountHolderName = topSeller.Key,
                CreatedBy = "DataSeeder"
            };
            context.RedemptionRequests.Add(completedRedemption);

            // Deduct from wallet
            topSeller.Value.Balance -= 1000m;
            topSeller.Value.SarBalance -= 100m;

            // Redeemed transaction
            context.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = topSeller.Value.Id,
                Amount = -1000m,
                Type = WalletTransactionType.Redeemed,
                Description = "استبدال — تحويل بنكي",
                SarRate = sarRate,
                SarAmount = -100m,
                RemainingAmount = 0,
                CreatedBy = "DataSeeder"
            });

            await context.SaveChangesAsync();

            // Approval chain for completed redemption
            context.RedemptionApprovals.Add(new RedemptionApproval
            {
                RedemptionRequestId = completedRedemption.Id,
                ApproverId = smRiyadh1!.Id,
                Action = ApprovalAction.Approved,
                FromStatus = RedemptionRequestStatus.PendingSalesMan,
                ToStatus = RedemptionRequestStatus.PendingZoneManager,
                CreatedBy = "DataSeeder"
            });
            context.RedemptionApprovals.Add(new RedemptionApproval
            {
                RedemptionRequestId = completedRedemption.Id,
                ApproverId = zmRiyadh!.Id,
                Action = ApprovalAction.Approved,
                FromStatus = RedemptionRequestStatus.PendingZoneManager,
                ToStatus = RedemptionRequestStatus.PendingAdmin,
                CreatedBy = "DataSeeder"
            });
            context.RedemptionApprovals.Add(new RedemptionApproval
            {
                RedemptionRequestId = completedRedemption.Id,
                ApproverId = admin!.Id,
                Action = ApprovalAction.Approved,
                FromStatus = RedemptionRequestStatus.PendingAdmin,
                ToStatus = RedemptionRequestStatus.AdminApproved,
                CreatedBy = "DataSeeder"
            });

            await context.SaveChangesAsync();
        }

        // Pending cash redemption (if another user has enough)
        var pendingSeller = wallets.Where(kv => redeemableUsers.Contains(kv.Key) && kv.Key != topSeller.Key)
            .OrderByDescending(kv => kv.Value.Balance)
            .FirstOrDefault();

        if (pendingSeller.Value != null && pendingSeller.Value.Balance >= 1000m)
        {
            var pendingRedemption = new RedemptionRequest
            {
                UserId = createdUsers[pendingSeller.Key].Id,
                Method = RedemptionMethod.Cash,
                Status = RedemptionRequestStatus.PendingZoneManager,
                PointsAmount = 1000m,
                SarRate = sarRate,
                SarAmount = 100m,
                CreatedBy = "DataSeeder"
            };
            context.RedemptionRequests.Add(pendingRedemption);

            // Hold points
            pendingSeller.Value.HeldBalance += 1000m;
            pendingSeller.Value.HeldSarBalance += 100m;

            await context.SaveChangesAsync();

            // SalesMan approval only
            context.RedemptionApprovals.Add(new RedemptionApproval
            {
                RedemptionRequestId = pendingRedemption.Id,
                ApproverId = smJeddah!.Id,
                Action = ApprovalAction.Approved,
                FromStatus = RedemptionRequestStatus.PendingSalesMan,
                ToStatus = RedemptionRequestStatus.PendingZoneManager,
                CreatedBy = "DataSeeder"
            });

            await context.SaveChangesAsync();
        }

        // --- 7. Notifications ---
        var notifIndex = 0;
        void AddNotif(string userId, NotificationType type, string title, string body, string? refId = null, bool isRead = false)
        {
            context.Notifications.Add(new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Body = body,
                ReferenceId = refId,
                IsRead = isRead,
                ReadAt = isRead ? DateTime.UtcNow : null,
                CreatedBy = "DataSeeder"
            });
            notifIndex++;
        }

        // Registration approved for all demo users
        foreach (var (name, user) in createdUsers)
            AddNotif(user.Id, NotificationType.RegistrationApproved,
                "تمت الموافقة على تسجيلك", "مرحبًا بك في برنامج المكافآت", isRead: true);

        // Points earned for top 4 scanners
        foreach (var scan in allScanRecords.Take(4))
            AddNotif(scan.UserId, NotificationType.PointsEarned,
                "نقاط مكتسبة", $"حصلت على {scan.PointsAwarded} نقطة", scan.Id);

        // Invitation reward
        AddNotif(seller1.Id, NotificationType.InvitationReward,
            "مكافأة دعوة", "حصلت على 100 نقطة مكافأة دعوة");
        AddNotif(seller2.Id, NotificationType.InvitationReward,
            "مكافأة تسجيل", "حصلت على 50 نقطة مكافأة تسجيل بدعوة");

        // Redemption notifications
        if (completedRedemption != null)
        {
            AddNotif(createdUsers[topSeller.Key].Id, NotificationType.RedemptionCreated,
                "طلب استبدال جديد", "تم تقديم طلب استبدال 1000 نقطة", completedRedemption.Id, true);
            AddNotif(createdUsers[topSeller.Key].Id, NotificationType.RedemptionCompleted,
                "تم الاستبدال", "تم تحويل 100 ريال إلى حسابك البنكي", completedRedemption.Id, true);
        }

        // Admin broadcast
        foreach (var (name, user) in createdUsers.Take(5))
            AddNotif(user.Id, NotificationType.AdminMessage,
                "رسالة من الإدارة", "شكرًا لمشاركتك في برنامج المكافآت");

        await context.SaveChangesAsync();

        logger.LogInformation(
            "Seeded demo analytics: {Users} users, {Barcodes} barcodes, {Scans} scans, {Txns} wallet transactions, {Notifications} notifications",
            createdUsers.Count, barcodeIndex, allScanRecords.Count,
            await context.WalletTransactions.CountAsync(),
            notifIndex);
    }

    #endregion
}
