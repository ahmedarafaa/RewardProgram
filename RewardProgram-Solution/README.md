# AL-Raed Reward Program API

A loyalty and reward platform for the AL-Raed distribution network. Built with .NET 10.0, Clean Architecture, and ASP.NET Identity.

## Business Overview

AL-Raed distributes products across Saudi Arabia through a network of shops. Each product carries a unique barcode label. When sellers and technicians scan these barcodes, they earn reward points that can be converted to SAR and redeemed as cash or bank transfers.

### User Roles

| Role | Description |
|------|-------------|
| **ShopOwner** | Owns one or more shops. Manages sellers. Does not scan or earn points. |
| **Seller** | Works at a shop. Scans product barcodes to earn points. Can redeem rewards. |
| **Technician** | Independent field worker. Scans barcodes and earns points. Can redeem rewards. |
| **SalesMan (SM)** | Assigned to cities. Approves/rejects registrations and redemption requests for users in their cities. |
| **ZoneManager (ZM)** | Manages a region. Second-level approver for registrations and redemptions. |
| **SystemAdmin** | Full platform access. Final approver for redemptions. Manages users, products, barcodes, and settings. |

### Registration Flow

1. User sends OTP via WhatsApp (Twilio Verify)
2. User submits registration with verified OTP
3. ShopOwner/Seller provide shop data (store name, VAT, CRN, shop image, short address)
4. First registrant for a CustomerCode creates the shop data; subsequent users reuse it
5. Registration goes to **PendingSalesMan** status
6. SalesMan approves/rejects -> if approved, moves to **PendingZoneManager**
7. ZoneManager approves/rejects -> if approved, user is **Approved** and can log in

### Barcode Scanning & Points

1. Admin generates barcode batches (NanoID codes, PDF labels for Zebra printers)
2. Each barcode is linked to a product with a defined point value
3. Seller scans a barcode -> earns points (barcode moves to **SellerScanned**)
4. Technician scans the same barcode -> earns points (barcode moves to **Consumed**)
5. A barcode can be scanned once per role (seller + technician)
6. Points are added to the user's wallet with SAR conversion at the current rate

### Redemption Flow

1. User requests redemption (cash or bank transfer) if they meet the minimum points threshold
2. Request goes through 3-level approval: **SalesMan -> ZoneManager -> SystemAdmin**
3. Cash redemptions require OTP verification at creation
4. On final approval, points are deducted and SAR balance updated
5. Any approver can reject; the user can cancel before completion

### Invitation System

- Each user gets a unique 8-character invitation code and QR code
- Inviter earns 100 points, invitee earns 50 points upon approval
- Inviter rewards are capped at 20 approved invitations

### Trader Map

- `GET /api/shops/map` returns shop locations based on Saudi Short Address
- Flutter app resolves Short Address to coordinates client-side via Google Maps / Saudi SPL

## Geographic Hierarchy

```
Region (e.g., Riyadh Region)
  -> has ONE ZoneManager
  -> City (e.g., Riyadh city)
       -> has ONE SalesMan (ApprovalSalesMan)
       -> Users register under a City
```

## Solution Architecture

4-layer Clean Architecture:

```
RewardProgram (API)             -> Controllers, DI, Middleware
RewardProgram.Application       -> Services, DTOs, Interfaces, Validators
RewardProgram.Domain            -> Entities, Enums, Constants
RewardProgram.Infrastructure    -> EF Core DbContext, Migrations, External Services
```

### Key Patterns

- **Result\<T\>** pattern for error handling with ProblemDetails responses
- **Soft-delete** via TrackableEntity (IsDeleted, DeletedAt, DeletedBy)
- **Audit trail** on all entities (CreatedBy/At, UpdatedBy/At)
- **OTP-based auth** for mobile users (WhatsApp via Twilio Verify)
- **JWT authentication** with refresh tokens (365-day lifetime, reuse detection)
- **FluentValidation** for request validation
- **Serilog** for structured logging

### External Services

| Service | Purpose |
|---------|---------|
| **Twilio Verify** | WhatsApp OTP for registration and login |
| **Firebase Cloud Messaging** | Push notifications to mobile devices |
| **ZXing.Net + QuestPDF** | Barcode generation (CODE_128) and PDF labels |

## API Endpoints

### Public API

| Area | Endpoints |
|------|-----------|
| **Auth** | send-otp, register (shop-owner/seller/technician), login, refresh |
| **Scan** | scan barcode, scan history |
| **Wallet** | balance, transactions |
| **Redemption** | create request, view active |
| **Approvals** | pending queue, list (search + tabs) |
| **Dashboard** | seller/technician dashboard, shop-owner dashboard |
| **Invitation** | get invitation code + QR |
| **Notifications** | list, unread count, mark read, preferences, device registration |
| **Profile** | get profile, update photo, delete account |
| **Shop** | trader map (filter by city) |
| **Lookup** | regions, cities |

### Admin API

| Area | Endpoints |
|------|-----------|
| **Auth** | admin login (username/password) |
| **Users** | CRUD for all 5 user types, list, toggle status |
| **Products** | CRUD, delete (blocked if has barcodes) |
| **Barcodes** | generate batch (PDF), list barcodes, list scans |
| **Reward Settings** | points-to-SAR rate, minimum redemption, invitation rewards |
| **Redemptions** | list, approve/reject |
| **Analytics** | 11 dashboard endpoints (users, regions, points, top performers, revenue, etc.) |
| **Notifications** | send to user/role/all, history |
| **Content** | about app, contact us |

### Dev Helpers

| Endpoint | Description |
|----------|-------------|
| `GET /api/dev/seeded-users` | List all users with filters (Dev/Staging only) |
| `GET /api/dev/users/{id}/cities` | SalesMan's assigned cities |
| `GET /api/dev/users/{id}/regions` | ZoneManager's managed regions |

## Data Model

### Core Entities

- **ErpCustomer** — imported from ERP (CustomerCode, CustomerName, ShortAddress)
- **ShopData** — shop details (StoreName, VAT, CRN, image, address), one-to-one with ErpCustomer
- **Product** — 1,133 products from ERP with point values
- **ProductBarcode** — NanoID codes with state machine (Available -> Scanned -> Consumed)
- **Wallet** — per-user balance (points + SAR)
- **WalletTransaction** — ledger entries with immutable SAR rate per transaction
- **RedemptionRequest** — 3-level approval workflow
- **Notification** — 9 types + admin broadcast
- **ScanRecord** — links barcode to scanner, one scan per role per barcode

### Seeded Data (Dev/Staging)

- 6 roles, 31 users
- 8 regions, 140 cities
- 3,235 ErpCustomers (from CSV)
- 1,133 products
- Demo analytics data (wallets, scans, transactions, redemptions)

## Environments

| Environment | Description |
|-------------|-------------|
| **Development** | Local, mock Twilio (OTP: 123456), auto-migration |
| **Staging** | SmarterASP.NET (InProcess), real Twilio, auto-migration |
| **UAT** | SmarterASP.NET (OutOfProcess), real Twilio, auto-migration, no demo data |
| **Production** | TBD |

## Getting Started

### Prerequisites

- .NET 10.0 SDK
- SQL Server (LocalDB or full instance)
- Twilio account (optional — mock mode available in Development)

### Run Locally

```bash
cd RewardProgram-Solution
dotnet restore
dotnet run --project RewardProgram
```

The API launches at `https://localhost:7001`. Swagger UI is available at `/swagger`.

Database is auto-created and seeded on first run in Development.

### Configuration

Key settings in `appsettings.json`:

```
Jwt:Key                     -> JWT signing key (required)
Twilio:UseMockMode          -> true for development (accepts OTP "123456")
Firebase:Enabled            -> false to skip FCM setup
ConnectionStrings:Default   -> SQL Server connection string
```

Environment-specific overrides: `appsettings.{Environment}.json` (gitignored for Staging/UAT/Production).

## Postman Collections

Three Postman collections are maintained in the repo root:

| File | Description |
|------|-------------|
| `RewardProgram.API.postman_collection.json` | Public API reference |
| `RewardProgram-Admin-API.postman_collection.json` | Admin API reference |
| `RewardProgram-E2E-Tests.postman_collection.json` | End-to-end flow tests (24 folders) |

## Tech Stack

- .NET 10.0, ASP.NET Core, EF Core 10.0.2
- ASP.NET Identity (custom ApplicationUser)
- SQL Server
- Twilio Verify (WhatsApp OTP)
- Firebase Cloud Messaging (push notifications)
- FluentValidation, Serilog, QuestPDF, ZXing.Net
- NanoID for barcode and invitation code generation
