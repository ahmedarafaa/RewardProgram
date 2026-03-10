# RewardProgram Codebase — Audit & Context

## Architecture Overview
- **4-layer Clean Architecture**: API (RewardProgram) → Application → Domain, Infrastructure → Application
- **.NET 10.0**, EF Core 10.0.2, ASP.NET Identity, FluentValidation, Serilog
- **Result<T>** pattern for error handling → ProblemDetails responses
- **Soft-delete** via `TrackableEntity` with global query filters
- **OTP-based auth**: Registration + Login via WhatsApp (Twilio Verify)
- **QuestPDF + ZXing.Net** for barcode PDF generation

## Project Structure
```
RewardProgram/                    # API layer — controllers, DI, Program.cs
  Controllers/
    Admin/                        # Admin endpoints (namespace-based Swagger separation)
    AuthController.cs
    ScanController.cs
    WalletController.cs
    ...
  DependencyInjection.cs          # Service registration
  Program.cs                      # Startup, middleware, auto-migration
  GlobalExceptionHandler.cs       # Centralized exception → ProblemDetails
  ResultExtension.cs              # Result → IActionResult conversion

RewardProgram.Application/        # Business logic layer
  Abstractions/                   # Result.cs, Error.cs, ITransaction.cs
  Contracts/                      # DTOs organized by feature
    Admin/{Feature}/              # Admin-specific DTOs + Validators
    Auth/                         # Auth DTOs (register, login, OTP)
    Scan/                         # Scan DTOs
    Wallet/                       # Wallet DTOs
  Errors/                         # Domain error definitions
  Helpers/                        # Shared utilities
    MobileNumberHelper.cs         # Normalize + Mask Saudi mobiles
    PaginationHelper.cs           # Page/PageSize normalization
    FileUploadHelper.cs           # Image upload with type/size validation
    ShopDataValidationHelper.cs   # VAT/CRN/ShortAddress uniqueness + ApplyPartialUpdate
    UserCreationHelper.cs         # CreateAsync + AddToRoleAsync consolidation
  Interfaces/                     # Service contracts
    Admin/                        # Admin service interfaces
  Services/                       # Service implementations
    Admin/                        # AdminUserService, AdminBarcodeService, etc.
    Auth/                         # AuthService, OtpService
    ScanService.cs
    WalletService.cs

RewardProgram.Domain/             # Entities, enums, constants
  Entities/Users/                 # ApplicationUser, profiles, NationalAddress
  Entities/                       # Product, ProductBarcode, Wallet, etc.
  Enums/                          # BarcodeStatus, ScannerRole, UserType, etc.
  Constants/                      # UserRoles

RewardProgram.Infrastructure/     # EF, Identity, external services
  Data/ApplicationDbContext.cs    # DbContext with soft-delete filters
  Repositories/UserRepository.cs  # UserManager wrapper
  Services/                       # TokenService, TwilioService, FileStorageService, BarcodePdfGenerator
```

## Key Patterns & Conventions

### Error Handling
- All service methods return `Result<T>` or `Result` (never throw for business logic)
- Error definitions in `Application/Errors/` with plain int status codes
- `ResultExtension.ToProblem()` maps to ASP.NET ProblemDetails in API layer

### User Creation
- `UserCreationHelper.CreateWithRoleAsync()` — consolidates Identity CreateAsync + AddToRoleAsync + error handling (used by all 8 registration/admin-add methods)
- Transaction wraps the full create flow; caller handles rollback on failure

### ShopData Lifecycle
- Created at user registration time (not at approval)
- ShopOwner always provides shop data (overwrites Seller's data if exists)
- Seller: first-come-first-served — first Seller for a CustomerCode provides data
- `ShopDataValidationHelper.ValidateUniqueFieldsAsync` — single-query VAT/CRN/ShortAddress uniqueness
- `ShopDataValidationHelper.ApplyPartialUpdate` — consolidates edit field updates

### Scanning & Wallet
- **ProductBarcode**: NanoID 12-char codes, state machine (Available → SellerScanned/TechnicianScanned → Consumed)
- **ScanRecord**: (BarcodeId, ScannerRole) composite unique — one scan per role per barcode
- Points logic: Seller first → 50%, Technician first → 100%, second scan completes + awards deferred points
- **Wallet**: per-user Balance + SarBalance, lazy-created on first scan
- SAR conversion immutable — rate stored per WalletTransaction

### Mobile Number Normalization
- `MobileNumberHelper.Normalize()` canonicalizes Saudi formats (05XX, 966XX, +966XX → +966XXXXXXXXX)
- Applied at entry points: all auth methods + OTP methods

### Barcode PDF Generation
- `BarcodePdfGenerator` uses QuestPDF + ZXing.Net BarcodeWriterPixelData
- `[ThreadStatic]` lazy initialization for thread safety (singleton service)
- Manual BMP encoder for RGBA → BMP conversion (dependency-free)
- Batches >500 return JSON instead of PDF (OOM/timeout protection)

### Pagination
- `PaginationHelper.Normalize(page, pageSize)` — clamps page ≥ 1, pageSize 1–100
- `PaginatedResult<T>` record for all list endpoints

### Admin Feature Organization
- Feature-based folder structure under `Admin/`
- DTOs: `Contracts/Admin/{Feature}/` + `Contracts/Admin/{Feature}/Validators/`
- Errors: `Errors/Admin{Feature}Errors.cs`
- Service: `Interfaces/Admin/IAdmin{Feature}Service.cs` + `Services/Admin/Admin{Feature}Service.cs`
- Controller: `Controllers/Admin/Admin{Feature}Controller.cs`

## API Endpoints (38 total)

### Auth (8 endpoints)
- `POST /api/auth/send-otp` — registration OTP
- `POST /api/auth/register/shop-owner` — [FromForm]
- `POST /api/auth/register/seller` — [FromForm]
- `POST /api/auth/register/technician` — [FromBody]
- `POST /api/auth/resend-otp`
- `POST /api/auth/login`
- `POST /api/auth/login/verify`
- `POST /api/auth/refresh-token`

### Public (4 endpoints)
- `POST /api/scan` — scan barcode [Seller,Technician]
- `GET /api/scan/history` — scan history [Seller,Technician]
- `GET /api/wallet/balance` — point + SAR balance [Seller,Technician]
- `GET /api/wallet/transactions` — transaction history [Seller,Technician]

### Lookups (4 endpoints)
- `GET /api/lookup/regions`
- `GET /api/lookup/regions/{regionId}/cities`
- `GET /api/lookup/cities`
- `GET /api/lookup/customer/{customerCode}/shop-data-status`

### Admin Users (12 endpoints)
- `POST /api/admin/users/{salesman|zone-manager|shop-owner|seller|technician}` — create
- `PUT /api/admin/users/{salesman|zone-manager|shop-owner|seller|technician}/{id}` — edit
- `GET /api/admin/users` — list/search/filter
- `PATCH /api/admin/users/{id}/toggle-status`

### Admin Products (4 endpoints)
- CRUD: `POST`, `GET` (list), `GET /{id}`, `PUT /{id}`, `DELETE /{id}`

### Admin Barcodes & Scans (3 endpoints)
- `POST /api/admin/barcodes/generate` — generate N barcodes
- `GET /api/admin/barcodes` — list/filter
- `GET /api/admin/scans` — list/filter scan records

### Admin Settings (2 endpoints)
- `GET /api/admin/reward-settings`
- `PUT /api/admin/reward-settings`

## Database Entities

### Core
- **ApplicationUser** (Identity) — Name, MobileNumber, UserType, RegistrationStatus, IsDisabled, NationalAddress (owned), AssignedSalesManId
- **Region** — NameAr, NameEn, ZoneManagerId, IsActive
- **City** — NameAr, NameEn, RegionId, ApprovalSalesManId, IsActive
- **ErpCustomer** — CustomerCode (unique), CustomerName
- **ShopData** — CustomerCode (FK), StoreName, VAT, CRN, ShortAddress, ShopImageUrl, address fields

### Profiles
- **ShopOwnerProfile** — UserId, CustomerCode
- **SellerProfile** — UserId, CustomerCode
- **TechnicianProfile** — UserId

### Products & Scanning
- **Product** — Name, ProductCode, PointValue, Price, Category
- **ProductBarcode** — Code (unique, NanoID 12-char), ProductId, Status, RowVersion
- **ScanRecord** — BarcodeId, UserId, ScannerRole, PointsAwarded (composite unique: BarcodeId+ScannerRole)
- **Wallet** — UserId (unique), Balance, SarBalance
- **WalletTransaction** — WalletId, Amount, Type, ReferenceId, SarRate, SarAmount
- **RewardSettings** — PointsToSarRate (singleton, lazy-created)

## Migrations (in order)
1. Initial (Identity + core entities)
2. ShopDataErpCustomerSeparation
3. AddShortAddressAndDistrict
4. AddProductBarcodeWalletScan
5. AddSarConversionAndRewardSettings

## Data Seeder
- 6 roles, 31 users, 8 regions, 140 cities, 3235 ErpCustomers, 1133 Products, 1 RewardSettings

## Audit Fixes Applied (2026-03-10)

| # | Issue | Priority | Fix |
|---|-------|----------|-----|
| 4 | Dead error duplicates in AuthErrors/AdminUserErrors | P2 | Removed 10 unused error constants |
| 6 | Thread-unsafe static BarcodeWriter in singleton service | P1 | `[ThreadStatic]` lazy initialization |
| 7 | No batch size limit on barcode PDF generation | P1 | MaxPdfBatchSize=500, returns JSON above |
| 10 | 3x DB queries for ShopData uniqueness validation | P2 | Consolidated to single OR query |
| 11 | Mobile number format inconsistency | P2 | `MobileNumberHelper.Normalize()` at all entry points |
| 12 | Redundant wallet lookup in WalletService | P2 | Query transactions directly via navigation |
| 13 | Wrong error code for NanoID collision exhaustion | P2 | Added `BarcodeErrors.CollisionRetryExhausted` |
| 14 | Missing AsNoTracking on read-only product list query | P3 | Added `.AsNoTracking()` |
| 15 | Admin scan endpoint in public IScanService | P2 | Moved to IAdminBarcodeService/AdminBarcodeService |
| 1+3 | 8x user creation duplication + 2x ShopData update duplication | P2 | `UserCreationHelper.CreateWithRoleAsync` + `ShopDataValidationHelper.ApplyPartialUpdate` |

## Known Remaining Items
- **C6**: Secrets as placeholders in appsettings.json
- **P3**: OTP table unbounded growth (no cleanup job)
- **M6**: No re-registration path for rejected users
- Manual BMP encoder is functional but produces larger files than PNG (acceptable for barcode-sized images)

## Feature Roadmap (remaining)
1. Gifts & Redeeming — gift catalog, redeem points
2. Invitations — invite rewards
3. Notifications — push/WhatsApp for scans, points, approvals
4. Trader Map — Google Maps integration using ShortAddress
5. Admin features for each above
