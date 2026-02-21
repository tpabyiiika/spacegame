import crypto from 'crypto';
import express from 'express';
import cors from 'cors';
import helmet from 'helmet';
import dotenv from 'dotenv';
import { pool } from './db.js';

dotenv.config();

const app = express();
const port = Number(process.env.PORT || 8080);
const corsOrigin = process.env.CORS_ORIGIN || '*';
const authTokenSecret = process.env.AUTH_TOKEN_SECRET || '';
const authTokenTtlSec = Math.max(300, Number(process.env.AUTH_TOKEN_TTL_SEC || 60 * 60 * 24 * 7));

if (!authTokenSecret) {
  throw new Error('AUTH_TOKEN_SECRET is required');
}

app.use(helmet());
app.use(cors({ origin: corsOrigin === '*' ? true : corsOrigin }));
app.use(express.json({ limit: '256kb' }));

app.get('/health', async (_req, res) => {
  try {
    await pool.query('select 1');
    res.json({ ok: true, db: 'up' });
  } catch {
    res.status(500).json({ ok: false, db: 'down' });
  }
});

function normalizeUsername(value) {
  return String(value || '').trim();
}

function isUsernameValid(username) {
  return /^[A-Za-z0-9_]{3,24}$/.test(username);
}

function isPasswordValid(password) {
  return typeof password === 'string' && password.length >= 6 && password.length <= 128;
}

function hashPassword(password, salt) {
  return crypto.scryptSync(password, salt, 64).toString('hex');
}

function safeEqualHex(a, b) {
  if (typeof a !== 'string' || typeof b !== 'string') return false;
  if (a.length !== b.length) return false;
  const left = Buffer.from(a, 'hex');
  const right = Buffer.from(b, 'hex');
  if (left.length !== right.length) return false;
  return crypto.timingSafeEqual(left, right);
}

function encodeBase64Url(value) {
  return Buffer.from(value).toString('base64url');
}

function decodeBase64Url(value) {
  return Buffer.from(value, 'base64url').toString('utf8');
}

function issueAuthToken(username, isAdmin) {
  const expUnix = Math.floor(Date.now() / 1000) + authTokenTtlSec;
  const payloadObj = {
    sub: username,
    admin: !!isAdmin,
    exp: expUnix
  };
  const payload = encodeBase64Url(JSON.stringify(payloadObj));
  const signature = crypto.createHmac('sha256', authTokenSecret).update(payload).digest('base64url');
  const expiresUtc = new Date(expUnix * 1000).toISOString();
  return {
    token: `${payload}.${signature}`,
    expiresUtc
  };
}

function verifyAuthToken(token) {
  if (!token || typeof token !== 'string') return null;
  const parts = token.split('.');
  if (parts.length !== 2) return null;
  const [payload, providedSignature] = parts;
  const expectedSignature = crypto.createHmac('sha256', authTokenSecret).update(payload).digest('base64url');
  if (providedSignature.length !== expectedSignature.length) return null;
  if (!crypto.timingSafeEqual(Buffer.from(providedSignature), Buffer.from(expectedSignature))) return null;

  let parsed;
  try {
    parsed = JSON.parse(decodeBase64Url(payload));
  } catch {
    return null;
  }

  if (!parsed || typeof parsed.sub !== 'string' || typeof parsed.exp !== 'number') return null;
  if (parsed.exp <= Math.floor(Date.now() / 1000)) return null;

  return {
    username: parsed.sub,
    isAdmin: !!parsed.admin,
    expUnix: parsed.exp
  };
}

function readBearerToken(req) {
  const header = req.header('authorization') || '';
  const prefix = 'Bearer ';
  if (!header.startsWith(prefix)) return '';
  return header.slice(prefix.length).trim();
}

function buildDefaultProgress() {
  return {
    MetaCoins: 0,
    CoreHealthLevel: 0,
    TowerDamageLevel: 0,
    TowerFireRateLevel: 0,
    RepairPowerLevel: 0,
    BestEasy: 0,
    BestNormal: 0,
    BestHard: 0,
    UpdatedUtc: new Date().toISOString()
  };
}

async function isUserBanned(username) {
  const result = await pool.query(
    'select banned, reason from user_flags where username = $1 limit 1',
    [username]
  );
  if (result.rows.length === 0) {
    return { banned: false, reason: '' };
  }
  return {
    banned: !!result.rows[0].banned,
    reason: result.rows[0].reason || ''
  };
}

async function userAccountExists(username) {
  const result = await pool.query(
    'select 1 from user_accounts where username = $1 limit 1',
    [username]
  );
  return result.rows.length > 0;
}

app.post('/api/auth/register', async (req, res) => {
  const username = normalizeUsername(req.body?.username);
  const password = String(req.body?.password || '');

  if (!isUsernameValid(username)) {
    return res.status(400).json({ error: 'username must match [A-Za-z0-9_] and be 3..24 chars' });
  }
  if (!isPasswordValid(password)) {
    return res.status(400).json({ error: 'password must be 6..128 chars' });
  }

  try {
    const existingFlag = await isUserBanned(username);
    if (existingFlag.banned) {
      return res.status(403).json({ error: 'banned', reason: existingFlag.reason || 'Access denied' });
    }

    const existing = await pool.query('select 1 from user_accounts where username = $1 limit 1', [username]);
    if (existing.rows.length > 0) {
      return res.status(409).json({ error: 'username already exists' });
    }

    const salt = crypto.randomBytes(16).toString('hex');
    const passwordHash = hashPassword(password, salt);

    await pool.query(
      `insert into user_accounts (username, password_hash, salt, is_admin, created_at, updated_at)
       values ($1, $2, $3, false, now(), now())`,
      [username, passwordHash, salt]
    );

    await pool.query(
      `insert into user_progress (username, data, updated_at)
       values ($1, $2::jsonb, now())
       on conflict (username)
       do nothing`,
      [username, JSON.stringify(buildDefaultProgress())]
    );

    const session = issueAuthToken(username, false);
    return res.status(201).json({
      username,
      token: session.token,
      isAdmin: false,
      expiresUtc: session.expiresUtc
    });
  } catch (err) {
    console.error('POST /api/auth/register error', err);
    return res.status(500).json({ error: 'internal error' });
  }
});

app.post('/api/auth/login', async (req, res) => {
  const username = normalizeUsername(req.body?.username);
  const password = String(req.body?.password || '');

  if (!isUsernameValid(username) || !isPasswordValid(password)) {
    return res.status(400).json({ error: 'invalid credentials format' });
  }

  try {
    const result = await pool.query(
      `select username, password_hash, salt, is_admin
       from user_accounts
       where username = $1
       limit 1`,
      [username]
    );

    if (result.rows.length === 0) {
      return res.status(401).json({ error: 'invalid credentials' });
    }

    const row = result.rows[0];
    const inputHash = hashPassword(password, row.salt);
    if (!safeEqualHex(inputHash, row.password_hash)) {
      return res.status(401).json({ error: 'invalid credentials' });
    }

    const flag = await isUserBanned(username);
    if (flag.banned) {
      return res.status(403).json({ error: 'banned', reason: flag.reason || 'Access denied' });
    }

    const session = issueAuthToken(username, !!row.is_admin);
    return res.status(200).json({
      username,
      token: session.token,
      isAdmin: !!row.is_admin,
      expiresUtc: session.expiresUtc
    });
  } catch (err) {
    console.error('POST /api/auth/login error', err);
    return res.status(500).json({ error: 'internal error' });
  }
});

app.use('/api', (req, res, next) => {
  if (req.path.startsWith('/auth')) return next();

  const token = readBearerToken(req);
  const auth = verifyAuthToken(token);
  if (!auth) {
    return res.status(401).json({ error: 'unauthorized' });
  }

  req.auth = auth;
  next();
});

app.get('/api/progress/:username', async (req, res) => {
  const username = normalizeUsername(req.params.username);
  if (!username) return res.status(400).json({ error: 'username required' });
  if (req.auth.username !== username) return res.status(403).json({ error: 'forbidden' });

  try {
    const flag = await isUserBanned(username);
    if (flag.banned) {
      return res.status(403).json({ error: 'banned', reason: flag.reason || 'Access denied' });
    }

    const result = await pool.query(
      'select data from user_progress where username = $1 limit 1',
      [username]
    );

    if (result.rows.length === 0) {
      return res.status(200).json(buildDefaultProgress());
    }

    return res.status(200).json(result.rows[0].data);
  } catch (err) {
    console.error('GET /api/progress error', err);
    return res.status(500).json({ error: 'internal error' });
  }
});

app.put('/api/progress/:username', async (req, res) => {
  const username = normalizeUsername(req.params.username);
  if (!username) return res.status(400).json({ error: 'username required' });
  if (req.auth.username !== username) return res.status(403).json({ error: 'forbidden' });

  const payload = req.body;
  if (!payload || typeof payload !== 'object') {
    return res.status(400).json({ error: 'invalid payload' });
  }

  const safeData = {
    MetaCoins: Math.max(0, Number(payload.MetaCoins || 0)),
    CoreHealthLevel: Math.max(0, Number(payload.CoreHealthLevel || 0)),
    TowerDamageLevel: Math.max(0, Number(payload.TowerDamageLevel || 0)),
    TowerFireRateLevel: Math.max(0, Number(payload.TowerFireRateLevel || 0)),
    RepairPowerLevel: Math.max(0, Number(payload.RepairPowerLevel || 0)),
    BestEasy: Math.max(0, Number(payload.BestEasy || 0)),
    BestNormal: Math.max(0, Number(payload.BestNormal || 0)),
    BestHard: Math.max(0, Number(payload.BestHard || 0)),
    UpdatedUtc: new Date().toISOString()
  };

  try {
    const flag = await isUserBanned(username);
    if (flag.banned) {
      return res.status(403).json({ error: 'banned', reason: flag.reason || 'Access denied' });
    }

    await pool.query(
      `insert into user_progress (username, data, updated_at)
       values ($1, $2::jsonb, now())
       on conflict (username)
       do update set data = excluded.data, updated_at = now()`,
      [username, JSON.stringify(safeData)]
    );

    return res.status(200).json({ ok: true });
  } catch (err) {
    console.error('PUT /api/progress error', err);
    return res.status(500).json({ error: 'internal error' });
  }
});

app.use('/api/admin', (req, res, next) => {
  if (!req.auth?.isAdmin) {
    return res.status(403).json({ error: 'forbidden' });
  }
  next();
});

app.get('/api/admin/users', async (req, res) => {
  const limit = Math.min(200, Math.max(1, Number(req.query.limit || 50)));
  const q = String(req.query.q || '').trim().toLowerCase();

  try {
    let result;
    if (q.length > 0) {
      result = await pool.query(
        `select
            a.username,
            coalesce(p.data, '{}'::jsonb) as data,
            coalesce(p.updated_at, a.updated_at) as updated_at,
            coalesce(f.banned, false) as banned,
            coalesce(f.reason, '') as ban_reason
         from user_accounts a
         left join user_progress p on p.username = a.username
         left join user_flags f on f.username = a.username
         where lower(a.username) like $1
         order by coalesce(p.updated_at, a.updated_at) desc
         limit $2`,
        [`%${q}%`, limit]
      );
    } else {
      result = await pool.query(
        `select
            a.username,
            coalesce(p.data, '{}'::jsonb) as data,
            coalesce(p.updated_at, a.updated_at) as updated_at,
            coalesce(f.banned, false) as banned,
            coalesce(f.reason, '') as ban_reason
         from user_accounts a
         left join user_progress p on p.username = a.username
         left join user_flags f on f.username = a.username
         order by coalesce(p.updated_at, a.updated_at) desc
         limit $1`,
        [limit]
      );
    }

    const users = result.rows.map((r) => ({
      username: r.username,
      metaCoins: Number(r.data?.MetaCoins || 0),
      coreHealthLevel: Number(r.data?.CoreHealthLevel || 0),
      towerDamageLevel: Number(r.data?.TowerDamageLevel || 0),
      towerFireRateLevel: Number(r.data?.TowerFireRateLevel || 0),
      repairPowerLevel: Number(r.data?.RepairPowerLevel || 0),
      bestEasy: Number(r.data?.BestEasy || 0),
      bestNormal: Number(r.data?.BestNormal || 0),
      bestHard: Number(r.data?.BestHard || 0),
      updatedUtc: r.updated_at,
      banned: !!r.banned,
      banReason: r.ban_reason || ''
    }));

    return res.status(200).json({ users });
  } catch (err) {
    console.error('GET /api/admin/users error', err);
    return res.status(500).json({ error: 'internal error' });
  }
});

app.post('/api/admin/grant-coins', async (req, res) => {
  const username = normalizeUsername(req.body?.username);
  const delta = Math.trunc(Number(req.body?.delta || 0));

  if (!username) return res.status(400).json({ error: 'username required' });
  if (!Number.isFinite(delta) || delta === 0) return res.status(400).json({ error: 'delta must be non-zero number' });
  if (Math.abs(delta) > 1_000_000) return res.status(400).json({ error: 'delta is too large' });

  try {
    if (!(await userAccountExists(username))) {
      return res.status(404).json({ error: 'user not found' });
    }

    const existing = await pool.query(
      'select data from user_progress where username = $1 limit 1',
      [username]
    );

    const base = existing.rows.length === 0 ? buildDefaultProgress() : existing.rows[0].data;
    const next = {
      ...base,
      MetaCoins: Math.max(0, Number(base.MetaCoins || 0) + delta),
      UpdatedUtc: new Date().toISOString()
    };

    await pool.query(
      `insert into user_progress (username, data, updated_at)
       values ($1, $2::jsonb, now())
       on conflict (username)
       do update set data = excluded.data, updated_at = now()`,
      [username, JSON.stringify(next)]
    );

    return res.status(200).json({ ok: true, username, metaCoins: next.MetaCoins });
  } catch (err) {
    console.error('POST /api/admin/grant-coins error', err);
    return res.status(500).json({ error: 'internal error' });
  }
});

app.post('/api/admin/reset-user', async (req, res) => {
  const username = normalizeUsername(req.body?.username);
  if (!username) return res.status(400).json({ error: 'username required' });

  const clean = buildDefaultProgress();

  try {
    if (!(await userAccountExists(username))) {
      return res.status(404).json({ error: 'user not found' });
    }

    await pool.query(
      `insert into user_progress (username, data, updated_at)
       values ($1, $2::jsonb, now())
       on conflict (username)
       do update set data = excluded.data, updated_at = now()`,
      [username, JSON.stringify(clean)]
    );
    return res.status(200).json({ ok: true, username });
  } catch (err) {
    console.error('POST /api/admin/reset-user error', err);
    return res.status(500).json({ error: 'internal error' });
  }
});

app.post('/api/admin/delete-user', async (req, res) => {
  const username = normalizeUsername(req.body?.username);
  if (!username) return res.status(400).json({ error: 'username required' });

  try {
    if (!(await userAccountExists(username))) {
      return res.status(404).json({ error: 'user not found' });
    }

    await pool.query('delete from user_progress where username = $1', [username]);
    await pool.query('delete from user_flags where username = $1', [username]);
    await pool.query('delete from user_accounts where username = $1', [username]);
    return res.status(200).json({ ok: true, username });
  } catch (err) {
    console.error('POST /api/admin/delete-user error', err);
    return res.status(500).json({ error: 'internal error' });
  }
});

app.post('/api/admin/ban-user', async (req, res) => {
  const username = normalizeUsername(req.body?.username);
  const banned = !!req.body?.banned;
  const reason = String(req.body?.reason || '').trim();
  if (!username) return res.status(400).json({ error: 'username required' });

  try {
    if (!(await userAccountExists(username))) {
      return res.status(404).json({ error: 'user not found' });
    }

    await pool.query(
      `insert into user_flags (username, banned, reason, updated_at)
       values ($1, $2, $3, now())
       on conflict (username)
       do update set banned = excluded.banned, reason = excluded.reason, updated_at = now()`,
      [username, banned, reason]
    );
    return res.status(200).json({ ok: true, username, banned, reason });
  } catch (err) {
    console.error('POST /api/admin/ban-user error', err);
    return res.status(500).json({ error: 'internal error' });
  }
});

app.listen(port, () => {
  console.log(`Server listening on :${port}`);
});
