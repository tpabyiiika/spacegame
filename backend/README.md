# Galactic Guardian Backend

## 1. Local run

```bash
cd backend
cp .env.example .env
# edit .env with your DATABASE_URL and API_KEY
npm install
npm run migrate
npm run dev
```

Health check:

```bash
curl http://localhost:8080/health
```

## 2. API

- `GET /api/progress/:username`
- `PUT /api/progress/:username`

If `API_KEY` is set, pass header:

`x-api-key: <API_KEY>`

## 3. Deploy on Render

- Push `backend/` to GitHub.
- Create Render Web Service from repo.
- Root directory: `backend`
- Build command: `npm install && npm run migrate`
- Start command: `npm start`
- Env vars:
  - `DATABASE_URL` = your Neon connection string
  - `API_KEY` = your secret key
  - `CORS_ORIGIN` = `*` (or your domain)

## 4. Unity config

Edit:

`Assets/Resources/backend_config.json`

Example:

```json
{
  "Enabled": true,
  "BaseUrl": "https://your-render-service.onrender.com/api",
  "ApiKey": "your-secret-key",
  "TimeoutSec": 8
}
```
