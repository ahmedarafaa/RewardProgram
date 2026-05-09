# RewardProgram API Guide — Flutter Integration

**Author:** Mahmoud Zahran
**Last Updated:** 2026-03-25

**Base URLs:**
- Dev: `https://localhost:44315`
- Staging: `http://staging.raedrewardapp.com`

**Auth:** All protected endpoints require `Authorization: Bearer <JWT>` header.

**Content Types:**
- JSON endpoints: `Content-Type: application/json`
- FormData endpoints (marked `[FormData]`): `Content-Type: multipart/form-data`

---

## 1. Auth Flow

### Send OTP (no auth)

```
POST /api/auth/send-otp
```
```json
{ "mobileNumber": "0512345678" }
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| mobileNumber | string | yes | `05XXXXXXXX` (10 digits) or `+XXXXXXXXXXX` (international, 10-15 digits) |

**Response:** `{ "pinId": "...", "maskedMobileNumber": "05****5678" }`

### Resend OTP (no auth)

```
POST /api/auth/resend-otp
```
```json
{ "mobileNumber": "0512345678" }
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| mobileNumber | string | yes | `05XXXXXXXX` or `+XXXXXXXXXXX` |

**Response:** `{ "pinId": "...", "maskedMobileNumber": "05****5678" }`

### Register ShopOwner (no auth) `[FormData]`

```
POST /api/auth/register/shop-owner
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| pinId | string | yes | From send-otp response |
| otp | string | yes | 6-digit code received via WhatsApp |
| customerCode | string | yes | Max 50 chars. One ShopOwner per CustomerCode |
| ownerName | string | yes | 3-100 chars, letters only (`^[\p{L}\s]+$`), no numbers |
| mobileNumber | string | yes | `05XXXXXXXX` or `+XXXXXXXXXXX` |
| cityId | string (GUID) | yes | Valid city ID from lookups |
| storeName | string | yes | 5-150 chars |
| vat | string | yes | Exactly 15 digits, must start and end with `3` (`^3\d{13}3$`) |
| crn | string | yes | Exactly 10 digits (`^\d{10}$`) |
| shortAddress | string | yes | 4 letters + 4 digits (`^[A-Za-z]{4}\d{4}$`) |
| shopImage | file | yes | JPG/PNG only, max 5 MB |
| nationalAddress.buildingNumber | int | yes | 4 digits (1000-9999) |
| nationalAddress.street | string | yes | 3-100 chars |
| nationalAddress.postalCode | string | yes | 5 digits (`^\d{5}$`) |
| nationalAddress.subNumber | int | yes | 4 digits (1000-9999) |
| nationalAddress.district | string | no | Max 100 chars |
| invitationCode | string | no | 8-char referral code |

**Response:** `{ "userId": "...", "message": "..." }`

**Notes:**
- ShopOwner ALWAYS provides shop data — overwrites existing Seller data if any.
- OTP is validated AFTER all field validation (prevents OTP waste on validation errors).

### Register Seller (no auth) `[FormData]`

```
POST /api/auth/register/seller
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| pinId | string | yes | From send-otp response |
| otp | string | yes | 6-digit code |
| name | string | yes | 3-100 chars, letters only (`^[\p{L}\s]+$`) |
| mobileNumber | string | yes | `05XXXXXXXX` or `+XXXXXXXXXXX` |
| customerCode | string | yes | Max 50 chars |
| storeName | string | conditional | 5-150 chars (required if first Seller for this CustomerCode) |
| vat | string | conditional | 15 digits, starts & ends with `3` |
| crn | string | conditional | 10 digits |
| shortAddress | string | conditional | 4 letters + 4 digits |
| shopImage | file | conditional | JPG/PNG, max 5 MB |
| cityId | string (GUID) | conditional | Required with shop data |
| nationalAddress | object | conditional | Required with shop data (same fields as ShopOwner) |
| invitationCode | string | no | 8-char referral code |

**Notes:**
- First Seller for a CustomerCode provides shop data; subsequent Sellers skip shop fields.
- Use `GET /api/lookup/customer/{customerCode}/shop-data-status` to check if shop data is needed before showing the form.

### Register Technician (no auth) `[JSON]`

```
POST /api/auth/register/technician
```
```json
{
  "pinId": "...",
  "otp": "123456",
  "name": "محمد عبد الرحمن",
  "mobileNumber": "0512345678",
  "cityId": "...",
  "postalCode": "12345",
  "district": "الروضة",
  "invitationCode": "ABC12345"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| pinId | string | yes | From send-otp response |
| otp | string | yes | 6-digit code |
| name | string | yes | 3-100 chars, letters only (`^[\p{L}\s]+$`) |
| mobileNumber | string | yes | `05XXXXXXXX` or `+XXXXXXXXXXX` |
| cityId | string (GUID) | yes | Valid city ID |
| postalCode | string | yes | 5 digits (`^\d{5}$`) |
| district | string | yes | Max 100 chars |
| invitationCode | string | no | 8-char referral code |

### Registration Flow

1. Call `send-otp` with mobile number → get `pinId`
2. Show registration form to user
3. Submit registration with `pinId` + `otp` + all fields
4. User created with status **PendingSalesman** (awaiting approval)
5. User cannot login until status = **Approved**

### Login (no auth)

```
POST /api/auth/login
```
```json
{ "mobileNumber": "0512345678" }
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| mobileNumber | string | yes | `05XXXXXXXX` or `+XXXXXXXXXXX` |

**Response:** `{ "pinId": "...", "maskedMobileNumber": "05****5678" }`

### Verify Login (no auth)

```
POST /api/auth/login/verify
```
```json
{ "pinId": "...", "otp": "123456" }
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| pinId | string | yes | From login response |
| otp | string | yes | Exactly 6 digits (`^\d{6}$`) |

**Response:**
```json
{
  "token": "<JWT>",
  "refreshToken": "...",
  "expiresIn": 3600,
  "refreshTokenExpiration": "2026-03-24T12:00:00Z",
  "user": {
    "id": "...",
    "name": "محمد عبد الرحمن",
    "mobileNumber": "0512345678",
    "userType": 2,
    "registrationStatus": 3
  }
}
```

| Field | Type | Description |
|-------|------|------------|
| token | string | JWT access token |
| refreshToken | string | Token for refreshing the JWT |
| expiresIn | int | Token lifetime in seconds |
| refreshTokenExpiration | DateTime | When the refresh token expires |
| user.userType | int | 1=ShopOwner, 2=Seller, 3=Technician, 4=SalesMan, 5=ZoneManager, 6=SystemAdmin |
| user.registrationStatus | int | 1=PendingSalesman, 2=PendingZoneManager, 3=Approved, 4=Rejected |

**Login errors:**
- Rejected user → 403
- Non-Approved user (PendingSalesman/PendingZoneManager) → 403

### Token Management (auth required)

```
POST /api/auth/refresh-token     → { token, refreshToken, expiresIn, refreshTokenExpiration, user }
POST /api/auth/revoke-token      → 200 OK
```

- Send `{ "refreshToken": "..." }` to refresh
- Send `{ "refreshToken": "..." }` to revoke (logout)
- Store both `token` and `refreshToken` securely on device
- Refresh before token expires; if refresh fails, redirect to login

---

## 2. Lookups (no auth)

```
GET /api/lookup/regions                              → [{ id, nameAr, nameEn }]
GET /api/lookup/regions/{regionId}/cities             → [{ id, nameAr, nameEn, regionId }]
GET /api/lookup/cities                                → [{ id, nameAr, nameEn, regionId }]
GET /api/lookup/customer/{customerCode}/shop-data-status
```

**City response** includes `regionId` — useful for deriving the region from the selected city.

**Shop data status response:**
```json
{
  "customerCodeExists": true,
  "customerName": "شركة الرائد",
  "shopDataExists": false
}
```

| Field | Description |
|-------|------------|
| customerCodeExists | Whether the CustomerCode exists in ERP |
| customerName | ERP customer name (null if not found) |
| shopDataExists | Whether shop data already exists — tells Seller if shop fields are needed |

- Use city IDs for registration forms.
- If `shopDataExists` is `true`, the Seller registration form should skip shop fields (storeName, vat, crn, etc.).
- If `customerCodeExists` is `false`, show an error — the CustomerCode is invalid.
- Cache regions/cities locally; they rarely change.

---

## 3. Dashboard (ShopOwner, Seller, Technician)

```
GET /api/dashboard
```

**Response:**
```json
{
  "userName": "محمد عبد الرحمن",
  "points": 532.0,
  "sarBalance": 53.20,
  "minimumRedemptionPoints": 1000,
  "pointsToRedeem": 468,
  "canRedeem": false,
  "recentTransactions": [
    {
      "id": "...",
      "amount": 15,
      "sarAmount": 1.50,
      "type": 1,
      "description": "مسح باركود — شريط إضاءة لاسلكي",
      "createdAt": "2026-03-10T12:00:00Z"
    }
  ]
}
```

| Field | Description |
|-------|------------|
| points | Current wallet balance (decimal) |
| sarBalance | Points converted to SAR (admin-configurable rate) |
| minimumRedemptionPoints | Min points needed to redeem (admin-configurable, default 1000) |
| pointsToRedeem | Remaining points needed = max(0, minimumRedemptionPoints - points) |
| canRedeem | `true` when points >= minimumRedemptionPoints |
| recentTransactions | Last 10 transactions |

`type` values: 1=Earned, 2=Redeemed, 3=Cancelled, 4=Expired, 5=Refunded, 6=InvitationReward

This is the **mobile home screen** endpoint — single call for all data. Returns 0/empty for users with no wallet yet.

---

## 4. Scanning (Seller, Technician)

### Scan Barcode

```
POST /api/scan
```
```json
{ "barcodeCode": "ABCDEFGHIJKL", "latitude": 24.7136, "longitude": 46.6753 }
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| barcodeCode | string | yes | Exactly 12 chars |
| latitude | double | no | Send if GPS available |
| longitude | double | no | Send if GPS available |

**Response:**
```json
{ "productName": "...", "pointsAwarded": 15, "newBalance": 547, "message": "..." }
```

**Rules:**
- Seller and Technician each scan once per barcode (same barcode, different roles)
- When both scan → barcode becomes **Consumed**
- Points are credited to wallet immediately
- Scanning is **in-app only** — barcodes use NanoID codes, not standard product barcodes
- User must be **Approved** to scan (403 if not)

**Common errors:**
- `Scan.AlreadyScanned` (409) — user already scanned this barcode
- `Scan.UserNotApproved` (403) — account not yet approved
- `Scan.BarcodeNotFound` (404) — invalid barcode code

### Scan History

```
GET /api/scan/history?page=1&pageSize=20
```

**Response item:**
```json
{
  "id": "...",
  "barcodeCode": "ABCDEFGHIJKL",
  "productName": "شريط إضاءة لاسلكي",
  "productCode": "P001",
  "productPointValue": 15,
  "pointsAwarded": 15.0,
  "scannerRole": 1,
  "barcodeStatus": 4,
  "scannedAt": "2026-03-10T12:00:00Z",
  "latitude": 24.7136,
  "longitude": 46.6753
}
```

Paginated scan history for the current user.

---

## 5. Wallet (Seller, Technician)

```
GET /api/wallet/balance       → { balance, sarBalance }
GET /api/wallet/transactions?page=1&pageSize=20&type=1
```

| Param | Type | Required | Description |
|-------|------|----------|------------|
| page | int | no | Default 1 |
| pageSize | int | no | Default 20 |
| type | int | no | Filter by transaction type (1-6) |

`type` filter is optional (same enum as dashboard transactions). Omit to get all types.

**Transaction response item:**
```json
{
  "id": "...",
  "amount": 15.0,
  "sarRate": 0.10,
  "sarAmount": 1.50,
  "type": 1,
  "description": "مسح باركود — شريط إضاءة لاسلكي",
  "createdAt": "2026-03-10T12:00:00Z"
}
```

| Field | Description |
|-------|------------|
| sarRate | The SAR conversion rate at the time of this transaction |
| sarAmount | amount * sarRate |

Returns `{ "balance": 0, "sarBalance": 0 }` if wallet not created yet (user hasn't earned any points).

---

## 6. Invitation (ShopOwner, Seller, Technician)

```
GET /api/invitation
```

**Response:**
```json
{
  "invitationCode": "ABC12345",
  "shareLink": "https://app.raedrewardapp.com/invite/ABC12345",
  "totalInvitations": 5,
  "approvedInvitations": 3,
  "totalPointsEarned": 300
}
```

**Display QR code (render client-side from `shareLink`):**
```dart
// pubspec.yaml: qr_flutter: ^4.x
QrImageView(data: response.shareLink, size: 240)
```

**Business rules:**
- Each user gets a permanent 8-char invitation code (lazy-generated on first call to this endpoint)
- Share via link or QR code (WhatsApp sharing handled by the app)
- New user provides the code during registration (`invitationCode` field)
- Rewards credited **on approval** (not on registration): inviter = 100 pts, invitee = 50 pts (admin-configurable)
- Inviter reward capped at **20 approved invitations** — after that, inviter gets nothing but invitee still gets their reward
- Invalid invitation codes are silently ignored during registration (no error thrown)
- Self-invitation is blocked

### Invitation Deep Link Flow

The `invitationCode` field exists in the registration API, but the **user should never type it manually**. The app should extract it from the deep link URL and inject it silently into the registration request.

**Share link format:** `https://app.raedrewardapp.com/invite/{CODE}`

#### Scenario 1: User already has the app installed

1. User A shares link `https://app.raedrewardapp.com/invite/ABC12345` via WhatsApp
2. User B taps the link
3. OS deep link handler opens the app directly
4. App reads the URL, extracts `ABC12345` from the path
5. App navigates to the registration screen with the code stored in memory
6. User B fills in their details (name, mobile, etc.) — **no invitation code field shown**
7. On submit, the app includes `invitationCode: "ABC12345"` in the API request automatically

```dart
// Deep link handler
void handleDeepLink(Uri uri) {
  if (uri.pathSegments.length == 2 && uri.pathSegments[0] == 'invite') {
    final code = uri.pathSegments[1]; // "ABC12345"
    navigateToRegistration(invitationCode: code);
  }
}
```

#### Scenario 2: User does NOT have the app installed

1. User A shares link `https://app.raedrewardapp.com/invite/ABC12345` via WhatsApp
2. User B taps the link
3. App is not installed — the link opens in the browser
4. **Option A — Firebase Dynamic Links (recommended):**
   - The link is a Firebase Dynamic Link that detects the platform
   - Redirects to App Store (iOS) or Play Store (Android)
   - After install and first launch, Firebase delivers the original deep link to the app
   - App extracts `ABC12345` from the deferred deep link and proceeds as Scenario 1
5. **Option B — Fallback landing page (implemented today):**
   - `GET /invite/{code}` on the API serves a self-contained HTML page that:
     - Detects platform via user-agent
     - On Android: fires an `intent://` URL that opens the app if installed, else falls back to Play Store via `S.browser_fallback_url`
     - On iOS: fires the custom-scheme deep link (`raedreward://invite/{code}`) and falls back to App Store after 1.5s if it doesn't resolve
     - On desktop / fallback: shows the invitation code prominently with a Copy button + manual store-download buttons
   - DNS for `app.raedrewardapp.com` must be configured to route `/invite/*` to the API host (CNAME or reverse proxy). Until that's set up, the page is reachable directly via the API host (e.g. `https://staging.raedrewardapp.com/invite/ABC12345`).
   - Configure store URLs and Android package name via the `Invitation` section in `appsettings.json` (or env-var overrides):
     ```json
     "Invitation": {
       "ShareBaseUrl": "https://app.raedrewardapp.com/invite/",
       "IosAppStoreUrl": "https://apps.apple.com/app/idXXXXXXXXX",
       "AndroidPlayStoreUrl": "https://play.google.com/store/apps/details?id=com.raed.rewardapp",
       "AndroidPackageName": "com.raed.rewardapp",
       "DeepLinkScheme": "raedreward://invite/"
     }
     ```
   - Once Universal Links / App Links are configured (AASA + assetlinks.json hosted at the brand domain), the page becomes a no-op pass-through for users who have the app installed.

```
┌─────────────────────────────────────────────────┐
│         User taps invitation link                │
│                    │                             │
│          ┌────────┴────────┐                     │
│          ▼                 ▼                     │
│    App installed?    App NOT installed?           │
│          │                 │                     │
│          ▼                 ▼                     │
│   Deep link opens    Redirect to store           │
│   app directly       (via Dynamic Link)          │
│          │                 │                     │
│          ▼                 ▼                     │
│   Extract code       User installs app           │
│   from URL           & opens it                  │
│          │                 │                     │
│          │                 ▼                     │
│          │          Deferred deep link            │
│          │          delivers the code             │
│          │                 │                     │
│          └────────┬────────┘                     │
│                   ▼                              │
│       Registration screen opens                  │
│       (code injected silently)                   │
│       No invitation code field shown             │
└─────────────────────────────────────────────────┘
```

**Recommended approach:** Use [Firebase Dynamic Links](https://firebase.google.com/docs/dynamic-links) or [App Links](https://developer.android.com/training/app-links) (Android) + [Universal Links](https://developer.apple.com/documentation/xcode/allowing-apps-and-websites-to-link-to-your-content) (iOS). This handles both scenarios seamlessly with zero manual code entry.

**Backend note:** No backend changes needed. The API already accepts `invitationCode` as an optional field in all registration endpoints. The deep link handling is entirely a Flutter/mobile concern.

---

## 7. Redemption (Seller, Technician)

### Check Available Balance

```
GET /api/redemption/available-balance
```

**Response:**
```json
{
  "totalBalance": 2500,
  "heldBalance": 1000,
  "availableBalance": 1500,
  "availableSarBalance": 150.00
}
```

- `heldBalance` = points locked in pending redemption requests
- `availableBalance` = totalBalance - heldBalance (what the user can actually redeem)

### Create Redemption Request

```
POST /api/redemption/request
```
```json
{
  "method": 1,
  "pointsAmount": 1500,
  "iban": "SA1234567890123456789012",
  "bankName": "الراجحي",
  "accountHolderName": "محمد عبد الرحمن"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| method | int | yes | 1=BankTransfer, 2=Cash |
| pointsAmount | decimal | yes | >= 1000 (minimum redemption) |
| iban | string | conditional | Required for BankTransfer. `SA` + 22 digits (`^SA\d{22}$`) |
| bankName | string | conditional | Required for BankTransfer. Max 200 chars |
| accountHolderName | string | conditional | Required for BankTransfer. Max 200 chars |

**Notes:**
- Only ONE active redemption request allowed at a time
- Points are held (locked) when request is created
- If rejected/cancelled, held points are released back

### Get Active Request

```
GET /api/redemption/active
```

Returns the user's current active redemption request, or 404 if none.

**Response:**
```json
{
  "id": "...",
  "method": 1,
  "status": 1,
  "pointsAmount": 1500.0,
  "sarRate": 0.10,
  "sarAmount": 150.00,
  "iban": "SA1234567890123456789012",
  "bankName": "الراجحي",
  "accountHolderName": "محمد عبد الرحمن",
  "cashOtpExpiresAt": null,
  "rejectionReason": null,
  "createdAt": "2026-03-10T12:00:00Z"
}
```

| Field | Description |
|-------|------------|
| method | 1=BankTransfer, 2=Cash |
| status | Current status (see RedemptionRequestStatus enum) |
| sarRate | SAR conversion rate at time of request |
| cashOtpExpiresAt | Only for Cash method — when the handover OTP expires |
| rejectionReason | Only if status = Rejected |
| iban/bankName/accountHolderName | Only for BankTransfer method |

### Redemption History

```
GET /api/redemption/history?page=1&pageSize=20
```

Returns paginated list of all past redemption requests (same response shape as active request).

### Approval Endpoints (SalesMan, ZoneManager, SystemAdmin)

```
GET  /api/redemption-approvals/pending?page=1&pageSize=20
POST /api/redemption-approvals/approve    → { "redemptionRequestId": "..." }
POST /api/redemption-approvals/reject     → { "redemptionRequestId": "...", "rejectionReason": "..." }
POST /api/redemption-approvals/confirm-cash → { "redemptionRequestId": "...", "otp": "123456" }
```

**3-level approval flow:**
1. SalesMan approves → PendingSalesMan → PendingZoneManager
2. ZoneManager approves → PendingZoneManager → PendingAdmin
3. SystemAdmin approves → PendingAdmin → AdminApproved
4. For **Cash**: SalesMan confirms cash handover with OTP → Completed
5. For **BankTransfer**: Admin processes payment externally → Completed

**Status enum:** 1=PendingSalesMan, 2=PendingZoneManager, 3=PendingAdmin, 4=AdminApproved, 5=Completed, 6=Rejected, 7=Cancelled

---

## 8. Registration Approvals (SalesMan, ZoneManager)

Separate from redemption approval.

```
GET  /api/approvals/pending?page=1&pageSize=20
POST /api/approvals/approve    → { "userId": "..." }
POST /api/approvals/reject     → { "userId": "...", "reason": "..." }
```

**2-level flow:**
1. SalesMan: PendingSalesman → PendingZoneManager
2. ZoneManager: PendingZoneManager → Approved (triggers invitation rewards if applicable)

---

## Enums Reference

| Enum | Values |
|------|--------|
| UserType | 1=ShopOwner, 2=Seller, 3=Technician, 4=SalesMan, 5=ZoneManager, 6=SystemAdmin |
| RegistrationStatus | 1=PendingSalesman, 2=PendingZoneManager, 3=Approved, 4=Rejected |
| WalletTransactionType | 1=Earned, 2=Redeemed, 3=Cancelled, 4=Expired, 5=Refunded, 6=InvitationReward |
| BarcodeStatus | 1=Available, 2=SellerScanned, 3=TechnicianScanned, 4=Consumed |
| ScannerRole | 1=Seller, 2=Technician |
| RedemptionMethod | 1=BankTransfer, 2=Cash |
| RedemptionRequestStatus | 1=PendingSalesMan, 2=PendingZoneManager, 3=PendingAdmin, 4=AdminApproved, 5=Completed, 6=Rejected, 7=Cancelled |

---

## Validation Rules Summary

| Field | Rule | Regex/Range |
|-------|------|-------------|
| Mobile Number | 05 + 8 digits OR + followed by 10-15 digits | `^(05\d{8}\|\+\d{10,15})$` |
| Name (all types) | Letters + spaces only, 3-100 chars | `^[\p{L}\s]+$` |
| OTP | Exactly 6 digits | `^\d{6}$` |
| VAT | 15 digits, starts & ends with 3 | `^3\d{13}3$` |
| CRN | Exactly 10 digits | `^\d{10}$` |
| Short Address | 4 letters + 4 digits (e.g. ABCD1234) | `^[A-Za-z]{4}\d{4}$` |
| Postal Code | 5 digits | `^\d{5}$` |
| Building Number | 4-digit integer | 1000-9999 |
| Sub Number | 4-digit integer | 1000-9999 |
| IBAN | SA + 22 digits | `^SA\d{22}$` |
| Barcode Code | Exactly 12 characters | Length = 12 |
| Redemption Points | Minimum 1000 | >= 1000 |
| Shop Image | JPG/PNG, max 5 MB | Extensions: .jpg, .jpeg, .png |
| Store Name | 5-150 chars | — |
| Street | 3-100 chars | — |
| District | Max 100 chars | — |
| Bank Name | Max 200 chars | — |
| Account Holder Name | Max 200 chars | — |
| Customer Code | Max 50 chars | — |
| Invitation Code | 8 chars | — |

---

## Error Handling

All errors return `ProblemDetails` (RFC 7807):
```json
{
  "type": "Scan.UserNotApproved",
  "title": "الحساب غير مفعل أو غير معتمد",
  "status": 403
}
```

| Status | Meaning |
|--------|---------|
| 400 | Validation error (check `errors` object for field-level details) |
| 401 | Missing or expired JWT token |
| 403 | Wrong role, not approved, or insufficient permissions |
| 404 | Resource not found |
| 409 | Conflict (duplicate scan, concurrency issue) |

**Validation errors (400)** return field-level details:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "MobileNumber": ["رقم الجوال يجب أن يبدأ بـ 05 ويتكون من 10 أرقام"],
    "VAT": ["الرقم الضريبي يجب أن يبدأ وينتهي بالرقم 3"]
  }
}
```

---

## Pagination

All list endpoints use:
```
?page=1&pageSize=20
```

Response wrapper:
```json
{
  "items": [...],
  "totalCount": 150,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

---

## Role-Based Access Summary

| Endpoint Group | ShopOwner | Seller | Technician | SalesMan | ZoneManager | SystemAdmin |
|----------------|-----------|--------|------------|----------|-------------|-------------|
| Dashboard | x | x | x | | | |
| Scanning | | x | x | | | |
| Wallet | | x | x | | | |
| Invitation | x | x | x | | | |
| Redemption (user) | | x | x | | | |
| Redemption (approval) | | | | x | x | x |
| Registration Approval | | | | x | x | |
| Admin endpoints | | | | | | x |

---

## Flutter Implementation Notes

1. **OTP flow**: Always validate all fields client-side before sending registration request — OTP is consumed on validation, so a validation error wastes the OTP.
2. **FormData uploads**: ShopOwner and Seller registration use `multipart/form-data`. Nested `nationalAddress` fields should be sent as flat keys: `nationalAddress.buildingNumber`, `nationalAddress.street`, etc.
3. **Token storage**: Store JWT and refresh token securely (e.g., `flutter_secure_storage`). Refresh proactively before expiry.
4. **QR code display**: render client-side from `shareLink` with `qr_flutter` (e.g. `QrImageView(data: shareLink, size: 240)`). The backend no longer ships a QR image.
5. **Wallet zero state**: New users with no scans have no wallet — balance endpoints return zeros. Dashboard handles this gracefully.
6. **SAR conversion**: The SAR rate is set by admin and stored per transaction at earn time. Displayed SAR values reflect the rate at time of earning, not the current rate.
7. **Arabic error messages**: All validation error messages are in Arabic — display them directly to the user.
8. **Conditional shop data**: Before showing shop fields for Seller registration, call `shop-data-status` to check if data already exists for the given CustomerCode.
