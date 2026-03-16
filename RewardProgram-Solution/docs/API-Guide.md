# RewardProgram API Guide — Flutter Integration

**Base URLs:**
- Dev: `https://localhost:44315`
- Staging: `http://staging.raedrewardapp.com`

**Auth:** All protected endpoints require `Authorization: Bearer <JWT>` header.

---

## 1. Auth Flow

### Registration (no auth)

```
POST /api/auth/send-otp          → { pinId, maskedMobileNumber }
POST /api/auth/register/shop-owner   [FormData]  → { userId, message }
POST /api/auth/register/seller       [FormData]  → { userId, message }
POST /api/auth/register/technician   [JSON]      → { userId, message }
POST /api/auth/resend-otp        → { pinId, maskedMobileNumber }
```

**Flow:** Send OTP → get `pinId` → register with `pinId` + `otp` → user created (PendingSalesman).

All registration endpoints accept an optional `invitationCode` (8-char string). If provided, the inviter gets 100 pts and the invitee gets 50 pts when the new user is approved.

### Login (no auth)

```
POST /api/auth/login             → { pinId, maskedMobileNumber }
POST /api/auth/login/verify      → { token, refreshToken, ... }
```

**Flow:** Login → get `pinId` → verify with `pinId` + `otp` → get JWT + refresh token.

### Token Management (auth required)

```
POST /api/auth/refresh-token     → { token, refreshToken, ... }
POST /api/auth/revoke-token      → 200 OK
```

---

## 2. Lookups (no auth)

```
GET /api/lookup/regions                              → [{ id, nameAr, nameEn }]
GET /api/lookup/regions/{regionId}/cities             → [{ id, nameAr, nameEn }]
GET /api/lookup/cities                                → [{ id, nameAr, nameEn }]
GET /api/lookup/customer/{customerCode}/shop-data-status → { exists }
```

Use city IDs for registration. Shop-data-status tells Seller if shop fields are needed.

---

## 3. Dashboard (ShopOwner, Seller, Technician)

```
GET /api/dashboard
```

**Response:**
```json
{
  "userName": "محمد عبد الرحمن",
  "points": 532,
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

`type` values: 1=Earned, 2=Redeemed, 3=Cancelled, 4=Expired, 5=Refunded, 6=InvitationReward

This is the **mobile home screen** endpoint — single call for all data.

---

## 4. Scanning (Seller, Technician)

```
POST /api/scan
```
```json
{ "barcodeCode": "ABCDEFGHIJKL", "latitude": 24.7136, "longitude": 46.6753 }
```
- `barcodeCode`: exactly 12 chars (NanoID from printed barcode)
- `latitude`/`longitude`: optional, send if GPS available

**Response:**
```json
{ "productName": "...", "pointsAwarded": 15, "newBalance": 547, "message": "..." }
```

**Rules:**
- Seller and Technician each scan once per barcode
- When both scan → barcode becomes Consumed
- Points are credited to wallet immediately

```
GET /api/scan/history?page=1&pageSize=20
```

Paginated scan history for the current user.

---

## 5. Wallet (Seller, Technician)

```
GET /api/wallet/balance       → { balance, sarBalance }
GET /api/wallet/transactions?page=1&pageSize=20&type=1
```

`type` filter is optional (same enum as dashboard transactions).

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
  "qrCodeBase64": "<base64 BMP image>",
  "totalInvitations": 5,
  "approvedInvitations": 3,
  "totalPointsEarned": 300
}
```

**Business rules:**
- Each user gets a permanent 8-char invitation code (lazy-generated on first call)
- Share via link or QR code (WhatsApp sharing handled by app)
- New user provides the code during registration (`invitationCode` field)
- Rewards credited on approval: inviter = 100 pts, invitee = 50 pts (admin-configurable)
- Inviter reward capped at **20 approved invitations** — after that, inviter gets nothing but invitee still gets their reward
- QR code is a base64-encoded BMP image — display with `Image.memory(base64Decode(qrCodeBase64))`

---

## 7. Redemption (Seller, Technician)

### User endpoints

```
GET  /api/redemption/available-balance  → { totalBalance, heldBalance, availableBalance, availableSarBalance }
POST /api/redemption/request            → RedemptionRequestResponse
GET  /api/redemption/active             → RedemptionRequestResponse (or 404)
GET  /api/redemption/history?page=1&pageSize=20
```

**Create request:**
```json
{
  "redemptionMethod": 1,
  "pointsAmount": 1500,
  "iban": "SA1234567890123456789012",
  "bankName": "الراجحي",
  "accountHolderName": "محمد عبد الرحمن"
}
```

- `redemptionMethod`: 1=BankTransfer, 2=Cash
- `pointsAmount`: minimum 1000
- Bank fields required only for BankTransfer
- IBAN: "SA" + 22 digits

### Approval endpoints (SalesMan, ZoneManager, SystemAdmin)

```
GET  /api/redemption-approvals/pending?page=1&pageSize=20
POST /api/redemption-approvals/approve    → { redemptionRequestId }
POST /api/redemption-approvals/reject     → { redemptionRequestId, rejectionReason }
POST /api/redemption-approvals/confirm-cash → { redemptionRequestId, otp }
```

**3-level approval flow:**
1. SalesMan approves → PendingSalesMan → PendingZoneManager
2. ZoneManager approves → PendingZoneManager → PendingAdmin
3. SystemAdmin approves → PendingAdmin → AdminApproved
4. For Cash: SalesMan confirms cash handover with OTP → Completed
5. For BankTransfer: Admin processes externally → Completed

**Status enum:** 1=PendingSalesMan, 2=PendingZoneManager, 3=PendingAdmin, 4=AdminApproved, 5=Completed, 6=Rejected, 7=Cancelled

---

## 8. Approvals (SalesMan, ZoneManager)

Registration approval — separate from redemption approval.

```
GET  /api/approvals/pending?page=1&pageSize=20
POST /api/approvals/approve    → { userId }
POST /api/approvals/reject     → { userId, reason }
```

**2-level flow:**
1. SalesMan: PendingSalesman → PendingZoneManager
2. ZoneManager: PendingZoneManager → Approved (triggers invitation rewards)

---

## Enums Reference

| Enum | Values |
|------|--------|
| UserType | 1=ShopOwner, 2=Seller, 3=Technician, 4=SalesMan, 5=ZoneManager |
| RegistrationStatus | 1=PendingSalesman, 2=PendingZoneManager, 3=Approved, 4=Rejected |
| WalletTransactionType | 1=Earned, 2=Redeemed, 3=Cancelled, 4=Expired, 5=Refunded, 6=InvitationReward |
| BarcodeStatus | 1=Available, 2=SellerScanned, 3=TechnicianScanned, 4=Consumed |
| ScannerRole | 1=Seller, 2=Technician |
| RedemptionMethod | 1=BankTransfer, 2=Cash |
| RedemptionRequestStatus | 1=PendingSalesMan, 2=PendingZoneManager, 3=PendingAdmin, 4=AdminApproved, 5=Completed, 6=Rejected, 7=Cancelled |

---

## Error Handling

All errors return `ProblemDetails`:
```json
{
  "type": "Scan.UserNotApproved",
  "title": "الحساب غير مفعل أو غير معتمد",
  "status": 403
}
```

Common status codes: 400 (validation), 401 (no token), 403 (wrong role/not approved), 404 (not found), 409 (conflict/concurrency).

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
  "pageSize": 20
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
