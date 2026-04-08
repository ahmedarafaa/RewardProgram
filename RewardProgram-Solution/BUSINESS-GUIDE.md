# AL-Raed Reward Program — Business & Developer Guide

> A loyalty program where **sellers** and **technicians** scan product barcodes to earn points, convert them to SAR, and redeem via cash or bank transfer — all governed by a multi-level approval hierarchy.

---

## 1. User Roles

| Role | Who | Can Earn Points? | Key Responsibility |
|------|-----|:---:|-----|
| **ShopOwner** | Store owner linked to ERP customer | No | Provides shop data (VAT, CRN, image), views linked sellers |
| **Seller** | Sales rep at a shop | Yes | Scans barcodes, earns points, redeems rewards |
| **Technician** | Field technician | Yes | Scans barcodes, earns points, redeems rewards |
| **SalesMan** | Regional sales manager | No | Approves registrations & redemptions for assigned cities |
| **ZoneManager** | District/zone manager | No | Second-level approver for their region |
| **SystemAdmin** | Platform administrator | No | Full system control, final redemption approver |

> A user can hold **dual roles** (e.g., SalesMan + ZoneManager) and will see a combined approval queue.

---

## 2. Geographic Hierarchy & Assignment

```
Region (e.g., Riyadh)
 ├── ZoneManagerId  →  one ZM per region
 └── City (e.g., Al Kharj)
      └── ApprovalSalesManId  →  one SM per city
```

- Registration routing depends on the user's **city** — the assigned SalesMan gets the request first.
- If a city has no SalesMan, registration skips to ZoneManager.
- ZoneManager sees all pending requests from cities within their region.

---

## 3. Registration Flow

```
User submits OTP  →  Verifies OTP  →  Registers (with shop data if ShopOwner/Seller)
     │
     ▼
 PendingSalesman  ──SM approves──▶  PendingZoneManager  ──ZM approves──▶  Approved
                   ──SM rejects──▶  Rejected
```

**Key rules:**
- Auth is **OTP-only via WhatsApp** (no passwords for mobile users).
- **ShopOwner** always provides shop data and overwrites any existing data for that CustomerCode.
- **Seller** provides shop data only if no one has entered it yet (first-come-first-served).
- **Technician** has no shop linkage — just personal + address info.
- An optional **invitation code** can be provided at registration (rewards credited on approval).

---

## 4. ERP & Shop Data Model

```
ErpCustomer (from external ERP)
 ├── CustomerCode (unique, alternate key)
 ├── CustomerName
 └── ShopData (1:1 via CustomerCode)
      ├── StoreName, VAT, CRN, ShopImageUrl
      ├── ShortAddress (unique, 4 letters + 4 digits)
      └── CityId, Street, District, PostalCode, BuildingNumber, SubNumber

ShopOwnerProfile ──FK──▶ ErpCustomer.CustomerCode
SellerProfile    ──FK──▶ ErpCustomer.CustomerCode
```

- One ShopOwner per CustomerCode (enforced).
- Multiple Sellers can share a CustomerCode.
- VAT, CRN, and ShortAddress are each globally unique.

---

## 5. Product Scanning & Point Earning

### Barcode Lifecycle

```
Available  ──first scan──▶  SellerScanned / TechnicianScanned  ──second scan──▶  Consumed
```

Each barcode allows **one scan per role** (composite unique on BarcodeId + ScannerRole).

### Point Distribution

| Scenario | Scanner Gets | Other Role Gets Later |
|----------|:-----------:|:--------------------:|
| Seller scans first (Available) | 50% of product points | Technician gets 100% on second scan, Seller gets remaining 50% |
| Technician scans first (Available) | 100% of product points | Seller gets 100% on second scan |

- Points are **immediately credited** to the scanner's wallet.
- Each earned transaction stores its own **SAR rate** (immutable — rate changes don't affect past earnings).
- **Concurrency**: RowVersion on Wallet and ProductBarcode with retry logic (up to 2 retries).

---

## 6. Wallet & SAR Conversion

```
Wallet (one per user, unique UserId)
 ├── Balance        (available points)
 ├── SarBalance     (available SAR equivalent)
 ├── HeldBalance    (locked during pending redemption)
 └── HeldSarBalance (locked SAR during pending redemption)
```

- **SAR rate** is admin-configurable (default: 10 points = 1 SAR).
- Each `WalletTransaction` stores: Amount, SarRate, SarAmount, RemainingAmount (for FIFO expiry).
- Transaction types: `Earned`, `Redeemed`, `Cancelled`, `Expired`, `Refunded`, `InvitationReward`.
- **Points expire after 15 months** (FIFO — oldest first, only non-held points). Background service runs this.

---

## 7. Redemption Flow

### Creating a Request

- Only **Seller** and **Technician** can redeem.
- Minimum points required (admin-configurable, default: 1000).
- SAR amount must be a whole number.
- Points are **held** (locked) in wallet immediately.
- Only one active redemption per user at a time.

### 3-Level Approval Chain

```
PendingSalesMan ──SM──▶ PendingZoneManager ──ZM──▶ PendingAdmin ──Admin──▶ AdminApproved
                                                                              │
                                             ┌────────────────────────────────┤
                                             ▼                                ▼
                                        Bank Transfer                       Cash
                                      (auto-completes)              (OTP required)
```

**Rejection** can happen at any level — held points are immediately refunded.

### Cash Redemption OTP

On admin approval of a **Cash** request:
1. System generates a 6-digit OTP, hashes it (SHA256), sends via WhatsApp.
2. OTP valid for **14 days**, max **5 verification attempts**.
3. The person handing over cash calls `confirm-cash` with the OTP.
4. On success: points deducted (FIFO), status = Completed.
5. On expiry or max attempts: auto-cancelled, points refunded.

**Bank Transfer** skips OTP — completes immediately after admin approval.

---

## 8. Invitations & Referrals

- Each user gets a unique **8-character invitation code** (generated on first request).
- New users can enter an invitation code during registration.
- On the invitee's **final approval** (ZM approves):
  - Invitee receives reward points (default: 50).
  - Inviter receives reward points (default: 100).
- **Cap**: Inviter can earn from max **20 approved invitations**.
- QR code generated from `https://app.raedrewardapp.com/invite/{code}`.

---

## 9. Notifications

**9 notification types**, triggered automatically by business events:

| Type | Trigger |
|------|---------|
| RegistrationApproved | ZM approves registration |
| RegistrationRejected | SM or ZM rejects registration |
| PointsEarned | Barcode scanned successfully |
| RedemptionCreated | User submits redemption request |
| RedemptionApproved | Advances through approval chain |
| RedemptionRejected | Rejected at any level |
| RedemptionCompleted | Cash confirmed or bank transfer done |
| InvitationReward | Points credited from referral |
| AdminMessage | Admin sends targeted or broadcast message |

- **FCM push notifications** (Firebase) with per-type mute preferences.
- Admin can send to: specific user, all users in a role, or broadcast to everyone.
- Expired FCM tokens are auto-cleared.

---

## 10. Admin Capabilities

| Area | What Admin Can Do |
|------|-------------------|
| **Users** | CRUD all 5 user types, toggle enable/disable, view deleted accounts |
| **Products** | CRUD products (delete blocked if barcodes exist) |
| **Barcodes** | Generate (returns PDF), list, view scans, cancel/reverse scans |
| **Reward Settings** | Configure SAR rate, invitation rewards, minimum redemption |
| **Redemptions** | View all requests with full approval chain history |
| **Content** | Manage "About App" and "Contact Us" pages |
| **Notifications** | Send to user/role/all, view notification history |
| **Analytics** | 13 dashboard endpoints (users, regions, points, barcodes, revenue, etc.) |

---

## 11. Architecture Overview

```
┌─────────────────────────────────────────────────────┐
│  RewardProgram (API Layer)                          │
│  Controllers, Middleware, DI, Program.cs             │
├─────────────────────────────────────────────────────┤
│  RewardProgram.Application                          │
│  Services, Interfaces, DTOs, Validators, Errors      │
├─────────────────────────────────────────────────────┤
│  RewardProgram.Domain                               │
│  Entities, Enums, Value Objects                      │
├─────────────────────────────────────────────────────┤
│  RewardProgram.Infrastructure                       │
│  EF Core DbContext, Configurations, Repositories,    │
│  Twilio, Firebase, File Storage, Background Services │
└─────────────────────────────────────────────────────┘
```

- **Clean Architecture** — dependencies point inward (API → Application → Domain; Infrastructure → Application).
- **Result\<T\> pattern** — no exceptions for business errors, explicit error paths.
- **Soft-delete** via `TrackableEntity` (IsDeleted flag + query filters).
- **Audit trail** — CreatedBy/At, UpdatedBy/At on all trackable entities.

---

## 12. Auth & Security

| Mechanism | Details |
|-----------|---------|
| **Mobile users** | OTP via WhatsApp (Twilio Verify), no passwords |
| **Admin** | Username + password (`POST /api/admin/auth/login`) |
| **Tokens** | JWT access (60 min) + refresh token (30 days) |
| **OTP rate limiting** | Max 5 per 24h window, 30s cooldown between resends |
| **OTP verification** | Max 5 attempts per OTP, 3-minute expiry |
| **Cash OTP** | SHA256 hashed, 14-day expiry, 5-attempt brute-force limit |
| **Concurrency** | RowVersion on Wallet and ProductBarcode |
| **Twilio mock mode** | ON in Dev/Staging/UAT (accepts "123456"), OFF in Production |

---

## 13. Environments

| Environment | Auto-Migrate | Seeder | Twilio | Swagger |
|-------------|:---:|:---:|:---:|:---:|
| Development | Yes | Full + demo data | Mock | Yes |
| Staging | Yes | Full + demo data | Mock | Yes |
| UAT | Yes | Full (no demo data) | Mock | Yes |
| Production | No | Manual | Live | No |

---

## 14. Background Services

| Service | Schedule | Purpose |
|---------|----------|---------|
| `OtpCleanupBackgroundService` | Every 24h | Deletes OTP records older than 30 days |
| `PointsExpiryBackgroundService` | Periodic | Expires earned points older than 15 months (FIFO) |

---

## 15. Key Database Constraints

| Constraint | Enforcement |
|------------|-------------|
| One wallet per user | Unique index on Wallet.UserId |
| One scan per role per barcode | Composite unique (BarcodeId, ScannerRole) |
| One ShopOwner per CustomerCode | Unique index on ShopOwnerProfile.UserId |
| One preference per user per type | Composite PK (UserId, NotificationType) |
| One ZM per region | Unique filtered index on Region.ZoneManagerId |
| SAR immutability | SarRate stored per WalletTransaction, never recalculated |
| Soft-delete + unique reuse | Filtered unique indexes (`WHERE IsDeleted = 0`) |

---

## 16. API Structure

**Public API** — 13 controllers, ~50 endpoints:
- Auth, Lookup, Content, Dashboard, Scan, Wallet, Redemption, Approvals, Invitations, Notifications, Profile

**Admin API** — 10 controllers, ~55 endpoints:
- Auth, Users, Products, Barcodes, Scans, Reward Settings, Redemptions, Content, Notifications, Analytics

**Swagger**: Split into two docs (`/swagger/public` and `/swagger/admin`) via namespace routing.

---

## 17. Quick Reference — What Goes Where

| "I need to..." | Look in... |
|-----------------|-----------|
| Change a business rule | `RewardProgram.Application/Services/` |
| Add a new entity | `RewardProgram.Domain/Entities/` + Infrastructure config |
| Modify DB schema | `RewardProgram.Infrastructure/Persistance/EntitiesConfigurations/` |
| Add an endpoint | `RewardProgram/Controllers/` (or `Controllers/Admin/`) |
| Change validation | `RewardProgram.Application/Contracts/.../Validators/` |
| Update seed data | `RewardProgram.Infrastructure/Persistance/Data/DataSeeder.cs` |
| Modify error messages | `RewardProgram.Application/Errors/` |
| Change auth/token logic | `RewardProgram.Application/Services/Auth/` |
| Configure DI/middleware | `RewardProgram/DependencyInjection.cs` + `Program.cs` |

---

*Last updated: April 2026*
