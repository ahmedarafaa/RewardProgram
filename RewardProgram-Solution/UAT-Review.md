# Full Codebase Review - UAT Readiness Assessment

**Date:** 2026-04-02
**Branch:** dev
**Build:** 0 errors, 0 warnings, 108 tests passing

---

## Part 1: Business Logic Flows

### 1. Registration Flow

**ShopOwner Registration:**
1. User calls `POST /api/auth/send-otp` with mobile number
2. System checks mobile uniqueness, sends OTP via WhatsApp (Twilio Verify)
3. User calls `POST /api/auth/register/shop-owner` with OTP + all data
4. Pre-OTP validation: mobile unique, CustomerCode exists in ERP, city active, city has SalesMan, no other ShopOwner on this CustomerCode
5. Validate ShopData fields (StoreName, VAT, CRN, ShortAddress, ShopImage) - ALL mandatory for ShopOwner
6. Validate VAT/CRN/ShortAddress uniqueness
7. Upload shop image
8. Validate invitation code (if provided)
9. **Consume OTP** (only after all validation passes - prevents OTP waste)
10. Re-check mobile uniqueness (race condition guard)
11. **Transaction:** Create user (status=PendingSalesman), create ShopData (overwrites Seller's if exists), create ShopOwnerProfile
12. Return success with userId

**Seller Registration:**
- Same flow as ShopOwner EXCEPT:
- If ShopData already exists for CustomerCode: shop fields are OPTIONAL (Seller reuses existing data)
- If ShopData doesn't exist: first Seller must provide all shop fields
- ShopOwner always overwrites Seller's ShopData (Owner wins)
- Multiple Sellers can share same CustomerCode

**Technician Registration:**
- Simplest flow: no CustomerCode, no ShopData
- Just mobile, name, city, postal code, district
- Same OTP + approval flow

**Key Rule:** All 3 types start as `PendingSalesman` status, assigned to the city's ApprovalSalesMan.

---

### 2. Login Flow

1. User calls `POST /api/auth/login` with mobile
2. System checks: user exists, not disabled, not rejected, status=Approved
3. Only Approved users can login
4. OTP sent via WhatsApp
5. User calls `POST /api/auth/verify-login` with PinId + OTP
6. System verifies OTP, generates JWT (1hr) + RefreshToken (7 days)
7. Returns AuthResponse with token, user info

**Token Refresh:** Old token revoked, stale tokens cleaned up, new pair issued.

---

### 3. User Approval Flow (2-Level)

**Level 1 - SalesMan Approval:**
1. SalesMan sees users assigned to them with status `PendingSalesman`
2. SalesMan approves -> status becomes `PendingZoneManager`
3. System validates ZoneManager exists for user's region
4. SalesMan can also reject -> status becomes `Rejected`

**Level 2 - ZoneManager Approval:**
1. ZoneManager sees users in their managed region with status `PendingZoneManager`
2. ZoneManager approves -> status becomes `Approved`
3. On approval:
   - Credit invitation rewards (if user was invited)
   - Send WhatsApp welcome message
   - Create RegistrationApproved notification
4. ZoneManager can also reject -> status becomes `Rejected`, notification sent with reason

**Geographic Authorization:**
- SalesMan can only approve users in cities they're assigned to
- ZoneManager can only approve users in their managed region

---

### 4. Barcode Scanning Flow

**Point Distribution Rules:**
- Seller scans first (Available -> SellerScanned): gets 50% of product points
- Technician scans second (SellerScanned -> Consumed): gets 100%, Seller gets deferred 50%
- Technician scans first (Available -> TechnicianScanned): gets 100%
- Seller scans second (TechnicianScanned -> Consumed): gets 100%
- **Net result:** Both roles always get full product points, but timing differs

**Scan Process:**
1. Validate user: exists, not disabled, Approved, has Seller or Technician role
2. Validate barcode: exists, not Consumed, not already scanned by same role
3. Fetch SAR rate from RewardSettings (default 10:1)
4. **Transaction:** Create ScanRecord, update barcode status, credit wallet(s), create WalletTransaction(s)
5. Send PointsEarned notification

**Wallet:** Lazy-created on first scan. Tracks Balance (points), SarBalance (SAR equivalent), HeldBalance, HeldSarBalance.

---

### 5. Redemption Flow (3-Level Approval + Cash/Bank)

**Request Creation:**
1. User requests redemption with points amount and method (Cash or BankTransfer)
2. Minimum: 1000 points, SAR amount must be integer
3. System runs point expiry (15-month rule) before checking balance
4. Available = Balance - HeldBalance
5. **Transaction:** Check no pending request exists, verify sufficient balance, hold points (HeldBalance += amount), create RedemptionRequest

**3-Level Approval Chain:**
```
PendingSalesMan -> PendingZoneManager -> PendingAdmin -> AdminApproved/Completed
```
- Each level has geographic authorization checks
- Each approval creates an ApprovalRecord for audit trail

**After Admin Approval:**
- **Cash:** Generate 6-digit OTP, hash with SHA256, send via WhatsApp. OTP valid 14 days, max 5 attempts. SalesMan confirms cash handover with OTP -> Completed.
- **BankTransfer:** Immediately completed after admin approval. Bank details (IBAN, BankName, AccountHolder) stored for external processing.

**Completion:** Deduct points from wallet using FIFO (oldest transactions consumed first), create WalletTransaction(type=Redemption).

**Rejection at any level:** Refund held points (HeldBalance -= amount), create WalletTransaction(type=Refund), notify user.

**OTP Expiry:** If 14 days pass, auto-cancel + refund held points.

---

### 6. Point Expiry (15-Month Rule)

1. Triggered before balance checks (redemption creation, balance queries)
2. Find all Earned transactions older than 15 months with RemainingAmount > 0
3. **Protection:** maxExpirable = Balance - HeldBalance (never expire held points)
4. FIFO: expire oldest first, per-transaction SAR rate calculation
5. Create WalletTransaction(type=Expiry) for each expired batch

---

### 7. Invitation/Referral Flow

1. Each user gets a unique 8-char invitation code (generated on registration)
2. User shares code/QR/link with others
3. New user registers with invitation code
4. On ZoneManager approval of invitee:
   - Invitee gets 50 reward points
   - Inviter gets 100 reward points (capped at 20 approved invitations)
5. Both get InvitationReward notifications

---

### 8. Notification System

**9 Types:** RegistrationApproved, RegistrationRejected, PointsEarned, RedemptionCreated, RedemptionApproved, RedemptionRejected, RedemptionCompleted, InvitationReward, AdminMessage

**Triggers:** Fired automatically from: ApprovalService, ScanService, RedemptionService, RedemptionApprovalService, InvitationService

**Admin Broadcast:** Send to single user, role, or all users.

**User API:** List (paginated), unread count, mark read, mark all read.

---

### 9. Admin Dashboard & Analytics

11 endpoints providing:
- **Dashboard:** KPI summary (user counts, points, SAR, barcodes, scans, pending)
- **User Analytics:** by type, status, region, monthly trend
- **Region Analytics:** nested regions -> cities with user counts
- **Points Analytics:** totals, by region, by representative, trend
- **Points Details:** paginated transaction list with filters
- **Top Performers:** top sellers + technicians by scans
- **Inactive Users:** users with no scans in N days
- **Barcode Analytics:** status breakdown, scan rate, top products
- **Redemption Analytics:** by status, method, avg processing time, trend
- **SalesMan Performance:** per-salesman metrics
- **Revenue Analytics:** SAR liability, held, paid out, volume by type, payout trend

---

### 10. Admin User Management

- CRUD for all 5 user types (SalesMan, ZoneManager, ShopOwner, Seller, Technician)
- Admin-created users start as Approved (skip approval flow)
- SalesMan: assigned to cities, reassignment moves users
- ZoneManager: assigned to region (1 per region)
- Toggle active/deactive any user
- Edit limited to name only for ShopOwner/Seller/Technician

---

## Part 2: Issues Found

### CRITICAL - Production Blockers

| # | Issue | File | Impact |
|---|-------|------|--------|
| C1 | **JWT signing key exposed in appsettings.json (tracked in git)** | appsettings.json | Anyone with repo access can forge tokens and impersonate any user |
| C2 | **Twilio credentials in appsettings.Development.json (tracked in git)** | appsettings.Development.json | Account hijack, cost fraud, unauthorized WhatsApp messages |

> **Note:** appsettings.Production.json and appsettings.Staging.json are NOT tracked in git (only exist locally). DB credentials are safe. Only `appsettings.json` (JWT key) and `appsettings.Development.json` (Twilio creds) are in source control.

### HIGH - Fix Before UAT

| # | Issue | Service | Impact |
|---|-------|---------|--------|
| H1 | NationalAddress null-forgiving operator (`!`) in analytics joins | AdminDashboardService | If any user lacks CityId, analytics endpoints crash with NullRef |
| H2 | TopPerformers region lookup doesn't handle missing region | AdminDashboardService | NullReferenceException if user has no region |
| H3 | InactiveUsers loads ALL eligible users into memory | AdminDashboardService | Memory exhaustion at scale |
| H4 | Edit ShopOwner/Seller/Technician missing transaction wrapper | AdminUserService | Race condition on concurrent edits |
| H5 | SalesMan city unassignment orphans users (AssignedSalesManId=null) | AdminUserService | Users stuck without approver |
| H6 | Concurrent refresh token requests can create duplicate tokens | AuthService | Multiple valid tokens, inconsistent state |
| H7 | OTP records never cleaned up (table grows unbounded) | OtpService | DB bloat, slower rate-limit queries over time |
| H8 | Wallet creation race condition (2 threads both see no wallet) | ScanService | DbUpdateException on concurrent first-scan |
| H9 | Invitation reward double-count in notification logic | InvitationService | Off-by-one on 20th invitation notification |
| H10 | ScanRecords loaded before transaction (stale data in concurrent scans) | ScanService | Two same-role users could scan same barcode |

### MEDIUM - Fix Post-UAT

| # | Issue | Service | Impact |
|---|-------|---------|--------|
| M1 | OTP consumed before mobile re-check — poor UX on race | AuthService | User wastes OTP if mobile taken between checks |
| M2 | Multiple active OTPs can accumulate per mobile | OtpService | Wastes Twilio credits |
| M3 | Refresh tokens stored plaintext in DB (not hashed) | TokenService | DB breach exposes all valid tokens |
| M4 | Multi-device token cleanup too aggressive | AuthService | Refreshing on Device A can kill Device B's token |
| M5 | File upload not rolled back on transaction failure | AdminUserService | Orphaned files in storage |
| M6 | No rate limiting on admin broadcast endpoint | NotificationService | Could create millions of notifications |
| M7 | Hardcoded invitation share URL | InvitationService | Wrong URL in non-production environments |
| M8 | BroadcastAsync loads ALL user IDs into memory | NotificationService | Memory issue at scale |
| M9 | No database indexes on notification queries | Infrastructure | Slow queries at scale |
| M10 | SalesMan performance query doesn't filter by transaction type before join | AdminDashboardService | Inefficient query |
| M11 | CancelScan balance check has TOCTOU race with concurrent redemption | AdminBarcodeService | Balance could go negative |
| M12 | No idempotency protection on scan endpoint | ScanService | Double-tap = double points |
| M13 | SHA256 OTP hash without salt (precomputable for 6-digit codes) | RedemptionApprovalService | Rainbow table if DB breached |
| M14 | GlobalExceptionHandler returns 500 for all exceptions | API | Loses useful HTTP status semantics |

### LOW - Backlog

| # | Issue | Service | Impact |
|---|-------|---------|--------|
| L1 | Unused `registrationData` parameter in OtpService.SendAsync | OtpService | Dead code |
| L2 | No exponential backoff on OTP brute force | OtpService | 5 attempts per code is fine, but no global throttle |
| L3 | No audit log for high-value transactions | All services | Limited forensic capability |
| L4 | Mobile number not normalized in admin user search | AdminUserService | Search might miss users |
| L5 | Lat/Long not validated in scan requests | ScanService | Invalid coordinates stored |
| L6 | No re-registration path for rejected users | AuthService | Known issue (M6 in memory) |

---

## Part 3: Production Readiness Assessment

### Blocking (Must Fix)

1. **C1-C2: Rotate secrets in git** — JWT key (appsettings.json) and Twilio creds (appsettings.Development.json) are tracked. Move to environment variables or secrets manager for production. DB creds are safe (not in git).

2. **H1-H2: Null reference guards in analytics** — Quick fix, prevents 500 errors when users have incomplete data.

### Strongly Recommended Before UAT

3. **H3: InactiveUsers memory issue** — Pagination should happen at DB level, not in-memory.

4. **H8: Wallet creation race** — Add unique constraint on (UserId) in Wallets table as safety net.

5. **H10: Concurrent scan guard** — Add unique constraint on (BarcodeId, ScannerRole) in ScanRecords table.

### Can Proceed to UAT With

- H4-H7, H9: Low probability in UAT with limited users, but should be fixed before production
- All MEDIUM and LOW items: acceptable technical debt for UAT phase

### What's Working Well

- Clean Architecture properly layered
- Result pattern for error handling (no exception-driven control flow)
- Transaction boundaries on critical financial operations
- Geographic authorization on approvals
- OTP security (hashed, brute-force protected, time-limited)
- FIFO point expiry protecting held balances
- Comprehensive notification system
- Localization support (Arabic/English)
- Soft-delete with audit trail
- CORS properly configured
- Middleware pipeline correctly ordered
- 108 tests covering domain + application layer
