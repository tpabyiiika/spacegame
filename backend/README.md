# Galactic Guardian Backend

## 1. Local run

```bash
cd backend
cp .env.example .env
# edit .env with your DATABASE_URL and AUTH_TOKEN_SECRET
npm install
npm run migrate
npm run dev
```

Health check:

```bash
curl http://localhost:8080/health
```

## 2. API

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/progress/:username`
- `PUT /api/progress/:username`
- `GET /api/admin/users?limit=50&q=abc` (admin)
- `POST /api/admin/grant-coins` (admin)
- `POST /api/admin/reset-user` (admin)
- `POST /api/admin/delete-user` (admin)
- `POST /api/admin/ban-user` (admin)

Auth is token-based:

`Authorization: Bearer <token>`

Admin endpoints are allowed only for accounts where `user_accounts.is_admin = true`.

## 3. Deploy on Render

- Push `backend/` to GitHub.
- Create Render Web Service from repo.
- Root directory: `backend`
- Build command: `npm install && npm run migrate`
- Start command: `npm start`
- Env vars:
  - `DATABASE_URL` = your Neon connection string
  - `AUTH_TOKEN_SECRET` = long random secret (required)
  - `AUTH_TOKEN_TTL_SEC` = token lifetime in seconds (optional, default 604800)
  - `DB_SSL_REJECT_UNAUTHORIZED` = `true` for strict TLS (recommended)
  - `CORS_ORIGIN` = `*` (or your domain)

## 4. Unity config

Edit:

`Assets/Resources/backend_config.json`

Example:

```json
{
  "Enabled": true,
  "BaseUrl": "https://your-render-service.onrender.com/api",
  "TimeoutSec": 8
}
```

## 5. Grant admin role

Example SQL:

```sql
update user_accounts set is_admin = true where username = 'your_admin_username';
```
