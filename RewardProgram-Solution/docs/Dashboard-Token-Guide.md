# Admin Dashboard — Token & Refresh Token

## Lifetimes
- Access token: **15 min** (shorter than app — admin surface is sensitive)
- Refresh token: **365 days** (rotates on every refresh — as long as the operator uses the dashboard at least once a year, they stay signed in)

## Endpoints (Admin)
- Login: `POST /api/admin/auth/login` — body `{ "username", "password" }`
- Refresh: `POST /api/admin/auth/refresh` — body `{ "refreshToken": "..." }`
- Logout: `POST /api/admin/auth/logout` — body `{ "refreshToken": "..." }` (auth required)

Admin login is **username/password** (not OTP). Role-gated — only `SystemAdmin` users pass.

## Response shape (login / refresh — identical)
```json
{
  "token": "<jwt>",
  "refreshToken": "<opaque>",
  "expiresIn": 900,
  "refreshTokenExpiration": "2027-04-14T10:00:00Z",
  "user": { "id", "name", "mobileNumber", "userType", "registrationStatus" }
}
```

## Usage
- Send every authenticated call with `Authorization: Bearer <token>`
- Refresh **proactively** when ≤60s left, or **reactively** on `401`
- On successful refresh, overwrite BOTH `token` and `refreshToken`

## ⚠️ Critical rules
1. **Serialize refreshes.** A single refresh mutex across the axios/fetch layer. Parallel refreshes with the same token trigger reuse detection → kills all sessions.
2. **Never retry the same refresh token.** If `/refresh` fails, go to login. The server may have already rotated it; retrying looks like replay.
3. **Token rotates every call.** Treat refresh token as single-use.
4. **Atomic update.** Save both tokens together.

## Error handling
| Error | Action |
|---|---|
| `401` on normal call | Refresh once, retry call |
| `InvalidRefreshToken` | Clear tokens → login page |
| `RefreshTokenRevoked` | Clear tokens → login page (NO retry) |
| `RefreshTokenExpired` | Clear tokens → login page |
| `UserDisabled` | Clear tokens → login + "account disabled" message |

Any refresh failure → login page. Never loop.

## Storage
- Preferred: `httpOnly` cookies (requires backend change — not in place yet).
- Current: use **`sessionStorage`** (not `localStorage`) so tokens clear on tab close.
- Mitigate XSS rigorously (CSP, no inline eval, trusted dependencies).

## Axios interceptor sketch
```ts
let refreshPromise: Promise<void> | null = null;

api.interceptors.response.use(null, async (err) => {
  if (err.response?.status === 401 && !err.config.url.includes('/refresh')) {
    refreshPromise ??= (async () => {
      const r = await api.post('/admin/auth/refresh', { refreshToken: store.refresh });
      store.save(r.data.token, r.data.refreshToken);
    })().finally(() => { refreshPromise = null; });

    await refreshPromise;
    return api.request(err.config);
  }
  if (err.response?.status === 401) {
    store.clear(); router.push('/login');
  }
  throw err;
});
```

## Behavior notes
- 15-min access token means the UI **must** handle silent refresh cleanly — no random mid-session 401 leaks to the user.
- Admin login has lockout: repeated wrong passwords lock the account temporarily (401 with a different message). Surface this distinctly to the operator.
- Logout only revokes the current device's refresh token. Other sessions remain active.
- If a refresh token is replayed (reuse detected), **every** active session for that admin is revoked — all operator tabs/devices must re-login.

## When the operator is forced to re-login
1. **Explicit logout**
2. **365 days idle** (refresh token expired)
3. **Reuse detected** (family wipe — all tabs/devices)
4. **Account disabled** by another admin
5. **Repeated wrong passwords** → temporary lockout

In normal use, only logout forces re-authentication.
