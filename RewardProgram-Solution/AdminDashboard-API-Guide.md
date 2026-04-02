# Admin Dashboard API Guide

**Base URL:** `https://staging.raedrewardapp.com`

**Authentication:** All endpoints (except login) require a Bearer token with `SystemAdmin` role.

```
Authorization: Bearer <token>
```

---

## Enum Reference

### UserType
| Value | Name |
|-------|------|
| 1 | ShopOwner |
| 2 | Seller |
| 3 | Technician |
| 4 | SalesMan |
| 5 | ZoneManager |
| 6 | SystemAdmin |

### RegistrationStatus
| Value | Name |
|-------|------|
| 1 | PendingSalesman |
| 2 | PendingZoneManager |
| 3 | Approved |
| 4 | Rejected |

### WalletTransactionType
| Value | Name |
|-------|------|
| 1 | Earned |
| 2 | Redeemed |
| 3 | Cancelled |
| 4 | Expired |
| 5 | Refunded |
| 6 | InvitationReward |

### RedemptionRequestStatus
| Value | Name |
|-------|------|
| 1 | PendingSalesMan |
| 2 | PendingZoneManager |
| 3 | PendingAdmin |
| 4 | AdminApproved |
| 5 | Completed |
| 6 | Rejected |
| 7 | Cancelled |

### RedemptionMethod
| Value | Name |
|-------|------|
| 1 | BankTransfer |
| 2 | Cash |

### BarcodeStatus
| Value | Name |
|-------|------|
| 1 | Available |
| 2 | SellerScanned |
| 3 | TechnicianScanned |
| 4 | Consumed |

---

## 1. Admin Login

**POST** `/api/admin/auth/login`

No authentication required.

### Request Body

```json
{
  "username": "admin",
  "password": "Admin@123"
}
```

### Response `200 OK`

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "expiresIn": 3600,
  "refreshTokenExpiration": "2026-04-09T05:18:02Z",
  "user": {
    "id": "admin-001",
    "name": "System Admin",
    "mobileNumber": "966500000000",
    "userType": 6,
    "registrationStatus": 3
  }
}
```

### Error `401 Unauthorized`

```json
{
  "title": "اسم المستخدم أو كلمة المرور غير صحيحة",
  "status": 401
}
```

---

## 2. Dashboard Summary

**GET** `/api/admin/dashboard`

### Response `200 OK`

```json
{
  "totalShopOwners": 45,
  "totalSellers": 128,
  "totalTechnicians": 87,
  "totalPendingApprovals": 12,
  "totalPointsEarned": 254300.0,
  "totalPointsRedeemed": 85000.0,
  "totalSarRedeemed": 8500.00,
  "totalActiveBarcodes": 3420,
  "totalScans": 1876,
  "pendingRedemptions": 5
}
```

---

## 3. User Analytics

**GET** `/api/admin/analytics/users`

### Response `200 OK`

```json
{
  "countByUserType": [
    { "userType": 1, "count": 45 },
    { "userType": 2, "count": 128 },
    { "userType": 3, "count": 87 },
    { "userType": 4, "count": 8 },
    { "userType": 5, "count": 4 }
  ],
  "countByRegistrationStatus": [
    { "status": 1, "count": 12 },
    { "status": 2, "count": 8 },
    { "status": 3, "count": 248 },
    { "status": 4, "count": 6 }
  ],
  "countByRegion": [
    {
      "regionId": "reg-riyadh",
      "regionNameAr": "الرياض",
      "regionNameEn": "Riyadh",
      "count": 95
    },
    {
      "regionId": "reg-jeddah",
      "regionNameAr": "جدة",
      "regionNameEn": "Jeddah",
      "count": 72
    }
  ],
  "registrationTrend": [
    { "year": 2026, "month": 1, "count": 35 },
    { "year": 2026, "month": 2, "count": 48 },
    { "year": 2026, "month": 3, "count": 62 },
    { "year": 2026, "month": 4, "count": 21 }
  ]
}
```

---

## 4. Region Analytics

**GET** `/api/admin/analytics/regions`

### Response `200 OK`

```json
{
  "regions": [
    {
      "regionId": "reg-riyadh",
      "regionNameAr": "الرياض",
      "regionNameEn": "Riyadh",
      "zoneManagerName": "Ahmad Al-Rashid",
      "cityCount": 18,
      "shopOwnerCount": 20,
      "sellerCount": 45,
      "technicianCount": 30,
      "cities": [
        {
          "cityId": "city-riyadh",
          "cityNameAr": "الرياض",
          "cityNameEn": "Riyadh",
          "approvalSalesManName": "Mohammed Ali",
          "userCount": 52
        },
        {
          "cityId": "city-kharj",
          "cityNameAr": "الخرج",
          "cityNameEn": "Al Kharj",
          "approvalSalesManName": null,
          "userCount": 8
        }
      ]
    }
  ]
}
```

---

## 5. Points Analytics

**GET** `/api/admin/analytics/points`

### Response `200 OK`

```json
{
  "totalEarned": 254300.0,
  "totalRedeemed": 85000.0,
  "totalBalance": 169300.0,
  "pointsByRegion": [
    {
      "regionId": "reg-riyadh",
      "regionNameAr": "الرياض",
      "regionNameEn": "Riyadh",
      "totalEarned": 120500.0
    },
    {
      "regionId": "reg-jeddah",
      "regionNameAr": "جدة",
      "regionNameEn": "Jeddah",
      "totalEarned": 85200.0
    }
  ],
  "pointsByRepresentative": [
    {
      "salesManId": "sm-001",
      "salesManName": "Mohammed Ali",
      "totalEarned": 45000.0,
      "userCount": 32
    },
    {
      "salesManId": "sm-002",
      "salesManName": "Khalid Hassan",
      "totalEarned": 38500.0,
      "userCount": 28
    }
  ],
  "pointsTrend": [
    { "year": 2026, "month": 1, "total": 52000.0 },
    { "year": 2026, "month": 2, "total": 68000.0 },
    { "year": 2026, "month": 3, "total": 78300.0 },
    { "year": 2026, "month": 4, "total": 56000.0 }
  ]
}
```

---

## 6. Points Details (Paginated)

**GET** `/api/admin/analytics/points/details`

### Query Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| userId | string | No | - | Filter by user ID |
| regionId | string | No | - | Filter by region ID |
| dateFrom | DateTime | No | - | Start date (e.g. `2026-01-01`) |
| dateTo | DateTime | No | - | End date (e.g. `2026-04-01`) |
| type | int | No | - | WalletTransactionType enum value (1-6) |
| page | int | No | 1 | Page number |
| pageSize | int | No | 20 | Items per page |

**Example:** `/api/admin/analytics/points/details?regionId=reg-riyadh&type=1&page=1&pageSize=10`

### Response `200 OK`

```json
{
  "items": [
    {
      "transactionId": "txn-a1b2c3d4",
      "userId": "usr-001",
      "userName": "عبدالله محمد",
      "userMobile": "9665********",
      "amount": 150.0,
      "sarAmount": 15.00,
      "type": 1,
      "description": "Scan barcode ABC123",
      "createdAt": "2026-03-28T14:30:00"
    },
    {
      "transactionId": "txn-e5f6g7h8",
      "userId": "usr-002",
      "userName": "فهد العتيبي",
      "userMobile": "9665********",
      "amount": -1000.0,
      "sarAmount": -100.00,
      "type": 2,
      "description": "Cash redemption",
      "createdAt": "2026-03-27T09:15:00"
    }
  ],
  "totalCount": 1876,
  "page": 1,
  "pageSize": 10,
  "totalPages": 188,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

---

## 7. Top Performers

**GET** `/api/admin/analytics/top-performers`

### Query Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| top | int | No | 10 | Number of top performers to return |

**Example:** `/api/admin/analytics/top-performers?top=5`

### Response `200 OK`

```json
{
  "topSellers": [
    {
      "userId": "usr-s01",
      "userName": "محمد السعيد",
      "mobileNumber": "966501234567",
      "regionNameAr": "الرياض",
      "regionNameEn": "Riyadh",
      "totalPointsEarned": 12500.0,
      "totalScans": 245
    },
    {
      "userId": "usr-s02",
      "userName": "أحمد الشمري",
      "mobileNumber": "966509876543",
      "regionNameAr": "جدة",
      "regionNameEn": "Jeddah",
      "totalPointsEarned": 10800.0,
      "totalScans": 198
    }
  ],
  "topTechnicians": [
    {
      "userId": "usr-t01",
      "userName": "خالد العمري",
      "mobileNumber": "966512345678",
      "regionNameAr": "الرياض",
      "regionNameEn": "Riyadh",
      "totalPointsEarned": 9200.0,
      "totalScans": 176
    }
  ]
}
```

---

## 8. Inactive Users (Paginated)

**GET** `/api/admin/analytics/inactive-users`

### Query Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| inactiveDays | int | No | 30 | Days since last scan to consider inactive |
| page | int | No | 1 | Page number |
| pageSize | int | No | 20 | Items per page |

**Example:** `/api/admin/analytics/inactive-users?inactiveDays=60&page=1&pageSize=10`

### Response `200 OK`

```json
{
  "items": [
    {
      "userId": "usr-i01",
      "userName": "سعد الدوسري",
      "mobileNumber": "966507654321",
      "userType": 2,
      "lastScanDate": "2026-02-15T10:30:00",
      "daysSinceLastScan": 46
    },
    {
      "userId": "usr-i02",
      "userName": "ياسر القحطاني",
      "mobileNumber": "966501112222",
      "userType": 3,
      "lastScanDate": null,
      "daysSinceLastScan": 90
    }
  ],
  "totalCount": 23,
  "page": 1,
  "pageSize": 10,
  "totalPages": 3,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

> **Note:** `lastScanDate: null` means the user has never scanned a barcode.

---

## 9. Barcode Analytics

**GET** `/api/admin/analytics/barcodes`

### Response `200 OK`

```json
{
  "totalGenerated": 5000,
  "totalAvailable": 3420,
  "totalSellerScanned": 980,
  "totalTechnicianScanned": 850,
  "totalConsumed": 750,
  "scanRate": 31.6,
  "topProductsByBarcodes": [
    {
      "productId": "prod-001",
      "productName": "زيت محركات 5W-30",
      "productCode": "OIL-5W30",
      "totalBarcodes": 500,
      "scannedCount": 320,
      "consumedCount": 280
    },
    {
      "productId": "prod-002",
      "productName": "فلتر هواء",
      "productCode": "FLT-AIR01",
      "totalBarcodes": 400,
      "scannedCount": 210,
      "consumedCount": 180
    }
  ]
}
```

> **Note:** `scanRate` is a percentage (scanned / total * 100).

---

## 10. Redemption Analytics

**GET** `/api/admin/analytics/redemptions`

### Response `200 OK`

```json
{
  "countByStatus": [
    { "status": 1, "count": 3, "totalSar": 1500.00 },
    { "status": 2, "count": 2, "totalSar": 800.00 },
    { "status": 3, "count": 5, "totalSar": 1200.00 },
    { "status": 4, "count": 15, "totalSar": 4200.00 },
    { "status": 5, "count": 10, "totalSar": 3500.00 },
    { "status": 6, "count": 4, "totalSar": 0.00 },
    { "status": 7, "count": 1, "totalSar": 0.00 }
  ],
  "countByMethod": [
    { "method": 1, "count": 15, "totalSar": 4000.00 },
    { "method": 2, "count": 20, "totalSar": 6000.00 }
  ],
  "totalSarRedeemed": 8500.00,
  "averageProcessingDays": 3.5,
  "pendingCount": 5,
  "redemptionTrend": [
    { "year": 2026, "month": 1, "total": 1500.00 },
    { "year": 2026, "month": 2, "total": 2200.00 },
    { "year": 2026, "month": 3, "total": 3100.00 },
    { "year": 2026, "month": 4, "total": 1700.00 }
  ]
}
```

---

## 11. SalesMan Performance

**GET** `/api/admin/analytics/salesman-performance`

### Response `200 OK`

```json
{
  "salesMen": [
    {
      "salesManId": "sm-001",
      "salesManName": "Mohammed Ali",
      "mobileNumber": "966501234567",
      "assignedUserCount": 45,
      "approvedUserCount": 38,
      "pendingApprovalCount": 3,
      "totalPointsEarned": 45000.0,
      "cityCount": 5
    },
    {
      "salesManId": "sm-002",
      "salesManName": "Khalid Hassan",
      "mobileNumber": "966509876543",
      "assignedUserCount": 32,
      "approvedUserCount": 28,
      "pendingApprovalCount": 2,
      "totalPointsEarned": 38500.0,
      "cityCount": 3
    }
  ]
}
```

---

## 12. Revenue Analytics

**GET** `/api/admin/analytics/revenue`

### Response `200 OK`

```json
{
  "totalSarLiability": 16930.00,
  "totalSarHeld": 2300.00,
  "totalSarPaidOut": 8500.00,
  "totalPointsOutstanding": 169300.0,
  "volumeByType": [
    { "type": "Earned", "count": 1876, "totalPoints": 254300.0, "totalSar": 25430.00 },
    { "type": "Redeemed", "count": 35, "totalPoints": 85000.0, "totalSar": 8500.00 },
    { "type": "Cancelled", "count": 8, "totalPoints": 1200.0, "totalSar": 120.00 },
    { "type": "Expired", "count": 12, "totalPoints": 5200.0, "totalSar": 520.00 },
    { "type": "Refunded", "count": 4, "totalPoints": 3000.0, "totalSar": 300.00 },
    { "type": "InvitationReward", "count": 20, "totalPoints": 2000.0, "totalSar": 200.00 }
  ],
  "payoutTrend": [
    { "year": 2026, "month": 1, "total": 1500.00 },
    { "year": 2026, "month": 2, "total": 2200.00 },
    { "year": 2026, "month": 3, "total": 3100.00 },
    { "year": 2026, "month": 4, "total": 1700.00 }
  ]
}
```

> **Revenue fields explained:**
> - `totalSarLiability` = total SAR balance across all user wallets (what the company owes)
> - `totalSarHeld` = SAR amount held for pending redemptions
> - `totalSarPaidOut` = SAR already paid out via completed redemptions
> - `totalPointsOutstanding` = total point balance across all wallets

---

## Error Responses

All endpoints return errors in ProblemDetails format:

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
  "title": "Internal Server Error",
  "status": 500
}
```

Common HTTP status codes:
- `401` — Missing or invalid token
- `403` — Token valid but user is not SystemAdmin
- `500` — Internal server error
