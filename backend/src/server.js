import express from 'express';
import cors from 'cors';
import helmet from 'helmet';
import dotenv from 'dotenv';
import { pool } from './db.js';

dotenv.config();

const app = express();
const port = Number(process.env.PORT || 8080);
const apiKey = process.env.API_KEY || '';
const adminKey = process.env.ADMIN_KEY || '';
const corsOrigin = process.env.CORS_ORIGIN || '*';

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

app.use('/api', (req, res, next) => {
  if (!apiKey) return next();
  const provided = req.header('x-api-key');
  if (provided !== apiKey) {
    return res.status(401).json({ error: 'Unauthorized' });
  }
  next();
});

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

app.get('/api/progress/:username', async (req, res) => {
  const username = String(req.params.username || '').trim();
  if (!username) return res.status(400).json({ error: 'username required' });

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
      return res.status(200).json({
        MetaCoins: 0,
        CoreHealthLevel: 0,
        TowerDamageLevel: 0,
        TowerFireRateLevel: 0,
        RepairPowerLevel: 0,
        BestEasy: 0,
        BestNormal: 0,
        BestHard: 0,
        UpdatedUtc: new Date().toISOString()
      });
    }

    return res.status(200).json(result.rows[0].data);
  } catch (err) {
    console.error('GET /api/progress error', err);
    return res.status(500).json({ error: 'internal error' });
  }
});

app.put('/api/progress/:username', async (req, res) => {
  const username = String(req.params.username || '').trim();
  if (!username) return res.status(400).json({ error: 'username required' });

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
  if (!adminKey) {
    return res.status(503).json({ error: 'ADMIN_KEY not configured' });
  }
  const provided = req.header('x-admin-key');
  if (provided !== adminKey) {
    return res.status(401).json({ error: 'Unauthorized' });
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
        `select username, data, updated_at
         from user_progress
         where lower(username) like $1
         order by updated_at desc
         limit $2`,
        [`%${q}%`, limit]
      );
    } else {
      result = await pool.query(
        `select username, data, updated_at
         from user_progress
         order by updated_at desc
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
      banned: false,
      banReason: ''
    }));

    for (let i = 0; i < users.length; i++) {
      const f = await isUserBanned(users[i].username);
      users[i].banned = f.banned;
      users[i].banReason = f.reason;
    }

    return res.status(200).json({ users });
  } catch (err) {
    console.error('GET /api/admin/users error', err);
    return res.status(500).json({ error: 'internal error' });
  }
});

app.post('/api/admin/grant-coins', async (req, res) => {
  const username = String(req.body?.username || '').trim();
  const delta = Number(req.body?.delta || 0);

  if (!username) return res.status(400).json({ error: 'username required' });
  if (!Number.isFinite(delta) || delta === 0) return res.status(400).json({ error: 'delta must be non-zero number' });

  try {
    const existing = await pool.query(
      'select data from user_progress where username = $1 limit 1',
      [username]
    );

    const base = existing.rows.length === 0 ? {
      MetaCoins: 0,
      CoreHealthLevel: 0,
      TowerDamageLevel: 0,
      TowerFireRateLevel: 0,
      RepairPowerLevel: 0,
      BestEasy: 0,
      BestNormal: 0,
      BestHard: 0
    } : existing.rows[0].data;

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
  const username = String(req.body?.username || '').trim();
  if (!username) return res.status(400).json({ error: 'username required' });

  const clean = {
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

  try {
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
  const username = String(req.body?.username || '').trim();
  if (!username) return res.status(400).json({ error: 'username required' });

  try {
    await pool.query('delete from user_progress where username = $1', [username]);
    await pool.query('delete from user_flags where username = $1', [username]);
    return res.status(200).json({ ok: true, username });
  } catch (err) {
    console.error('POST /api/admin/delete-user error', err);
    return res.status(500).json({ error: 'internal error' });
  }
});

app.post('/api/admin/ban-user', async (req, res) => {
  const username = String(req.body?.username || '').trim();
  const banned = !!req.body?.banned;
  const reason = String(req.body?.reason || '').trim();
  if (!username) return res.status(400).json({ error: 'username required' });

  try {
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
