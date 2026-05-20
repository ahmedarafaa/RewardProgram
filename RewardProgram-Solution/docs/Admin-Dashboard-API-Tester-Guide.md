# AL-Raed RewardProgram — Admin Dashboard API Tester Guide
**Last Updated:** 2026-05-04
**Scope:** Admin dashboard endpoints only. Mobile/public endpoints are documented separately.

---

## 1. Environments & Base URLs

| Environment | Base URL                                   |
|-------------|--------------------------------------------|
| Development | `https://localhost:44315`                  |
| Staging     | `https://staging.raedrewardapp.com`        |
| UAT         | `https://uat.raedrewardapp.com`            |
| Production  | TBD                                        |

Admin frontend: `admin.raedrewardapp.com`
All admin endpoints are namespaced under `/api/admin/...`.

---

## 2. Authentication Model

- Admin login is **username/password** at `POST /api/admin/auth/login`.
- The account must hold the `SystemAdmin` role; any other role gets 401.
- After login, every admin call requires a JWT in the `Authorization` header.
- Default seeded credentials (Dev / Staging / UAT): **`admin` / `Raed@2026`** (the seeder resets this on every startup, so it is reliable for testing).
- Lockout policy: ASP.NET Identity default — repeated bad passwords lock the account for ~5 minutes (HTTP 401, message: "تم قفل الحساب مؤقتاً...").
- Admin refresh and logout flows mirror the public auth flows but live under `/api/admin/auth`.

```
Authorization: Bearer <JWT>
```

Refresh token lifetime: **365 days**. Reuse of an already-rotated refresh token revokes the entire token family (security hardening).

---

## 3. Common Conventions

### 3.1 Success envelope — `Result<T>`
For successful calls the body is the raw `T` (no wrapper), e.g.:

```json
{
  "id": "abc",
  "name": "..."
}
```

For `204 No Content` actions there is **no body**.

### 3.2 Error envelope — `ProblemDetails`
Every failure (validation, business, not-found, auth) returns `application/problem+json`:

```json
{
  "type": "about:blank",
  "title": "رقم الجوال مسجل مسبقاً",
  "status": 409,
  "code": "Admin.MobileAlreadyExists",
  "errors": null
}
```

For FluentValidation failures (HTTP 400) the `errors` field contains a per-field array.

### 3.3 Pagination — `PaginatedResult<T>`
List endpoints return:

```json
{
  "items": [ { "...": "..." } ],
  "totalCount": 137,
  "page": 1,
  "pageSize": 20,
  "totalPages": 7,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

Default pagination: `page=1`, `pageSize=20` unless overridden.

### 3.4 Common query parameters
- `page` (int, default 1) — 1-based page number.
- `pageSize` (int, default 20).
- `search` — free-text contains-search (depends on endpoint).
- `dateFrom`, `dateTo`, `fromDate`, `toDate` — ISO-8601 UTC, e.g. `2026-04-01T00:00:00Z`.

### 3.5 Date format
All datetimes are **ISO-8601 UTC** in requests and responses.

### 3.6 Enums (numeric values used in JSON)

**UserType:** 1=ShopOwner, 2=Seller, 3=Technician, 4=SalesMan, 5=ZoneManager, 6=SystemAdmin
**RegistrationStatus:** 1=PendingSalesman, 2=PendingZoneManager, 3=Approved, 4=Rejected
**WalletTransactionType:** 1=Earned, 2=Redeemed, 3=Cancelled, 4=Expired, 5=Refunded, 6=InvitationReward
**RedemptionRequestStatus:** 1=PendingSalesMan, 2=PendingZoneManager, 3=PendingAdmin, 4=AdminApproved, 5=Completed, 6=Rejected, 7=Cancelled
**RedemptionMethod:** 1=BankTransfer, 2=Cash
**BarcodeStatus:** 1=Available, 2=SellerScanned, 3=TechnicianScanned, 4=Consumed
**ScannerRole:** 1=Seller, 2=Technician
**ApprovalAction:** 1=Approved, 2=Rejected
**NotificationType:** 1=RegistrationApproved, 2=RegistrationRejected, 3=PointsEarned, 4=RedemptionCreated, 5=RedemptionApproved, 6=RedemptionRejected, 7=RedemptionCompleted, 8=InvitationReward, 9=AdminMessage

---

## 4. Endpoints — by feature

### 4.1 Admin Auth

#### Admin Auth — Login
- **Method + Path:** `POST /api/admin/auth/login`
- **Auth:** None (anonymous)
- **Description:** Username/password sign-in for SystemAdmin users.
- **Request body:**

| Field    | Type   | Required | Validation / notes                  |
|----------|--------|----------|-------------------------------------|
| username | string | yes      | Identity username (case-insensitive)|
| password | string | yes      | Plain password                      |

- **Success (200):** `AuthResponse`

```json
{
  "token": "eyJhbGciOi...",
  "refreshToken": "x9p2n8...",
  "expiresIn": 3600,
  "refreshTokenExpiration": "2027-05-04T10:00:00Z",
  "user": {
    "id": "...",
    "name": "Admin",
    "mobileNumber": "0500000000",
    "userType": 6,
    "registrationStatus": 3
  }
}
```

- **Error responses:**

| code (title) | HTTP | When it triggers |
|--------------|------|------------------|
| اسم المستخدم أو كلمة المرور غير صحيحة | 401 | Username not found, wrong password, or account `IsDisabled=true`. |
| تم قفل الحساب مؤقتاً بسبب محاولات فاشلة متكررة | 401 | Identity lockout active. Wait and retry. |
| غير مصرح لك بالدخول | 401 | Credentials valid but the user is **not** in `SystemAdmin`. |

- **Tester notes:** No request body validation is enforced server-side beyond Identity's password check. The endpoint does not enumerate which side (user/password) failed.

---

#### Admin Auth — Refresh Token
- **Method + Path:** `POST /api/admin/auth/refresh`
- **Auth:** None (anonymous; the refresh token is the credential)
- **Description:** Exchanges a valid admin refresh token for a fresh JWT + new refresh token. Old refresh token is revoked (rotation).
- **Request body:**

| Field        | Type   | Required | Validation / notes |
|--------------|--------|----------|--------------------|
| refreshToken | string | yes      | Issued by `/login` |

- **Success (200):** `AuthResponse` (same shape as login).
- **Error responses:**

| code | HTTP | When |
|------|------|------|
| `Auth.InvalidRefreshToken` | 401 | Token unknown or has been revoked (token-family revocation triggers if a previously rotated token is replayed). |
| `Auth.RefreshTokenExpired` | 401 | Token is older than 365 days. |
| `Auth.RefreshTokenRevoked` | 401 | Token was explicitly revoked via logout or family revocation. |

- **Tester notes:** Reusing an already-rotated refresh token will **revoke every active session** of that admin. Use this to verify reuse-detection.

---

#### Admin Auth — Logout
- **Method + Path:** `POST /api/admin/auth/logout`
- **Auth:** Bearer (Role: SystemAdmin)
- **Description:** Revokes the supplied refresh token. JWT remains valid until natural expiry.
- **Request body:**

| Field        | Type   | Required | Validation / notes |
|--------------|--------|----------|--------------------|
| refreshToken | string | yes      | Token to revoke    |

- **Success (200):** `{ "message": "تم تسجيل الخروج بنجاح" }`
- **Error responses:**

| code | HTTP | When |
|------|------|------|
| `Auth.InvalidRefreshToken` | 401 | Token does not belong to caller. |

---

### 4.2 User Management

All endpoints under `/api/admin/users` require **Bearer (Role: SystemAdmin)** unless stated.

There are 5 user types with parallel CRUD: SalesMan, ZoneManager, ShopOwner, Seller, Technician.

#### Users — List
- **Method + Path:** `GET /api/admin/users`
- **Auth:** Bearer (Role: SystemAdmin)
- **Description:** Searchable / filterable paged list of all users.
- **Query params:**

| Param              | Type                | Notes |
|--------------------|---------------------|-------|
| search             | string?             | Matches name or mobile |
| userType           | UserType?           | 1..6 |
| registrationStatus | RegistrationStatus? | 1..4 |
| regionId           | string?             | Filter by region |
| isDisabled         | bool?               | Toggle filter |
| isDeleted          | bool?               | Self-deleted accounts |
| page               | int (default 1)     |       |
| pageSize           | int (default 20)    |       |

- **Success (200):** `PaginatedResult<AdminUserListItemResponse>` — items contain `id, name, mobileNumber, userType, registrationStatus, isDisabled, isAccountDeleted, accountDeletedAt, createdAt, regionName?, cityName?, customerCode?, storeName?`.

---

#### Users — Add SalesMan
- **Method + Path:** `POST /api/admin/users/salesman`
- **Auth:** Bearer (Role: SystemAdmin)
- **Description:** Creates a SalesMan and (optionally) assigns a list of cities to them.
- **Request body (JSON):**

| Field        | Type           | Required | Validation |
|--------------|----------------|----------|------------|
| name         | string         | yes      | 3–100 chars; letters + spaces only (`^[\p{L}\s]+$`) |
| mobileNumber | string         | yes      | `^(05\d{8}|\+\d{10,15})$` (KSA local or international) |
| cityIds      | string[]?      | no       | If provided, every city must exist and be active and currently free of a SalesMan |

- **Success (200):** `AdminAddUserResponse { userId, name, mobileNumber, userType, message }`
- **Error responses:**

| code | HTTP | When |
|------|------|------|
| `Admin.MobileAlreadyExists` | 409 | Mobile already used by any user. |
| `Admin.SomeCitiesNotFound` | 400 | One or more cityIds invalid/inactive. |
| `Admin.CityAlreadyHasSalesMan` | 409 | A target city already has a SalesMan. |
| `Admin.CreateUserFailed` | 500 | Identity create call failed. |

---

#### Users — Add ZoneManager
- **Method + Path:** `POST /api/admin/users/zone-manager`
- **Auth:** Bearer (Role: SystemAdmin)
- **Request body (JSON):**

| Field        | Type    | Required | Validation |
|--------------|---------|----------|------------|
| name         | string  | yes      | 3–100 chars; letters + spaces only |
| mobileNumber | string  | yes      | `^(05\d{8}|\+\d{10,15})$` |
| regionId     | string? | no       | If provided, region must exist and currently have no ZM |

- **Success (200):** `AdminAddUserResponse`
- **Error responses:**

| code | HTTP | When |
|------|------|------|
| `Admin.MobileAlreadyExists` | 409 | Mobile already used. |
| `Admin.RegionNotFound` | 400 | regionId unknown. |
| `Admin.RegionAlreadyHasZoneManager` | 409 | Region already has a ZM. |
| `Admin.CreateUserFailed` | 500 | Identity create failed. |

---

#### Users — Add ShopOwner (multipart/form-data)
- **Method + Path:** `POST /api/admin/users/shop-owner`
- **Auth:** Bearer (Role: SystemAdmin)
- **Description:** Creates a ShopOwner. Shop data is required only when no `ShopData` exists yet for this `customerCode`. ShopOwner creation overwrites any prior Seller-supplied shop data.
- **Body type:** `multipart/form-data` (because of `shopImage`).
- **Request fields:**

| Field           | Type                       | Required                       | Validation |
|-----------------|----------------------------|--------------------------------|------------|
| ownerName       | string                     | yes                            | 3–100 chars; letters/spaces only |
| mobileNumber    | string                     | yes                            | `^(05\d{8}|\+\d{10,15})$` |
| customerCode    | string                     | yes                            | ≤ 50 chars; must exist in ErpCustomer; must not already be owned by another ShopOwner |
| cityId          | string                     | yes                            | City must exist; must have an ApprovalSalesMan |
| storeName       | string                     | conditional*                   | 5–150 chars |
| vat             | string                     | conditional*                   | 15 digits, starts and ends with 3 (`^3\d{13}3$`) |
| crn             | string                     | conditional*                   | 10 digits |
| shortAddress    | string                     | conditional*                   | `^[A-Za-z]{4}\d{4}$` (4 letters + 4 digits) |
| shopImage       | file                       | conditional*                   | .jpg/.jpeg/.png, ≤ 5 MB |
| nationalAddress | object                     | conditional*                   | See Section 4.2.NA |

\* = conditional. Required iff any one of `storeName/vat/crn/shopImage` is supplied OR no prior ShopData exists for the customerCode (server-side check; returns `Admin.ShopDataRequired`).

**`nationalAddress` shape (when required):**

| Field          | Type   | Required | Validation |
|----------------|--------|----------|------------|
| buildingNumber | int    | yes      | 1000–9999  |
| street         | string | yes      | 3–100 chars |
| postalCode     | string | yes      | 5 digits |
| subNumber      | int    | yes      | 1000–9999 |
| district       | string | yes      | ≤ 100 chars |

- **Success (200):** `AdminAddUserResponse`
- **Error responses:**

| code | HTTP | When |
|------|------|------|
| `Admin.MobileAlreadyExists` | 409 | Mobile already in use. |
| `Admin.CityNotFound` | 400 | cityId invalid. |
| `Admin.NoApprovalSalesMan` | 400 | City has no ApprovalSalesMan assigned. |
| `Admin.CustomerCodeNotFound` | 400 | customerCode not in ErpCustomer master. |
| `Admin.CustomerCodeAlreadyOwned` | 409 | Another ShopOwner already owns this customerCode. |
| `Admin.ShopDataRequired` | 400 | No prior ShopData and required shop fields are missing. |
| `Admin.CreateUserFailed` | 500 | Identity create failed. |

- **Tester notes:** Created ShopOwners always start in `Approved` status (admin manual creation). Their wallet is **not** auto-created since ShopOwners do not scan or earn.

---

#### Users — Add Seller (multipart/form-data)
- **Method + Path:** `POST /api/admin/users/seller`
- **Auth:** Bearer (Role: SystemAdmin)
- **Body type:** `multipart/form-data`
- **Request fields:** Identical to Add ShopOwner except the user-name field is `name` (not `ownerName`). Shop data is **only** required if no ShopData yet exists for the customerCode (first-come-first-served).
- **Errors:** Same set as Add ShopOwner. `Admin.CustomerCodeAlreadyOwned` does **not** apply (multiple Sellers can share a customerCode).
- **Tester notes:** `cityId` is mandatory only when shop data is being supplied (validator wraps it in the same `When(...)` block).

---

#### Users — Add Technician
- **Method + Path:** `POST /api/admin/users/technician`
- **Auth:** Bearer (Role: SystemAdmin)
- **Body type:** `application/json`
- **Request body:**

| Field        | Type   | Required | Validation |
|--------------|--------|----------|------------|
| name         | string | yes      | 3–100 chars, letters/spaces |
| mobileNumber | string | yes      | `^(05\d{8}|\+\d{10,15})$` |
| cityId       | string | yes      | City must exist with ApprovalSalesMan |
| postalCode   | string | yes      | 5 digits |
| district     | string | yes      | ≤ 100 chars |

- **Success (200):** `AdminAddUserResponse`
- **Error responses:**

| code | HTTP | When |
|------|------|------|
| `Admin.MobileAlreadyExists` | 409 | Mobile already used. |
| `Admin.CityNotFound` | 400 | cityId invalid. |
| `Admin.NoApprovalSalesMan` | 400 | City has no ApprovalSalesMan. |
| `Admin.CreateUserFailed` | 500 | Identity create failed. |

---

#### Users — Edit (5 endpoints)

All edit endpoints accept JSON body and update **only the name** (mobile/customerCode/etc. are not editable here — strip-and-reassign is handled via dedicated reassign endpoints).

| Endpoint | Body field |
|----------|-----------|
| `PUT /api/admin/users/salesman/{id}` | `name` |
| `PUT /api/admin/users/zone-manager/{id}` | `name` |
| `PUT /api/admin/users/shop-owner/{id}` | `ownerName` |
| `PUT /api/admin/users/seller/{id}` | `name` |
| `PUT /api/admin/users/technician/{id}` | `name` |

- **Auth:** Bearer (Role: SystemAdmin)
- **Validation (all):** name 3–100 chars, letters + spaces only.
- **Success (200):** `AdminAddUserResponse`
- **Error responses:**

| code | HTTP | When |
|------|------|------|
| `Admin.UserNotFound` | 404 | id unknown |
| `Admin.UserTypeMismatch` | 400 | Calling SalesMan endpoint with a Technician id, etc. |
| `Admin.UserIsSystemAdmin` | 403 | Cannot edit a SystemAdmin via these endpoints. |
| `Admin.UpdateUserFailed` | 500 | Identity update failed. |

- **Tester notes:** Editing a SalesMan/ZoneManager **strips their assignments and mobile** (per business rule — re-issue assignments via reassign endpoints).

---

#### Users — Toggle Status
- **Method + Path:** `PATCH /api/admin/users/{id}/toggle-status`
- **Auth:** Bearer (Role: SystemAdmin)
- **Description:** Flips `IsDisabled` on the target user.
- **Path params:** `id` — user id.
- **Success (200):** `AdminToggleStatusResponse { userId, isDisabled, message }`
- **Error responses:**

| code | HTTP | When |
|------|------|------|
| `Admin.UserNotFound` | 404 | id unknown |
| `Admin.UserIsSystemAdmin` | 403 | Cannot disable another SystemAdmin (or yourself). |

---

#### Users — Reassign Cities (to another SalesMan)
- **Method + Path:** `POST /api/admin/users/cities/reassign`
- **Auth:** Bearer (Role: SystemAdmin)
- **Request body:**

| Field         | Type     | Required | Validation |
|---------------|----------|----------|------------|
| cityIds       | string[] | yes      | non-empty |
| toSalesManId  | string   | yes      | non-empty; must be an active SalesMan |

- **Success:** `204 No Content`
- **Error responses:**

| code | HTTP | When |
|------|------|------|
| `Admin.SomeCitiesNotFound` | 400 | One or more cityIds invalid. |
| `Admin.ReassignmentTargetNotSalesMan` | 400 | toSalesManId is not a SalesMan. |
| `Admin.CannotReassignToSelf` | 400 | Source and target are the same. |
| `Admin.UserNotFound` | 404 | toSalesManId unknown. |

---

#### Users — Reassign Region (to another ZoneManager)
- **Method + Path:** `POST /api/admin/users/regions/reassign`
- **Auth:** Bearer (Role: SystemAdmin)
- **Request body:**

| Field            | Type   | Required | Validation |
|------------------|--------|----------|------------|
| regionId         | string | yes      | non-empty |
| toZoneManagerId  | string | yes      | non-empty; must be an active ZoneManager |

- **Success:** `204 No Content`
- **Error responses:**

| code | HTTP | When |
|------|------|------|
| `Admin.RegionNotFound` | 400 | regionId invalid |
| `Admin.ReassignmentTargetNotZoneManager` | 400 | toZoneManagerId is not a ZM |
| `Admin.ZoneManagerAlreadyAssigned` | 409 | Target ZM is already on a different region |
| `Admin.CannotReassignToSelf` | 400 | Same source/target |
| `Admin.UserNotFound` | 404 | toZoneManagerId unknown |

---

#### Users — Delete SalesMan (with mandatory city reassignment)
- **Method + Path:** `DELETE /api/admin/users/salesman/{id}`
- **Auth:** Bearer (Role: SystemAdmin)
- **Description:** Soft-deletes a SalesMan. The body must reassign **every** city currently owned by that SalesMan to another SalesMan.
- **Request body:**

```json
{
  "cityReassignments": [
    { "cityId": "...", "newSalesManId": "..." },
    ...
  ]
}
```

| Field             | Type | Required | Validation |
|-------------------|------|----------|------------|
| cityReassignments | array | yes     | not-null; every item validated below |
| .cityId           | string | yes    | non-empty |
| .newSalesManId    | string | yes    | non-empty |

- **Success:** `204 No Content`
- **Error responses:**

| code | HTTP | When |
|------|------|------|
| `Admin.UserNotFound` | 404 | id unknown |
| `Admin.AllCitiesMustBeReassigned` | 400 | cityReassignments does not cover every city the SalesMan owns. |
| `Admin.CityNotOwnedBySalesMan` | 400 | A cityId in the body does not belong to this SalesMan. |
| `Admin.ReassignmentTargetNotSalesMan` | 400 | newSalesManId is not a SalesMan. |
| `Admin.CannotReassignToSelf` | 400 | Trying to reassign to the same SalesMan being deleted. |

---

#### Users — Delete ZoneManager
- **Method + Path:** `DELETE /api/admin/users/zone-manager/{id}`
- **Auth:** Bearer (Role: SystemAdmin)
- **Description:** Soft-deletes a ZoneManager. If the ZM owned a region, a replacement must be supplied.
- **Request body:**

| Field             | Type    | Required                         | Validation |
|-------------------|---------|----------------------------------|------------|
| newZoneManagerId  | string? | conditional (yes if owns region) | If supplied must be a ZoneManager |

- **Success:** `204 No Content`
- **Error responses:**

| code | HTTP | When |
|------|------|------|
| `Admin.UserNotFound` | 404 | id unknown |
| `Admin.ReplacementZoneManagerRequired` | 400 | ZM owned a region but newZoneManagerId is null. |
| `Admin.ReassignmentTargetNotZoneManager` | 400 | newZoneManagerId not a ZM. |
| `Admin.ZoneManagerAlreadyAssigned` | 409 | Replacement is already assigned elsewhere. |
| `Admin.CannotReassignToSelf` | 400 | Replacement equals the user being deleted. |

---

### 4.3 Products

All endpoints require **Bearer (Role: SystemAdmin)**.

#### Products — Add
- **Method + Path:** `POST /api/admin/products`
- **Request body:**

| Field        | Type    | Required | Validation |
|--------------|---------|----------|------------|
| name         | string  | yes      | ≤ 200 chars |
| productCode  | string  | yes      | ≤ 50 chars; unique |
| pointValue   | int     | yes      | > 0 |
| price        | decimal | yes      | ≥ 0 |
| category     | string? | no       | ≤ 100 chars when supplied |

- **Success (200):** `AdminProductResponse { id, name, productCode, pointValue, price, category, totalBarcodes, availableBarcodes }`
- **Error responses:**

| code | HTTP | When |
|------|------|------|
| `Product.CodeAlreadyExists` | 409 | productCode already used. |

---

#### Products — List
- **Method + Path:** `GET /api/admin/products`
- **Query params:**

| Param    | Type    | Notes |
|----------|---------|-------|
| search   | string? | matches name/productCode |
| category | string? | exact match |
| page     | int (default 1) | |
| pageSize | int (default 20) | |

- **Success (200):** `PaginatedResult<AdminProductResponse>`

---

#### Products — List Categories
- **Method + Path:** `GET /api/admin/products/categories`
- **Query params:** `search`, `page`, `pageSize` (defaults 1 / 20)
- **Success (200):** `PaginatedResult<CategoryItem { id, name }>`

---

#### Products — Get By Id
- **Method + Path:** `GET /api/admin/products/{id}`
- **Success (200):** `AdminProductResponse`
- **Errors:** `Product.NotFound` (404)

---

#### Products — Edit
- **Method + Path:** `PUT /api/admin/products/{id}`
- **Request body:** Same shape and validation as Add.
- **Success (200):** `AdminProductResponse`
- **Errors:**

| code | HTTP | When |
|------|------|------|
| `Product.NotFound` | 404 | id unknown |
| `Product.CodeAlreadyExists` | 409 | New productCode collides with another product. |

---

#### Products — Delete
- **Method + Path:** `DELETE /api/admin/products/{id}`
- **Success:** `204 No Content`
- **Errors:**

| code | HTTP | When |
|------|------|------|
| `Product.NotFound` | 404 | id unknown |
| `Product.HasBarcodes` | 400 | Product has any associated barcodes — delete is blocked. |

---

### 4.4 Barcodes & Scans

#### Barcodes — Generate (returns PDF)
- **Method + Path:** `POST /api/admin/barcodes/generate`
- **Auth:** Bearer (Role: SystemAdmin)
- **Description:** Creates `quantity` new ProductBarcodes for the given product and returns a printable PDF (QuestPDF + ZXing CODE_128). **Response is a binary PDF, not JSON**.
- **Request body:**

| Field      | Type   | Required | Validation |
|------------|--------|----------|------------|
| productId  | string | yes      | must exist |
| quantity   | int    | yes      | 1–1000 inclusive |

- **Success (200):**
  - `Content-Type: application/pdf`
  - `Content-Disposition: attachment; filename="barcodes-<productCode>-<yyyyMMddHHmmss>.pdf"`
  - Body: raw PDF bytes.
- **Error responses (problem+json):**

| code | HTTP | When |
|------|------|------|
| `Product.NotFound` | 404 | productId invalid. |
| `Barcode.InvalidQuantity` | 400 | quantity outside 1–1000 (caught by validator). |
| `Barcode.CollisionRetryExhausted` | 500 | Could not generate unique NanoID codes after retries. |

- **Tester notes:** In Postman, click **Save Response → Save to a file** to inspect the generated PDF. Codes are 12-char NanoIDs.

---

#### Barcodes — List
- **Method + Path:** `GET /api/admin/barcodes`
- **Auth:** Bearer (Role: SystemAdmin)
- **Query params:**

| Param      | Type            | Notes |
|------------|-----------------|-------|
| productId  | string?         | filter by product |
| status     | BarcodeStatus?  | 1=Available, 2=SellerScanned, 3=TechnicianScanned, 4=Consumed |
| page       | int (default 1) |       |
| pageSize   | int (default 20)|       |

- **Success (200):** `PaginatedResult<AdminBarcodeListItemResponse { id, code, productName, pointValue, status, createdAt }>`

---

#### Scans — List
- **Method + Path:** `GET /api/admin/scans`
- **Auth:** Bearer (Role: SystemAdmin)
- **Query params:**

| Param         | Type            | Notes |
|---------------|-----------------|-------|
| userId        | string?         | filter by scanner |
| productId     | string?         |       |
| scannerRole   | ScannerRole?    | 1=Seller, 2=Technician |
| barcodeStatus | BarcodeStatus?  | 1..4 |
| fromDate      | DateTime? (UTC) |       |
| toDate        | DateTime? (UTC) |       |
| page          | int (default 1) |       |
| pageSize      | int (default 20)|       |

- **Success (200):** `PaginatedResult<AdminScanListItemResponse { id, barcodeCode, productName, productCode, productPointValue, pointsAwarded, scannerRole, barcodeStatus, userName, userMobile, scannedAt, latitude?, longitude? }>`

---

#### Scans — Cancel
- **Method + Path:** `POST /api/admin/scans/{scanId}/cancel`
- **Auth:** Bearer (Role: SystemAdmin)
- **Description:** Cancels a previously-recorded scan and reverses the points + SAR awarded for that scan.
- **Path params:** `scanId`
- **Success (200):** `AdminCancelScanResponse { scanId, barcodeCode, pointsReversed, sarReversed, message }`
- **Error responses:**

| code | HTTP | When |
|------|------|------|
| `Scan.NotFound` | 404 | scanId unknown. |
| `Scan.AlreadyCancelled` | 400 | Scan already cancelled. |
| `Scan.CannotCancelFirstScan` | 400 | First scan cannot be cancelled while a second-role scan exists — cancel the second first. |
| `Redemption.CannotCancelScanWithInsufficientBalance` | 400 | Reversing this scan would push the wallet negative (points already redeemed/held). |

---

### 4.5 Reward Settings

Singleton record managed by admins. Created lazily on first GET.

#### Reward Settings — Get
- **Method + Path:** `GET /api/admin/reward-settings`
- **Auth:** Bearer (Role: SystemAdmin)
- **Success (200):** `RewardSettingsResponse { id, pointsToSarRate, inviterRewardPoints, inviteeRewardPoints, minimumRedemptionPoints }`

---

#### Reward Settings — Update
- **Method + Path:** `PUT /api/admin/reward-settings`
- **Auth:** Bearer (Role: SystemAdmin)
- **Request body:**

| Field                   | Type    | Required | Validation |
|-------------------------|---------|----------|------------|
| pointsToSarRate         | decimal | yes      | > 0 |
| inviterRewardPoints     | decimal | yes      | ≥ 0 |
| inviteeRewardPoints     | decimal | yes      | ≥ 0 |
| minimumRedemptionPoints | decimal | yes      | > 0 |

- **Success (200):** `RewardSettingsResponse`
- **Tester notes:** SAR conversion at scan time uses the **then-current** rate, stored on the WalletTransaction. Updating the rate does NOT retroactively recalculate prior earnings.

---

### 4.6 Analytics / Dashboard

All endpoints under `/api/admin/...` require **Bearer (Role: SystemAdmin)**. None of the endpoints take a request body. **There are 12 analytics endpoints** (1 dashboard + 11 analytics). MEMORY mentioned 11 — confirm with project lead whether `analytics/notifications` is in/out of scope.

#### Dashboard — Summary
- **Method + Path:** `GET /api/admin/dashboard`
- **Success (200):** `AdminDashboardResponse`

```json
{
  "totalShopOwners": 0,
  "totalSellers": 0,
  "totalTechnicians": 0,
  "totalPendingApprovals": 0,
  "totalPointsEarned": 0,
  "totalPointsRedeemed": 0,
  "totalSarRedeemed": 0,
  "totalActiveBarcodes": 0,
  "totalScans": 0,
  "pendingRedemptions": 0,
  "totalInvitations": 0,
  "totalNotificationsSent": 0,
  "totalDeletedAccounts": 0
}
```

---

#### Analytics — Users
- **Method + Path:** `GET /api/admin/analytics/users`
- **Success (200):** `AdminUserAnalyticsResponse { countByUserType[], countByRegistrationStatus[], countByRegion[], registrationTrend[] }` (`registrationTrend` is `MonthlyCount { year, month, count }`).

#### Analytics — Regions
- **Method + Path:** `GET /api/admin/analytics/regions`
- **Success (200):** `AdminRegionAnalyticsResponse { regions[] }` (each region carries cities[] with assigned ApprovalSalesMan + counts).

#### Analytics — Points
- **Method + Path:** `GET /api/admin/analytics/points`
- **Success (200):** `AdminPointsAnalyticsResponse { totalEarned, totalRedeemed, totalBalance, pointsByRegion[], pointsByRepresentative[], pointsTrend[] }`.

#### Analytics — Points (Details list)
- **Method + Path:** `GET /api/admin/analytics/points/details`
- **Query params:**

| Param    | Type                    | Notes |
|----------|-------------------------|-------|
| userId   | string?                 |       |
| regionId | string?                 |       |
| dateFrom | DateTime? (UTC)         |       |
| dateTo   | DateTime? (UTC)         |       |
| type     | WalletTransactionType?  | 1..6 |
| page     | int (default 1)         |       |
| pageSize | int (default 20)        |       |

- **Success (200):** `PaginatedResult<AdminPointsDetailItemResponse { transactionId, userId, userName, userMobile, amount, sarAmount, type, description?, createdAt }>`

#### Analytics — Top Performers
- **Method + Path:** `GET /api/admin/analytics/top-performers?top=10`
- **Query params:** `top` (int, default 10)
- **Success (200):** `TopPerformersResponse { topSellers[], topTechnicians[] }` (each item: `userId, userName, mobileNumber, regionNameAr?, regionNameEn?, totalPointsEarned, totalScans`).

#### Analytics — Inactive Users
- **Method + Path:** `GET /api/admin/analytics/inactive-users`
- **Query params:** `inactiveDays` (int, default 30), `page` (1), `pageSize` (20)
- **Success (200):** `PaginatedResult<InactiveUserItem { userId, userName, mobileNumber, userType, lastScanDate?, daysSinceLastScan }>`

#### Analytics — Barcodes
- **Method + Path:** `GET /api/admin/analytics/barcodes`
- **Success (200):** `BarcodeAnalyticsResponse { totalGenerated, totalAvailable, totalSellerScanned, totalTechnicianScanned, totalConsumed, scanRate, topProductsByBarcodes[] }`

#### Analytics — Redemptions
- **Method + Path:** `GET /api/admin/analytics/redemptions`
- **Success (200):** `RedemptionAnalyticsResponse { countByStatus[], countByMethod[], totalSarRedeemed, averageProcessingDays, pendingCount, redemptionTrend[] }`

#### Analytics — SalesMan Performance
- **Method + Path:** `GET /api/admin/analytics/salesman-performance`
- **Success (200):** `SalesManPerformanceResponse { salesMen[] }` (each: `salesManId, salesManName, mobileNumber, assignedUserCount, approvedUserCount, pendingApprovalCount, totalPointsEarned, cityCount`).

#### Analytics — Revenue
- **Method + Path:** `GET /api/admin/analytics/revenue`
- **Success (200):** `RevenueAnalyticsResponse { totalSarLiability, totalSarHeld, totalSarPaidOut, totalPointsOutstanding, volumeByType[], payoutTrend[] }`

#### Analytics — Invitations
- **Method + Path:** `GET /api/admin/analytics/invitations?top=10`
- **Query params:** `top` (int, default 10)
- **Success (200):** `InvitationAnalyticsResponse { totalInvitationsSent, totalAccepted, totalPending, conversionRate, topInviters[], totalRewardPointsSpent, totalRewardSarSpent, invitationTrend[] }`

#### Analytics — Notifications
- **Method + Path:** `GET /api/admin/analytics/notifications`
- **Success (200):** `NotificationAnalyticsResponse { totalAllTime, totalThisMonth, totalToday, countByType[], totalRead, totalUnread, readRate, adminSentCount, systemTriggeredCount, notificationTrend[] }`

---

### 4.7 Notifications

#### Notifications — Send
- **Method + Path:** `POST /api/admin/notifications/send`
- **Auth:** Bearer (Role: SystemAdmin)
- **Description:** Sends an in-app notification (and FCM push if device tokens exist) to a single user, a role, or all users. The dispatch target is selected by which optional field is provided:
  1. If `targetUserId` is non-empty → send to that user only.
  2. Else if `roleName` is non-empty → send to everyone in that role.
  3. Else → broadcast to **all** non-deleted users.
- **Request body:**

| Field        | Type    | Required | Validation |
|--------------|---------|----------|------------|
| targetUserId | string? | no       | If supplied, must exist |
| roleName     | string? | no       | One of: `ShopOwner`, `Seller`, `Technician`, `SalesMan`, `ZoneManager`, `SystemAdmin` |
| title        | string  | yes      | 1–200 chars |
| body         | string  | yes      | 1–1000 chars |

- **Success (200):** `AdminSendNotificationResponse { sentCount }`
- **Error responses:**

| code | HTTP | When |
|------|------|------|
| `Notification.UserNotFound` | 404 | targetUserId unknown |

- **Tester notes:** Notifications stored in DB are tagged `AdminMessage` (NotificationType=9) when sent through this endpoint. Use `GET /api/admin/notifications` to verify.

---

#### Notifications — History
- **Method + Path:** `GET /api/admin/notifications`
- **Auth:** Bearer (Role: SystemAdmin)
- **Query params:**

| Param        | Type             | Notes |
|--------------|------------------|-------|
| targetUserId | string?          | filter by recipient |
| type         | NotificationType?| 1..9 |
| fromDate     | DateTime? (UTC)  |       |
| toDate       | DateTime? (UTC)  |       |
| page         | int (default 1)  |       |
| pageSize     | int (default 20) |       |

- **Success (200):** `PaginatedResult<AdminNotificationHistoryItem { id, userId, userName, type, title, body, referenceId?, isRead, createdAt }>`

---

### 4.8 Redemption Approvals

The mobile-side flow has three approval levels (SalesMan → ZoneManager → Admin) plus optional final cash handover. Admin endpoints below cover **only the admin-level approve/reject + read**. Earlier approval levels and the cash OTP/handover steps are exposed under `/api/redemption/*` (public side).

#### Redemptions — List
- **Method + Path:** `GET /api/admin/redemptions`
- **Auth:** Bearer (Role: SystemAdmin)
- **Query params:**

| Param     | Type                       | Notes |
|-----------|----------------------------|-------|
| userId    | string?                    | filter by requester |
| method    | RedemptionMethod?          | 1=BankTransfer, 2=Cash |
| status    | RedemptionRequestStatus?   | 1..7 |
| fromDate  | DateTime? (UTC)            |       |
| toDate    | DateTime? (UTC)            |       |
| page      | int (default 1)            |       |
| pageSize  | int (default 20)           |       |

- **Success (200):** `PaginatedResult<AdminRedemptionListItemResponse { id, userFullName, userMobile, method, status, pointsAmount, sarAmount, createdAt }>`

---

#### Redemptions — Get By Id
- **Method + Path:** `GET /api/admin/redemptions/{id}`
- **Success (200):** `AdminRedemptionResponse` — full record including `iban?, accountNumber?, address?, swiftCode?, accountName?, cashOtpExpiresAt?, cashHandoverByName?, cashHandoverAt?, rejectionReason?, rejectedByName?, approvals[]`.
- **Errors:** `Redemption.RequestNotFound` (404)

---

#### Redemptions — Approve
- **Method + Path:** `POST /api/admin/redemptions/approve`
- **Auth:** Bearer (Role: SystemAdmin)
- **Description:** SystemAdmin approves a request that has reached `PendingAdmin`. For BankTransfer: status moves to `AdminApproved` (operations complete the transfer offline). For Cash: status moves to `AdminApproved` and a 6-digit OTP is sent to the user via WhatsApp; the SalesMan later confirms cash handover with that OTP through the public endpoint.
- **Request body:**

| Field                | Type   | Required | Validation |
|----------------------|--------|----------|------------|
| redemptionRequestId  | string | yes      | non-empty (no FluentValidator — service rejects empty) |

- **Success (200):** empty body
- **Error responses:**

| code | HTTP | When |
|------|------|------|
| `Redemption.RequestNotFound` | 404 | id unknown |
| `Redemption.NotPendingApproval` | 400 | Status is not in a state that the admin can approve. |
| `Redemption.NotAuthorizedToApprove` | 403 | Caller is admin but request is not at the admin level (still SM/ZM pending). |
| `Redemption.OtpSendFailed` | 502 | Cash OTP send to WhatsApp failed. |

---

#### Redemptions — Reject
- **Method + Path:** `POST /api/admin/redemptions/reject`
- **Auth:** Bearer (Role: SystemAdmin)
- **Request body:**

| Field                | Type   | Required | Validation |
|----------------------|--------|----------|------------|
| redemptionRequestId  | string | yes      | non-empty |
| rejectionReason      | string | yes      | non-empty (free text shown to user) |

- **Success (200):** empty body
- **Error responses:**

| code | HTTP | When |
|------|------|------|
| `Redemption.RequestNotFound` | 404 | id unknown |
| `Redemption.NotPendingApproval` | 400 | Status not in a rejectable state. |
| `Redemption.NotAuthorizedToApprove` | 403 | Request not at admin tier. |

- **Tester notes:** Rejection refunds the held points back to the user wallet and triggers a `RedemptionRejected` notification.

---

### 4.9 Content Management

Singleton documents shown in the mobile app's Help / About screens.

#### Content — Get Contact Us
- **Method + Path:** `GET /api/admin/content/contact-us`
- **Success (200):** `ContactUsResponse { phone, email, whatsApp, address, workingHours }`

#### Content — Update Contact Us
- **Method + Path:** `PUT /api/admin/content/contact-us`
- **Request body:**

| Field        | Type   | Required | Validation |
|--------------|--------|----------|------------|
| phone        | string | yes      | ≤ 20 chars |
| email        | string | yes      | valid email; ≤ 200 chars |
| whatsApp     | string | yes      | ≤ 20 chars |
| address      | string | yes      | ≤ 500 chars |
| workingHours | string | yes      | ≤ 200 chars |

- **Success (200):** `ContactUsResponse`

---

#### Content — Get About App
- **Method + Path:** `GET /api/admin/content/about-app`
- **Success (200):** `AboutAppResponse { content }`

#### Content — Update About App
- **Method + Path:** `PUT /api/admin/content/about-app`
- **Request body:**

| Field   | Type   | Required | Validation |
|---------|--------|----------|------------|
| content | string | yes      | non-empty |

- **Success (200):** `AboutAppResponse`

---

## 5. Admin Test Flows (end-to-end)

### Flow A — Admin login and user lifecycle
1. `POST /api/admin/auth/login` with `admin` / `Raed@2026` → save `token`, `refreshToken`.
2. `GET /api/admin/users?registrationStatus=1` → see PendingSalesman queue.
3. `POST /api/admin/users/shop-owner` (multipart) — supply mobile, customerCode, cityId, plus full shop data & shopImage. Expect 200.
4. `PATCH /api/admin/users/{id}/toggle-status` → flips `isDisabled`. Toggle again to restore.
5. `PUT /api/admin/users/shop-owner/{id}` — change ownerName.

### Flow B — Barcode batch + scan inspection
1. `POST /api/admin/products` — create a product with `pointValue`.
2. `POST /api/admin/barcodes/generate` body `{ "productId": "...", "quantity": 50 }` → save the PDF.
3. (Mobile side) Seller and Technician each scan one of the printed codes.
4. `GET /api/admin/scans?productId=...` → inspect the two ScanRecords (one Seller, one Technician).
5. `POST /api/admin/scans/{scanId}/cancel` for the second scan → verify points and SAR are reversed in the user's wallet.

### Flow C — Reward settings impact on mobile
1. `GET /api/admin/reward-settings` → note current `pointsToSarRate`.
2. `PUT /api/admin/reward-settings` with a new rate (e.g. 20 points = 1 SAR).
3. (Mobile side) New scans should record SAR using the new rate; older transactions remain at their original rate.
4. Verify `minimumRedemptionPoints` change is enforced on the next public `POST /api/redemption/cash` or `POST /api/redemption/bank-transfer` call.

### Flow D — Admin broadcast notification
1. `POST /api/admin/notifications/send` body `{ "roleName": "Seller", "title": "...", "body": "..." }`.
2. Verify response `sentCount` matches the number of active Sellers.
3. `GET /api/admin/notifications?type=9` → confirm history records exist with `type = AdminMessage (9)`.

### Flow E — Cash redemption admin approval
1. (Mobile + SM + ZM side) A user creates a Cash redemption; SM approves; ZM approves. Status now `PendingAdmin`.
2. `GET /api/admin/redemptions?method=2&status=3` → find the request id.
3. `POST /api/admin/redemptions/approve` `{ "redemptionRequestId": "..." }`. Expect 200.
4. Verify on the user's WhatsApp that a 6-digit OTP arrived.
5. (Public side) The handling SalesMan calls `POST /api/redemption/cash/confirm-handover` with the OTP — final status moves to `Completed`.

---

## 6. Role-Based Access Matrix

All endpoints in this guide require **Bearer (Role: SystemAdmin)** with the following exceptions:

| Endpoint                            | Auth                     |
|-------------------------------------|--------------------------|
| POST /api/admin/auth/login          | Anonymous                |
| POST /api/admin/auth/refresh        | Anonymous (refresh token IS the credential) |
| POST /api/admin/auth/logout         | SystemAdmin              |

---

## 7. Quick Reference (cheat sheet)

| Method  | Path                                         | Purpose                                |
|---------|----------------------------------------------|----------------------------------------|
| POST    | /api/admin/auth/login                        | Username/password sign-in              |
| POST    | /api/admin/auth/refresh                      | Rotate refresh token                   |
| POST    | /api/admin/auth/logout                       | Revoke refresh token                   |
| POST    | /api/admin/users/salesman                    | Add SalesMan                           |
| POST    | /api/admin/users/zone-manager                | Add ZoneManager                        |
| POST    | /api/admin/users/shop-owner                  | Add ShopOwner (multipart)              |
| POST    | /api/admin/users/seller                      | Add Seller (multipart)                 |
| POST    | /api/admin/users/technician                  | Add Technician                         |
| GET     | /api/admin/users                             | List/search users                      |
| PATCH   | /api/admin/users/{id}/toggle-status          | Disable / re-enable user               |
| PUT     | /api/admin/users/salesman/{id}               | Edit SalesMan name                     |
| PUT     | /api/admin/users/zone-manager/{id}           | Edit ZoneManager name                  |
| PUT     | /api/admin/users/shop-owner/{id}             | Edit ShopOwner ownerName               |
| PUT     | /api/admin/users/seller/{id}                 | Edit Seller name                       |
| PUT     | /api/admin/users/technician/{id}             | Edit Technician name                   |
| POST    | /api/admin/users/cities/reassign             | Reassign cities to another SalesMan    |
| POST    | /api/admin/users/regions/reassign            | Reassign region to another ZoneManager |
| DELETE  | /api/admin/users/salesman/{id}               | Delete SalesMan + reassign cities      |
| DELETE  | /api/admin/users/zone-manager/{id}           | Delete ZoneManager + replacement       |
| POST    | /api/admin/products                          | Add product                            |
| GET     | /api/admin/products                          | List products                          |
| GET     | /api/admin/products/categories               | List categories                        |
| GET     | /api/admin/products/{id}                     | Get product                            |
| PUT     | /api/admin/products/{id}                     | Edit product                           |
| DELETE  | /api/admin/products/{id}                     | Delete product (blocked if has barcodes)|
| POST    | /api/admin/barcodes/generate                 | Generate barcodes — returns PDF        |
| GET     | /api/admin/barcodes                          | List barcodes                          |
| GET     | /api/admin/scans                             | List scans                             |
| POST    | /api/admin/scans/{scanId}/cancel             | Cancel scan + reverse points           |
| GET     | /api/admin/reward-settings                   | Get reward settings                    |
| PUT     | /api/admin/reward-settings                   | Update reward settings                 |
| GET     | /api/admin/dashboard                         | Dashboard summary                      |
| GET     | /api/admin/analytics/users                   | User analytics                         |
| GET     | /api/admin/analytics/regions                 | Region analytics                       |
| GET     | /api/admin/analytics/points                  | Points totals                          |
| GET     | /api/admin/analytics/points/details          | Points ledger (paginated)              |
| GET     | /api/admin/analytics/top-performers          | Top sellers + technicians              |
| GET     | /api/admin/analytics/inactive-users          | Inactive users                         |
| GET     | /api/admin/analytics/barcodes                | Barcode analytics                      |
| GET     | /api/admin/analytics/redemptions             | Redemption analytics                   |
| GET     | /api/admin/analytics/salesman-performance    | SalesMan analytics                     |
| GET     | /api/admin/analytics/revenue                 | Revenue / SAR liability                |
| GET     | /api/admin/analytics/invitations             | Invitation analytics                   |
| GET     | /api/admin/analytics/notifications           | Notification analytics                 |
| POST    | /api/admin/notifications/send                | Send to user / role / all              |
| GET     | /api/admin/notifications                     | Notification history                   |
| GET     | /api/admin/redemptions                       | List redemptions                       |
| GET     | /api/admin/redemptions/{id}                  | Get redemption                         |
| POST    | /api/admin/redemptions/approve               | Admin approve                          |
| POST    | /api/admin/redemptions/reject                | Admin reject                           |
| GET     | /api/admin/content/contact-us                | Get Contact Us                         |
| PUT     | /api/admin/content/contact-us                | Update Contact Us                      |
| GET     | /api/admin/content/about-app                 | Get About App                          |
| PUT     | /api/admin/content/about-app                 | Update About App                       |

**Total: 56 endpoints** (3 auth + 19 user-management + 6 products + 4 barcodes/scans + 2 reward-settings + 12 dashboard/analytics + 2 notifications + 4 redemptions + 4 content).
