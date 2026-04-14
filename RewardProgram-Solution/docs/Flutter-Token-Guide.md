# Flutter — Token & Refresh Token

## Lifetimes
- Access token: **60 min**
- Refresh token: **365 days** (rotates on every refresh — as long as the user opens the app at least once a year, they stay signed in)

## Endpoints (App)
- Login / OTP: `POST /api/auth/login`, `POST /api/auth/register`
- Refresh: `POST /api/auth/refresh` — body `{ "refreshToken": "..." }`
- Logout: `POST /api/auth/logout` — body `{ "refreshToken": "..." }` (auth required)

## Response shape (login / refresh — identical)
```json
{
  "token": "<jwt>",
  "refreshToken": "<opaque>",
  "expiresIn": 3600,
  "refreshTokenExpiration": "2027-04-14T10:00:00Z",
  "user": { "id", "name", "mobileNumber", "userType", "registrationStatus" }
}
```

## Usage
- Send every authenticated call with `Authorization: Bearer <token>`
- Refresh **proactively** when ≤60s left, or **reactively** on `401`
- On successful refresh, overwrite BOTH `token` and `refreshToken`

## ⚠️ Critical rules
1. **Serialize refreshes.** If N calls hit 401 at once, only one calls `/refresh`; others wait. Parallel refreshes with the same token trigger reuse detection → kills all sessions.
2. **Never retry the same refresh token.** If `/refresh` fails (timeout / network), go straight to login. Retrying a token the server may have already rotated looks like replay.
3. **Token rotates every call.** Cached old values are poison.
4. **Atomic update.** Save both tokens together.

## Error handling
| Error | Action |
|---|---|
| `401` on normal call | Refresh once, retry call |
| `InvalidRefreshToken` | Clear tokens → login screen |
| `RefreshTokenRevoked` | Clear tokens → login screen (NO retry) |
| `RefreshTokenExpired` | Clear tokens → login screen |
| `UserDisabled` | Clear tokens → login screen + "account disabled" |

Any refresh failure → login screen. Never loop.

## Storage
Use `flutter_secure_storage` (Keychain on iOS, EncryptedSharedPreferences on Android). Do not use `SharedPreferences`.

## Dio interceptor sketch
```dart
onError: (err) async {
  if (err.response?.statusCode == 401 && !err.requestOptions.path.contains('/refresh')) {
    await refreshMutex.protect(() async {
      if (!isTokenStillExpired()) return;
      final r = await dio.post('/auth/refresh', data: {'refreshToken': storage.refresh});
      storage.saveTokens(r.data['token'], r.data['refreshToken']);
    });
    return dio.fetch(err.requestOptions);
  }
  if (err.response?.statusCode == 401) {
    storage.clear(); navigator.toLogin();
  }
}
```

## Multi-device
Signing into another device does NOT kick the first. All devices stay active until their refresh token expires or is revoked. If one device's refresh token is replayed after rotation (reuse detected), **every** device for that user is logged out.

## When the user is forced to re-login
1. **Explicit logout** on that device
2. **365 days idle** (refresh token expired)
3. **Reuse detected** (family wipe — all devices)
4. **Admin disables the account**
5. **Registration rejected**

In normal use, only logout forces re-authentication.
