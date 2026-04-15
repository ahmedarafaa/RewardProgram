# Admin Dashboard — SM/ZM Assignment Changes

_Target: admin dashboard developer_
_Feature: enforce the invariant that every city has exactly one SalesMan and every region has exactly one ZoneManager. Identity edits are split from assignment changes._

---

## TL;DR

- **Edit SM/ZM** now only changes the **name**. Mobile, cities, region are read-only.
- **Add SM/ZM** can create an **idle user** (no cities / no region) — cities and region fields are optional.
- Use **new reassign endpoints** to move cities between SalesMen or move a region to a different ZoneManager.
- **Deleting an SM/ZM requires a replacement plan** in the request body; the backend refuses to orphan cities or regions.

---

## 1. Modified Endpoints

### 1.1 Edit SalesMan — name only

`PUT /api/admin/users/salesman/{id}`

**New body:**
```json
{ "name": "أحمد المندوب" }
```

**What to change in the UI:**
- Remove `cityIds` and `mobileNumber` from the form.
- Display mobile as **read-only** (informational).
- Display the owned-cities list as **read-only**.
- Add a separate "نقل المدن" (Move cities) button that opens the Reassign-Cities dialog described in §2.1.

### 1.2 Edit ZoneManager — name only

`PUT /api/admin/users/zone-manager/{id}`

**New body:**
```json
{ "name": "خالد المدير" }
```

**What to change in the UI:**
- Remove `regionId` and `mobileNumber` from the form.
- Display mobile as **read-only**.
- Display the managed region as **read-only**.
- Add a "نقل المنطقة" (Move region) button that opens the Reassign-Region dialog described in §2.2.

### 1.3 Add SalesMan — cities optional

`POST /api/admin/users/salesman`

**Body (cityIds optional):**
```json
{
  "name": "أحمد المندوب",
  "mobileNumber": "0512345678",
  "cityIds": ["city-id-1", "city-id-2"]   // OR omit / send []
}
```

**UI notes:**
- Allow submitting with no cities → creates an **idle SalesMan** who can be assigned cities later via Reassign Cities.
- If `cityIds` are provided, every target city must have **no current SalesMan** — otherwise `409 CityAlreadyHasSalesMan` is returned.

### 1.4 Add ZoneManager — region optional

`POST /api/admin/users/zone-manager`

**Body (regionId optional):**
```json
{
  "name": "خالد المدير",
  "mobileNumber": "0512345679",
  "regionId": "region-id-1"                 // OR omit / send null
}
```

**UI notes:**
- Allow submitting with no region → creates an **idle ZoneManager**.
- If `regionId` is provided, the target region must have **no current ZoneManager** — otherwise `409 RegionAlreadyHasZoneManager`.

---

## 2. New Endpoints (4)

### 2.1 Reassign Cities

`POST /api/admin/users/cities/reassign`

**Body:**
```json
{
  "cityIds": ["city-id-1", "city-id-2"],
  "toSalesManId": "sm-user-id"
}
```

**Response:** `204 No Content` on success.

**Use when:** the admin moves one or more cities from their current SalesMan (or from unassigned) to a different SalesMan.

**What the backend does automatically (you don't need to handle):**
- Updates `city.ApprovalSalesManId` for each city.
- Rewires `AssignedSalesManId` on every user (shop owner / seller / technician) in those cities so **pending registration requests** are routed to the new SalesMan.
- **Pending redemption requests** are resolved dynamically from `city.ApprovalSalesManId`, so no further work is needed.

**UI flow:**
1. Open a "Move cities" dialog (from Edit SM page, or from a dedicated Reassign page).
2. Multi-select cities currently owned by the source SalesMan.
3. Pick a destination SalesMan from a dropdown (list returned by `GET /api/admin/users?userType=SalesMan`).
4. Submit.

**Validation errors:**
- `404 UserNotFound` — destination user ID doesn't exist.
- `400 ReassignmentTargetNotSalesMan` — destination user isn't a SalesMan.
- `400 SomeCitiesNotFound` — one or more city IDs invalid/inactive.

### 2.2 Reassign Region

`POST /api/admin/users/regions/reassign`

**Body:**
```json
{
  "regionId": "region-id-1",
  "toZoneManagerId": "zm-user-id"
}
```

**Response:** `204 No Content`.

**Use when:** the admin moves a region from its current ZoneManager to a different ZoneManager.

**Backend behaviour:** updates `region.ZoneManagerId`. Pending ZM-level registration and redemption requests are resolved dynamically via this field, so no user-record migration is needed.

**UI flow:**
1. Open a "Move region" dialog.
2. Pick the region (or pre-fill if invoked from an Edit-ZM page).
3. Pick a destination ZoneManager from a dropdown.
4. Submit.

**Validation errors:**
- `404 UserNotFound` / `400 RegionNotFound`.
- `400 ReassignmentTargetNotZoneManager` — destination user isn't a ZoneManager.

### 2.3 Delete SalesMan

`DELETE /api/admin/users/salesman/{id}`

**Body (required — even if empty):**
```json
{
  "cityReassignments": [
    { "cityId": "city-id-1", "newSalesManId": "sm-user-id-a" },
    { "cityId": "city-id-2", "newSalesManId": "sm-user-id-b" }
  ]
}
```

**Response:** `204 No Content`.

**Critical rule:** `cityReassignments` MUST contain an entry for **every** city the SalesMan currently owns — not less, not more. If the SalesMan is idle (zero owned cities), send `{ "cityReassignments": [] }`.

**UI flow:**
1. On "Delete SM" click, first fetch the SM's currently owned cities.
2. **If zero cities** → show a simple confirm dialog and POST with an empty array.
3. **If one or more cities**:
   - Render a grid: `city name | dropdown of other SalesMen`.
   - The dropdown for each row excludes the SM being deleted.
   - Require every row to have a selection before enabling Submit.
   - Submit the full `cityReassignments` array.

**Validation errors:**
- `400 AllCitiesMustBeReassigned` — the array is missing cities, has extras, or duplicates.
- `400 CityNotOwnedBySalesMan` — one of the `newSalesManId` values is the SM being deleted (cannot reassign to self).
- `400 ReassignmentTargetNotSalesMan` — one of the `newSalesManId` values is not a SalesMan.

### 2.4 Delete ZoneManager

`DELETE /api/admin/users/zone-manager/{id}`

**Body (required — `newZoneManagerId` may be null):**
```json
{ "newZoneManagerId": "zm-user-id-replacement" }
```

Or for an idle ZM:
```json
{ "newZoneManagerId": null }
```

**Response:** `204 No Content`.

**Critical rule:** if the ZM currently manages a region, `newZoneManagerId` is **mandatory** and must point to another ZoneManager. If the ZM has no region, it can be `null`.

**UI flow:**
1. On "Delete ZM" click, fetch whether the ZM manages a region.
2. **If no region** → simple confirm, send `{ "newZoneManagerId": null }`.
3. **If managing a region** → force the admin to pick a replacement ZoneManager from a dropdown before confirm.

**Validation errors:**
- `400 ReplacementZoneManagerRequired` — ZM owns a region but `newZoneManagerId` was null.
- `400 ReassignmentTargetNotZoneManager` — replacement isn't a ZoneManager, or is the ZM being deleted.

---

## 3. Error Code Reference

All error responses are `ProblemDetails` with an Arabic `detail` message; the codes below appear in the `code` / `title` field.

| HTTP | Code | When |
|------|------|------|
| 400 | `AllCitiesMustBeReassigned` | Delete SM body doesn't cover every owned city |
| 400 | `ReplacementZoneManagerRequired` | Delete ZM with a region but no `newZoneManagerId` |
| 400 | `ReassignmentTargetNotSalesMan` | Target user isn't a SalesMan |
| 400 | `ReassignmentTargetNotZoneManager` | Target user isn't a ZoneManager |
| 400 | `CityNotOwnedBySalesMan` | Reassign target equals the user being deleted |
| 400 | `SomeCitiesNotFound` | Invalid / inactive city ID in the list |
| 400 | `RegionNotFound` | Invalid region ID |
| 404 | `UserNotFound` | Target user ID doesn't exist |
| 409 | `CityAlreadyHasSalesMan` | Add/assign targeting a city that already has a SalesMan |
| 409 | `RegionAlreadyHasZoneManager` | Add/assign targeting a region that already has a ZoneManager |

---

## 4. What the Admin App Does NOT Need to Handle

- **Pending registration routing** — backend rewires `AssignedSalesManId` automatically on reassign/delete.
- **Pending redemption routing** — fully dynamic lookup; no action needed.
- **Mobile number edit** — immutable for SM/ZM by design. Always show as read-only.
- **"Soft-delete vs hard-delete"** — the backend soft-deletes via `IsAccountDeleted` + `AccountDeletedAt`; the dashboard only sees that the user no longer appears in active listings.

---

## 5. Suggested Admin Dashboard Flow (end-to-end example)

**Scenario:** SalesMan "أحمد" currently owns 3 cities (Riyadh, Jeddah, Dammam) and needs to be deleted.

1. Admin opens SalesMan list → clicks "Delete" on أحمد's row.
2. Dashboard calls `GET /api/admin/users?userType=SalesMan` to get the list of other SalesMen.
3. Dashboard fetches أحمد's current cities from the user-detail response.
4. Dialog shows:

   | City | New SalesMan |
   |------|--------------|
   | Riyadh | _[dropdown]_ |
   | Jeddah | _[dropdown]_ |
   | Dammam | _[dropdown]_ |

5. Each dropdown excludes أحمد from its options.
6. "Delete" button stays disabled until all three rows have a selection.
7. Admin picks destinations → clicks Delete.
8. Dashboard sends `DELETE /api/admin/users/salesman/{ahmed-id}` with the full `cityReassignments` array.
9. On `204` → remove أحمد from the list, show toast "تم حذف المندوب ونقل المدن بنجاح".
10. Cities now belong to the new owners; pending requests auto-routed.

---

## 6. Postman Reference

The `RewardProgram-Admin-API.postman_collection.json` collection has a new folder **"Users — Reassign & Delete"** with all four new endpoints pre-populated. Use it to prototype before wiring the dashboard UI.
