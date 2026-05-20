# AL-Raed RewardProgram — Mobile App API Tester Guide

**Last Updated:** 2026-05-04
**Scope:** Public (mobile app) endpoints only. Admin endpoints are documented separately.

---

## 1. Environments & Base URLs

| Environment | Base URL |
|---|---|
| Development (local) | `https://localhost:44315` |
| Staging | `https://staging.raedrewardapp.com` *(verify exact host before testing)* |
| UAT | `https://uat.raedrewardapp.com` |
| Production | TBD |

All endpoints below are relative to the chosen base URL.

---

## 2. Authentication Model

- **JWT Bearer token** — required for all protected endpoints. Send in header: `Authorization: Bearer <accessToken>`.
- **Refresh token flow** — use `POST /api/auth/refresh-token` with the refresh token to obtain a new access token before expiry. Refresh tokens currently live 365 days.
- **OTP via WhatsApp (Twilio Verify)** — registration and login both use WhatsApp OTP delivery.
  - **Mock OTP `123456`** is accepted in Development and UAT (Twilio Verify only). Staging hits real WhatsApp.
  - **Cash redemption OTP** is server-generated and hash-compared. **No mock value** works for it; testers must read the actual OTP from the WhatsApp message.
- **Roles seen on the public API:** `ShopOwner`, `Seller`, `Technician`, `SalesMan` (SM), `ZoneManager` (ZM). `SystemAdmin` is admin-API only and out of scope here.

---

## 3. Common Conventions

### Result envelope — success
Successful responses return the action's DTO directly (no wrapper). Examples:
```json
{ "pinId": "VEa1bc...", "maskedMobileNumber": "05****0001" }
```

### Result envelope — error (ProblemDetails RFC 7807)
Failures return an HTTP error status with this body shape:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "رمز التحقق غير صحيح",
  "extensions": { "code": "Auth.OtpInvalid" }
}
```
Use the `extensions.code` (or top-level `code`) to identify the error reliably; `detail` is Arabic user-facing text.

### Pagination — `PaginatedResult<T>`
Endpoints that return lists wrap items in this generic shape:
```json
{
  "items": [ /* array of T */ ],
  "totalCount": 134,
  "page": 1,
  "pageSize": 20,
  "totalPages": 7,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

### Mobile masking
Server-side masking format: `05****5678` (first 2 + last 4 visible).

### Date format
All timestamps are **ISO-8601 UTC** (`2026-05-04T13:25:11.000Z`).

---

## 4. Validation Rules (cross-cutting)

These rules are enforced by FluentValidation. Errors return HTTP 400 with the field-specific message.

| Field | Rule |
|---|---|
| **Name / OwnerName** (any user type) | Required. 3–100 chars. Letters + spaces only — regex `^[\p{L}\s]+$`. No digits. |
| **MobileNumber** | Required. Regex `^(05\d{8}\|\+\d{10,15})$`. Either Saudi local `05XXXXXXXX` (10 digits starting `05`) or international `+CC...` (10–15 digits after `+`). |
| **OTP** | Required. Exactly 6 digits — regex `^\d{6}$`. |
| **PinId** | Required, non-empty (Twilio Verify session ID returned by send-otp). |
| **VerificationToken** | Required, non-empty (returned by verify-registration-otp; used to register). |
| **CustomerCode** | Required. Max 50 chars. Must already exist in ErpCustomer table. |
| **StoreName** | Required (when shop data is provided). 5–150 chars. |
| **VAT** | Required. Exactly 15 digits. Must start AND end with `3` — regex `^3\d{13}3$`. |
| **CRN** | Required. Exactly 10 digits — regex `^\d{10}$`. |
| **ShortAddress** | Required. 4 letters + 4 digits — regex `^[A-Za-z]{4}\d{4}$` (e.g. `ABCD1234`). |
| **ShopImage** | Required for ShopOwner; required for first Seller of a CustomerCode. Allowed types: `.jpg`, `.jpeg`, `.png`. Max size **5 MB**. |
| **CityId** | Required, non-empty. |
| **PostalCode** | Required. Exactly 5 digits — regex `^\d{5}$`. |
| **District** | Required. Max 100 chars (free text). |
| **NationalAddress.BuildingNumber** | Integer 1000–9999 inclusive. |
| **NationalAddress.SubNumber** | Integer 1000–9999 inclusive. |
| **NationalAddress.Street** | Required. 3–100 chars. |
| **InvitationCode** | Optional. 8-char NanoID (case-sensitive). |
| **FcmToken** | Required. Max 512 chars. |
| **BarcodeCode** (scan) | Required. Exactly 12 chars. |
| **Iban** (bank redemption) | Regex `^SA\d{22}$` (Saudi IBAN: `SA` + 22 digits, total 24 chars). |
| **SwiftCode** (bank redemption) | Regex `^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$` (8 or 11 chars). |
| **Reject reason** | Required. Max 500 chars. |

Validation happens BEFORE OTP/business-rule checks so testers won't waste OTPs on bad payloads.

---

## 5. Endpoints — by feature

Enums referenced below:

- **UserType:** `1=ShopOwner, 2=Seller, 3=Technician, 4=SalesMan, 5=ZoneManager, 6=SystemAdmin`
- **RegistrationStatus:** `1=PendingSalesman, 2=PendingZoneManager, 3=Approved, 4=Rejected`
- **WalletTransactionType:** `1=Earned, 2=Redeemed, 3=Cancelled, 4=Expired, 5=Refunded, 6=InvitationReward`
- **BarcodeStatus:** `1=Available, 2=SellerScanned, 3=TechnicianScanned, 4=Consumed`
- **ScannerRole:** `1=Seller, 2=Technician`
- **NotificationType:** `1=RegistrationApproved, 2=RegistrationRejected, 3=PointsEarned, 4=RedemptionCreated, 5=RedemptionApproved, 6=RedemptionRejected, 7=RedemptionCompleted, 8=InvitationReward, 9=AdminMessage`
- **RedemptionMethod:** `1=BankTransfer, 2=Cash`
- **RedemptionRequestStatus:** `1=PendingSalesMan, 2=PendingZoneManager, 3=PendingAdmin, 4=AdminApproved, 5=Completed, 6=Rejected, 7=Cancelled`
- **ApprovalListStatusFilter / RedemptionListStatusFilter:** `0=All, 1=Pending, 2=Approved, 3=Rejected`

---

### 5.1 Auth

#### Auth — Send registration OTP
- **Method + Path:** `POST /api/auth/send-otp`
- **Auth:** None
- **Description:** Sends the registration OTP via WhatsApp to a mobile number that has not yet been registered.
- **Request body:**
  | Field | Type | Required | Validation / notes |
  |---|---|---|---|
  | mobileNumber | string | yes | `05XXXXXXXX` or `+CC...` |
- **Success (200):**
  ```json
  { "pinId": "VEabc123...", "maskedMobileNumber": "05****0001" }
  ```
- **Error responses:**
  | Code | HTTP | When |
  |---|---|---|
  | `Auth.MobileAlreadyRegistered` | 409 | Mobile already has an account |
  | `Auth.OtpResendTooSoon` | 429 | Last OTP sent <30 s ago |
  | `Auth.OtpSendFailed` | 500 | Twilio failure |
- **Tester notes:** Save `pinId` from response; you will need it for the verify step.

#### Auth — Resend OTP
- **Method + Path:** `POST /api/auth/resend-otp`
- **Auth:** None
- **Description:** Resends the registration OTP to the same mobile (same 30-second cooldown applies).
- **Request body:** `{ "mobileNumber": "0555000001" }`
- **Success (200):** Same shape as send-otp.
- **Error responses:**
  | Code | HTTP | When |
  |---|---|---|
  | `Auth.OtpResendTooSoon` | 429 | <30 s since last send |
  | `Auth.TooManyOtpRequests` | 429 | Daily / hourly limit hit |
  | `Auth.OtpSendFailed` | 500 | Twilio failure |

#### Auth — Verify registration OTP
- **Method + Path:** `POST /api/auth/verify-registration-otp`
- **Auth:** None
- **Description:** Confirms the OTP and returns a short-lived `verificationToken` to be used in the matching `register/...` endpoint.
- **Request body:**
  | Field | Type | Required | Validation |
  |---|---|---|---|
  | pinId | string | yes | non-empty |
  | otp | string | yes | exactly 6 digits |
  | mobileNumber | string | yes | matches the one used in send-otp |
- **Success (200):**
  ```json
  { "verificationToken": "eyJhbGciOi...", "maskedMobileNumber": "05****0001" }
  ```
- **Error responses:**
  | Code | HTTP | When |
  |---|---|---|
  | `Auth.OtpInvalid` | 400 | Wrong OTP |
  | `Auth.OtpExpired` | 400 | OTP older than its TTL |
  | `Auth.MaxVerificationAttempts` | 400 | Too many wrong tries |
  | `Auth.MobileMismatch` | 400 | mobileNumber differs from send-otp |
- **Tester notes:** Mock OTP `123456` works in Dev/UAT. Token expires fast — register immediately.

#### Auth — Register Shop Owner
- **Method + Path:** `POST /api/auth/register/shop-owner`
- **Auth:** None
- **Content-Type:** `multipart/form-data`
- **Description:** Self-registers a ShopOwner. ShopOwner ALWAYS supplies the full shop data — overwrites any prior Seller-supplied data for the same CustomerCode.
- **Form-data fields:**
  | Field | Type | Required | Validation / notes |
  |---|---|---|---|
  | verificationToken | string | yes | from verify-registration-otp |
  | customerCode | string | yes | must exist in ErpCustomer |
  | ownerName | string | yes | letters only, 3–100 |
  | mobileNumber | string | yes | matches verified mobile |
  | cityId | string | yes | |
  | storeName | string | yes | 5–150 |
  | vat | string | yes | 15 digits, starts/ends with 3 |
  | crn | string | yes | 10 digits |
  | shortAddress | string | yes | 4 letters + 4 digits |
  | shopImage | file | yes | jpg/jpeg/png, ≤5 MB |
  | nationalAddress.buildingNumber | int | yes | 1000–9999 |
  | nationalAddress.street | string | yes | 3–100 |
  | nationalAddress.postalCode | string | yes | 5 digits |
  | nationalAddress.subNumber | int | yes | 1000–9999 |
  | nationalAddress.district | string | yes | max 100 |
  | invitationCode | string | no | optional 8-char code |
- **Success (200):**
  ```json
  { "userId": "guid", "message": "...", "invitationBonusPoints": 50 }
  ```
- **Error responses:**
  | Code | HTTP | When |
  |---|---|---|
  | `Auth.VerificationTokenInvalid` | 400 | bad/forged token |
  | `Auth.VerificationTokenExpired` | 400 | token too old |
  | `Auth.MobileAlreadyRegistered` | 409 | mobile already used |
  | `Auth.CustomerCodeNotFound` | 400 | code not in ErpCustomer |
  | `Auth.CustomerCodeAlreadyOwned` | 409 | another ShopOwner already owns this CustomerCode |
  | `Auth.CityNotFound` | 400 | unknown city |
  | `Auth.NoApprovalSalesMan` | 400 | city has no SalesMan assigned |
  | `Auth.ShopDataRequired` | 400 | missing shop fields |
  | `ShopData.VatAlreadyExists` | 409 | VAT clash with another shop |
  | `ShopData.CrnAlreadyExists` | 409 | CRN clash |
  | `ShopData.ShortAddressAlreadyExists` | 409 | ShortAddress clash |
  | `Invitation.InvalidCode` | 400 | unknown invitation code |
  | `Invitation.SelfInvitation` | 400 | own code (not applicable on first-time reg) |
  | `Invitation.InviterNotApproved` | 400 | inviter not yet Approved |
  | `File.InvalidImageType` / `File.ImageTooLarge` / `File.ImageUploadFailed` | 400/500 | image issue |
  | `Auth.CreateUserFailed` | 500 | identity create failed |
- **Tester notes:** User created in `PendingSalesman`. Cannot login until SalesMan + ZoneManager both approve.

#### Auth — Register Seller
- **Method + Path:** `POST /api/auth/register/seller`
- **Auth:** None
- **Content-Type:** `multipart/form-data`
- **Description:** Self-registers a Seller for an existing CustomerCode. **First Seller** (no prior ShopData and no ShopOwner) MUST supply shop fields. Subsequent Sellers may omit them and the server uses existing ShopData.
- **Form-data fields:** same shape as ShopOwner BUT shop fields (storeName, vat, crn, shortAddress, shopImage, cityId, nationalAddress) are **conditional** — only required if any of them is provided OR if no ShopData exists yet. Replace `ownerName` with `name`.
  | Field | Type | Required | Validation / notes |
  |---|---|---|---|
  | verificationToken | string | yes | |
  | name | string | yes | letters only, 3–100 |
  | mobileNumber | string | yes | |
  | customerCode | string | yes | |
  | storeName, vat, crn, shortAddress, shopImage, cityId, nationalAddress.* | varies | conditional | required when shop data is being created |
  | invitationCode | string | no | |
- **Success (200):** Same as ShopOwner.
- **Error responses:** Same set as ShopOwner registration. Note the conditional-validation behavior — sending only some shop fields triggers all of them.
- **Tester notes:**
  - To test first-Seller-creates-ShopData: pick a CustomerCode that has no ShopOwner and no Seller registered yet, supply full shop fields.
  - To test subsequent Seller: pick a CustomerCode that already has ShopData, send only `verificationToken`, `name`, `mobileNumber`, `customerCode`.

#### Auth — Register Technician
- **Method + Path:** `POST /api/auth/register/technician`
- **Auth:** None
- **Content-Type:** `application/json`
- **Description:** Self-registers a field Technician. No CustomerCode, no shop data.
- **Request body:**
  | Field | Type | Required | Validation |
  |---|---|---|---|
  | verificationToken | string | yes | |
  | name | string | yes | letters only, 3–100 |
  | mobileNumber | string | yes | |
  | cityId | string | yes | |
  | postalCode | string | yes | 5 digits |
  | district | string | yes | max 100 |
  | invitationCode | string | no | |
- **Success (200):** Same `RegisterResponse` shape.
- **Error responses:** subset of ShopOwner errors (no shop/CustomerCode errors, no ShopData errors, no image errors).

#### Auth — Login (request OTP)
- **Method + Path:** `POST /api/auth/login`
- **Auth:** None
- **Description:** Step 1 of login — sends a login OTP via WhatsApp to a registered, approved user.
- **Request body:** `{ "mobileNumber": "0555000001" }`
- **Success (200):** `{ "pinId": "VE...", "maskedMobileNumber": "05****0001" }`
- **Error responses:**
  | Code | HTTP | When |
  |---|---|---|
  | `Auth.UserNotFound` | 404 | mobile not registered |
  | `Auth.UserRejected` | 403 | RegistrationStatus = Rejected |
  | `Auth.UserNotApproved` | 403 | still Pending |
  | `Auth.UserDisabled` | 403 | admin disabled the account |
  | `Auth.OtpResendTooSoon` | 429 | cooldown |
  | `Auth.OtpSendFailed` | 500 | Twilio failure |

#### Auth — Verify login OTP (issue tokens)
- **Method + Path:** `POST /api/auth/login/verify`
- **Auth:** None
- **Description:** Step 2 of login — exchanges OTP for JWT + refresh token.
- **Request body:**
  | Field | Type | Required | Validation |
  |---|---|---|---|
  | pinId | string | yes | from login response |
  | otp | string | yes | 6 digits |
- **Success (200):**
  ```json
  {
    "token": "eyJhbGc...",
    "refreshToken": "abc123...",
    "expiresIn": 3600,
    "refreshTokenExpiration": "2027-05-04T...",
    "user": {
      "id": "guid", "name": "...", "mobileNumber": "0555000001",
      "userType": 2, "registrationStatus": 3
    }
  }
  ```
- **Error responses:**
  | Code | HTTP | When |
  |---|---|---|
  | `Auth.OtpInvalid` | 400 | wrong code |
  | `Auth.OtpExpired` | 400 | timed out |
  | `Auth.MaxVerificationAttempts` | 400 | too many tries |
  | `Auth.UserRejected` / `Auth.UserNotApproved` / `Auth.UserDisabled` | 403 | account state |

#### Auth — Refresh token
- **Method + Path:** `POST /api/auth/refresh-token`
- **Auth:** None (`[AllowAnonymous]`) — but body must contain a valid refresh token.
- **Request body:** `{ "refreshToken": "..." }`
- **Success (200):** Same `AuthResponse` shape as login/verify.
- **Error responses:**
  | Code | HTTP | When |
  |---|---|---|
  | `Auth.InvalidRefreshToken` | 401 | not in DB / forged |
  | `Auth.RefreshTokenExpired` | 401 | past expiration |
  | `Auth.RefreshTokenRevoked` | 401 | revoked or reused (reuse triggers full revocation chain) |
- **Tester notes:** Old refresh token is rotated out; always use the newly returned token next call.

#### Auth — Revoke (logout)
- **Method + Path:** `POST /api/auth/revoke-token`
- **Auth:** Bearer (any role)
- **Request body:** `{ "refreshToken": "..." }`
- **Success (200):** `{ "message": "تم تسجيل الخروج بنجاح" }`
- **Error responses:** `Auth.InvalidRefreshToken` 401.

---

### 5.2 Lookup

#### Lookup — Regions list
- **Method + Path:** `GET /api/lookup/regions`
- **Auth:** None
- **Success (200):** `RegionResponse[]` — `[{ "id", "nameAr", "nameEn" }, ...]`

#### Lookup — Cities by region
- **Method + Path:** `GET /api/lookup/regions/{regionId}/cities`
- **Auth:** None
- **Success (200):** `CityResponse[]` — `[{ "id", "nameAr", "nameEn", "regionId" }, ...]`
- **Errors:** `Lookup.RegionNotFound` 404.

#### Lookup — All cities
- **Method + Path:** `GET /api/lookup/cities`
- **Auth:** None
- **Success (200):** `CityResponse[]`.

#### Lookup — Customer / shop-data status
- **Method + Path:** `GET /api/lookup/customer/{customerCode}/shop-data-status`
- **Auth:** None
- **Description:** Check if a CustomerCode exists and whether ShopData has been created for it (drives whether Seller registration must include shop fields).
- **Success (200):**
  ```json
  { "customerCodeExists": true, "customerName": "ABC Trading", "shopDataExists": false }
  ```
- **Errors:** `Lookup.CustomerCodeNotFound` 404 (only when `customerCodeExists=false` is signaled differently — service decides).

---

### 5.3 Profile

All endpoints under `/api/profile` require Bearer token. Authorized roles: `Seller, Technician, ShopOwner, SalesMan, ZoneManager`.

#### Profile — Get profile
- **Method + Path:** `GET /api/profile`
- **Auth:** Bearer (Seller / Technician / ShopOwner / SalesMan / ZoneManager)
- **Success (200):** `ProfileResponse`
  ```json
  {
    "id": "guid", "name": "...", "mobileNumber": "0555000001",
    "userType": 2, "profileImageUrl": "/uploads/profiles/abc.jpg",
    "points": 240.0, "cityName": "الرياض", "district": "حي السلام",
    "street": "...", "buildingNumber": 1234, "postalCode": "12345", "subNumber": 5678
  }
  ```
- **Errors:** `Auth.UserNotFound` 404.

#### Profile — Update photo
- **Method + Path:** `PUT /api/profile/photo`
- **Auth:** Bearer (same roles as above)
- **Content-Type:** `multipart/form-data`
- **Form-data:** `photo` (file) — jpg/jpeg/png/webp, ≤5 MB.
- **Success (200):** `{ "profileImageUrl": "/uploads/profiles/xyz.jpg" }`
- **Errors:** `Profile.InvalidImageType` 400, `Profile.ImageTooLarge` 400, `File.ImageUploadFailed` 500.

#### Profile — Delete account
- **Method + Path:** `DELETE /api/profile`
- **Auth:** Bearer (Seller / Technician / ShopOwner only — SM/ZM cannot self-delete)
- **Success (200):** `{ "message": "تم حذف الحساب بنجاح" }`
- **Errors:** `Profile.HasPendingRedemptions` 400.
- **Tester notes:** Soft-delete; account becomes inaccessible.

---

### 5.4 Wallet

Roles: `Seller, Technician`. ShopOwner cannot earn/redeem and is excluded from wallet endpoints.

#### Wallet — Get balance
- **Method + Path:** `GET /api/wallet/balance`
- **Auth:** Bearer (Seller / Technician)
- **Success (200):** `{ "balance": 240.0, "sarBalance": 24.00 }`

#### Wallet — Transactions list (paginated)
- **Method + Path:** `GET /api/wallet/transactions`
- **Auth:** Bearer (Seller / Technician)
- **Query params:**
  | Param | Type | Required | Notes |
  |---|---|---|---|
  | type | int (WalletTransactionType) | no | filter |
  | fromDate | datetime | no | inclusive |
  | toDate | datetime | no | inclusive |
  | page | int | no | default 1 |
  | pageSize | int | no | default 20 |
- **Success (200):** `PaginatedResult<WalletTransactionResponse>` — items have `{ id, amount, sarRate, sarAmount, type, description, createdAt }`.

---

### 5.5 Scan

Roles: `Seller, Technician`.

#### Scan — Scan a barcode
- **Method + Path:** `POST /api/scan`
- **Auth:** Bearer (Seller / Technician)
- **Description:** Records a scan, awards points, updates wallet. Each role can scan a given barcode at most once. Two complementary scans (Seller + Technician) move the barcode to `Consumed`.
- **Request body:**
  | Field | Type | Required | Validation |
  |---|---|---|---|
  | barcodeCode | string | yes | exactly 12 chars |
  | latitude | double | no | optional GPS |
  | longitude | double | no | optional GPS |
- **Success (200):**
  ```json
  { "productName": "...", "pointsAwarded": 10.0, "newBalance": 250.0, "message": "..." }
  ```
- **Error responses:**
  | Code | HTTP | When |
  |---|---|---|
  | `Scan.UnauthorizedRole` | 403 | role not Seller/Technician |
  | `Scan.UserNotApproved` | 403 | account disabled or not Approved |
  | `Barcode.NotFound` | 404 | code unknown |
  | `Barcode.AlreadyScanned` | 409 | this role already scanned this barcode |
  | `Barcode.Consumed` | 400 | barcode fully consumed |
  | `Barcode.ConcurrencyConflict` | 409 | retry — race condition |
- **Tester notes:** ShopOwner is blocked at the role check (403). Generate barcodes via the admin API first.

#### Scan — Scan history (paginated)
- **Method + Path:** `GET /api/scan/history`
- **Auth:** Bearer (Seller / Technician)
- **Query:** `fromDate`, `toDate`, `page` (1), `pageSize` (20)
- **Success (200):** `PaginatedResult<ScanHistoryItemResponse>` — items have `{ id, barcodeCode, productName, productCode, productPointValue, pointsAwarded, scannerRole, barcodeStatus, scannedAt, latitude, longitude }`.

---

### 5.6 Redemption

Roles: `Seller, Technician`.

#### Redemption — Create request
- **Method + Path:** `POST /api/redemption/request`
- **Auth:** Bearer (Seller / Technician)
- **Request body:**
  | Field | Type | Required | Validation |
  |---|---|---|---|
  | method | int (RedemptionMethod) | yes | 1=BankTransfer, 2=Cash |
  | pointsAmount | decimal | yes | > 0; service also enforces `>= MinimumRedemptionPoints`; SAR conversion must be integer |
  | iban | string | only for BankTransfer | regex `^SA\d{22}$` |
  | accountNumber | string | only for BankTransfer | max 50 |
  | address | string | only for BankTransfer | max 200 |
  | swiftCode | string | only for BankTransfer | 8 or 11 chars uppercase/digits |
  | accountName | string | only for BankTransfer | max 200 |
- **Success (200):** `RedemptionRequestResponse` (see history section for shape).
- **Error responses:**
  | Code | HTTP | When |
  |---|---|---|
  | `Redemption.InsufficientBalance` | 400 | available balance < amount |
  | `Redemption.BelowMinimum` | 400 | < MinimumRedemptionPoints (admin setting) |
  | `Redemption.NotIntegerSar` | 400 | resulting SAR amount not whole |
  | `Redemption.AlreadyHasPendingRequest` | 409 | another active request exists |
  | `Redemption.UserNotApproved` | 403 | account state |
- **Tester notes:** Only one active request per user. Active = anything not Completed/Rejected/Cancelled.

#### Redemption — Get active request
- **Method + Path:** `GET /api/redemption/active`
- **Auth:** Bearer (Seller / Technician)
- **Success (200):** `RedemptionRequestResponse` or `null`.

#### Redemption — History (paginated)
- **Method + Path:** `GET /api/redemption/history`
- **Auth:** Bearer (Seller / Technician)
- **Query:** `page` (1), `pageSize` (20)
- **Success (200):** `PaginatedResult<RedemptionRequestResponse>` — items have `{ id, method, status, pointsAmount, sarRate, sarAmount, iban?, accountNumber?, address?, swiftCode?, accountName?, cashOtpExpiresAt?, rejectionReason?, createdAt }`.

#### Redemption — Available balance
- **Method + Path:** `GET /api/redemption/available-balance`
- **Auth:** Bearer (Seller / Technician)
- **Success (200):**
  ```json
  { "totalBalance": 500.0, "heldBalance": 100.0, "availableBalance": 400.0, "availableSarBalance": 40.00 }
  ```
- **Tester notes:** "Held" = points reserved by a pending redemption.

#### Redemption — Resend cash OTP
- **Method + Path:** `POST /api/redemption/resend-cash-otp`
- **Auth:** Bearer (Seller / Technician)
- **Description:** Resends the WhatsApp OTP used during cash handover, when the user has an active Cash request awaiting handover.
- **Success (200):** empty.
- **Error responses:**
  | Code | HTTP | When |
  |---|---|---|
  | `Redemption.NoActiveCashRequest` | 404 | no Cash request waiting handover |
  | `Redemption.ResendCooldown` | 429 | cooldown not elapsed |
  | `Redemption.OtpSendFailed` | 502 | Twilio failure |

---

### 5.7 Approval (Registration)

Roles: `SalesMan, ZoneManager`. Two-tier approval: SalesMan approves first, ZoneManager second.

#### Approval — Pending requests
- **Method + Path:** `GET /api/approvals/pending`
- **Auth:** Bearer (SalesMan / ZoneManager)
- **Query:** `search` (free text against name/mobile), `page` (1), `pageSize` (20)
- **Success (200):** `PaginatedResult<PendingUserResponse>` — items contain user basics + ERP customer + shop data + resolved location names.
- **Errors:** 403 if approver is not in scope for any pending users.

#### Approval — List with status filter
- **Method + Path:** `GET /api/approvals/list`
- **Auth:** Bearer (SalesMan / ZoneManager)
- **Query:**
  | Param | Type | Default | Notes |
  |---|---|---|---|
  | status | int (ApprovalListStatusFilter) | 0 (All) | 0/1/2/3 |
  | search | string | — | name/mobile |
  | page | int | 1 | |
  | pageSize | int | 20 | |
- **Success (200):** `PaginatedResult<ApprovalListItem>` — `{ userId, name, mobileNumber, userType, storeName, reviewStatus, currentStatus, activityAt, rejectionReason }`.

#### Approval — Approve
- **Method + Path:** `POST /api/approvals/approve`
- **Auth:** Bearer (SalesMan / ZoneManager)
- **Request body:** `{ "userId": "guid" }` (required, non-empty)
- **Success (200):** `{ "message": "تمت الموافقة بنجاح" }`
- **Error responses:**
  | Code | HTTP | When |
  |---|---|---|
  | `Approval.UserNotPendingApproval` | 400 | user not in approver's queue |
  | `Approval.NotAuthorizedToApprove` | 403 | wrong role/region/city |
  | `Approval.NoZoneManagerForRegion` | 400 | region missing ZM (escalation impossible) |
  | `Approval.NoSalesManAssigned` | 400 | city has no SM |
  | `Approval.UserAddressMissing` | 400 | corrupted user record |
  | `Auth.UserNotFound` | 404 | bad userId |

#### Approval — Reject
- **Method + Path:** `POST /api/approvals/reject`
- **Auth:** Bearer (SalesMan / ZoneManager)
- **Request body:**
  | Field | Type | Required | Validation |
  |---|---|---|---|
  | userId | string | yes | non-empty |
  | reason | string | yes | 1–500 chars |
- **Success (200):** `{ "message": "تم الرفض بنجاح" }`
- **Error responses:** Same set as Approve.

---

### 5.8 Redemption Approval

Roles: `SalesMan, ZoneManager, SystemAdmin`. Three-tier flow: SM → ZM → Admin → Cash handover OTP.

#### RedemptionApproval — Pending
- **Method + Path:** `GET /api/redemption-approvals/pending`
- **Auth:** Bearer (SM / ZM / SystemAdmin)
- **Query:** `search`, `page` (1), `pageSize` (20)
- **Success (200):** `PaginatedResult<PendingRedemptionResponse>`.

#### RedemptionApproval — List
- **Method + Path:** `GET /api/redemption-approvals/list`
- **Auth:** Bearer (SM / ZM / SystemAdmin)
- **Query:** `status` (RedemptionListStatusFilter, default 0), `search`, `page`, `pageSize`.
- **Success (200):** `PaginatedResult<RedemptionListItem>`.

#### RedemptionApproval — Approve
- **Method + Path:** `POST /api/redemption-approvals/approve`
- **Auth:** Bearer (SM / ZM / SystemAdmin)
- **Request body:** `{ "redemptionRequestId": "guid" }`
- **Success (200):** empty.
- **Error responses:**
  | Code | HTTP | When |
  |---|---|---|
  | `Redemption.RequestNotFound` | 404 | bad id |
  | `Redemption.NotPendingApproval` | 400 | wrong status |
  | `Redemption.NotAuthorizedToApprove` | 403 | wrong tier/scope |

#### RedemptionApproval — Reject
- **Method + Path:** `POST /api/redemption-approvals/reject`
- **Auth:** Bearer (SM / ZM / SystemAdmin)
- **Request body:**
  | Field | Type | Required | Notes |
  |---|---|---|---|
  | redemptionRequestId | string | yes | |
  | rejectionReason | string | yes | non-empty |
- **Success (200):** empty.
- **Errors:** Same as Approve.

#### RedemptionApproval — Confirm cash handover
- **Method + Path:** `POST /api/redemption-approvals/confirm-cash`
- **Auth:** Bearer (SM / ZM / SystemAdmin) — the agent physically handing over cash.
- **Request body:**
  | Field | Type | Required | Notes |
  |---|---|---|---|
  | redemptionRequestId | string | yes | |
  | otp | string | yes | 6-digit OTP read from the user's WhatsApp |
- **Success (200):** empty.
- **Error responses:**
  | Code | HTTP | When |
  |---|---|---|
  | `Redemption.RequestNotFound` | 404 | bad id |
  | `Redemption.NotInCashHandoverState` | 400 | request not in awaiting-handover state |
  | `Redemption.InvalidOtp` | 400 | wrong OTP |
  | `Redemption.OtpExpired` | 400 | OTP timed out |
  | `Redemption.OtpMaxAttemptsExceeded` | 429 | too many wrong tries |
- **Tester notes:** No mock OTP for cash handover — read it from WhatsApp on the user's phone (or test logs in Dev).

---

### 5.9 Dashboard

#### Dashboard — Seller / Technician
- **Method + Path:** `GET /api/dashboard`
- **Auth:** Bearer (Seller / Technician)
- **Success (200):** `DashboardResponse`
  ```json
  {
    "userName": "...",
    "points": 250.0,
    "sarBalance": 25.00,
    "minimumRedemptionPoints": 1000,
    "pointsToRedeem": 750,
    "canRedeem": false,
    "recentTransactions": [
      { "id": "...", "amount": 10.0, "sarAmount": 1.00, "type": 1, "description": "...", "createdAt": "..." }
    ]
  }
  ```
- **Errors:** `Auth.UserNotFound` 404.

#### Dashboard — Shop Owner
- **Method + Path:** `GET /api/dashboard/shop-owner`
- **Auth:** Bearer (ShopOwner only)
- **Success (200):** `ShopOwnerDashboardResponse` — `{ userName, profileImageUrl, shop: { customerCode, storeName, vat, crn, shopImageUrl, shortAddress, district, cityName, street, postalCode, buildingNumber, subNumber }, sellers: [{ id, name, mobileNumber, joinedAt }], totalSellers }`
- **Errors:** `Auth.UserNotFound` 404.
- **Tester notes:** ShopOwner has no points/wallet; this endpoint shows shop info + linked Sellers.

---

### 5.10 Notifications

All endpoints require Bearer (any authenticated user).

#### Notifications — Register FCM device
- **Method + Path:** `POST /api/notifications/register-device`
- **Auth:** Bearer (any role)
- **Request body:** `{ "fcmToken": "..." }` (required, max 512 chars)
- **Success:** 204 No Content
- **Errors:** `Notification.UserNotFound` 404.

#### Notifications — Unregister device
- **Method + Path:** `DELETE /api/notifications/register-device`
- **Auth:** Bearer
- **Success:** 204 No Content
- **Errors:** `Notification.UserNotFound` 404.

#### Notifications — List (paginated)
- **Method + Path:** `GET /api/notifications`
- **Auth:** Bearer
- **Query:** `page` (1), `pageSize` (20)
- **Success (200):** `PaginatedResult<NotificationResponse>` — items `{ id, type, title, body, referenceId?, isRead, createdAt }`.

#### Notifications — Unread count
- **Method + Path:** `GET /api/notifications/unread-count`
- **Auth:** Bearer
- **Success (200):** `int` — raw integer (e.g. `5`).

#### Notifications — Mark one as read
- **Method + Path:** `PATCH /api/notifications/{id}/read`
- **Auth:** Bearer
- **Success:** 204 No Content
- **Errors:** `Notification.NotFound` 404, `Notification.NotOwned` 403.

#### Notifications — Mark all as read
- **Method + Path:** `PATCH /api/notifications/read-all`
- **Auth:** Bearer
- **Success:** 204 No Content

#### Notifications — Delete
- **Method + Path:** `DELETE /api/notifications/{id}`
- **Auth:** Bearer
- **Success:** 204 No Content
- **Errors:** `Notification.NotFound` 404, `Notification.NotOwned` 403.

#### Notifications — Get preferences
- **Method + Path:** `GET /api/notifications/preferences`
- **Auth:** Bearer
- **Success (200):** `NotificationPreferenceItem[]` — `[{ "type": 3, "isPushMuted": false }, ...]` (one entry per NotificationType).

#### Notifications — Update preferences
- **Method + Path:** `PUT /api/notifications/preferences`
- **Auth:** Bearer
- **Request body:**
  ```json
  { "preferences": [ { "type": 3, "isPushMuted": true }, { "type": 8, "isPushMuted": false } ] }
  ```
- **Success:** 204 No Content

---

### 5.11 Invitation

Roles: `ShopOwner, Seller, Technician`.

#### Invitation — Get info
- **Method + Path:** `GET /api/invitation`
- **Auth:** Bearer (ShopOwner / Seller / Technician)
- **Description:** Returns the user's invitation code, share link, and stats. The mobile app renders the QR code locally from `shareLink` (e.g. `qr_flutter`).
- **Success (200):** `InvitationInfoResponse`
  ```json
  {
    "invitationCode": "ABCD1234",
    "shareLink": "https://app.raedrewardapp.com/invite/ABCD1234",
    "totalInvitations": 4,
    "approvedInvitations": 3,
    "totalPointsEarned": 300
  }
  ```
- **Errors:** 400 if user record missing data (rare).
- **Tester notes:** Inviter reward capped at 20 approved invitations. Reward triggers when invitee reaches `Approved`.

---

### 5.12 Shops (map)

#### Shops — Map (list of shops with coords)
- **Method + Path:** `GET /api/shops/map`
- **Auth:** Bearer (any authenticated role)
- **Query:** `cityId` (string, optional — filter to one city)
- **Success (200):** `ShopMapItemResponse[]`
  ```json
  [{ "customerName": "...", "shopImageUrl": "...", "cityName": "...", "shortAddress": "ABCD1234", "street": "...", "district": "...", "buildingNumber": 1234, "phone": "0555..." }]
  ```
- **Tester notes:** Latitude/longitude are not returned in the current shape — Flutter resolves locations from `shortAddress` via Saudi SPL.

---

### 5.13 Content (static app content)

Both endpoints are **anonymous** (no auth required).

#### Content — Contact Us
- **Method + Path:** `GET /api/content/contact-us`
- **Auth:** None
- **Success (200):** `{ "phone", "email", "whatsApp", "address", "workingHours" }`

#### Content — About App
- **Method + Path:** `GET /api/content/about-app`
- **Auth:** None
- **Success (200):** `{ "content": "..." }`

---

### 5.14 Dev (Development & Staging only)

`/api/dev/*` endpoints return **404 Not Found** outside Development/Staging environments.

#### Dev — Seeded users
- **Method + Path:** `GET /api/dev/seeded-users`
- **Auth:** None (gated by environment)
- **Query:** `id`, `search`, `userType` (1–6), `registrationStatus` (1–4), `regionId`, `cityId`, `isDisabled` (bool), `page` (1), `pageSize` (50)
- **Success (200):** Custom shape: `{ items: [...], totalCount, page, pageSize, totalPages }` where each item carries id, name, mobile, type, status, location, customerCode, storeName, managed region (ZM), assigned cities (SM), invitation fields.

#### Dev — Cities for SalesMan
- **Method + Path:** `GET /api/dev/users/{userId}/cities`
- **Auth:** None (env-gated)
- **Success (200):** `{ userId, userName, userType, count, cities: [{ id, nameAr, nameEn, regionId, regionName, isActive }] }`
- **Errors:** 404 if user missing; 400 if user is not a SalesMan.

#### Dev — Salesmen and ZoneManagers
- **Method + Path:** `GET /api/dev/salesmen-and-zonemanagers`
- **Auth:** None (env-gated)
- **Success (200):** `{ salesMen: [...], zoneManagers: [...], totalSalesMen, totalZoneManagers }`.

#### Dev — Shop owners with their sellers
- **Method + Path:** `GET /api/dev/shop-owners-with-sellers`
- **Auth:** None (env-gated)
- **Query:** `take` (default 10, max 100)
- **Success (200):** `{ items: [{ shopOwnerId, shopOwnerName, mobileNumber, registrationStatus, isDisabled, customerCode, storeName, totalSellers, sellers: [...] }], count }`.

#### Dev — Regions managed by ZoneManager
- **Method + Path:** `GET /api/dev/users/{userId}/regions`
- **Auth:** None (env-gated)
- **Success (200):** `{ userId, userName, userType, count, regions: [{ id, nameAr, nameEn, isActive, cityCount }] }`
- **Errors:** 404 if user missing; 400 if user is not a ZoneManager.

---

## 6. Test Flows (end-to-end golden paths)

### 6.1 ShopOwner registration → approval → first scan
1. `GET /api/lookup/regions` → pick a region; `GET /api/lookup/regions/{id}/cities` → pick a city.
2. `GET /api/lookup/customer/{customerCode}/shop-data-status` — confirm code exists, no current shop owner.
3. `POST /api/auth/send-otp` → save `pinId`.
4. `POST /api/auth/verify-registration-otp` with mock OTP `123456` → save `verificationToken`.
5. `POST /api/auth/register/shop-owner` (multipart, all fields) → `userId` returned, status = `PendingSalesman`.
6. Login as the SalesMan whose city covers this user → `GET /api/approvals/pending` → confirm appears → `POST /api/approvals/approve` (status now `PendingZoneManager`).
7. Login as the ZoneManager for that region → `POST /api/approvals/approve` (status now `Approved`).
8. ShopOwner logs in: `POST /api/auth/login` → `POST /api/auth/login/verify` → JWT.
9. ShopOwner has no scan capability — scanning is for Sellers/Technicians. Use `GET /api/dashboard/shop-owner` to view shop + sellers.

### 6.2 Seller registration with existing CustomerCode
1. Pick a CustomerCode where ShopData already exists (`shopDataExists=true` from lookup).
2. Send OTP → verify → register seller with **only** `verificationToken`, `name`, `mobileNumber`, `customerCode`, `invitationCode?`. Do NOT include shop fields.
3. Approve via SM then ZM as in 6.1.
4. Seller logs in → `POST /api/scan` with a 12-char barcode → wallet credited.

### 6.3 Technician registration
1. OTP → verify → `POST /api/auth/register/technician` with `cityId`, `postalCode` (5 digits), `district`.
2. Approve via SM → ZM.
3. Login → `GET /api/dashboard` → `POST /api/scan`.

### 6.4 Login + refresh + logout
1. `POST /api/auth/login` → `pinId`.
2. `POST /api/auth/login/verify` → JWT + refresh token.
3. Wait until close to expiry (or any time): `POST /api/auth/refresh-token` with old refresh → returns new pair (old refresh is revoked).
4. `POST /api/auth/revoke-token` to log out.

### 6.5 Scan → wallet → redemption → 3-tier approval → cash OTP
1. Seller scans valid barcode (`POST /api/scan`) → `pointsAwarded` shown.
2. `GET /api/wallet/balance` to verify balance.
3. `POST /api/redemption/request` with `method=2` (Cash), `pointsAmount=1000` (or admin minimum).
4. SM logs in → `POST /api/redemption-approvals/approve` (status → PendingZoneManager).
5. ZM logs in → `POST /api/redemption-approvals/approve` (status → PendingAdmin).
6. SystemAdmin (admin API) approves → status reaches `AdminApproved`; cash OTP is generated and sent on WhatsApp to the user.
7. Field agent (SM/ZM/Admin) reads OTP from user's phone → `POST /api/redemption-approvals/confirm-cash` with the OTP → status → `Completed`.

### 6.6 Invitation flow
1. Approved user A → `GET /api/invitation` → save `invitationCode`.
2. New user B registers with `invitationCode = A.invitationCode` (any of the 3 register endpoints).
3. SM + ZM approve B.
4. On B reaching `Approved`, A receives `InvitationReward` wallet credit (capped at 20 approved invitees) and a notification.
5. A: `GET /api/invitation` → `approvedInvitations` and `totalPointsEarned` increment.

---

## 7. Role-Based Access Matrix

ShopOwner cannot scan, earn points, or redeem (architecture rule). Public endpoints only.

| Endpoint group | ShopOwner | Seller | Technician | SalesMan | ZoneManager |
|---|:---:|:---:|:---:|:---:|:---:|
| Auth — send-otp / register / login (anon) | x | x | x | x | x |
| Auth — refresh / revoke | x | x | x | x | x |
| Lookup (anon) | x | x | x | x | x |
| Profile — get / photo | x | x | x | x | x |
| Profile — delete | x | x | x | - | - |
| Wallet — balance / transactions | - | x | x | - | - |
| Scan — scan / history | - | x | x | - | - |
| Redemption — request / active / history / available-balance / resend-cash-otp | - | x | x | - | - |
| Approval — pending / list / approve / reject | - | - | - | x | x |
| Redemption Approval — pending / list / approve / reject / confirm-cash | - | - | - | x | x |
| Dashboard — `/api/dashboard` | - | x | x | - | - |
| Dashboard — `/api/dashboard/shop-owner` | x | - | - | - | - |
| Notifications — all | x | x | x | x | x |
| Invitation | x | x | x | - | - |
| Shops — map | x | x | x | x | x |
| Content (anon) | x | x | x | x | x |

Legend: `x` = allowed, `-` = forbidden (403). SystemAdmin is allowed only on `redemption-approvals/*` (and full admin API not covered here).

---

## 8. Quick Reference (cheat sheet)

| # | Method | Path | Auth |
|---|---|---|---|
| 1 | POST | /api/auth/send-otp | none |
| 2 | POST | /api/auth/resend-otp | none |
| 3 | POST | /api/auth/verify-registration-otp | none |
| 4 | POST | /api/auth/register/shop-owner | none (form-data) |
| 5 | POST | /api/auth/register/seller | none (form-data) |
| 6 | POST | /api/auth/register/technician | none |
| 7 | POST | /api/auth/login | none |
| 8 | POST | /api/auth/login/verify | none |
| 9 | POST | /api/auth/refresh-token | none |
| 10 | POST | /api/auth/revoke-token | Bearer (any) |
| 11 | GET | /api/lookup/regions | none |
| 12 | GET | /api/lookup/regions/{regionId}/cities | none |
| 13 | GET | /api/lookup/cities | none |
| 14 | GET | /api/lookup/customer/{customerCode}/shop-data-status | none |
| 15 | GET | /api/profile | Bearer (SO/Sel/Tech/SM/ZM) |
| 16 | PUT | /api/profile/photo | Bearer (SO/Sel/Tech/SM/ZM, form-data) |
| 17 | DELETE | /api/profile | Bearer (SO/Sel/Tech) |
| 18 | GET | /api/wallet/balance | Bearer (Sel/Tech) |
| 19 | GET | /api/wallet/transactions | Bearer (Sel/Tech) |
| 20 | POST | /api/scan | Bearer (Sel/Tech) |
| 21 | GET | /api/scan/history | Bearer (Sel/Tech) |
| 22 | POST | /api/redemption/request | Bearer (Sel/Tech) |
| 23 | GET | /api/redemption/active | Bearer (Sel/Tech) |
| 24 | GET | /api/redemption/history | Bearer (Sel/Tech) |
| 25 | GET | /api/redemption/available-balance | Bearer (Sel/Tech) |
| 26 | POST | /api/redemption/resend-cash-otp | Bearer (Sel/Tech) |
| 27 | GET | /api/approvals/pending | Bearer (SM/ZM) |
| 28 | GET | /api/approvals/list | Bearer (SM/ZM) |
| 29 | POST | /api/approvals/approve | Bearer (SM/ZM) |
| 30 | POST | /api/approvals/reject | Bearer (SM/ZM) |
| 31 | GET | /api/redemption-approvals/pending | Bearer (SM/ZM/Admin) |
| 32 | GET | /api/redemption-approvals/list | Bearer (SM/ZM/Admin) |
| 33 | POST | /api/redemption-approvals/approve | Bearer (SM/ZM/Admin) |
| 34 | POST | /api/redemption-approvals/reject | Bearer (SM/ZM/Admin) |
| 35 | POST | /api/redemption-approvals/confirm-cash | Bearer (SM/ZM/Admin) |
| 36 | GET | /api/dashboard | Bearer (Sel/Tech) |
| 37 | GET | /api/dashboard/shop-owner | Bearer (ShopOwner) |
| 38 | POST | /api/notifications/register-device | Bearer (any) |
| 39 | DELETE | /api/notifications/register-device | Bearer (any) |
| 40 | GET | /api/notifications | Bearer (any) |
| 41 | GET | /api/notifications/unread-count | Bearer (any) |
| 42 | PATCH | /api/notifications/{id}/read | Bearer (any) |
| 43 | PATCH | /api/notifications/read-all | Bearer (any) |
| 44 | DELETE | /api/notifications/{id} | Bearer (any) |
| 45 | GET | /api/notifications/preferences | Bearer (any) |
| 46 | PUT | /api/notifications/preferences | Bearer (any) |
| 47 | GET | /api/invitation | Bearer (SO/Sel/Tech) |
| 48 | GET | /api/shops/map | Bearer (any) |
| 49 | GET | /api/content/contact-us | none |
| 50 | GET | /api/content/about-app | none |
| 51 | GET | /api/dev/seeded-users | none (Dev/Staging only) |
| 52 | GET | /api/dev/users/{userId}/cities | none (Dev/Staging only) |
| 53 | GET | /api/dev/salesmen-and-zonemanagers | none (Dev/Staging only) |
| 54 | GET | /api/dev/shop-owners-with-sellers | none (Dev/Staging only) |
| 55 | GET | /api/dev/users/{userId}/regions | none (Dev/Staging only) |
