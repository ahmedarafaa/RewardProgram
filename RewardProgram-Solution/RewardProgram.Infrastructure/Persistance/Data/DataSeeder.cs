using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RewardProgram.Domain.Constants;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums.UserEnums;

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
            roles: [UserRoles.SystemAdmin]);

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
        string[] roles)
    {
        var trimmedName = name.Trim();

        // Check if already exists by name
        var existing = await userManager.Users.FirstOrDefaultAsync(u => u.Name == trimmedName);
        if (existing != null)
        {
            users[trimmedName] = existing;
            return;
        }

        var mobile = $"05{_mobileCounter:D8}";
        _mobileCounter++;

        var user = new ApplicationUser
        {
            Name = trimmedName,
            UserName = mobile,
            MobileNumber = mobile,
            PhoneNumber = mobile,
            PhoneNumberConfirmed = true,
            UserType = userType,
            RegistrationStatus = RegistrationStatus.Approved,
            IsDisabled = false
        };

        var result = await userManager.CreateAsync(user);
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
        logger.LogInformation("User '{Name}' created ({UserType}, Mobile: {Mobile}, Roles: {Roles})",
            trimmedName, userType, mobile, string.Join("+", roles));
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
}
