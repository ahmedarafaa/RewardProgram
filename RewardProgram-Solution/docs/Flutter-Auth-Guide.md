# Auth Feature - Flutter Integration Guide

**Base URL (Staging):** `https://subsite4.smarterasp.net/api`
**Base URL (UAT):** TBD

---

## 1. Auth Flow Overview

### Registration Flow (3 steps)

```
Step 1: Send OTP          POST /api/auth/send-otp
            |
            v
Step 2: Verify OTP        POST /api/auth/verify-registration-otp
            |
            v
Step 3: Register           POST /api/auth/register/shop-owner   (multipart/form-data)
                           POST /api/auth/register/seller        (multipart/form-data)
                           POST /api/auth/register/technician    (application/json)
```

### Login Flow (2 steps)

```
Step 1: Request OTP        POST /api/auth/login
            |
            v
Step 2: Verify & Login     POST /api/auth/login/verify
            |
            v
        Returns JWT + RefreshToken + User info
```

### Token Refresh

```
POST /api/auth/refresh-token   →   New JWT + RefreshToken
POST /api/auth/revoke-token    →   Logout (requires Authorization header)
```

---

## 2. Endpoints Detail

### 2.1 Send OTP (Registration)

```
POST /api/auth/send-otp
Content-Type: application/json
```

**Request:**
```json
{
  "mobileNumber": "+201121007505"
}
```

**Success Response (200):**
```json
{
  "pinId": "VExxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "maskedMobileNumber": "+20****505"
}
```

**Error Responses:**
| Status | Code | Message (Arabic) |
|--------|------|-------------------|
| 409 | `Auth.MobileAlreadyRegistered` | رقم الجوال مسجل مسبقاً |
| 400 | Validation | رقم الجوال مطلوب / صيغة غير صحيحة |
| 429 | `Auth.TooManyOtpRequests` | محاولات كثيرة، يرجى المحاولة لاحقاً |

**Mobile format:** `05XXXXXXXX` (Saudi local) or `+XXXXXXXXXXX` (international with +)

---

### 2.2 Resend OTP

```
POST /api/auth/resend-otp
Content-Type: application/json
```

**Request:**
```json
{
  "mobileNumber": "+201121007505"
}
```

**Success Response (200):** Same as send-otp (new PinId)

**Error Responses:**
| Status | Code | Message |
|--------|------|---------|
| 429 | `Auth.OtpResendTooSoon` | يرجى الانتظار 30 ثانية قبل إعادة إرسال رمز التحقق |
| 429 | `Auth.TooManyOtpRequests` | محاولات كثيرة، يرجى المحاولة لاحقاً |

**Note:** 30-second cooldown between resends. Max 3 OTPs per 15-minute window.

---

### 2.3 Verify Registration OTP

```
POST /api/auth/verify-registration-otp
Content-Type: application/json
```

**Request:**
```json
{
  "pinId": "VExxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "otp": "123456",
  "mobileNumber": "+201121007505"
}
```

**Success Response (200):**
```json
{
  "verificationToken": "eyJhbGciOi...",
  "maskedMobileNumber": "+20****505"
}
```

**Error Responses:**
| Status | Code | Message |
|--------|------|---------|
| 400 | `Auth.OtpInvalid` | رمز التحقق غير صحيح |
| 400 | `Auth.OtpExpired` | انتهت صلاحية رمز التحقق (3 min TTL) |
| 400 | `Auth.OtpAlreadyUsed` | رمز التحقق مستخدم مسبقاً |
| 400 | `Auth.MaxVerificationAttempts` | تم تجاوز الحد الأقصى لمحاولات التحقق (5 max) |
| 409 | `Auth.MobileAlreadyRegistered` | رقم الجوال مسجل مسبقاً |

**Important:** Save the `verificationToken` — you need it in the registration step. It expires (short-lived JWT).

**Mock mode (Dev/UAT):** OTP code is always `123456`.

---

### 2.4 Register Shop Owner

```
POST /api/auth/register/shop-owner
Content-Type: multipart/form-data
```

**Form Fields:**
| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `VerificationToken` | string | Yes | From verify-registration-otp |
| `CustomerCode` | string | Yes | Must exist in ERP system, max 50 chars |
| `OwnerName` | string | Yes | 3-100 chars, letters only (Arabic/English, no numbers) |
| `MobileNumber` | string | Yes | `05XXXXXXXX` or `+XXXXXXXXXXX` |
| `CityId` | string (GUID) | Yes | From lookup API |
| `StoreName` | string | Yes | 5-150 chars |
| `VAT` | string | Yes | 15 digits, starts & ends with `3` (e.g., `300000000000003`) |
| `CRN` | string | Yes | 10 digits |
| `ShortAddress` | string | Yes | 4 letters + 4 digits (e.g., `ABCD1234`) |
| `ShopImage` | file | Yes | .jpg/.jpeg/.png, max 5MB |
| `NationalAddress.BuildingNumber` | int | Yes | 4 digits (1000-9999) |
| `NationalAddress.Street` | string | Yes | 3-100 chars |
| `NationalAddress.PostalCode` | string | Yes | 5 digits |
| `NationalAddress.SubNumber` | int | Yes | 4 digits (1000-9999) |
| `NationalAddress.District` | string | Yes | 1-100 chars |
| `InvitationCode` | string | No | 8-char referral code |

**Success Response (200):**
```json
{
  "userId": "guid-here",
  "message": "تم التسجيل بنجاح، بانتظار الموافقة"
}
```

**Error Responses:**
| Status | Code | Message |
|--------|------|---------|
| 400 | `Auth.VerificationTokenInvalid` | رمز التحقق غير صالح |
| 400 | `Auth.VerificationTokenExpired` | انتهت صلاحية رمز التحقق، يرجى إعادة التحقق |
| 400 | `Auth.MobileMismatch` | رقم الجوال لا يتطابق مع رمز التحقق |
| 400 | `Auth.CustomerCodeNotFound` | كود العميل غير موجود في النظام |
| 400 | `Auth.CityNotFound` | المدينة غير موجودة |
| 400 | `Auth.NoApprovalSalesMan` | لا يوجد مندوب مبيعات معتمد لهذه المدينة |
| 409 | `Auth.MobileAlreadyRegistered` | رقم الجوال مسجل مسبقاً |
| 409 | `Auth.CustomerCodeAlreadyOwned` | كود العميل مسجل لصاحب محل آخر |

**Note:** ShopOwner ALWAYS provides shop data. If another Seller already entered data for this CustomerCode, the ShopOwner's data overwrites it.

---

### 2.5 Register Seller

```
POST /api/auth/register/seller
Content-Type: multipart/form-data
```

**Form Fields:**
| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `VerificationToken` | string | Yes | From verify-registration-otp |
| `Name` | string | Yes | 3-100 chars, letters only |
| `MobileNumber` | string | Yes | `05XXXXXXXX` or `+XXXXXXXXXXX` |
| `CustomerCode` | string | Yes | Must exist in ERP, max 50 chars |
| `StoreName` | string | Conditional* | 5-150 chars |
| `VAT` | string | Conditional* | 15 digits, starts & ends with `3` |
| `CRN` | string | Conditional* | 10 digits |
| `ShortAddress` | string | Conditional* | 4 letters + 4 digits |
| `ShopImage` | file | Conditional* | .jpg/.jpeg/.png, max 5MB |
| `CityId` | string (GUID) | Conditional* | From lookup API |
| `NationalAddress.BuildingNumber` | int | Conditional* | 1000-9999 |
| `NationalAddress.Street` | string | Conditional* | 3-100 chars |
| `NationalAddress.PostalCode` | string | Conditional* | 5 digits |
| `NationalAddress.SubNumber` | int | Conditional* | 1000-9999 |
| `NationalAddress.District` | string | Conditional* | 1-100 chars |
| `InvitationCode` | string | No | 8-char referral code |

*\*Conditional: Required ONLY if no ShopData exists for this CustomerCode yet. Use the lookup endpoint to check first.*

**Tip:** Call `GET /api/lookup/customer/{customerCode}/shop-data-status` before showing the form. If `shopDataExists: true`, skip the shop data fields.

---

### 2.6 Register Technician

```
POST /api/auth/register/technician
Content-Type: application/json
```

**Request:**
```json
{
  "verificationToken": "eyJhbGciOi...",
  "name": "محمد أحمد",
  "mobileNumber": "+201121007505",
  "cityId": "guid-here",
  "postalCode": "12345",
  "district": "حي النزهة",
  "invitationCode": "ABC12345"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `verificationToken` | string | Yes | From verify-registration-otp |
| `name` | string | Yes | 3-100 chars, letters only |
| `mobileNumber` | string | Yes | `05XXXXXXXX` or `+XXXXXXXXXXX` |
| `cityId` | string (GUID) | Yes | From lookup API |
| `postalCode` | string | Yes | 5 digits |
| `district` | string | Yes | 1-100 chars |
| `invitationCode` | string | No | 8-char referral code |

**Note:** Technician does NOT have shop data or CustomerCode.

---

### 2.7 Login (Request OTP)

```
POST /api/auth/login
Content-Type: application/json
```

**Request:**
```json
{
  "mobileNumber": "+201121007505"
}
```

**Success Response (200):**
```json
{
  "pinId": "VExxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "maskedMobileNumber": "+20****505"
}
```

**Error Responses:**
| Status | Code | Message |
|--------|------|---------|
| 404 | `Auth.UserNotFound` | المستخدم غير موجود |
| 403 | `Auth.UserRejected` | تم رفض طلب التسجيل |
| 403 | `Auth.UserNotApproved` | حسابك قيد المراجعة |
| 403 | `Auth.UserDisabled` | الحساب معطل، يرجى التواصل مع الإدارة |

---

### 2.8 Login Verify

```
POST /api/auth/login/verify
Content-Type: application/json
```

**Request:**
```json
{
  "pinId": "VExxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "otp": "123456"
}
```

**Success Response (200):**
```json
{
  "token": "eyJhbGciOi...",
  "refreshToken": "base64-string",
  "expiresIn": 3600,
  "refreshTokenExpiration": "2026-04-13T12:00:00Z",
  "user": {
    "id": "guid-here",
    "name": "محمد أحمد",
    "mobileNumber": "+201121007505",
    "userType": 2,
    "registrationStatus": 3
  }
}
```

**Error Responses:**
| Status | Code | Message |
|--------|------|---------|
| 400 | `Auth.OtpInvalid` | رمز التحقق غير صحيح |
| 400 | `Auth.OtpExpired` | انتهت صلاحية رمز التحقق |
| 403 | `Auth.UserRejected` | تم رفض طلب التسجيل |
| 403 | `Auth.UserNotApproved` | حسابك قيد المراجعة |
| 403 | `Auth.UserDisabled` | الحساب معطل |

---

### 2.9 Refresh Token

```
POST /api/auth/refresh-token
Content-Type: application/json
```

**Request:**
```json
{
  "refreshToken": "base64-string"
}
```

**Success Response (200):** Same as login verify (new JWT + RefreshToken + User)

**Error Responses:**
| Status | Code | Message |
|--------|------|---------|
| 401 | `Auth.InvalidRefreshToken` | التوكن غير صالح |
| 401 | `Auth.RefreshTokenExpired` | انتهت صلاحية التوكن |
| 401 | `Auth.RefreshTokenRevoked` | التوكن ملغي |

**Important:** Each refresh returns a NEW refresh token. The old one is revoked. Store the new one immediately.

---

### 2.10 Revoke Token (Logout)

```
POST /api/auth/revoke-token
Content-Type: application/json
Authorization: Bearer <jwt-token>
```

**Request:**
```json
{
  "refreshToken": "base64-string"
}
```

**Success Response (200):**
```json
{
  "message": "تم تسجيل الخروج بنجاح"
}
```

---

## 3. Lookup Endpoints (No Auth Required)

These are needed **before** registration to populate dropdowns.

### 3.1 Get All Regions

```
GET /api/lookup/regions
```

**Response (200):**
```json
[
  { "id": "guid", "nameAr": "الرياض", "nameEn": "Riyadh" },
  { "id": "guid", "nameAr": "مكة المكرمة", "nameEn": "Makkah" }
]
```

### 3.2 Get Cities by Region

```
GET /api/lookup/regions/{regionId}/cities
```

**Response (200):**
```json
[
  { "id": "guid", "nameAr": "الرياض", "nameEn": "Riyadh", "regionId": "guid" }
]
```

### 3.3 Get All Cities

```
GET /api/lookup/cities
```

Returns all 158 cities. Use for search/autocomplete.

### 3.4 Check Customer Shop Data Status

```
GET /api/lookup/customer/{customerCode}/shop-data-status
```

**Response (200):**
```json
{
  "customerCodeExists": true,
  "customerName": "مؤسسة الراشد للتجارة",
  "shopDataExists": false
}
```

**Use this to determine:** If `shopDataExists: false` → show shop data fields in Seller registration. If `true` → skip them.

---

## 4. Enums Reference

### UserType
| Value | Name | Can Register? |
|-------|------|--------------|
| 1 | ShopOwner | Yes (mobile app) |
| 2 | Seller | Yes (mobile app) |
| 3 | Technician | Yes (mobile app) |
| 4 | SalesMan | No (admin-created) |
| 5 | ZoneManager | No (admin-created) |
| 6 | SystemAdmin | No (seeded) |

### RegistrationStatus
| Value | Name | Meaning |
|-------|------|---------|
| 1 | PendingSalesman | Waiting for SalesMan approval |
| 2 | PendingZoneManager | SalesMan approved, waiting for ZoneManager |
| 3 | Approved | Fully approved, can login |
| 4 | Rejected | Registration rejected |

---

## 5. Error Response Format

All errors follow the ProblemDetails format:

```json
{
  "status": 400,
  "error": [
    {
      "code": "Auth.OtpInvalid",
      "description": "رمز التحقق غير صحيح"
    }
  ]
}
```

Validation errors (FluentValidation) return multiple items:

```json
{
  "status": 400,
  "error": [
    { "code": "Name.NotEmpty", "description": "الاسم مطلوب" },
    { "code": "VAT.Length", "description": "الرقم الضريبي يجب أن يكون 15 رقم" }
  ]
}
```

---

## 6. OTP Constraints

| Rule | Value |
|------|-------|
| OTP length | 6 digits |
| OTP expiry | 3 minutes |
| Max verification attempts | 5 per OTP |
| Rate limit | 3 OTPs per 15-minute window |
| Resend cooldown | 30 seconds |
| Mock mode OTP (Dev/UAT) | `123456` |

---

## 7. JWT Usage

After login, include the JWT in all authenticated requests:

```
Authorization: Bearer eyJhbGciOi...
```

**Token lifecycle:**
- JWT expires in `expiresIn` seconds (from AuthResponse)
- When JWT expires, call `/api/auth/refresh-token` with the stored refresh token
- If refresh token is also expired/revoked → redirect to login screen

---

## 8. Registration UI Flow Recommendations

### Screen 1: Role Selection
- ShopOwner / Seller / Technician

### Screen 2: Mobile Number
- Input: mobile number
- Call `POST /api/auth/send-otp`
- On success → navigate to OTP screen

### Screen 3: OTP Verification
- Input: 6-digit OTP code
- Timer: 3 minutes countdown
- Resend button (disabled for 30 seconds)
- Call `POST /api/auth/verify-registration-otp`
- On success → save `verificationToken`, navigate to registration form

### Screen 4: Registration Form

**For ShopOwner:**
1. Load cities dropdown: `GET /api/lookup/regions` → `GET /api/lookup/regions/{id}/cities`
2. Input: OwnerName, CustomerCode, CityId
3. After CustomerCode entered → call `GET /api/lookup/customer/{code}/shop-data-status` to validate
4. Shop data: StoreName, VAT, CRN, ShortAddress, ShopImage (camera/gallery)
5. National address: BuildingNumber, Street, PostalCode, SubNumber, District
6. Optional: InvitationCode
7. Submit: `POST /api/auth/register/shop-owner` (multipart/form-data)

**For Seller:**
1. Same as ShopOwner but check `shopDataExists` first
2. If `shopDataExists: true` → hide shop data fields
3. If `shopDataExists: false` → show all shop data fields
4. Submit: `POST /api/auth/register/seller` (multipart/form-data)

**For Technician:**
1. Load cities dropdown
2. Input: Name, CityId, PostalCode, District
3. Optional: InvitationCode
4. Submit: `POST /api/auth/register/technician` (JSON)

### After Registration
- Show "pending approval" screen
- User CANNOT login until status = Approved (3)
- Approval flow: SalesMan approves → ZoneManager approves → Approved

---

## 9. Mobile Number Formats Accepted

| Format | Example | Notes |
|--------|---------|-------|
| Saudi local | `0512345678` | 10 digits starting with 05 |
| Saudi international | `+966512345678` | With + prefix |
| Saudi no-plus | `966512345678` | Without + prefix |
| Egyptian local | `01121007505` | 11 digits starting with 01 |
| Egyptian international | `+201121007505` | With + prefix |
| Egyptian no-plus | `201121007505` | Without + prefix |

All formats are normalized to `+966XXXXXXXXX` or `+20XXXXXXXXXXX` internally.

---

## 10. Testing on Staging/UAT

- **Mock mode is ON** in UAT → OTP is always `123456`
- **Mock mode is OFF** in Staging → real WhatsApp OTP delivery
- Seeded test users have placeholder mobiles (`050000002`-`050000031`)
- Seeded seller for testing: `+201008928356` (احمد كمال)
