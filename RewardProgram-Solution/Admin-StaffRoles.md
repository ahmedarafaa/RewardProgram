# Admin Dashboard — Staff Roles (SalesMan ⇄ ZoneManager ⇄ Dual)

_Target: admin dashboard developer_
_Companion to `Admin-SMZM-Changes.md` (reassign / delete flows)._

---

## TL;DR

- One new endpoint: **`PUT /api/admin/users/{id}/staff-roles`**. It sets the user's full role set: `["SalesMan"]`, `["ZoneManager"]` or both.
- It converts **in place**. Same user id, same mobile, same history. No delete + recreate.
- A role being **removed** must hand off its territory in the same request. A role being **added** may optionally receive territory.
- Two new read-only fields on every user row and on user detail: **`isDualRole`** (bool) and the existing **`roles`** array. Badge dual-role users.
- After a successful change the user is **signed out everywhere** and must log in again.

---

## 1. Showing dual-role users

`GET /api/admin/users` rows and `GET /api/admin/users/{id}` both now carry:

```json
"roles": ["ZoneManager", "SalesMan"],
"isDualRole": true
```

- `roles` is ordered ZoneManager first, then SalesMan. For non-staff users it holds their single role.
- `userType` is still the **primary** type (4 = SalesMan, 5 = ZoneManager). For a dual user it can be either: it is whatever the user was before the second role was added. Do not infer roles from `userType`; use `roles`.
- A dual user appears under **both** the SalesMan tab (`?userType=4`) and the ZoneManager tab (`?userType=5`).

**UI:** when `isDualRole` is true show a badge next to the name, e.g. `مندوب + مدير منطقة` / `SM + ZM`, on the list row and on the detail header. Tooltip: "This user approves as both roles. Deleting under one role is blocked while they still hold territory under the other."

---

## 2. The role-change action

Show a row action **"تغيير الأدوار" / "Change roles"** on rows where `userType` is 4 or 5 and the user is not deleted and not disabled. Open a dialog prefilled from `GET /api/admin/users/{id}`.

### Dialog

Two checkboxes: **SalesMan**, **ZoneManager**. At least one must stay checked. Prefill from `roles`.

Then show conditional sections based on the diff between the current roles and the checked ones:

| Change | Show | Field |
|---|---|---|
| SalesMan **unchecked** and `ownedCities` is non-empty | "Hand off cities" table: one row per owned city with a SalesMan picker | `cityReassignments[]` |
| ZoneManager **unchecked** and `managedRegion` is not null | "Replacement zone manager" picker | `newZoneManagerId` |
| SalesMan **newly checked** | Optional multi-select of cities that currently have **no** SalesMan | `cityIds[]` |
| ZoneManager **newly checked** | Optional select of regions that currently have **no** ZoneManager | `regionId` |

Pickers: reuse the ones from the delete dialogs. SalesMan picker = users with `"SalesMan"` in `roles`, active, excluding this user. ZoneManager picker = users with `"ZoneManager"` in `roles`, active, excluding this user and anyone who already has a `managedRegion`.

### Request

```
PUT /api/admin/users/{id}/staff-roles
Authorization: Bearer <admin token>   (permission: Users.Manage)
```

```json
{
  "roles": ["ZoneManager"],
  "cityReassignments": [
    { "cityId": "c1", "newSalesManId": "sm-2" },
    { "cityId": "c2", "newSalesManId": "sm-3" }
  ],
  "newZoneManagerId": null,
  "regionId": "r-north",
  "cityIds": null
}
```

Send only what the diff needs. Unused fields may be `null` or omitted. `roles` values are case-insensitive.

### Response `200`

```json
{
  "userId": "…",
  "name": "خالد",
  "userType": 5,
  "roles": ["ZoneManager"],
  "isDualRole": false,
  "message": "تم تحديث أدوار المستخدم بنجاح — يجب عليه تسجيل الدخول مجدداً"
}
```

Show `message` as the success toast. Refresh the row. If the checked set equals the current set the server returns `200` with the "unchanged" message and writes nothing.

`userType` in the response is sticky: adding a role never changes it, removing the primary role switches it to the remaining role. Gate UI on `roles`, not on `userType`.

---

## 3. Examples

**SalesMan → ZoneManager** (owns 2 cities, give him the North region)

```json
{ "roles": ["ZoneManager"],
  "cityReassignments": [ { "cityId": "c1", "newSalesManId": "sm-2" }, { "cityId": "c2", "newSalesManId": "sm-2" } ],
  "regionId": "r-north" }
```

**ZoneManager → SalesMan** (manages a region, start idle)

```json
{ "roles": ["SalesMan"], "newZoneManagerId": "zm-2" }
```

**ZoneManager → dual** (keep region, also take 3 unassigned cities)

```json
{ "roles": ["ZoneManager", "SalesMan"], "cityIds": ["c7", "c8", "c9"] }
```

**Dual → SalesMan only** (drop the region)

```json
{ "roles": ["SalesMan"], "newZoneManagerId": "zm-2" }
```

**Idle SalesMan → ZoneManager, stay idle**

```json
{ "roles": ["ZoneManager"] }
```

---

## 4. Errors

Errors come back as ProblemDetails with an `error` array: read `error[0].code` and show `error[0].description` (already localized by `Accept-Language`).

| HTTP | Code | When | UI hint |
|---|---|---|---|
| 400 | `Admin.AllCitiesMustBeReassigned` | SalesMan removed but not every owned city has a target | Highlight rows missing a picker value |
| 400 | `Admin.CityNotOwnedBySalesMan` | A `cityId` in `cityReassignments` is not owned by this user | Stale dialog, reload detail |
| 400 | `Admin.DuplicateCityReassignment` | Same city listed twice | Client-side guard |
| 400 | `Admin.CannotReassignToSelf` | A target is the user themself | Exclude user from pickers |
| 400 | `Admin.ReassignmentTargetNotSalesMan` | City target is not a SalesMan | Filter picker by `roles` |
| 400 | `Admin.ReassignmentTargetNotZoneManager` | Region target is not a ZoneManager | Filter picker by `roles` |
| 400 | `Admin.ReassignmentTargetInactive` | Target is disabled or deleted | Filter picker to active |
| 400 | `Admin.ReplacementZoneManagerRequired` | ZoneManager removed while managing a region, no `newZoneManagerId` | Make the field required |
| 409 | `Admin.ZoneManagerAlreadyAssigned` | Replacement already manages another region | Exclude from picker |
| 400 | `Admin.SomeCitiesNotFound` | A `cityIds` entry is unknown or inactive | Reload city list |
| 409 | `Admin.CityAlreadyHasSalesMan` | A `cityIds` entry already has a SalesMan | Only offer unassigned cities |
| 400 | `Admin.RegionNotFound` | `regionId` unknown or inactive | Reload region list |
| 409 | `Admin.RegionAlreadyHasZoneManager` | `regionId` already has a ZoneManager | Only offer unmanaged regions |
| 400 | `Admin.InvalidStaffRole` | A role outside SalesMan / ZoneManager, or empty set | Client-side guard |
| 400 | `Admin.UserNotStaff` | Target user is not SalesMan / ZoneManager | Hide the action |
| 400 | `Admin.CannotChangeRolesOfInactiveUser` | User is disabled or deleted | Hide the action |
| 403 | `Admin.UserIsSystemAdmin` | Target is the system admin | Hide the action |
| 404 | `Admin.UserNotFound` | Unknown id | Toast + refresh list |
| 409 | `Admin.ConcurrencyConflict` | Someone changed territory meanwhile | Toast "retry" and reload detail |

Body validation failures (missing `roles`, empty `cityId`) come back as the standard `400` with an `errors` dictionary.

---

## 5. Changes to existing flows you already use

- **Delete SalesMan / Delete ZoneManager** now return `400 Admin.OtherRoleTerritoryMustBeHandedOff` when a **dual-role** user still holds territory under the *other* role. Message tells the admin to reassign it or remove that role first. Show it as-is. For dual users prefer the Change-roles dialog over delete.
- **Reassign cities / Reassign region / Delete** now accept a dual-role user as the target even when their primary `userType` is the other role. Build pickers from `roles`, not from `userType`.
- **Disable (toggle-status)** on a dual-role user is blocked while they hold cities *or* a region. Same error as before, `Admin.ReassignTerritoryBeforeDisable`.

---

## 6. What happens to the user

- Their refresh tokens are revoked and the security stamp is bumped. The mobile app will get a 401 on its next refresh and must log in again. Their live access token stays valid for at most 60 minutes.
- Approval queues follow the database, not the token, so from the moment of the change they see the queue of their new role set.
- No notification is sent to the user. Tell them out of band.
