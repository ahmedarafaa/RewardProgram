# AL-Raed Reward Program — QA Test Guide

**Version:** 3.1
**Author:** Mahmoud Ibrahim Zahran — Backend Engineer
**Last Updated:** 2026-03-03
**API Status:** Ready for QA Testing
**Build Status:** 0 errors, 0 warnings

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Environment Setup](#2-environment-setup)
3. [Seeded Test Data](#3-seeded-test-data)
4. [API Endpoints — Public](#4-api-endpoints--public)
5. [API Endpoints — Admin](#5-api-endpoints--admin)
6. [Test Flows (Step-by-Step)](#6-test-flows-step-by-step)
7. [Validation Rules](#7-validation-rules)
8. [Error Codes Reference](#8-error-codes-reference)
9. [Role-Based Access Matrix](#9-role-based-access-matrix)
10. [Test Checklist](#10-test-checklist)
11. [Frontend Integration Notes](#11-frontend-integration-notes)
12. [Known Limitations](#12-known-limitations)
13. [Quick Reference Card](#quick-reference-card)

---

<details id="1-system-overview">
<summary><strong>1. System Overview</strong></summary>

## 1. System Overview

### What Is This System?

AL-Raed Reward Program is an onboarding platform for AL-Raed (a Saudi FMCG/retail distribution company). It digitizes the registration of retail partners (shop owners, sellers, technicians) into a reward program, with a two-tier approval workflow.

### The 6 Roles

| Role | Who They Are | How They Get In |
|------|-------------|-----------------|
| **SystemAdmin** | IT/operations admin | Pre-seeded — manages users via Admin API |
| **ZoneManager** | Regional manager | Created by SystemAdmin or pre-seeded |
| **SalesMan** | Distribution rep assigned to cities | Created by SystemAdmin or pre-seeded |
| **ShopOwner** | Retail store owner (VAT, CRN, shop) | Self-registers via Public API |
| **Seller** | Employee working inside a shop | Self-registers with CustomerCode |
| **Technician** | Field service technician | Self-registers via Public API |

### Geographic Hierarchy

```
Region (top level, managed by 1 ZoneManager)
  └── City (has 1 ApprovalSalesMan)
```

- Registration uses **City only** — Region is auto-derived from `City.RegionId`
- District is a free-text field (not a lookup)

### ERP / ShopData Model

```
ErpCustomer (external ERP source — 3,235 seeded)
  └── CustomerCode (unique) + CustomerName

ShopData (one-to-one with ErpCustomer via CustomerCode)
  └── StoreName, VAT, CRN, ShortAddress, District, ShopImageUrl,
      CityId, Street, BuildingNumber, PostalCode, SubNumber, EnteredByUserId

ShopOwnerProfile ──► CustomerCode ──► ErpCustomer
SellerProfile ──────► CustomerCode ──► ErpCustomer
```

**Key rules:**
- Both ShopOwner and Seller register with a **CustomerCode** (must exist in ErpCustomers)
- **ShopOwner ALWAYS provides shop data** — all shop fields are mandatory every time
- If a Seller already created ShopData, the ShopOwner's data **overwrites** it
- **Seller** uses first-come-first-served: first Seller for a CustomerCode provides shop data, subsequent Sellers skip it

### Two-Tier Approval Workflow

```
User Registers
    │
    ▼
PendingSalesman ──► SalesMan Reviews
                        │
                  ┌─────┴─────┐
               Approve      Reject
                  │           │
                  ▼           ▼
         PendingZoneManager  Rejected (final)
                  │
            ZoneManager Reviews
                  │
            ┌─────┴─────┐
         Approve      Reject
            │           │
            ▼           ▼
        Approved      Rejected (final)
        (can login)   (cannot login)
```

> Admin-created users skip this workflow — they are **pre-approved** immediately.

### Authentication

- **No passwords** — purely OTP-based via **SMS** (Twilio Verify)
- **OTP-first registration**: Send OTP → then register with `pinId` + `otp` in the same request
- JWT access tokens (60 min) + refresh tokens (7 days)

</details>

---

<details id="2-environment-setup">
<summary><strong>2. Environment Setup</strong></summary>

## 2. Environment Setup

### 2.1 URLs

| Environment | Base URL | Swagger |
|-------------|----------|---------|
| **Staging** | `http://staging.raedrewardapp.com` | `/swagger` |
| Dev — IIS Express | `https://localhost:44315` | `/swagger` |
| Dev — Kestrel (http) | `http://localhost:5243` | `/swagger` |
| Dev — Kestrel (https) | `https://localhost:7100` | `/swagger` |

### 2.2 Swagger UI

Swagger has two docs:
- **Public API** — registration, login, approvals, lookups
- **Admin API** — user management (CRUD)

To authenticate in Swagger:
1. Click the **Authorize** button (lock icon, top right)
2. Enter: `Bearer {your-jwt-token}` (include the word "Bearer")
3. Click Authorize

### 2.3 Postman Collections

Three pre-built Postman collections are in the solution root:

| File | Purpose |
|------|---------|
| `RewardProgram.postman_collection.json` | Public API — localhost (dev) |
| `RewardProgram.API.postman_collection.json` | Public API — staging |
| `RewardProgram-Admin-API.postman_collection.json` | Admin API — staging |

**Features:**
- Auto-captures `pinId`, `token`, `refreshToken` from responses
- Bearer auth pre-configured on protected endpoints
- Sample Arabic data

**Import:** Open Postman → Import → select the JSON file.

> **Note (Dev only):** Disable SSL certificate verification: Settings → General → SSL → OFF.

### 2.4 Twilio Mock Mode

In **Development** and **Staging**, Twilio runs in mock mode:
- OTPs are **not actually sent** via SMS
- **Any 6-digit OTP code works** (e.g., `123456`)

### 2.5 First-Time Setup (Dev)

On first run:
1. Database auto-migrates
2. DataSeeder runs (6 roles, 31 users, 8 regions, 140 cities, 3,235 ErpCustomers)
3. Swagger available at `/swagger`

</details>

---

<details id="3-seeded-test-data">
<summary><strong>3. Seeded Test Data</strong></summary>

## 3. Seeded Test Data

### 3.1 Users (31 total)

#### SystemAdmin (1)

| Name | Mobile |
|------|--------|
| مدير النظام | `0500000001` |

#### ZoneManagers (5)

| Name | Mobile | Region |
|------|--------|--------|
| فرحان ممدوح | `0500000002` | الرياض |
| الطيب حسين | `0500000003` | المدينة المنورة |
| محمد العجوز | `0500000004` | جازان |
| نيازي عمر | `0500000005` | المنطقة الجنوبية |
| محمد اسماعيل | `0500000006` | تبوك و الشمال |

#### Dual-Role: ZoneManager + SalesMan (3)

| Name | Mobile | ZM Region |
|------|--------|-----------|
| نعيم عوض | `0500000007` | المنطقة الغربية |
| سيد بخيت | `0500000008` | الشرقية |
| وليد السكري | `0500000009` | القصيم |

#### SalesMen (22)

| Name | Mobile |
|------|--------|
| محمود حجازي | `0500000010` |
| احمد سمير | `0500000011` |
| احمد جمال | `0500000012` |
| ابراهيم الشعراوي | `0500000013` |
| محمد المشير | `0500000014` |
| محمد اياد | `0500000015` |
| يوسف جابر | `0500000016` |
| احمد الجن | `0500000017` |
| احمد القليوبي | `0500000018` |
| اشرف وائل | `0500000019` |
| محمد فؤاد | `0500000020` |
| اسامة عبد العليم | `0500000021` |
| محمد مدبولي | `0500000022` |
| نبيل صلاح | `0500000023` |
| محمد مراد | `0500000024` |
| معتز مكرم | `0500000025` |
| هيثم محمد | `0500000026` |
| محمد الصاوي | `0500000027` |
| عمرو عادل | `0500000028` |
| ايهاب صلاح | `0500000029` |
| حسام حسن | `0500000030` |
| محمد ناصر | `0500000031` |

> All seeded users are **pre-approved**. Login with any mobile above.

### 3.2 Regions (8)

| Arabic | English | ZoneManager |
|--------|---------|-------------|
| الرياض | Riyadh | فرحان ممدوح |
| المنطقة الغربية | Western | نعيم عوض |
| المدينة المنورة | Madinah | الطيب حسين |
| الشرقية | Eastern | سيد بخيت |
| جازان | Jazan | محمد العجوز |
| المنطقة الجنوبية | Southern | نيازي عمر |
| تبوك و الشمال | Tabuk & Northern | محمد اسماعيل |
| القصيم | Qassim | وليد السكري |

### 3.3 Cities (140)

Use lookups to get IDs:
- `GET /api/lookup/cities` — all 140 cities
- `GET /api/lookup/regions/{regionId}/cities` — cities by region

### 3.4 SalesMan → City Assignments

To test registration + approvals, you need to know which SalesMan covers which city:

| SalesMan | Mobile | Assigned Cities (examples) |
|----------|--------|---------------------------|
| محمود حجازي | `0500000010` | الرياض, المزاحمية |
| احمد سمير | `0500000011` | الخرج, الأفلاج, حفر الباطن |
| احمد جمال | `0500000012` | القويعية, عفيف, وادي الدواسر |
| محمد المشير | `0500000014` | مكة, الطائف, الجموم |
| محمد اياد | `0500000015` | جدة, بحرة |
| سيد بخيت (dual) | `0500000008` | الدمام |

> Use lookups to get city GUIDs, then register a user in one of these cities. Login with the matching SalesMan to see the pending request.

### 3.5 ErpCustomers (3,235)

Seeded from embedded CSV. Each has a unique `CustomerCode` + `CustomerName`.

**Sample codes for testing:**

| CustomerCode | CustomerName |
|--------------|-------------|
| `AB-0001` | نقدي - خميس مشيط |
| `AB-0002` | مؤسسة صالح فيصل سعيد القطاني |
| `AB-0003` | مؤسسة رواسي التقدم التجارية |
| `AB-0004` | مؤسسة / روافد الانارة |
| `AB-0005` | مؤسسة شريفه ناصر علي المازني |
| `AB-0010` | مؤسسة محلات ابها التجارية |
| `AB-0015` | مؤسسة بن ظفير التجارية |
| `AB-0020` | مؤسسة إعمار النهضة للتجارة |

> Use any of these codes when registering ShopOwner or Seller. The code must exist in the system.

</details>

---

<details id="4-api-endpoints--public">
<summary><strong>4. API Endpoints — Public</strong></summary>

## 4. API Endpoints — Public

### 4.1 Lookup Endpoints (No Auth)

#### `GET /api/lookup/regions`

Returns all active regions.

```json
// Response 200
[
  { "id": "guid", "nameAr": "الرياض", "nameEn": "Riyadh" }
]
```

---

#### `GET /api/lookup/regions/{regionId}/cities`

Returns cities in a region.

```json
// Response 200
[
  { "id": "guid", "nameAr": "الرياض", "nameEn": "Riyadh", "regionId": "guid" }
]
```

Error: `404` if regionId not found.

---

#### `GET /api/lookup/cities`

Returns all 140 active cities.

---

#### `GET /api/lookup/customer/{customerCode}/shop-data-status`

Checks if CustomerCode exists in ERP and whether ShopData is already created.

```json
// Response 200
{
  "customerCodeExists": true,
  "customerName": "اسم العميل",
  "shopDataExists": false
}
```

| Field | Meaning |
|-------|---------|
| `customerCodeExists` | Always `true` on 200 |
| `customerName` | Name from ERP system |
| `shopDataExists` | `true` → Seller can skip shop fields. `false` → Seller must provide all shop fields |

Error: `404` if code not in ErpCustomers.

**Frontend usage (Seller only):** Call this after user enters CustomerCode. If `shopDataExists: true`, hide StoreName/VAT/CRN/ShortAddress/ShopImage/NationalAddress fields. **ShopOwner always shows all shop fields regardless** — do NOT use this endpoint for ShopOwner registration.

---

### 4.2 OTP Endpoints (No Auth)

#### `POST /api/auth/send-otp`

**First step in registration.** Sends OTP via SMS after checking mobile uniqueness.

```json
// Request — application/json
{ "mobileNumber": "0555000001" }

// Response 200
{ "pinId": "VE...", "maskedMobileNumber": "0555****01" }
```

**Errors:**
- `409` — mobile already registered

> Save the `pinId` — you need it for the register endpoint.

---

#### `POST /api/auth/resend-otp`

Resends OTP to a mobile (for both registration and login).

```json
// Request — application/json
{ "mobileNumber": "0555000001" }

// Response 200
{ "pinId": "VE...", "maskedMobileNumber": "0555****01" }
```

**Rate limits:**
- 30-second cooldown between resends
- Max 3 OTP requests per mobile in 15 minutes

**Errors:** `429` if too soon or too many requests.

---

### 4.3 Registration Endpoints (No Auth)

> **Flow:** `send-otp` → get `pinId` → register with `pinId` + `otp` (OTP verified inline, no separate verify step)

#### `POST /api/auth/register/shop-owner`

**Content-Type:** `multipart/form-data`

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| PinId | string | **Yes** | From send-otp response |
| Otp | string | **Yes** | 6-digit OTP |
| CustomerCode | string | **Yes** | Must exist in ErpCustomers |
| OwnerName | string | **Yes** | 2–100 chars |
| MobileNumber | string | **Yes** | `05XXXXXXXX` (10 digits) |
| CityId | string | **Yes** | Must exist, must have SalesMan |
| StoreName | string | **Yes** | 2–150 chars |
| VAT | string | **Yes** | 15 digits, starts & ends with 3 |
| CRN | string | **Yes** | 10 digits |
| ShortAddress | string | **Yes** | 8-char alphanumeric, unique |
| ShopImage | file | **Yes** | JPG/PNG, max 5 MB |
| NationalAddress.Street | string | **Yes** | 1–100 chars |
| NationalAddress.BuildingNumber | int | **Yes** | > 0 |
| NationalAddress.PostalCode | string | **Yes** | 5 digits |
| NationalAddress.SubNumber | int | **Yes** | > 0 |
| NationalAddress.District | string | No | Free-text district name |

> **ShopOwner ALWAYS provides all shop data fields.** If ShopData was previously created by a Seller, the ShopOwner's data overwrites it.

```json
// Response 200
{
  "userId": "guid",
  "message": "تم تسجيل طلبك بنجاح، سيتم مراجعته وإشعارك فور اكتمال التحقق"
}
```

**Errors:** `400` validation/city, `409` duplicate mobile/VAT/CRN

---

#### `POST /api/auth/register/seller`

**Content-Type:** `multipart/form-data`

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| PinId | string | **Yes** | From send-otp |
| Otp | string | **Yes** | 6-digit OTP |
| Name | string | **Yes** | 2–100 chars |
| MobileNumber | string | **Yes** | `05XXXXXXXX` |
| CustomerCode | string | **Yes** | Must exist in ErpCustomers |
| StoreName | string | If no ShopData | |
| VAT | string | If no ShopData | |
| CRN | string | If no ShopData | |
| ShortAddress | string | If no ShopData | |
| ShopImage | file | If no ShopData | |
| CityId | string | If no ShopData | |
| NationalAddress.* | | If no ShopData | Same as ShopOwner |

> If ShopData already exists for the CustomerCode, only `PinId`, `Otp`, `Name`, `MobileNumber`, `CustomerCode` are needed. City and address are inherited.

**Response:** Same as ShopOwner.

---

#### `POST /api/auth/register/technician`

**Content-Type:** `application/json`

```json
{
  "pinId": "VE...",
  "otp": "123456",
  "name": "فني تجريبي",
  "mobileNumber": "0555000003",
  "cityId": "guid",
  "postalCode": "12345",
  "district": "حي السلام"
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| PinId | string | **Yes** | From send-otp |
| Otp | string | **Yes** | 6-digit OTP |
| Name | string | **Yes** | 2–100 chars |
| MobileNumber | string | **Yes** | `05XXXXXXXX` |
| CityId | string | **Yes** | Must exist, must have SalesMan |
| PostalCode | string | **Yes** | 5 digits |
| District | string | **Yes** | Free-text district name |

**Response:** Same as ShopOwner.

---

### 4.4 Login Endpoints (No Auth)

#### `POST /api/auth/login`

Sends OTP via SMS for login.

```json
// Request
{ "mobileNumber": "0500000010" }

// Response 200
{ "pinId": "VE...", "maskedMobileNumber": "0500****10" }
```

**Errors:** `404` user not found, `403` rejected/not approved/disabled

---

#### `POST /api/auth/login/verify`

Verifies OTP and returns JWT tokens.

```json
// Request
{ "pinId": "VE...", "otp": "123456" }

// Response 200
{
  "token": "eyJ...",
  "refreshToken": "random-64-byte-string",
  "expiresIn": 3600,
  "refreshTokenExpiration": "2026-03-10T...",
  "user": {
    "id": "guid",
    "name": "محمود حجازي",
    "mobileNumber": "0500000010",
    "userType": "SalesMan",
    "registrationStatus": "Approved"
  }
}
```

---

### 4.5 Token Management (Auth Required)

#### `POST /api/auth/refresh-token`

```json
// Request — Authorization: Bearer {token}
{ "refreshToken": "..." }

// Response 200 — same as login/verify
```

Old refresh token is automatically revoked.

---

#### `POST /api/auth/revoke-token`

Logout — revokes the refresh token.

```json
// Request — Authorization: Bearer {token}
{ "refreshToken": "..." }

// Response 200
{ "message": "تم تسجيل الخروج بنجاح" }
```

---

### 4.6 Approval Endpoints (Auth — SalesMan/ZoneManager Only)

#### `GET /api/approvals/pending?page=1&pageSize=20`

| Approver | Sees Users With Status | Filter |
|----------|----------------------|--------|
| SalesMan | PendingSalesman | Their assigned cities |
| ZoneManager | PendingZoneManager | Their region |
| Dual (SM+ZM) | Both | Combined queue |

```json
// Response 200
{
  "items": [
    {
      "id": "guid",
      "name": "محمد أحمد",
      "mobileNumber": "0512345678",
      "userType": "ShopOwner",
      "registrationStatus": "PendingSalesman",
      "registeredAt": "2026-03-03T10:30:00Z",
      "customerCode": "10001",
      "customerName": "اسم العميل",
      "storeName": "متجر الجودة",
      "vat": "300000000000003",
      "crn": "1234567890",
      "shopImageUrl": "/uploads/shops/image.jpg",
      "regionName": "الرياض",
      "cityName": "الرياض",
      "street": "شارع الملك فهد",
      "buildingNumber": 1234,
      "postalCode": "12345",
      "subNumber": 1,
      "assignedSalesManName": "محمود حجازي"
    }
  ],
  "totalCount": 5,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

---

#### `POST /api/approvals/approve`

```json
// Request
{ "userId": "guid" }

// Response 200
{ "message": "تمت الموافقة بنجاح" }
```

| Current Status | Approver | Result |
|---------------|----------|--------|
| PendingSalesman | Assigned SalesMan | → PendingZoneManager |
| PendingZoneManager | Region's ZoneManager | → Approved |

---

#### `POST /api/approvals/reject`

```json
// Request
{ "userId": "guid", "reason": "المستندات غير مكتملة" }

// Response 200
{ "message": "تم الرفض بنجاح" }
```

Reason is required (1–500 chars). Result is always `Rejected` (final).

</details>

---

<details id="5-api-endpoints--admin">
<summary><strong>5. API Endpoints — Admin</strong></summary>

## 5. API Endpoints — Admin

> All admin endpoints require **SystemAdmin JWT** (`Authorization: Bearer {token}`).

### 5.1 Add User

#### `POST /api/admin/users/salesman`

```json
// application/json
{
  "name": "أحمد المندوب",
  "mobileNumber": "0512345678",
  "cityIds": ["city-guid-1", "city-guid-2"]
}
```

Creates SalesMan + assigns as ApprovalSalesMan for the given cities. User is pre-approved.

```json
// Response 200
{
  "id": "guid",
  "name": "أحمد المندوب",
  "mobileNumber": "0512345678",
  "userType": "SalesMan",
  "message": "تم إنشاء حساب المندوب بنجاح"
}
```

> All Add/Edit user endpoints return the same response shape.

---

#### `POST /api/admin/users/zone-manager`

```json
// application/json
{
  "name": "خالد المدير",
  "mobileNumber": "0512345679",
  "regionId": "region-guid"
}
```

Creates ZoneManager + assigns to region. User is pre-approved.

---

#### `POST /api/admin/users/shop-owner`

**Content-Type:** `multipart/form-data`

| Field | Type | Required |
|-------|------|----------|
| ownerName | string | Yes |
| mobileNumber | string | Yes |
| customerCode | string | Yes |
| cityId | string | Yes |
| storeName | string | Yes |
| vat | string | Yes |
| crn | string | Yes |
| shortAddress | string | Yes |
| shopImage | file | Yes |
| nationalAddress.buildingNumber | int | Yes |
| nationalAddress.street | string | Yes |
| nationalAddress.postalCode | string | Yes |
| nationalAddress.subNumber | int | Yes |
| nationalAddress.district | string | No |

Creates ShopOwner + ShopData (creates or overwrites existing). User is pre-approved.

---

#### `POST /api/admin/users/seller`

**Content-Type:** `multipart/form-data`

Same fields as ShopOwner but with `name` instead of `ownerName`.

---

#### `POST /api/admin/users/technician`

```json
// application/json
{
  "name": "فهد الفني",
  "mobileNumber": "0512345682",
  "cityId": "city-guid",
  "postalCode": "12345",
  "district": "حي السلام"
}
```

Creates Technician. User is pre-approved.

---

### 5.2 List & Status

#### `GET /api/admin/users`

Paginated list with optional filters:

| Param | Type | Description |
|-------|------|-------------|
| search | string | Search by name or mobile |
| userType | int | `1`=ShopOwner, `2`=Seller, `3`=Technician, `4`=SalesMan, `5`=ZoneManager |
| registrationStatus | int | `1`=PendingSalesman, `2`=PendingZoneManager, `3`=Approved, `4`=Rejected |
| regionId | guid | Filter by region |
| isDisabled | bool | `true`=disabled only, `false`=active only |
| page | int | Default: 1 |
| pageSize | int | Default: 20, max: 50 |

```json
// Response 200
{
  "items": [
    {
      "id": "guid",
      "name": "محمود حجازي",
      "mobileNumber": "0500000010",
      "userType": "SalesMan",
      "registrationStatus": "Approved",
      "isDisabled": false,
      "createdAt": "2026-03-03T...",
      "regionName": "الرياض",
      "cityName": null,
      "customerCode": null,
      "storeName": null
    }
  ],
  "totalCount": 31,
  "page": 1,
  "pageSize": 20,
  "totalPages": 2,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

---

#### `PATCH /api/admin/users/{id}/toggle-status`

Flips user between active and disabled. No request body.

```json
// Response 200
{
  "userId": "guid",
  "isDisabled": true,
  "message": "..."
}
```

---

### 5.3 Edit User

#### `PUT /api/admin/users/salesman/{id}`

```json
{
  "name": "أحمد المندوب - معدل",
  "mobileNumber": "0512345678",
  "cityIds": ["city-guid-1", "city-guid-3"]
}
```

---

#### `PUT /api/admin/users/zone-manager/{id}`

```json
{
  "name": "خالد المدير - معدل",
  "mobileNumber": "0512345679",
  "regionId": "region-guid-2"
}
```

---

#### `PUT /api/admin/users/shop-owner/{id}`

**Content-Type:** `multipart/form-data` — same fields as Add ShopOwner.

---

#### `PUT /api/admin/users/seller/{id}`

**Content-Type:** `multipart/form-data` — same fields as Add Seller.

---

#### `PUT /api/admin/users/technician/{id}`

```json
{
  "name": "فهد الفني - معدل",
  "mobileNumber": "0512345682",
  "cityId": "city-guid-2",
  "postalCode": "54321",
  "district": "حي الملك فهد"
}
```

</details>

---

<details id="6-test-flows-step-by-step">
<summary><strong>6. Test Flows (Step-by-Step)</strong></summary>

## 6. Test Flows (Step-by-Step)

### Flow 1: ShopOwner Registration → Approval → Login (Full E2E)

**Step 1 — Get a CityId**
```
GET /api/lookup/regions                          → pick a region
GET /api/lookup/regions/{regionId}/cities         → pick a city, note cityId
```

**Step 2 — Send OTP**
```
POST /api/auth/send-otp
{ "mobileNumber": "0555111222" }
```
Save `pinId` from response.

**Step 3 — Register ShopOwner (all shop fields always required)**
```
POST /api/auth/register/shop-owner (multipart/form-data)

PinId: {pinId from step 2}
Otp: 123456
CustomerCode: AB-0001
OwnerName: صاحب المتجر
MobileNumber: 0555111222
CityId: {cityId from step 1 lookups}
StoreName: متجر اختبار
VAT: 300000000100003
CRN: 1234500001
ShortAddress: TEST0001
ShopImage: {upload .jpg or .png, under 5MB}
NationalAddress.Street: شارع التحلية
NationalAddress.BuildingNumber: 100
NationalAddress.PostalCode: 12345
NationalAddress.SubNumber: 1
NationalAddress.District: حي النزهة
```
Expected: `200` with `userId`. Save it.

**Step 4 — Login as SalesMan covering that city**

Check seeded data for which SalesMan covers your city (e.g., Riyadh → `0500000010`).
```
POST /api/auth/login         → { "mobileNumber": "0500000010" }
POST /api/auth/login/verify  → { "pinId": "...", "otp": "123456" }
```
Save `token`.

**Step 5 — View pending queue**
```
GET /api/approvals/pending?page=1&pageSize=20
Authorization: Bearer {salesman-token}
```
The ShopOwner from Step 3 should appear.

**Step 6 — Approve as SalesMan**
```
POST /api/approvals/approve
Authorization: Bearer {salesman-token}
{ "userId": "{userId from step 3}" }
```
Status moves to `PendingZoneManager`.

**Step 7 — Login as ZoneManager**

For Riyadh → فرحان ممدوح (`0500000002`). Login + verify.

**Step 8 — Approve as ZoneManager**
```
POST /api/approvals/approve
Authorization: Bearer {zm-token}
{ "userId": "{userId}" }
```
Status moves to `Approved`.

**Step 9 — Login as the new ShopOwner**
```
POST /api/auth/login         → { "mobileNumber": "0555111222" }
POST /api/auth/login/verify  → { "pinId": "...", "otp": "123456" }
```
Expected: `200` with JWT tokens, `registrationStatus: "Approved"`.

---

### Flow 2: Seller Registration (with CustomerCode)

**Step 1 — Check if shop data exists (Seller uses this, ShopOwner does NOT)**
```
GET /api/lookup/customer/AB-0001/shop-data-status
```
If `shopDataExists: true` → Seller skips shop fields. If `false` → Seller must provide all shop fields.

**Case A — ShopData already exists:**
```
POST /api/auth/send-otp      → { "mobileNumber": "0555111333" }
POST /api/auth/register/seller (form-data)
  PinId: {pinId}
  Otp: 123456
  Name: بائع اختبار
  MobileNumber: 0555111333
  CustomerCode: {same code from Flow 1}
```
Only 5 fields needed — shop data inherited.

**Case B — ShopData does NOT exist:**

Same as Case A but include all shop fields (StoreName, VAT, CRN, ShortAddress, ShopImage, CityId, NationalAddress.*).

Then approve via SalesMan → ZoneManager (same as Flow 1 Steps 5–9).

---

### Flow 3: Technician Registration

```
POST /api/auth/send-otp
{ "mobileNumber": "0555111444" }

POST /api/auth/register/technician
{
  "pinId": "{pinId}",
  "otp": "123456",
  "name": "فني اختبار",
  "mobileNumber": "0555111444",
  "cityId": "{cityId}",
  "postalCode": "54321",
  "district": "حي السلام"
}
```

Then approve (same pattern).

---

### Flow 4: Rejection

Follow Flow 1 Steps 1–5, then:
```
POST /api/approvals/reject
Authorization: Bearer {salesman-token}
{ "userId": "{userId}", "reason": "المستندات غير مكتملة" }
```

Verify rejected user **cannot login**:
```
POST /api/auth/login → { "mobileNumber": "..." }
```
Expected: `403 Forbidden`.

---

### Flow 5: Token Refresh & Revoke

```
1. Login + verify → save token + refreshToken
2. POST /api/auth/refresh-token → new token + new refreshToken
3. Try old refreshToken → 401 (revoked)
4. POST /api/auth/revoke-token → 200 (logout)
5. Try revoked token → 401
```

---

### Flow 6: Dual-Role Approval

Login as dual-role user (e.g., `0500000007` — SM + ZM for Western).
```
GET /api/approvals/pending
```
Expected: Combined queue of **PendingSalesman** (their cities) + **PendingZoneManager** (their region).

---

### Flow 7: Admin Creates Users

Login as SystemAdmin (`0500000001`), then:

```
POST /api/admin/users/salesman
{ "name": "مندوب جديد", "mobileNumber": "0599000001", "cityIds": [...] }

POST /api/admin/users/zone-manager
{ "name": "مدير جديد", "mobileNumber": "0599000002", "regionId": "..." }

POST /api/admin/users/technician
{ "name": "فني جديد", "mobileNumber": "0599000003", "cityId": "...", "postalCode": "12345", "district": "حي الملك" }
```

All created users are **pre-approved**. They can login immediately.

---

### Flow 8: Admin Edits & Toggles

```
GET /api/admin/users?page=1&pageSize=20                → find a user
PUT /api/admin/users/salesman/{id}                      → edit
PATCH /api/admin/users/{id}/toggle-status               → disable
POST /api/auth/login → { "mobileNumber": "..." }        → 403 (disabled)
PATCH /api/admin/users/{id}/toggle-status               → re-enable
POST /api/auth/login → { "mobileNumber": "..." }        → 200 (works again)
```

</details>

---

<details id="7-validation-rules">
<summary><strong>7. Validation Rules</strong></summary>

## 7. Validation Rules

| Field | Format | Rule |
|-------|--------|------|
| Mobile | `05XXXXXXXX` or `+XXXXXXXXXX` | Saudi: 10 digits starting with 05. International: `+` followed by 10–15 digits. Unique |
| CustomerCode | string | Must exist in ErpCustomers, max 50 chars |
| VAT | `3XXXXXXXXXXXXX3` | 15 digits, starts & ends with 3, unique |
| CRN | `XXXXXXXXXX` | 10 digits, unique |
| PostalCode | `XXXXX` | 5 digits |
| ShortAddress | `XXXXXXXX` | 8 alphanumeric chars, unique |
| OTP | `XXXXXX` | 6 digits |
| Name/OwnerName | string | 2–100 chars |
| StoreName | string | 2–150 chars |
| Street | string | 1–100 chars |
| BuildingNumber | int | > 0 |
| SubNumber | int | > 0 |
| District | string | Free text, optional for ShopOwner/Seller, **required** for Technician |
| ShopImage | file | JPG/PNG only, max 5 MB |
| Rejection Reason | string | 1–500 chars |

### OTP Limits

| Rule | Value |
|------|-------|
| OTP expiry | 5 minutes |
| Max verification attempts | 5 |
| Max OTP requests per mobile | 3 per 15 minutes |
| Resend cooldown | 30 seconds |

</details>

---

<details id="8-error-codes-reference">
<summary><strong>8. Error Codes Reference</strong></summary>

## 8. Error Codes Reference

There are **two error formats** depending on the source:

**Business errors** (from service layer — Result pattern):
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "...",
  "status": 400,
  "extensions": {
    "error": [{ "code": "Auth.MobileAlreadyRegistered", "description": "رقم الجوال مسجل مسبقاً" }]
  }
}
```

**Validation errors** (from FluentValidation — field-level):
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "MobileNumber": ["رقم الجوال يجب أن يبدأ بـ 05 ويتكون من 10 أرقام"],
    "VAT": ["الرقم الضريبي يجب أن يبدأ وينتهي بالرقم 3"]
  }
}
```

> **Frontend tip:** Check for `errors` (field-level validation) vs `extensions.error` (business logic) to determine error type.

### Auth & Registration Errors

| Code | Status | Arabic |
|------|--------|--------|
| Auth.MobileAlreadyRegistered | 409 | رقم الجوال مسجل مسبقاً |
| Auth.VatAlreadyExists | 409 | رقم الضريبة مسجل مسبقاً |
| Auth.CrnAlreadyExists | 409 | السجل التجاري مسجل مسبقاً |
| Auth.ShortAddressAlreadyExists | 409 | العنوان المختصر مسجل مسبقاً |
| Auth.CustomerCodeNotFound | 400 | كود العميل غير موجود |
| Auth.ShopDataRequired | 400 | بيانات المتجر مطلوبة |
| Auth.ImageUploadFailed | 400 | فشل رفع الصورة |
| Auth.UserNotFound | 404 | المستخدم غير موجود |
| Auth.UserNotApproved | 403 | المستخدم غير معتمد |
| Auth.UserRejected | 403 | تم رفض طلب التسجيل |
| Auth.UserDisabled | 403 | تم تعطيل حساب المستخدم |
| Auth.CityNotFound | 400 | المدينة غير موجودة |
| Auth.NoApprovalSalesMan | 400 | لا يوجد مندوب معتمد للمدينة |

### OTP Errors

| Code | Status | Arabic |
|------|--------|--------|
| Auth.OtpNotFound | 400 | رمز التحقق غير موجود |
| Auth.OtpExpired | 400 | انتهت صلاحية رمز التحقق |
| Auth.OtpInvalid | 400 | رمز التحقق غير صحيح |
| Auth.OtpAlreadyUsed | 400 | رمز التحقق مستخدم مسبقاً |
| Auth.TooManyOtpRequests | 429 | تم تجاوز الحد الأقصى لطلبات رمز التحقق |
| Auth.OtpResendTooSoon | 429 | يرجى الانتظار قبل إعادة إرسال رمز التحقق |
| Auth.MaxVerificationAttempts | 400 | تم تجاوز الحد الأقصى لمحاولات التحقق |

### Token Errors

| Code | Status | Arabic |
|------|--------|--------|
| Auth.InvalidRefreshToken | 401 | رمز التحديث غير صالح |
| Auth.RefreshTokenExpired | 401 | انتهت صلاحية رمز التحديث |
| Auth.RefreshTokenRevoked | 401 | تم إبطال رمز التحديث |

### Approval Errors

| Code | Status | Arabic |
|------|--------|--------|
| Approval.UserNotPendingApproval | 400 | المستخدم ليس في حالة انتظار الموافقة |
| Approval.NotAuthorizedToApprove | 403 | غير مصرح لك بالموافقة |
| Approval.NoZoneManagerForRegion | 400 | لا يوجد مدير منطقة للمنطقة |

### Admin Errors

| Code | Status | Arabic |
|------|--------|--------|
| Admin.MobileAlreadyExists | 409 | رقم الجوال مسجل مسبقاً |
| Admin.CityNotFound | 400 | المدينة غير موجودة |
| Admin.SomeCitiesNotFound | 400 | بعض المدن المحددة غير موجودة أو غير مفعلة |
| Admin.RegionNotFound | 400 | المنطقة غير موجودة |
| Admin.RegionAlreadyHasZoneManager | 409 | المنطقة لديها مدير منطقة بالفعل |
| Admin.CustomerCodeNotFound | 400 | كود العميل غير موجود في النظام |
| Admin.ShopDataRequired | 400 | بيانات المحل مطلوبة — لا توجد بيانات سابقة لهذا الكود |
| Admin.VatAlreadyExists | 409 | الرقم الضريبي مسجل مسبقاً |
| Admin.CrnAlreadyExists | 409 | رقم السجل التجاري مسجل مسبقاً |
| Admin.ShortAddressAlreadyExists | 409 | العنوان المختصر مسجل مسبقاً |
| Admin.CreateUserFailed | 500 | فشل إنشاء الحساب |
| Admin.ImageUploadFailed | 500 | فشل رفع الصورة |
| Admin.NoApprovalSalesMan | 400 | لا يوجد مندوب مبيعات معتمد لهذه المدينة |
| Admin.UserNotFound | 404 | المستخدم غير موجود |
| Admin.UserIsSystemAdmin | 403 | لا يمكن تعديل حساب مدير النظام |
| Admin.UserTypeMismatch | 400 | نوع المستخدم غير متطابق |
| Admin.MobileAlreadyInUse | 409 | رقم الجوال مستخدم من قبل مستخدم آخر |
| Admin.UpdateUserFailed | 500 | فشل تحديث بيانات المستخدم |

### Lookup Errors

| Code | Status | Arabic |
|------|--------|--------|
| Lookup.RegionNotFound | 404 | المنطقة غير موجودة |
| Lookup.CustomerCodeNotFound | 404 | كود العميل غير موجود |

</details>

---

<details id="9-role-based-access-matrix">
<summary><strong>9. Role-Based Access Matrix</strong></summary>

## 9. Role-Based Access Matrix

| Endpoint | No Auth | SystemAdmin | ZoneManager | SalesMan | ShopOwner | Seller | Technician |
|----------|---------|-------------|-------------|----------|-----------|--------|------------|
| `GET /api/lookup/*` | Yes | Yes | Yes | Yes | Yes | Yes | Yes |
| `POST /api/auth/send-otp` | Yes | — | — | — | — | — | — |
| `POST /api/auth/register/*` | Yes | — | — | — | — | — | — |
| `POST /api/auth/login` | Yes | — | — | — | — | — | — |
| `POST /api/auth/login/verify` | Yes | — | — | — | — | — | — |
| `POST /api/auth/resend-otp` | Yes | — | — | — | — | — | — |
| `POST /api/auth/refresh-token` | — | Yes | Yes | Yes | Yes | Yes | Yes |
| `POST /api/auth/revoke-token` | — | Yes | Yes | Yes | Yes | Yes | Yes |
| `GET /api/approvals/pending` | — | — | Yes | Yes | 403 | 403 | 403 |
| `POST /api/approvals/approve` | — | — | Yes | Yes | 403 | 403 | 403 |
| `POST /api/approvals/reject` | — | — | Yes | Yes | 403 | 403 | 403 |
| `POST /api/admin/users/*` | — | Yes | 403 | 403 | 403 | 403 | 403 |
| `GET /api/admin/users` | — | Yes | 403 | 403 | 403 | 403 | 403 |
| `PATCH /api/admin/users/*/toggle-status` | — | Yes | 403 | 403 | 403 | 403 | 403 |
| `PUT /api/admin/users/*` | — | Yes | 403 | 403 | 403 | 403 | 403 |

</details>

---

<details id="10-test-checklist">
<summary><strong>10. Test Checklist</strong></summary>

## 10. Test Checklist

### 10.1 Registration

#### Send OTP
- [ ] Valid new mobile → `200` with `pinId`
- [ ] Already registered mobile → `409`
- [ ] Invalid mobile format → `400`

#### ShopOwner (shop data ALWAYS required)
- [ ] Full registration (new CustomerCode, all fields) → `200`
- [ ] Registration when ShopData already exists → `200` (ShopOwner's data overwrites Seller's)
- [ ] Verify overwritten ShopData reflects ShopOwner's values (not Seller's)
- [ ] Missing any shop field (StoreName, VAT, CRN, ShortAddress, ShopImage) → `400`
- [ ] Missing PinId or Otp → `400`
- [ ] Invalid/expired OTP → `400`
- [ ] Invalid CustomerCode → `400`
- [ ] Duplicate mobile → `409`
- [ ] Duplicate VAT → `409`
- [ ] Duplicate CRN → `409`
- [ ] Invalid VAT (not 3…3) → `400`
- [ ] Invalid CRN (not 10 digits) → `400`
- [ ] Non-existent CityId → `400`
- [ ] City without SalesMan → `400`
- [ ] Non-JPG/PNG image → `400`
- [ ] Image > 5 MB → `400`

#### Seller
- [ ] ShopData exists → only 5 fields needed → `200`
- [ ] ShopData doesn't exist → full form → `200`
- [ ] Invalid CustomerCode → `400`
- [ ] Duplicate mobile → `409`

#### Technician
- [ ] Valid registration → `200`
- [ ] Missing district → `400`
- [ ] Non-existent CityId → `400`
- [ ] Invalid PostalCode → `400`

### 10.2 OTP

- [ ] Resend within 30 seconds → `429`
- [ ] Resend after 30 seconds → `200`
- [ ] More than 3 requests in 15 min → `429`
- [ ] Non-6-digit value → `400`

### 10.3 Login

- [ ] Approved user → `200` with `pinId`
- [ ] Non-existent mobile → `404`
- [ ] PendingSalesman user → `403`
- [ ] PendingZoneManager user → `403`
- [ ] Rejected user → `403`
- [ ] Disabled user → `403`
- [ ] Verify OTP → `200` with JWT

### 10.4 Tokens

- [ ] Refresh valid token → `200` new tokens
- [ ] Refresh revoked token → `401`
- [ ] Old refresh token after refresh → `401`
- [ ] Revoke → `200`
- [ ] Double revoke → `401`

### 10.5 Approvals

- [ ] SalesMan sees only their PendingSalesman users
- [ ] ZoneManager sees only their PendingZoneManager users
- [ ] Dual-role sees combined queue
- [ ] SM approves → PendingZoneManager
- [ ] Wrong SM → `403`
- [ ] ZM approves → Approved
- [ ] Wrong ZM → `403`
- [ ] Reject with reason → `200`
- [ ] Reject without reason → `400`
- [ ] Rejected user cannot login → `403`
- [ ] pageSize > 50 → clamped to 50
- [ ] Non-SM/ZM accessing approvals → `403`
- [ ] Unauthenticated → `401`

### 10.6 Lookups

- [ ] Get regions → 8 regions
- [ ] Get cities by region → correct cities
- [ ] Get all cities → 140
- [ ] Invalid regionId → `404`
- [ ] Shop data status — no ShopData → `shopDataExists: false`
- [ ] Shop data status — ShopData exists → `shopDataExists: true`
- [ ] Shop data status — invalid code → `404`

### 10.7 Admin — Add User

- [ ] Add SalesMan → user created + cities assigned
- [ ] Add ZoneManager → user created + region assigned
- [ ] Add ShopOwner → user + ShopData created, pre-approved
- [ ] Add Seller → user + ShopData created, pre-approved
- [ ] Add Technician → user created, pre-approved
- [ ] Duplicate mobile → error
- [ ] Non-admin JWT → `403`
- [ ] No JWT → `401`

### 10.8 Admin — List & Toggle

- [ ] List all users → paginated
- [ ] Filter by userType → correct results
- [ ] Filter by registrationStatus → correct results
- [ ] Search by name → matches
- [ ] Search by mobile → matches
- [ ] Toggle status (disable) → user cannot login
- [ ] Toggle status (re-enable) → user can login

### 10.9 Admin — Edit User

- [ ] Edit SalesMan name + reassign cities
- [ ] Edit ZoneManager + reassign region
- [ ] Edit ShopOwner + ShopData
- [ ] Edit Seller + ShopData
- [ ] Edit Technician (name, mobile, city, postalCode, district)
- [ ] Edit with duplicate mobile → error
- [ ] Edit non-existent user → `404`

### 10.10 Edge Cases

- [ ] Arabic characters in all name fields → accepted
- [ ] StoreName > 150 chars → `400`
- [ ] Concurrent registration with same mobile → one succeeds, one gets `409`
- [ ] Admin-created user can login immediately (no approval needed)
- [ ] Expired JWT (after 60 min) → `401` on protected endpoints

</details>

---

<details id="11-frontend-integration-notes">
<summary><strong>11. Frontend Integration Notes</strong></summary>

## 11. Frontend Integration Notes

### 11.1 Image Uploads

Shop images are stored under `wwwroot/uploads/` on the server:
- **URL pattern:** `/uploads/{folder}/{guid}.{ext}` (e.g., `/uploads/shop-images/f47ac10b-...jpg`)
- **Full URL:** `{baseUrl}/uploads/shop-images/f47ac10b-...jpg`
- Returned in `shopImageUrl` field in approval responses and admin list

### 11.2 Registration Flow — Frontend Decision Tree

```
User selects role:
├── ShopOwner:
│   ├── send-otp → get pinId
│   ├── Always show ALL shop fields (StoreName, VAT, CRN, ShortAddress, ShopImage, NationalAddress)
│   ├── Do NOT call shop-data-status endpoint
│   └── Submit register/shop-owner with all fields
│
├── Seller:
│   ├── send-otp → get pinId
│   ├── User enters CustomerCode → call GET /api/lookup/customer/{code}/shop-data-status
│   ├── If shopDataExists: true → hide shop fields, only show Name + MobileNumber + CustomerCode
│   ├── If shopDataExists: false → show all shop fields (same as ShopOwner)
│   └── Submit register/seller
│
└── Technician:
    ├── send-otp → get pinId
    ├── Show: Name, MobileNumber, CityId, PostalCode, District
    └── Submit register/technician (JSON, no file upload)
```

### 11.3 Enum Values (for filters and display)

**UserType:** `1`=ShopOwner, `2`=Seller, `3`=Technician, `4`=SalesMan, `5`=ZoneManager

**RegistrationStatus:** `1`=PendingSalesman, `2`=PendingZoneManager, `3`=Approved, `4`=Rejected

These are used as integers in query params (admin list) and returned as strings in JSON responses.

</details>

---

<details id="12-known-limitations">
<summary><strong>12. Known Limitations</strong></summary>

## 12. Known Limitations

| ID | Description | Impact |
|----|-------------|--------|
| C6 | Secrets are placeholders in appsettings.json | Must set properly for production |
| P3 | OTP records are never cleaned up | Table grows indefinitely |
| M6 | Rejected users cannot re-register | Mobile is permanently blocked |
| Mock OTP | Any 6-digit code works in Dev/Staging | Not a limitation — simplifies testing |

</details>

---

<details id="quick-reference-card">
<summary><strong>13. Quick Reference Card</strong></summary>

## 13. Quick Reference Card

| Item | Format |
|------|--------|
| Mobile | `05XXXXXXXX` (10 digits) or `+XXXXXXXXXX` (international) |
| VAT | `3XXXXXXXXXXXXX3` (15 digits) |
| CRN | `XXXXXXXXXX` (10 digits) |
| PostalCode | `XXXXX` (5 digits) |
| ShortAddress | `XXXXXXXX` (8 alphanumeric) |
| OTP | `XXXXXX` (6 digits — any in dev) |
| JWT Expiry | 60 minutes |
| Refresh Token Expiry | 7 days |
| Auth Header | `Authorization: Bearer {token}` |

### Registration Status Flow

```
PendingSalesman → (SM approves) → PendingZoneManager → (ZM approves) → Approved
       ↓                                    ↓
    Rejected                             Rejected
```

### Registration Flow (OTP-first)

```
send-otp → get pinId → register with pinId + otp → user created (PendingSalesman)
```

### Login Flow

```
login → get pinId → login/verify with pinId + otp → JWT + refreshToken
```

</details>

---

*End of QA Test Guide — Version 3.1*
