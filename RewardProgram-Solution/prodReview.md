# Production-Readiness Review — RewardProgram

**Date:** 2026-04-05  
**Branch:** dev  
**Scope:** Full codebase review for UAT and production readiness

---

## Summary

| Severity | Count |
|----------|-------|
| CRITICAL | 10 |
| HIGH | 18 |
| MEDIUM | 24 |
| LOW | 12 |

---

## CRITICAL Issues (Must fix before UAT)

### C1. Secrets committed to source control
- **Files:** `appsettings.Staging.json:3-4,24-26`, `appsettings.Production.json:3-4`
- Database password `rR123##$$` and Twilio credentials are hardcoded in plain text.
- **Fix:** Remove all real credentials. Use environment variables or Azure Key Vault.

### C2. Production Twilio mock mode is ON
- **File:** `appsettings.Production.json:19`
- `"UseMockMode": true` means production will never send real OTPs. Any user can bypass OTP.
- **Fix:** Set `UseMockMode: false` for production.

### C3. Staging and Production share identical database
- **Files:** `appsettings.Staging.json:3` vs `appsettings.Production.json:3`
- Both point to the same DB on `SQL11290601.site4now.net`.
- **Fix:** Use separate databases per environment.

### C4. No concurrency control on Wallet entity
- **File:** `Wallet.cs` (entity), `RedemptionService.cs:62`
- No `RowVersion` on Wallet. Concurrent scans/redemptions can silently overwrite balance updates, losing user points.
- **Fix:** Add `byte[] RowVersion` to Wallet and configure `.IsRowVersion()`.

### C5. Race condition on concurrent redemption approvals (double-approve)
- **File:** `RedemptionApprovalService.cs:119-134`
- Status check runs before the transaction. Two concurrent approvals can both pass validation and double-complete.
- **Fix:** Re-validate status inside the transaction or add RowVersion to `RedemptionRequest`.

### C6. Race condition on user approval (no transaction)
- **File:** `ApprovalService.cs:205-303`
- Neither `ApproveAsync` nor `RejectAsync` uses a transaction. Concurrent approvals can create duplicate records.
- **Fix:** Wrap read + check + update in a transaction.

### C7. Invitation reward double-credit (no idempotency guard)
- **File:** `InvitationService.cs:82-191`
- No check that a reward for a specific `(inviterId, inviteeId)` pair already exists. Retries can double-credit.
- **Fix:** Check for existing `WalletTransaction` with matching `Type + ReferenceId` before crediting.

### C8. Inviter reward cap miscounted
- **File:** `InvitationService.cs:131-133`
- Cap counts ALL `InvitationReward` transactions including when the user was an invitee, reducing effective cap.
- **Fix:** Filter count to only inviter-role transactions (by `ReferenceId` or add a direction field).

### C9. `FindAsync` bypasses soft-delete query filter
- **Files:** `AdminProductService.cs:57,84,152`, `AdminBarcodeService.cs:38`
- `FindAsync` skips global query filters. Soft-deleted products can be edited/used.
- **Fix:** Replace `FindAsync` with `FirstOrDefaultAsync(p => p.Id == id, ct)`.

### C10. PointsExpiryBackgroundService — division by zero + incorrect SAR calculation
- **File:** `PointsExpiryBackgroundService.cs:85,92`
- `tx.RemainingAmount / tx.SarRate` throws if `SarRate == 0`. Also uses first transaction's rate for entire group.
- **Fix:** Guard against zero rate. Accumulate per-transaction SAR amounts individually.

---

## HIGH Issues (Fix before production)

### H1. Revoke-token allows any user to revoke any other user's token
- **File:** `AuthController.cs:153-164`, `AuthService.cs:653-674`
- No ownership check — any authenticated user can revoke another user's refresh token.
- **Fix:** Verify `user.Id == currentUserId` before revoking.

### H2. TOCTOU race on mobile uniqueness during registration
- **File:** `AuthService.cs:117-118, 289-290, 469-470`
- `MobileExistsAsync` check is outside the transaction. Concurrent registrations can bypass it.
- **Fix:** Add DB unique constraint on `MobileNumber` and handle `DbUpdateException`.

### H3. TOCTOU race on ShopOwner CustomerCode ownership
- **File:** `AuthService.cs:144-147`
- `existingOwner` check runs before the transaction. Two concurrent registrations can both pass.
- **Fix:** Add unique constraint on `ShopOwnerProfile.CustomerCode`.

### H4. Verification token is not single-use (replayable within 10 min)
- **File:** `RegistrationVerificationToken.cs`, `AuthService.cs:106-108`
- No nonce or consumed tracking. Same token can register multiple accounts.
- **Fix:** Add one-time-use mechanism (nonce in DB, mark consumed on use).

### H5. Minimum redemption points hardcoded instead of using RewardSettings
- **File:** `RedemptionService.cs:43-44`, `CreateRedemptionRequestValidator.cs:16`
- Hardcoded `1000` ignores admin-configurable `MinimumRedemptionPoints`.
- **Fix:** Read from `RewardSettings` dynamically.

### H6. ShopOwner excluded from redemption
- **File:** `RedemptionController.cs:13`
- Only `Seller` and `Technician` allowed. ShopOwners who earn points cannot redeem.
- **Fix:** Confirm business rules; add `ShopOwner` if they should redeem.

### H7. ExpireOldPointsAsync called outside transaction in GetAvailableBalance
- **File:** `RedemptionService.cs:167`
- Mutates wallet balance without a transaction. Concurrent reads can corrupt data.
- **Fix:** Wrap in transaction or make it a pure read.

### H8. Nested SaveChanges inside ExpireOldPointsAsync
- **File:** `RedemptionService.cs:84`
- Inner `SaveChangesAsync` inside transaction can leave partial state on rollback.
- **Fix:** Remove inner save; let the caller handle persistence.

### H9. OTP lockout has no recovery path
- **File:** `RedemptionApprovalService.cs:273-274`
- After 5 failed OTP attempts, cash request is stuck forever — no regeneration or cancellation.
- **Fix:** Add admin endpoint for OTP regeneration or auto-cancel on lockout.

### H10. ShopOwner cannot see wallet/scan endpoints
- **File:** `WalletController.cs:13`, `ScanController.cs:13`
- Only `Seller` and `Technician`. Dashboard includes `ShopOwner` but wallet/scan do not.
- **Fix:** Align role access across controllers.

### H11. ScanRecord.Id used as ReferenceId before SaveChanges
- **File:** `ScanService.cs:149,169`
- If IDs are DB-generated, `scanRecord.Id` is default/empty at this point.
- **Fix:** Confirm client-side GUID generation, or save before referencing.

### H12. Notification pagination not normalized (unbounded page size)
- **File:** `NotificationService.cs:39-41`
- No bounds checking. `pageSize=1000000` dumps entire table.
- **Fix:** Apply `PaginationHelper.Normalize`.

### H13. EditSalesMan city replacement query always returns empty
- **File:** `AdminUserService.cs:743-745`
- Queries for non-null `ApprovalSalesManId` after just setting them to null in-memory.
- **Fix:** Query replacement SalesMan BEFORE clearing the old assignments.

### H14. Unbounded `top` parameter on analytics endpoints
- **File:** `AdminDashboardController.cs:76-77`
- No upper bound. `top=1000000` causes full table sort.
- **Fix:** Clamp to reasonable maximum (e.g., 100).

### H15. Analytics endpoints load large datasets into memory
- **Files:** `AdminDashboardService.cs:361-365` (inactive users), `:509-511` (salesman perf), `:216-233` (points)
- `ToDictionaryAsync`/`ToListAsync` on entire tables, then in-memory processing.
- **Fix:** Push grouping/filtering to SQL. Add pagination/time-bound filters.

### H16. Refresh token lifetime is 365 days
- **File:** `appsettings.json:29`
- 1-year refresh tokens are a security risk.
- **Fix:** Reduce to 7-30 days for production.

### H17. Production AllowedOrigins missing admin domain
- **File:** `appsettings.Production.json:15-17`
- Only lists atempurl.com. Admin dashboard at `admin.raedrewardapp.com` will get CORS errors.
- **Fix:** Add all production domains.

### H18. JWT key placeholder with no production override
- **File:** `appsettings.json:24`, no override in `appsettings.Production.json`
- Will crash on startup if not set via environment variable.
- **Fix:** Ensure deployment provides JWT key via env var or secrets manager.

---

## MEDIUM Issues

### M1. Refresh token cleanup removes in-flight tokens
- `AuthService.cs:642` — `!t.IsActive` removes tokens other sessions may be using.
- **Fix:** Only clean tokens that are both revoked AND expired.

### M2. OTP PinId logged in plaintext
- `OtpService.cs:56-57,113-114` — PinId leakage in logs.
- **Fix:** Mask or omit from logs.

### M3. Mock mode accepts ANY 6-digit OTP
- `TwilioService.cs:107-116` — Should use fixed mock code.
- **Fix:** Accept only "123456" in mock mode.

### M4. GenerateAuthResponseAsync persistence not owned by caller
- `TokenService.cs:94` — UpdateAsync called inside helper, not by the transaction owner.
- **Fix:** Let caller own persistence.

### M5. VerifyLoginAsync returns wrong error for Rejected users
- `AuthService.cs:598-603` — Returns `UserNotApproved` instead of `UserRejected`.
- **Fix:** Mirror the Rejected/Disabled/NotApproved check sequence.

### M6. Invitation code generation throws instead of returning Result
- `AuthService.cs:703-715` — `InvalidOperationException` after 10 attempts.
- **Fix:** Return `Result.Failure` with specific error.

### M7. Notification outside transaction (approval/scan)
- `RedemptionApprovalService.cs:182-195`, `ScanService.cs:185-195`
- **Fix:** Wrap in try-catch to prevent 500 when core operation succeeded.

### M8. Silent no-op on null wallet during completion/refund
- `RedemptionApprovalService.cs:384-385,428-429`
- **Fix:** Return failure to prevent committing without wallet update.

### M9. FIFO shortfall can drive balance negative
- `RedemptionApprovalService.cs:395-403`
- **Fix:** Verify `remaining == 0` after loop.

### M10. Cash OTP uses non-cryptographic PRNG
- `RedemptionApprovalService.cs:446-449` — Uses `Random.Shared`.
- **Fix:** Use `RandomNumberGenerator.GetInt32(100000, 1000000)`.

### M11. Fire-and-forget WhatsApp with no timeout
- `ApprovalService.cs:288`
- **Fix:** Add timeout or queue for background processing.

### M12. Duplicate GetOrCreateWalletAsync — InvitationService lacks DbUpdateException catch
- `ScanService.cs:259-275`, `InvitationService.cs:207-223`
- **Fix:** Add catch or extract to shared service.

### M13. Invitation code generation race condition
- `InvitationService.cs:47-50` — Two concurrent requests can overwrite each other's code.
- **Fix:** Use optimistic concurrency or generate at registration time.

### M14. Deferred SAR rate may differ from scan-time rate
- `ScanService.cs:74,159` — Uses current rate, not original scan's rate.
- **Fix:** Store SarRate on ScanRecord at first-scan time.

### M15. Broadcast can time out for large user bases
- `NotificationService.cs:152-177` — Single massive INSERT for all users.
- **Fix:** Batch inserts (500 at a time).

### M16. Soft-delete unique index issues (6 entities)
- `ShopDataConfiguration.cs:82-86` (VAT, CRN, ShortAddress), `ErpCustomerConfiguration.cs:30`, `ProductConfiguration.cs:40`, `ProductBarcodeConfiguration.cs:41`, `ScanRecordConfiguration.cs:50`
- **Fix:** Add `.HasFilter("[IsDeleted] = 0")` to all unique indexes.

### M17. NationalAddress District should be required
- `NationalAddressDtoValidator.cs:30-32` — Optional but should be mandatory.
- **Fix:** Remove `.When(x => x.District != null)`.

### M18. AdminLoginRequest has no validator
- `AdminAuthController.cs:60` — Empty username/password hits DB.
- **Fix:** Add FluentValidation validator.

### M19. AdminRedemptionService pagination not normalized
- `AdminRedemptionService.cs:45-48`, `AdminDashboardService.cs:286-288`
- **Fix:** Apply `PaginationHelper.Normalize`.

### M20. Missing AsNoTracking on read-only admin queries
- `AdminDashboardService.cs:44,186`, `AdminRedemptionService.cs:24`
- **Fix:** Add `.AsNoTracking()`.

### M21. Null Product on barcodes after soft-delete
- `AdminBarcodeService.cs:124` — `b.Product.Name` can NRE if product was soft-deleted.
- **Fix:** Add null check or filter.

### M22. Wallet.Balance decimal(10,1) — limited precision
- `WalletConfiguration.cs:20-22` — Only 1 decimal place.
- **Fix:** Consider decimal(10,2) to match SarBalance.

### M23. VerifyRegistrationOtpRequestValidator missing OTP format check
- Missing `.Length(6).Matches(@"^\d{6}$")` unlike other OTP validators.

### M24. Notification entity has no FK to ApplicationUser
- `NotificationConfiguration.cs` — No foreign key constraint. Orphaned notifications possible.
- **Fix:** Add FK relationship.

---

## LOW Issues

### L1. JWT key reused as HMAC key for verification tokens
- `DependencyInjection.cs:71-77` — Key separation is best practice.

### L2. FormatMobileNumber silent fallthrough
- `TwilioService.cs:237-248` — Unexpected format passes through unchanged.

### L3. No rate limiting on refresh token endpoint
- `AuthController.cs:140-151` — Potential DoS vector.

### L4. Seller re-check inside transaction uses same snapshot
- `AuthService.cs:398-399` — Re-check may be ineffective at READ COMMITTED.

### L5. `userRegionId` int null check always true
- `RedemptionApprovalService.cs:359-364` — Value type can't be null.

### L6. AdminRedemptionService page size not bounded
- `RedemptionApprovalService.cs:43-44`

### L7. Dashboard shows scan-centric data to ShopOwners
- `DashboardController.cs:12` — Always shows 0 points for ShopOwners.

### L8. SendToUserAsync returns wrong error type
- `NotificationService.cs:115` — Returns `NotificationNotFound` instead of user-not-found.

### L9. BarcodeErrors.InvalidQuantity message says 10000, validator enforces 1000
- `BarcodeErrors.cs:17` vs `AdminGenerateBarcodesRequestValidator.cs:14`

### L10. RedemptionErrors.BelowMinimum hardcodes "1000" in message
- `RedemptionErrors.cs:11` — Becomes misleading if admin changes minimum.

### L11. AdminEdit validators only validate Name field
- `AdminEditShopOwnerRequestValidator.cs`, etc. — Other mutable fields unvalidated.

### L12. Duplicate query filters (global + per-entity)
- `ApplicationDbContext.cs:54-67` + entity configs — Fragile if global filter is extended.

---

## Background Service Issues

### PointsExpiryBackgroundService
- **C10** (above): Division by zero + wrong SAR calculation
- `OperationCanceledException` swallowed on shutdown (logs error instead of clean exit)

### OtpCleanupBackgroundService  
- `OperationCanceledException` swallowed on shutdown (same pattern)
- **Fix for both:** Add `when (ex is not OperationCanceledException)` to catch filter.

---

## Priority Action Plan

### Before UAT (Blockers)
1. **C1-C3**: Fix secrets, mock mode, separate DBs
2. **C4-C6**: Add concurrency control (Wallet RowVersion, transaction wrapping)
3. **C9**: Replace FindAsync with filtered queries
4. **H1**: Add ownership check to revoke-token
5. **H13**: Fix EditSalesMan city replacement ordering

### Before Production
6. **C7-C8**: Fix invitation reward idempotency and cap counting
7. **C10**: Fix background service SAR calculation
8. **H2-H4**: Add DB unique constraints, single-use verification tokens
9. **H5, H16-H18**: Fix hardcoded values, config, CORS
10. **M16**: Add soft-delete filters to all unique indexes

### Post-Launch (Technical Debt)
11. All remaining MEDIUM and LOW issues
12. Performance optimization of analytics queries (H15)
13. Rate limiting (L3)
