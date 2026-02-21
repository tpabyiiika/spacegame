# Cloud API Contract

Client config file: `Assets/Resources/backend_config.json`

```json
{
  "Enabled": true,
  "BaseUrl": "https://your-backend.example.com/api",
  "TimeoutSec": 8
}
```

## Endpoints

1. `POST /auth/register`
- Body: `{ "username": "player1", "password": "password123" }`
- Response: `{ "username": "...", "token": "...", "isAdmin": false, "expiresUtc": "..." }`

2. `POST /auth/login`
- Body: `{ "username": "player1", "password": "password123" }`
- Response: `{ "username": "...", "token": "...", "isAdmin": false, "expiresUtc": "..." }`

3. `GET /progress/{username}`
- Response: JSON object compatible with `ProfileProgressData`.

4. `PUT /progress/{username}`
- Request body: JSON object compatible with `ProfileProgressData`.
- Response: `200 OK` (body optional).

5. `GET /admin/users?limit=50&q=abc` (admin)
- Response: `{ users: [...] }`

6. `POST /admin/grant-coins` (admin)
- Body: `{ \"username\": \"player1\", \"delta\": 500 }`

7. `POST /admin/reset-user` (admin)
- Body: `{ \"username\": \"player1\" }`

8. `POST /admin/delete-user` (admin)
- Body: `{ \"username\": \"player1\" }`

9. `POST /admin/ban-user` (admin)
- Body: `{ \"username\": \"player1\", \"banned\": true, \"reason\": \"text\" }`

## Payload (`ProfileProgressData`)

```json
{
  "MetaCoins": 1200,
  "CoreHealthLevel": 2,
  "TowerDamageLevel": 3,
  "TowerFireRateLevel": 1,
  "RepairPowerLevel": 2,
  "BestEasy": 15000,
  "BestNormal": 9800,
  "BestHard": 6200,
  "UpdatedUtc": "2026-02-20T21:00:00.0000000Z"
}
```

## Auth header

For all endpoints except `/auth/register` and `/auth/login`, send:

`Authorization: Bearer <token>`

Admin endpoints require account with `is_admin = true` in `user_accounts`.
