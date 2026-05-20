# Pre-Delivery Review — RewardProgram

Comprehensive review of the system before client delivery, synthesised from a 6-agent parallel deep-dive across the Admin Dashboard, the Mobile App API, and platform/cross-cutting layers.

Date: 2026-05-08
Branch: `dev`
Scope: every customer-facing feature plus shared infrastructure.

---

## Summary

- **Total distinct findings**: ~170 (after dedupe across agents)
- By severity: ~12 Critical, ~52 High, ~70 Medium, ~36 Low/Info
- Three review tables follow:
  1. **Admin Dashboard** — features admin staff use
  2. **Mobile App** — features end users use (Seller / Technician / ShopOwner)
  3. **Platform / Cross-cutting** — affects both surfaces (security, infra, ops)

Severity legend: **C**=Critical (blocks delivery) · **H**=High (must-fix-soon) · **M**=Medium · **L**=Low · **I**=Info.

---

## Top 12 Delivery Blockers (do these first)

| # | Sev | Surface | Issue | Location |
|---|-----|---------|-------|----------|
| 1 | C | Cross-cutting | File upload accepts any extension, no MIME / magic-byte sniffing, no `nosniff` header — stored XSS via SVG/HTML uploaded into `wwwroot/uploads/` and served by IIS | `FileStorageService.cs:24-51`; profile/shop-image/admin paths |
| 2 | C | Cross-cutting | No security headers anywhere (HSTS, CSP, X-Content-Type-Options, X-Frame-Options) | `Program.cs:50-67` |
| 3 | C | Cross-cutting | `GlobalExceptionHandler` writes raw `ProblemDetails` directly, bypassing `IProblemDetailsService` — drops `traceId`/`instance`, breaks log correlation | `GlobalExceptionHandler.cs:14-25` |
| 4 | C | Admin redemption | Concurrent SM/ZM approvals on same request create duplicate state transitions — `RedemptionRequest.RowVersion` defined but never read | `RedemptionApprovalService.cs:259-311, 348-378` |
| 5 | C | Admin redemption | `Wallet.RowVersion` exists but never enforced on completion — concurrent earn/scan during completion silently overwrites held balance | `RedemptionApprovalService.cs:584-587` |
| 6 | C | Admin redemption | Cash OTP attempt counter increments via `SaveChangesAsync` *outside* the wrapping transaction — brute-force lockout bypassable | `RedemptionApprovalService.cs:441-447, 660-664` |
| 7 | C | Admin notifications | `BroadcastAsync` loads all user IDs, inserts one Notification per user in a single transaction — OOM/timeout at scale, blocks DB writes | `NotificationService.cs:285-296, 250-261` |
| 8 | C | App scan | `EnsureWalletAsync` runs *before* the scan transaction; rollback leaves orphan wallet — wallet integrity diverges | `ScanService.cs:63, 104` |
| 9 | C | App scan | Concurrent scans on same barcode (Available→SellerScanned race) cause loser to incorrectly receive `BarcodeAlreadyScanned` instead of awarding 50% — `default:` arm hides actual state corruption | `ScanService.cs:67-77, 117-149` |
| 10 | C | App profile | Photo upload validates only extension (`.jpg`); no MIME / sniff. Combined with `wwwroot/uploads/profiles/*` static serving = stored XSS | `ProfileService.cs:78-86`; `FileStorageService.cs:35-43` |
| 11 | C | App shops/map | `/api/shops/map` returns `EnteredByUser.MobileNumber` (staff member's mobile) to all authenticated users — direct PII leak | `ShopService.cs:36`; `ShopMapItemResponse.cs:11` |
| 12 | C | Admin product | Hard-deletes Product despite `TrackableEntity` (audit gap) AND TOCTOU race with concurrent barcode generation creates orphan barcodes | `AdminProductService.cs:82-102` |

---

## Table 1 — Admin Dashboard

Grouped by feature. Each row: `# | Sev | Issue | Location | Fix`.

### A. Admin Authentication & Refresh

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| A1 | H | `AdminAuthController.Login` calls `AccessFailedAsync(user)` even when password is correct but user isn't SystemAdmin → any caller can lock out a real Seller/Technician's account by hitting admin login with their real password | `AdminAuthController.cs:52-72` | Don't increment failure count on the role-check branch |
| A2 | H | Username enumeration / timing oracle: distinct messages and response times for unknown user vs locked vs wrong password | `AdminAuthController.cs:36-50` | Always run `CheckPasswordAsync` (or dummy hash) before returning; collapse to one generic 401 message |
| A3 | H | No rate limiting on `/api/admin/auth/login`. Account lockout protects one account but does not throttle credential stuffing across many usernames | `AdminAuthController.cs:31`; `DependencyInjection.cs` (no `AddRateLimiter`) | Add ASP.NET rate limiter (FixedWindow keyed by IP) on admin login + refresh + public OTP endpoints |
| A4 | M | `AdminLoginRequest` has no FluentValidator — empty/whitespace username reaches `FindByNameAsync`; no max-length guard | `AdminAuthController.cs:104` | Add validator with `NotEmpty + MaxLength` |
| A5 | M | Every admin controller action uses `User.FindFirstValue(ClaimTypes.NameIdentifier)!` — null-forgiving NRE on missing claim returns 500 instead of 401 | `AdminUserController.cs:32+` (~17 places); `AdminAuthController.cs:96` | `User.GetRequiredUserId()` extension throwing typed `UnauthorizedAccessException` |

### B. Admin User Management (CRUD, list, toggle, edit, delete, reassign)

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| A6 | H | Add SalesMan/ZM/ShopOwner/Seller/Technician: orphan ApplicationUser persists if subsequent transaction fails because `UserManager.CreateAsync` does its own `SaveChanges` *outside* the `BeginTransactionAsync` scope | `AdminUserService.cs:63-130` (and 4 sibling Add methods) | Share connection/transaction with UserManager via `Database.UseTransactionAsync`, OR delete the user explicitly on conflict |
| A7 | H | Shop image uploaded *before* the DB transaction → orphan file on rollback. Several stranded files already visible in `wwwroot/uploads/shops/` per `git status` | `AdminUserService.cs:254-258, 382-386`; same pattern in `AuthService.cs:257-261, 422-427` | Upload after commit, or compensating delete in catch |
| A8 | H | Add ShopOwner: when ShopData already exists (Seller created it earlier), new ShopOwner gets `NationalAddress` with empty Street/Building/Postal/District — only CityId is filled | `AdminUserService.cs:274-285` | Copy existing shop address into user's NationalAddress, or read shop address everywhere |
| A9 | H | Add ShopOwner does not verify chosen city's region matches existing ShopData's region — admin can route SalesMan assignment to the wrong region | `AdminUserService.cs:229-285` | When `shopDataExists`, require `request.CityId == existingShopData.CityId` |
| A10 | H | List Users: `Name`/`MobileNumber` searched with `LIKE '%x%'` → full-table scan; no min-search-length | `AdminUserService.cs:561-566` | Switch mobile to `StartsWith`, require ≥3 chars |
| A11 | H | List Users: `OrderByDescending(u => u.CreatedAt)` with NO index on `ApplicationUser.CreatedAt` — list slows linearly with users | `AdminUserService.cs:605` | Add index `(CreatedAt DESC)` on ApplicationUser |
| A12 | H | Toggle Status does NOT prevent disabling the only ZM of a region or only SM of a city — `Region.ZoneManagerId` still points to disabled user; S19 invariants broken for toggle (enforced for delete only) | `AdminUserService.cs:716-753` | Block toggle for SM/ZM, route admin to reassign endpoints |
| A13 | H | Toggle Status / Delete: `user.RefreshTokens.Where(...)` enumerates a navigation collection that was never `Include`d → revocation loop is a no-op. Disabled user keeps refresh-token sessions for 365 days | `AdminUserService.cs:734-737, 1093-1094, 1180-1181`; `IUserRepository.FindByIdAsync` | Bulk-update `_context.Set<RefreshToken>().Where(...).ExecuteUpdateAsync(...)`. Or load with `.Include(u => u.RefreshTokens)` |
| A14 | H | Edit endpoints are strict `UserType ==` — dual-role SM+ZM users can be hidden behind `UserTypeMismatch` errors | `AdminUserService.cs:766, 793, 820, 847, 874, 1029, 1135` | Switch to role-based check (matches the list-fix already shipped for surfacing) |
| A15 | H | EditShopOwner / EditSeller only update `Name` — admins cannot fix StoreName, VAT, CRN, address, image, even though the helpers `ShopDataValidationHelper.ApplyPartialUpdate` already exist | `AdminUserService.cs:813-865` | Add `PUT /shop-owner/{id}/shop-data` endpoints |
| A16 | H | Reassign Cities updates `AssignedSalesManId` for ALL users in those cities **regardless of RegistrationStatus** — overwrites Approved users' historical SM (S19 spec said pending only) | `AdminUserService.cs:925-937`; same in `Delete` line 1071-1078 + `Add` line 95-101 | Add `&& u.RegistrationStatus == PendingSalesman` to the where clause |
| A17 | H | Delete SalesMan: dual-role SM+ZM deletion as SM does NOT reassign their region — `Region.ZoneManagerId` left pointing to deleted user | `AdminUserService.cs:1029-1126`; symmetric for ZM at 1128-1213 | Refuse delete-as-SM if user owns regions; require ZM-deletion path or combined endpoint |
| A18 | H | Delete SalesMan: `request.CityReassignments.ToDictionary(r => r.CityId, ...)` throws `ArgumentException` on duplicate CityId → 500 instead of 400 | `AdminUserService.cs:1038-1039` | Validate uniqueness in validator |
| A19 | M | Reassignment doesn't check `toSalesMan.IsDisabled` / `IsAccountDeleted` — cities can be transferred to a deleted SM | `AdminUserService.cs:901-906` and similar | Reject disabled/deleted target |
| A20 | M | Reassign Cities materializes all users in affected cities just to set one column — should be a bulk SQL `ExecuteUpdateAsync` | `AdminUserService.cs:928-937` | Use `ExecuteUpdateAsync(s => s.SetProperty(u => u.AssignedSalesManId, request.ToSalesManId))` |
| A21 | M | List Users: `GetUsersInRoleAsync` for SM and ZM hits DB on EVERY list request | `AdminUserService.cs:555-558` | Cache role-id sets in `IMemoryCache` (5-min, invalidate on toggle/delete) |
| A22 | M | List Users: missing `AsNoTracking()` — every page enters change tracker | `AdminUserService.cs:550, 604` | Add `.AsNoTracking()` |
| A23 | M | Toggle Status flips on/off — POST twice quickly = silent no-op. Not idempotent | `AdminUserService.cs:716-753` | Replace with `PUT /status { isDisabled: bool }` |
| A24 | M | Delete: mobile remains in unique index — re-hired employee blocked forever ("MobileAlreadyExists") | `ApplicationUserConfiguration.cs:93`; `AdminUserService.cs:1086-1095` | Filtered unique index `WHERE IsAccountDeleted = 0`, OR null mobile on delete |
| A25 | M | Self-edit / self-delete not blocked — sole SystemAdmin can brick the system | `AdminUserService.cs` (no self-targeting check) | Reject `userId == adminUserId` on edit/delete/toggle/reassign |
| A26 | M | Validators: Add ShopOwner/Seller image checks `BeValidImageType` by **extension only** | `AdminAddShopOwnerRequestValidator.cs:66-71`, sibling | Validate magic bytes (centralised — see X9) |
| A27 | M | Mobile regex `^(05\d{8}|\+\d{10,15})$` accepts `+9999999999` — orphan user created before Twilio rejects | All Add validators | Tighten to `^\+966\d{9}$` (or country allow-list) |
| A28 | M | EditTechnician only edits `Name` — no path to fix typos in District/Postal/CityId | `AdminEditTechnicianRequest.cs` | Extend or add address-edit endpoint |
| A29 | M | No admin audit table — only `ILogger.Information`. Cash redemption system needs an audit trail | `AdminUserService.cs:110-111, 749-750, 1109-1110, 1196-1197` | Add `AdminAudit (ts, adminId, action, targetId, payloadHash)` populated by service decorator or `SaveChangesAsync` interceptor |
| A30 | L | DTO inconsistency: `AdminAddShopOwnerRequest.OwnerName` vs `AdminAddSellerRequest.Name` for the same field | `AdminAddShopOwnerRequest.cs:7`, `AdminAddSellerRequest.cs:7` | Standardise on `Name` |
| A31 | L | `AdminAddUserResponse` reused for Edit responses — confusing name | `AdminAddUserResponse.cs` | Rename or add separate DTO |
| A32 | L | `AdminUserErrors.MobileAlreadyInUse` and `CityNotOwnedBySalesMan` defined but never referenced | `AdminUserErrors.cs:43, 67` | Delete or wire up |
| A33 | L | `AdminUserListItemResponse.Roles` mixes `UserRoles.*` strings and raw `UserType.ToString()` for non-SM/ZM users | `AdminUserService.cs:698-701` | Pick one namespace consistently |

### C. Admin Products / Barcodes / Scans / Reward Settings

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| A34 | C | Product **hard-deleted** despite `TrackableEntity`; TOCTOU race with concurrent `GenerateBarcodes` leaves orphan barcodes (FK Restrict → 500) | `AdminProductService.cs:82-102` | Soft-delete + serializable transaction; surface FK violation as `ProductHasBarcodes` |
| A35 | H | `GenerateBarcodes`: between collision-precheck and SaveChanges, no transaction or retry-on-DbUpdateException → entire batch (up to 1000) lost on race, caller sees 500 | `AdminBarcodeService.cs:50-81` | Skip precheck; rely on unique index + retry; serialize via SemaphoreSlim |
| A36 | H | Barcode PDF: 1000 pages × per-page bitmap (37 KB BMP each) + QuestPDF overhead → 50–100 MB per request held in memory; two concurrent requests can OOM SmarterASP | `BarcodePdfGenerator.cs:34-69`; `AdminBarcodeController.cs:39-42` | Stream to `FileStreamResult`; gate concurrent generations with semaphore; consider lower per-request cap |
| A37 | H | Admin scan list: unused `.Include(s => s.Barcode).ThenInclude(b => b.Product).Include(s => s.User)` followed by projection; no index on `ScanRecord.CreatedAt` | `AdminBarcodeService.cs:142-188` | Drop unused Includes, add `HasIndex(x => x.CreatedAt)` |
| A38 | H | **Cancel scan does not refuse cancellation when the earned points have already been redeemed FIFO** — silently leaves redemption intact, gives user free SAR | `AdminBarcodeService.cs:243-254` | Refuse if `wt.RemainingAmount < wt.Amount` |
| A39 | H | Cancel scan never catches `DbUpdateConcurrencyException` from Wallet RowVersion — concurrent contention surfaces as 500 | `AdminBarcodeService.cs:230-312` | Catch and return `BarcodeErrors.ConcurrencyConflict` |
| A40 | M | Cancel scan reversal sets default `RemainingAmount = 0` — latent if FIFO query ever broadens its type filter | `AdminBarcodeService.cs:257-266` | Set explicitly with comment |
| A41 | M | Cancel scan: `walletsById[wt.WalletId]` throws KeyNotFound if a wallet was hard-deleted between dictionary build and loop | `AdminBarcodeService.cs:240` | Use `TryGetValue` |
| A42 | M | RewardSettings has no DB-level singleton constraint; concurrent inserts can yield 2 rows; `FirstOrDefaultAsync` returns non-deterministic | `AdminRewardSettingsService.cs:50-75`; `RewardSettingsConfiguration.cs` | Fixed sentinel `Id = "singleton"` PK |
| A43 | M | UpdateRewardSettings only logs `PointsToSarRate`; other 3 fields silently changed; no historical audit | `AdminRewardSettingsService.cs:30-48` | Log all four old/new + persist `RewardSettingsHistory` |
| A44 | M | `PointsToSarRate` validator: `> 0` only, no min/max precision check; admin can input `0.001` → silent truncation → divide by zero in scan flow | `RewardSettingsConfiguration.cs:15-17`; validator | Add `>= 0.01m`, `<= 1000m`, configure `PrecisionScale(10,2)` |
| A45 | M | Code 128 alphabet mixes case (`ABC…abc…`) → human re-entry ambiguity (O/o, I/l, l/1) | `AdminBarcodeService.cs:20` | Restrict to upper-case unambiguous: `23456789ABCDEFGHJKLMNPQRSTUVWXYZ` |
| A46 | L | `[ThreadStatic] BarcodeWriterPixelData` mutable singleton — fragile across thread-pool reuse if ZXing writer keeps state | `BarcodePdfGenerator.cs:19-32` | Construct per call or use `AsyncLocal` |
| A47 | L | `ConvertToBmp` allocates fresh `MemoryStream` per barcode; high GC pressure on 1000-batch | `BarcodePdfGenerator.cs:78-124` | `ArrayPool<byte>` / `RecyclableMemoryStream` |
| A48 | L | List Categories has no covering index on `Product.Category` — fine today, watch as products grow | `AdminProductService.cs:163-193` | Add `HasIndex(x => x.Category)` |

### D. Approval Lists (registration approval queue)

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| A49 | H | `GetListAsync` materializes ALL pending + reviewed rows for the approver before slicing in memory — pagination is broken-by-design | `ApprovalService.cs:204-217, 237-250, 253-260` | Push the union + ordering + Skip/Take into SQL via `Concat` server-side |
| A50 | H | `pendingCount + reviewedCount = totalCount` double-counts a user who appears in both queues; page can return duplicate rows | `ApprovalService.cs:253` | Dedupe by UserId/latest activity |
| A51 | M | Search filters by `Name` only — phone/CustomerCode/StoreName visible in row but not searchable | `ApprovalService.cs:62, 201, 234` | Extend Where clause to include MobileNumber + CustomerCode subquery |
| A52 | M | Sort key `ActivityAt` mixes registration time (pending) and decision time (reviewed) — chronology misleading on combined tabs | `ApprovalService.cs:215, 248, 257` | Pick uniform timestamp; document it |
| A53 | M | Disabled SM/ZM with valid JWT can still read the queue (no `IsDisabled` check at service level) — up to 365-day refresh window | `ApprovalController.cs:13`; `ApprovalService.cs:45-55` | Check `approver.IsDisabled` after `FindByIdAsync` |
| A54 | L | `OrderBy(u => u.CreatedAt)` on pending — no index on `(RegistrationStatus, CreatedAt)` | `ApprovalService.cs:68`; `ApplicationUserConfiguration.cs:93-103` | Add composite index |

### E. Admin Analytics (11 endpoints)

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| A55 | H | Notifications/Analytics use `DateTime.UtcNow` for "today/this month" — KSA users get empty buckets until 03:00 KSA | `AdminDashboardService.cs:718-720` (and 12-month buckets) | Convert via `TimeZoneInfo "Arab Standard Time"` |
| A56 | H | `GetDashboardAsync`: 12+ sequential async DB roundtrips before computation — slow admin home, scales worse | `AdminDashboardService.cs:24-96` | Run independent counts in parallel (separate scoped DbContexts), or single SQL |
| A57 | H | `GetRegionAnalyticsAsync` Cartesian-prone multi-Include of regions × cities × SMs/ZMs | `AdminDashboardService.cs:143-150` | `AsSplitQuery()` or flat projection |
| A58 | H | `GetPointsAnalyticsAsync`: every wallet-transaction joined to user joined to SM loaded into memory → in-mem GroupBy. Unbounded as transactions grow | `AdminDashboardService.cs:227-244` | Push GroupBy + Sum to DB |
| A59 | H | `GetInactiveUsersAsync`: full scan of scans by user, then in-memory pagination on the materialized list | `AdminDashboardService.cs:385-415` | Push pagination into DB via LEFT JOIN to `(SELECT MAX(CreatedAt) ...)` subquery |
| A60 | H | `GetRedemptionAnalyticsAsync` loads ALL completed requests to compute `avgDays` in memory | `AdminDashboardService.cs:495-502` | Use `EF.Functions.DateDiffDay(...)` + `AverageAsync` server-side |
| A61 | H | `GetInvitationAnalyticsAsync` loads all invitedUsers in memory then groups | `AdminDashboardService.cs:633-657` | Push GroupBy to DB |
| A62 | H | `GetRevenueAnalyticsAsync`: sum-by-type GroupBy with NO date filter — full WalletTransactions scan forever | `AdminDashboardService.cs:586-605` | Add date-range filter; parallelise sums |
| A63 | H | `GetPointsDetailsAsync`: `dateTo` not normalized to end-of-day — events on `dateTo` excluded | `AdminDashboardService.cs:285-292` | `dateTo = dateTo.Value.Date.AddDays(1)` and use `<` |
| A64 | M | `GetTopPerformersAsync` GroupBy projection may hit EF Core 10 record-projection bug (per S10) — verify with SQL log | `AdminDashboardService.cs:319-341` | Test `ToQueryString()`; project to anonymous and map after |
| A65 | M | `GetSalesManPerformanceAsync` loads full ApplicationUser entities (wide row + tracking) instead of slim DTOs | `AdminDashboardService.cs:527-529` (& whole file) | `.AsNoTracking()` + projections |
| A66 | M | `GetBarcodeAnalyticsAsync` "top 20" ordered by total generated, not by activity (consumed/scanned) | `AdminDashboardService.cs:452` | Order by `Consumed` or expose ordering as query param |
| A67 | L | `WalletTransactions.Where(...).GroupBy(t => 1)` hack for single-row aggregate | `AdminDashboardService.cs:692-696` | Use plain `SumAsync` |

### F. Admin Notifications (broadcast / send / history)

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| A68 | C | `BroadcastAsync` / `SendToRoleAsync`: load all user IDs into memory then insert one row per user in a single transaction → seconds-long lock + log bloat at 10K+ users | `NotificationService.cs:285-296, 250-261` | Chunk inserts (e.g., 500), or push to background job + return 202 |
| A69 | H | Broadcast doesn't exclude SystemAdmin → admins receive their own broadcasts | `NotificationService.cs:280-283` | Filter `u.UserType != UserType.SystemAdmin` |
| A70 | H | Notification history `.Include(n => n.User)` redundant when projection already maps `n.User.Name`; no index on `(IsDeleted, CreatedAt)` for the order-by | `NotificationService.cs:317-349` | Drop Include; add composite index |
| A71 | H | Admin `SendNotification` accepts TargetUserId / RoleName / broadcast — no validator forces "exactly one"; both set silently picks TargetUserId | `AdminNotificationController.cs:30-50`; validator | Cross-field validation |
| A72 | M | `SendToRoleAsync` accepts arbitrary string `roleName` — no validation against `UserRoles` constants | `NotificationService.cs:241-275` | Validate against enum |
| A73 | M | `Notification` entity has no `SentByAdminId` / `SourceType` — admin-vs-system inferred from `Type == AdminMessage` only; no audit trail | `Notification.cs:6-16` | Add `SentByAdminId` (nullable FK), `BroadcastBatchId` |
| A74 | M | History `fromDate.Value.Date` strips caller-provided time but `toDate.Value.Date.AddDays(1)` honors midnight — asymmetric, silent | `NotificationService.cs:327-331` | Either honor time on both or document |

### G. Admin Content (About / Contact)

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| A75 | M | "Singleton" content has no DB unique constraint — concurrent admin-edit creates duplicate rows; `FirstAsync` returns non-deterministic row | `AdminContentService.cs:50-71, 107-128` | Sentinel PK or seed singleton at migration |
| A76 | M | `AboutApp.Content` validator only `NotEmpty` — admin can paste 10MB; no XSS sanitization if rendered as HTML in WebView | `UpdateAboutAppRequestValidator.cs:11-13` | `MaximumLength(50000)` + sanitize/escape HTML |
| A77 | M | No RowVersion / `[ConcurrencyCheck]` — two admins editing simultaneously = silent last-write-wins | `AdminContentService.cs:32-48, 93-105` | Add RowVersion + etag |
| A78 | L | No audit table for content edits beyond `ILogger`; `TrackableEntity` populator may not be wired | `AdminContentService.cs` | Verify audit interceptor populates `UpdatedBy`; persist history rows if needed |

### H. Admin Redemption Approvals (3-level)

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| A79 | C | (= top blocker #4) Concurrent SM/ZM approvals: `RowVersion` defined but never read, no isolation; both approvals commit | `RedemptionApprovalService.cs:259-311, 348-378` | Use `ConcurrencyToken` on RowVersion, or `ExecuteUpdateAsync` with `WHERE Status = @expected` |
| A80 | C | (= top blocker #6) Cash OTP attempt counter incremented via SaveChanges *outside* transaction → brute-force lockout bypass; SHA-256 hash with no salt | `RedemptionApprovalService.cs:441-447, 660-664` | Atomic `ExecuteUpdateAsync` for increment; HMAC with per-request salt |
| A81 | C | (= top blocker #5) Wallet completion writes balance/SarBalance/HeldBalance with no RowVersion check — concurrent earn during completion overwrites | `RedemptionApprovalService.cs:584-587` | Enforce Wallet RowVersion or use `ExecuteUpdateAsync` with subtraction |
| A82 | H | `ApproveAsync` calls notification AFTER `CommitAsync` — failure leaves user unnotified; same in Reject + ConfirmCashHandover | `RedemptionApprovalService.cs:312-338` | Wrap notification in try/catch+log; consider outbox for retries |
| A83 | H | Dual-role SM+ZM auto-skips ZM step but doesn't write a synthetic `RedemptionApproval` row → audit cannot show the skipped step | `RedemptionApprovalService.cs:233-238` | Write second approval row marked AutoSkippedByDualRole |
| A84 | M | Pending list in-memory `Concat` + Skip/Take — `pendingRows` & `reviewedRows` not paged | `RedemptionApprovalService.cs:168-185` | UNION ALL server-side, or paged separately |
| A85 | M | `RefundPointsAsync` decrements `wallet.HeldBalance` with no negative-guard; no DB CHECK constraint | `RedemptionApprovalService.cs:610-611` | `Math.Max(0, ...)`, plus DB CHECK |
| A86 | M | FIFO consumption only filters `Earned`; `InvitationReward` transactions with `RemainingAmount > 0` are ignored — redemption can fail despite plenty of total points | `RedemptionApprovalService.cs:558-563` | Also include `InvitationReward` |
| A87 | L | `AdminRedemptionService.GetAllAsync` `.Include(r => r.User)` materializes full ApplicationUser; only Name + Mobile projected | `AdminRedemptionService.cs:24-26` | Drop Include, project directly |

---

## Table 2 — Mobile App

### M. App Authentication & Registration

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| M1 | H | `_ = SendWelcomeMessageAsync(...)` and `_ = SendRejectionMessageAsync(...)` fire-and-forget after request scope tears down — captures scoped deps; `ObjectDisposedException` risk; lost messages | `ApprovalService.cs:515, 541, 568-580, 582-598` | Send sync inside scope, or queue to hosted background worker |
| M2 | H | `VerificationTokenOptions.ExpiryMinutes = 300` (5 hours) **and not single-use** — leaked token gives 5h replay window for registration | `DependencyInjection.cs:79-83`; `RegistrationVerificationToken.cs:10-19` | Drop expiry to 10–15 min; jti in DB marked consumed |
| M3 | H | Refresh tokens cleaned only when both revoked AND expired → with 365-day lifetime + multi-device users, collection grows unboundedly; loaded on every refresh | `AuthService.cs:776`; `UserRepository.cs:23-26` | Background sweep + cap (e.g. last 20 active per user) |
| M4 | H | Refresh-token "is admin?" decision uses **current** `roles.Contains(SystemAdmin)`, not the scope minted into the token — privilege change retroactively re-routes existing tokens | `AuthService.cs:761-770` | Bind scope to refresh-token row (e.g., `IsAdminToken` column) |
| M5 | H | `IFormFile` size validated post-binding; no `[RequestSizeLimit]` → IIS buffers full payload first | `AuthController.cs:81-105` | Add `[RequestSizeLimit(6 * 1024 * 1024)]`; tune `FormOptions.MultipartBodyLengthLimit` |
| M6 | H | Image MIME validated by **extension** only — `evil.png` containing HTML/JS gets stored XSS via `wwwroot/uploads/shops/*` | `RegisterShopOwnerRequestValidator.cs:67-72`; `FileUploadHelper.cs:21-26` | (See cross-cutting X9) |
| M7 | M | Approve/Reject have no `IsRowVersion` on ApplicationUser → race surfaces as 500; doesn't catch `DbUpdateConcurrencyException` | `ApprovalService.cs:389, 428, 503` | Catch + return friendly 409 |
| M8 | M | OTP verify race: `VerificationAttempts` incremented BEFORE Twilio call; concurrent verifies bypass the 5-attempt cap (Twilio's own cap remains) | `OtpService.cs:122-163` | Add RowVersion to OtpCode or rely entirely on Twilio's cap |
| M9 | M | OTP rate limit is per-mobile only (3/15min); attacker rotating mobile numbers can blow Twilio quota & SMS-bomb | `OtpService.cs:173-192` | Per-IP rate limiter middleware |
| M10 | M | Login enumeration: distinct error codes for `UserNotFound` (404), `UserDisabled`/`Rejected`/`NotApproved` (403), success (200) | `AuthService.cs:651-661, 54-55`; `AuthErrors.cs` | Collapse to one generic "OTP sent" / "verification required" |
| M11 | M | Verification token contains only `mobile|expiry` — same token replays across all 3 register endpoints during 5h window | `RegistrationVerificationToken.cs:10-54`; `AuthService.cs:201-640` | Bind UserType into token payload, mark consumed at first registration |
| M12 | M | All login validators reject Egyptian local format `01XXXXXXXXX` despite `MobileNumberHelper.Normalize` handling it — staff with `01...` can't log in | `LoginRequestValidator.cs:13`, sibling validators | Update regex to also accept `01\d{9}` |
| M13 | M | `RegistrationVerificationToken.Validate` compares HMACs as base64 strings via `FixedTimeEquals` → length mismatch throws `CryptographicException` → 500 | `RegistrationVerificationToken.cs:31-34` | Compare raw bytes; guard length first |
| M14 | M | `UserCreationHelper.CreateWithRoleAsync`: if `AddToRoleAsync` fails the User row is left in DB unless surrounding transaction rolls back — Identity context enrollment uncertain | `UserCreationHelper.cs:13-37`; `AuthService.cs:269-300, 451-490, 584-613` | Verify same DbContext / connection, or explicit cleanup on role failure |
| M15 | M | Approve/Reject `UpdateAsync` `IdentityResult.Succeeded` not checked → audit log shows rejection while user remains pending | `ApprovalService.cs:500-505` | Capture result and roll back transaction on failure |
| M16 | L | OTP `MobileMismatch` (400) returned when PinId/mobile differ → attacker with valid PinId can probe other mobiles for "wrong OTP" vs "wrong mobile" | `AuthService.cs:86-87` | Increment attempts on mismatch or return generic invalid-OTP |
| M17 | L | Approve `BuildRegisterResponseAsync` reads RewardSettings AFTER commit → if admin changed settings between commit and message, "you'll get 50 points" message can mismatch actual credit | `AuthService.cs:843-857` | Read once, reuse |
| M18 | L | `RegistrationVerificationToken.Generate` defaults `expiryMinutes=10` while AuthService passes 300 — misleading default trap | `RegistrationVerificationToken.cs:10` | Drop default arg |
| M19 | L | Seller validator `When(x => x.StoreName != null || x.VAT != null || ...)` doesn't include ShortAddress/CityId/NationalAddress → confusing generic error | `RegisterSellerRequestValidator.cs:33` | Include all shop-data fields in When |
| M20 | L | OtpCode `RegistrationData` column never read — dead PII column | `OtpCode.cs:37`; `OtpService.cs:46, 103, 170` | Drop in follow-up migration |
| M21 | L | Refresh-token cleanup uses `IsDisabled` only → if "delete without disable" added later, deleted users silently re-enabled for refresh | `AuthService.cs:752-759, 691-698` | Add explicit `IsAccountDeleted` check |
| M22 | L | ShopOwner registration that overwrites Seller's image leaves old image on disk | `AuthService.cs:332` | Capture old URL, delete after commit succeeds |
| M23 | L | WhatsApp rejection-reason variable can exceed Twilio limits; silent send failure | `ApprovalService.cs:582-598`; `RejectRequestValidator.cs:14-16` | Cap reason at e.g. 200 chars |
| M24 | L | `TwilioService.FormatMobileNumber` exception leaks first 4 digits of mobile in message | `TwilioService.cs:264` | Use `MobileNumberHelper.Mask` |
| M25 | I | `VerificationToken:HmacKey` derived from JWT key → JWT rotation invalidates pending verification tokens; JWT leak forges them | `DependencyInjection.cs:78-83` | Independent `VerificationToken:Key` config + HKDF |

### N. Profile

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| M26 | C | (= top blocker #10) Profile photo upload validates extension only, no MIME / sniff → stored XSS via `wwwroot/uploads/profiles/` | `ProfileService.cs:78-86`; `FileStorageService.cs:35-43` | (See X9) |
| M27 | H | `UpdateProfilePhotoAsync` deletes old photo BEFORE new upload succeeds — failed upload = lost photo + error | `ProfileService.cs:92-104` | Upload first, swap reference, delete old after success |
| M28 | H | `DeleteAccountAsync` doesn't bump `SecurityStamp` and doesn't validate `IsAccountDeleted` on JWT issuance/refresh — deleted user usable until JWT expiry | `ProfileService.cs:113-145` | Bump SecurityStamp; validate `IsAccountDeleted` at JWT issuance |
| M29 | H | Soft-delete cascades incomplete: `InvitationCode` not nulled (invitees can still register pointing to deleted user); reward credit then fails silently per `inviterEligible` | `ProfileService.cs:130-138`; `AuthService.cs:823-840` | Null `InvitationCode`, set status Rejected, decline new invites |
| M30 | M | `GetProfileAsync` returns `Points` field for ShopOwner (always 0; ShopOwner doesn't earn) | `ProfileService.cs:40-50` | Treat ShopOwner like staff (no Points field) |
| M31 | M | Photo size enforced post-materialization; no `[RequestSizeLimit]` on action | `ProfileService.cs:20, 97`; controller | `[RequestSizeLimit(6*1024*1024)]` |
| M32 | L | DeleteAccount route gates Seller/Technician/ShopOwner; SalesMan/ZM gated by class-level `[Authorize]` for GET/photo — works but easy to miss | `ProfileController.cs:11-12, 45` | Add comment / explicit attribute |

### O. Scan

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| M33 | C | (= top blocker #8) `EnsureWalletAsync` runs BEFORE the scan transaction → orphan wallet on rollback | `ScanService.cs:63, 104` | Move inside transaction or accept zero-balance orphan |
| M34 | C | (= top blocker #9) Concurrent scans Available→SellerScanned race: loser gets `BarcodeAlreadyScanned` instead of awarding 50%; `default:` arm hides corrupt state | `ScanService.cs:67-77, 117-149` | Distinguish same-role vs different-role on retry |
| M35 | H | `EnsureWalletAsync` swallows `DbUpdateException` indiscriminately — catches more than the unique-violation it intends to handle | `ScanService.cs:316-322` | Filter SqlException numbers 2627/2601 on `IX_Wallets_UserId` |
| M36 | H | `default:` returns `BarcodeAlreadyScanned` for what should be invariant violation → misdiagnosis | `ScanService.cs:148-149` | Distinct error + alert |
| M37 | H | `try/catch (Exception ex)` rethrows after rollback → 500s for known DB exceptions (e.g., FK race on `WalletId`) | `ScanService.cs:250-255` | Map known DB exceptions to typed errors |
| M38 | M | Scan barcode loaded with `.Include(b => b.Product)` no `AsNoTracking` | `ScanService.cs:84-87` | Project to slim DTO; load barcode tracked for mutation only |
| M39 | M | Scan request `Latitude/Longitude` stored as `double` with no range validation | `ScanBarcodeRequest.cs:5-6`; `ScanRecord.cs:11-12` | FluentValidation + `decimal` storage |
| M40 | L | Scan history filters use `>= FromDate.Date` and `< ToDate.Date.AddDays(1)` but `CreatedAt` is UTC while user date is ambiguous → off-by-up-to-3-hours for KSA | `ScanService.cs:269-273`; `WalletService.cs:50-54` | Document timezone in DTO; treat input as DateOnly + KSA offset |

### P. Wallet

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| M41 | H | `WalletTransactionResponse` returns `Math.Abs(t.Amount)` and `Math.Abs(t.SarAmount)` — sign lost; mobile UI cannot distinguish credit from debit without inspecting Type | `WalletService.cs:62-66`; `DashboardService.cs:54-55` | Return signed amount (or split Credit/Debit) |
| M42 | M | No composite index on `(WalletId, Type, CreatedAt)` for type-filter + ordered listing | `WalletTransactionConfiguration.cs:60` | Add composite index |
| M43 | L | `Wallet.RowVersion` enforced only via EF tracking; not visible to reviewers | `ScanService.cs:166-169, 188-191` | Add explanatory comment |

### Q. Invitation

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| M44 | H | Lazy-generates invitation code on every `GET /invitation` call when null → concurrent first calls race-create | `InvitationService.cs:48-52` | Always seed at registration; remove lazy path |
| M45 | H | Reward double-credit risk: idempotency check uses `t.Wallet.UserId == invitee.Id` but `ReferenceId` stores `inviter.Id` — re-registration (M6 path) can double-credit | `InvitationService.cs:125-134, 168-204` | Check by `(WalletId=inviter.Wallet, Type=InvitationReward, ReferenceId=invitee.Id)` |
| M46 | H | `ExecuteUpdateAsync` on `InviterRewardCount` runs OUTSIDE transaction (auto-commits) → if surrounding `SaveChanges` fails, count is +1 with no transaction record | `InvitationService.cs:168-171, 206-228` | Snapshot+OptConcurrency, or compensating decrement on failure |
| M47 | M | QR (BMP→base64) regenerated every request — pure CPU waste since invitation code is immutable | `InvitationService.cs:54-56` | Cache `(code → base64)` in `IMemoryCache` |
| M48 | M | `QrCodeGenerator` named `ConvertToPng` but writes BMP — mobile decoders may assume PNG and fail | `Infrastructure/Services/QrCodeGenerator.cs:29` | Generate PNG (SkiaSharp) or rename + document MIME |
| M49 | M | `[ThreadStatic] BarcodeWriterPixelData` cached writer ignores per-call width/height — second call asking 600×600 silently gets 300×300 | `QrCodeGenerator.cs:9-23` | Recreate writer when options differ |
| M50 | M | `totalPointsEarned` sums ALL `InvitationReward` transactions for the user — also counts the user's own 50pt signup bonus when they were invited | `InvitationService.cs:69-71` | Filter by ReferenceId or use distinct `InviteeSignup` enum value |

### R. Dashboard (home aggregator)

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| M51 | H | `GetShopOwnerDashboardAsync` does N+1 `FindByIdAsync` per seller | `DashboardService.cs:117-136` | Single batched `Where(u => sellerUserIds.Contains(u.Id))` |
| M52 | M | `pointsToRedeem` doesn't separately surface HeldBalance — UX confusion if user has pending redemption | `DashboardService.cs:39-42` | Show HeldBalance separately |
| M53 | L | Same `Math.Abs` issue as wallet transactions (M41) | `DashboardService.cs:54-60` | See M41 |

### S. Shops / Map

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| M54 | C | (= top blocker #11) Returns staff member's mobile (`EnteredByUser.MobileNumber`) — direct PII leak to all authenticated users | `ShopService.cs:36`; `ShopMapItemResponse.cs:11` | Remove the field, or expose ShopOwner mobile only, or mask |
| M55 | H | No pagination, no result cap; 3,235+ rows on every authenticated `/api/shops/map` call | `ShopService.cs:18-38` | `Take(N)` + bbox/radius filter + cache |
| M56 | M | OrderBy `CustomerName` (Arabic) with no index on `(CityId, CustomerName)` | `ErpCustomerConfiguration.cs:33-34`; `ShopService.cs:25-26` | Filtered composite index |
| M57 | M | No role gate on the controller — any authenticated user (incl. staff accounts) can enumerate all shops | `ShopController.cs:10` | `[Authorize(Roles="Seller,Technician,ShopOwner")]` |
| M58 | L | Invalid `cityId` returns empty list silently — indistinguishable from "no shops" | `ShopController.cs:22-27` | Validate cityId exists |

### T. App Notifications & FCM Push

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| M59 | H | `GetUnreadCountAsync` runs `COUNT(*)` on every poll — at 1k DAU × 30s polls = ~120 qps just for badge | `NotificationService.cs:94-98` | `IMemoryCache` 30s TTL keyed by userId, invalidate on Create/MarkRead/Delete |
| M60 | H | `MarkAllAsReadAsync` `ExecuteUpdateAsync` skips audit interceptor — `UpdatedBy/UpdatedAt` not set | `NotificationService.cs:121-130` | Either accept (document) or set audit fields explicitly via `SetProperty` |
| M61 | H | `CreateAsync` fire-and-forget `_ = FirePushToUserAsync(...)` swallows TaskScheduler unobserved exceptions | `NotificationService.cs:220-223` | Hosted background channel with proper try/catch + DLQ |
| M62 | H | (= A68) Broadcast scaling — also affects the user-side trigger paths | `NotificationService.cs:243-275, 277-310` | See A68 |
| M63 | H | FCM `SendToUserAsync` returns `true` (keeps token) for `InvalidArgument` / `SenderIdMismatch` — stale tokens never cleared | `FirebaseMessagingService.cs:73-87` | Treat as "clear token" alongside `Unregistered` |
| M64 | M | `RegisterDeviceAsync` doesn't check token uniqueness across users → push intended for B can go to A after device hand-off | `NotificationService.cs:39-50` | Clear conflicting users' tokens first |
| M65 | M | `MarkAsRead`/`Delete` IDOR-adjacent: returns `NotificationNotOwned` (different error) for IDs that exist vs `NotFound` for ones that don't — enumeration | `NotificationService.cs:100-119, 132-148` | `Where(n => n.UserId == userId)` in initial query; uniform NotFound |
| M66 | M | FCM init crashes singleton on bad creds — push subsystem dead until restart | `FirebaseMessagingService.cs:24-54` | try/catch, log, set `_enabled=false` |
| M67 | M | `SendToMultipleAsync` chunks at 500 sequentially; 30k broadcast = 60 sequential round trips; AppPool recycle = lost remainder | `FirebaseMessagingService.cs:90-131` | `Task.WhenAll` (semaphore 4–6) + persisted job log |
| M68 | L | `UpdatePreferencesAsync` keeps "unmute" rows in DB instead of deleting | `NotificationService.cs:166-192` | Delete row when `IsPushMuted == false` |

### U. App Redemption Requests

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| M69 | H | `CreateRequestAsync` calls Twilio `SendWhatsAppMessageAsync` *inside* a DB transaction — Wallet rows locked while waiting on remote | `RedemptionService.cs:69-156` | Send OTP after commit; failure → `OtpSendFailed` status with resend path |
| M70 | H | SAR rate read outside transaction; admin update during the window can change stored rate vs validated rate | `RedemptionService.cs:49-56, 107-108` | Re-read inside transaction (UPDLOCK) |
| M71 | H | Cash OTP weak: 6-digit numeric, attempts reset on resend, 14-day lifetime → effectively unlimited brute force | `RedemptionService.cs:21, 257, 352-355` | Cap total resends per request; shorten lifetime; rate-limit resend |
| M72 | M | Resend cooldown derives from `UpdatedAt ?? CreatedAt`; any status mutation effectively resets the cooldown | `RedemptionService.cs:250-252` | Dedicated `LastOtpSentAt` column |
| M73 | M | `GetActiveRequestAsync` `FirstOrDefaultAsync` with no `OrderByDescending(CreatedAt)` — wrong row in races | `RedemptionService.cs:166-177` | Add ordering |
| M74 | M | `ExpireOldPointsAsync` runs inside `GetAvailableBalanceAsync` — every "what's my balance?" mutates wallet | `RedemptionService.cs:204-226` | Move expiry to background service exclusively |
| M75 | M | `request.PointsAmount` is decimal with no integer enforcement at validator | `CreateRedemptionRequestValidator.cs:17-18` | `Must(p => p == Math.Floor(p))` + upper bound |
| M76 | L | Cash OTP template SID hardcoded as `const` in service | `RedemptionService.cs:20` | Move to options |

### V. App Content (read-only)

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| M77 | L | `ContentController.GetContactUs/GetAboutApp` are anonymous — and `AdminContentService.GetOrCreateContactUsAsync` lazy-creates a row on first read = unauth write trigger | `ContentController.cs:9-32`; `AdminContentService.cs:50-71, 107-128` | Seed defaults at migration; make GET read-only |

---

## Table 3 — Platform / Cross-cutting

These touch both surfaces. Fix once, both Admin Dashboard and App benefit.

### X. Security headers, error handling, exposure

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| X1 | C | (= top blocker #2) No HSTS, CSP, X-Content-Type-Options, X-Frame-Options, Referrer-Policy. `UseHsts()` not called | `Program.cs:50-67` | Add HSTS (non-Dev); middleware to inject `nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-Frame-Options: DENY`, restrictive CSP |
| X2 | C | (= top blocker #3) `GlobalExceptionHandler` writes `ProblemDetails` directly via `WriteAsJsonAsync` → bypasses `IProblemDetailsService`, drops `traceId`/`instance`/customizers | `GlobalExceptionHandler.cs:14-25` | Use `IProblemDetailsService.WriteAsync(new ProblemDetailsContext(...))` so registered customizers add traceId |
| X3 | C | CORS base `appsettings.json` lists localhost / netlify dev origins; if any deploy slip leaves prod env-specific config absent, dev origins are accepted in prod | `DependencyInjection.cs:44-57`; `appsettings.json:33-48` | Move dev origins out of base config; fail startup on `http://` prod origin |
| X4 | H | `ResultExtension.ToProblem` instantiates `new HttpContextAccessor()` per call instead of resolving registered singleton | `ResultExtension.cs:17` | Pass HttpContext from controllers, or resolve via DI |
| X5 | H | No health checks (`/health/live`, `/health/ready`); IIS marks app started before EF migrations finish on cold start → first requests get 500 | `Program.cs:24-37` | `services.AddHealthChecks().AddDbContextCheck<>()`; map endpoints |
| X6 | H | Auto-migrate runs on every Staging/UAT startup with no backup or replica lock — single-replica only; if Prod ever scales horizontally, migrations race | `Program.cs:26-37` | Move migrations to one-shot job or env-var-gated path |
| X7 | H | `services.AddDbContext` lacks `AddDbContextPool`, `EnableRetryOnFailure`, command timeout — transient drops on SmarterASP/Azure SQL produce raw 500s | `DependencyInjection.cs:64-65` | `AddDbContextPool(... o => o.UseSqlServer(cs, sql => sql.EnableRetryOnFailure(3).CommandTimeout(30)))` |
| X8 | H | No `UseResponseCompression`; `UseStaticFiles` returns no `Cache-Control` for `wwwroot/uploads/*` | `Program.cs:53` | Add compression; cache headers on `OnPrepareResponse` |
| X9 | C | (= top blocker #1) `FileStorageService.UploadAsync` does NO whitelist / MIME / magic-byte validation. Saves `wwwroot/uploads/<folder>/<guid>.<ext>` and IIS serves verbatim. SVG/HTML payloads = stored XSS. Same gap on profile photo, shop image (admin + public registration) | `FileStorageService.cs:24-51`; profile/shop callsites | Centralize validation: extension whitelist `(png,jpg,jpeg,webp)` + MIME check + magic bytes + size cap; serve uploads with `Content-Disposition: attachment` or via streaming controller |
| X10 | H | Swagger enabled in Staging AND UAT (`uat.raedrewardapp.com`) with no authentication on the UI itself — exposes full API surface | `Program.cs:40-48` | Restrict UI to Dev only, or basic-auth at reverse proxy |

### Y. Database / migrations / DI / config

| # | Sev | Issue | Location | Fix |
|---|-----|-------|----------|-----|
| Y1 | M | `appsettings.json` ships placeholder JWT key `"REPLACE_WITH_SECURE_KEY_IN_ENVIRONMENT"`; rejected only by exact string match | `DependencyInjection.cs:227-228`; `appsettings.json:24` | Drop placeholder from base; require env var; reject low-entropy keys |
| Y2 | M | DataProtection keys persisted to `App_Data/keys` with no encryption-at-rest on shared SmarterASP IIS — co-tenant ACL escape risk | `DependencyInjection.cs:37-41` | `ProtectKeysWithCertificate(...)` or move outside webroot |
| Y3 | M | `ApplicationDbContext.SaveChangesAsync(CancellationToken)` overrides only one form. `SaveChanges()` and `SaveChangesAsync(bool, CancellationToken)` bypass audit/soft-delete logic | `ApplicationDbContext.cs:80-110` | Override all SaveChanges overloads |
| Y4 | M | Cascade-FK rewrite loop sets every cascade to `Restrict` AFTER per-entity config, silently overriding explicit Cascade on `ShopOwnerProfile`/`SellerProfile`/`TechnicianProfile`/`Notification`. "Delete user" code will throw FK violations | `ApplicationDbContext.cs:71-77` | Either remove the global override or exempt entity-types declared as cascade |
| Y5 | M | `_fcm` Singleton + Twilio `TwilioClient.Init` per scope (TwilioService is Scoped) — re-init resets global state under load | `TwilioService.cs:51`; `DependencyInjection.cs:87` | Init Twilio once at startup, or change to Singleton |
| Y6 | M | Twilio failures bubble to 500 with no retry / circuit-break — outage cascades to login OTP + redemption flows | `TwilioService.cs:95-104, 137-146, 229-238` | Polly retry (exp backoff) for 5xx/429; circuit-break to 502 |
| Y7 | M | Twilio dev config `WhatsAppFromNumber: "whatsapp:+966..."` already prefixes `whatsapp:` but service prepends again → `whatsapp:whatsapp:+966...`. Mock mode hides this in dev; flips to real send → fails immediately | `TwilioService.cs:209-211`; `appsettings.Development.json:26` | Normalize: strip prefix if present; or remove from dev config |
| Y8 | M | `AddInviterRewardCount` migration: `UPDATE u SET ... FROM AspNetUsers u OUTER APPLY (...)` with no `WHERE InviterRewardCount = 0` guard — disaster-recovery replay overwrites real counts | `Migrations/20260423135626_AddInviterRewardCount.cs:24-37` | Add guard |
| Y9 | M | `ConstrainFcmTokenLength` migration shrinks `nvarchar(max)→nvarchar(512)` with no over-length pre-check | `Migrations/20260407123539_ConstrainFcmTokenLength.cs:13-21` | `UPDATE ... SET FcmToken=NULL WHERE LEN(FcmToken)>512` first |
| Y10 | M | Serilog Console-only sink — IIS `stdout` logs unbounded, no rotation | `appsettings.json:5-21` | Add `Serilog.Sinks.File` rolling daily, retain 14 |
| Y11 | M | `UseStaticFiles` before `UseCors` — admin SPA cross-origin requests for uploads see no CORS headers | `Program.cs:53-54` | Move `UseCors` first |
| Y12 | L | FCM token validator has no charset restriction (control chars / NULs allowed) | `RegisterDeviceRequestValidator.cs:11-13` | `Matches(@"^[A-Za-z0-9_:.\-]+$")` |
| Y13 | L | FluentValidation single-assembly scan — validators in other assemblies silently ignored | `DependencyInjection.cs:194-201` | Multi-assembly scan + startup self-test |
| Y14 | L | `FileStorageService.DeleteAsync(fileUrl)` no path-traversal check (`Path.GetFullPath` + StartsWith assertion missing) | `FileStorageService.cs:53-71` | Defense-in-depth assertion |
| Y15 | I | `ClockSkew = 30s` is good (default 5min reduced); no JWT replay protection (JTI + revocation list) | `DependencyInjection.cs:241-251` | Optional: JTI for high-value admin tokens |

---

## What I'd do, in order

**Week 1 — delivery blockers**
1. Centralised file-upload validation (`X9`) — closes 4 stored-XSS surfaces in one fix.
2. Security-header middleware (`X1`).
3. `GlobalExceptionHandler` fix (`X2`).
4. Wallet RowVersion enforcement (`A39 + A81 + cancel + complete`).
5. Redemption approval RowVersion (`A79`) and OTP-attempt atomicity (`A80`).
6. `/api/shops/map` PII (`M54`) and result cap (`M55`).
7. Notification broadcast batching (`A68`).
8. Scan transaction & state-machine fixes (`M33–M37`).
9. Profile delete cascade + JWT invalidation (`M28–M29`).
10. Hard-delete → soft-delete on Product (`A34`).
11. Rate limiting on `/api/admin/auth/login`, `/api/auth/login`, `/api/auth/send-otp` (`A3 + M9`).
12. Health checks + DB retry (`X5 + X7`).

**Week 2 — high-priority ops & integrity**
- Refresh-token revocation actually persisting (`A13`)
- S19 reassignment scope fix (`A16`) and dual-role delete invariants (`A17`)
- Auto-migrate gating + one-shot job (`X6`)
- Admin user listing perf (`A10–A11, A21–A22`)
- Analytics N+1 / unbounded queries (`A56–A62`)
- Notification unread-count cache (`M59`)
- Invitation reward double-credit (`M45–M46`)
- Cash OTP brute-force window (`M71`)
- Admin shop-data edit endpoints (`A15`)

**Week 3 — polish & ops**
- Logging file sink (`Y10`)
- Swagger UI auth in non-Dev (`X10`)
- DataProtection key encryption (`Y2`)
- Cascade-FK config bug (`Y4`)
- Dead code / DTO consistency / dead enums (`A30–A33`, `M20`, `M76`)
- KSA timezone for analytics (`A55`)
- Audit table for admin actions (`A29`) and content edits (`A78`)

---

## Notes on what was *not* found broken

- Admin-only `[Authorize(Roles=SystemAdmin)]` is consistently applied across admin controllers.
- `IDOR` on admin lookups by ID returns 404 cleanly without leaking existence.
- `ScanRecord` composite unique `(BarcodeId, ScannerRole)` with soft-delete filter works correctly.
- `ProductBarcode.RowVersion` is enforced by EF in the user-side scan flow.
- Mobile masking discipline is applied in error-path logs.
- `UseAuthentication` correctly runs before `UseAuthorization`.
- 3 user types' approval state machine (PendingSalesman → PendingZoneManager → Approved) is correctly modelled.
- Refresh token reuse detection (S17) is wired up.
- The 365-day refresh lifetime + 1h access lifetime split is sane for the mobile UX.

---

## Top 3 architecture-level recommendations

1. **Outbox pattern for side effects.** WhatsApp sends, FCM pushes, notification fan-outs are all currently fire-and-forget *inside or after* the request scope. Move them to a `Channel<T>`-backed hosted worker that resolves its own scope, with retries + DLQ. Single change closes M1, M61, M67, A68, X2-correlation, and removes the brittle `_ = SomeAsync()` pattern.
2. **Centralised upload pipeline.** One service that validates extension+MIME+magic-bytes+size, stores via random GUID, and serves through a controller (`Content-Disposition: attachment`, `nosniff`, signed URLs if needed). Closes X9, M6, M26, A26.
3. **Audit trail.** Append-only `AdminAudit` (timestamp, adminId, action, targetId, payloadHash) populated by a `SaveChangesAsync` interceptor. Closes A29, A78, A82, A83.
