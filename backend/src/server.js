import express from 'express';
import cors from 'cors';
import helmet from 'helmet';
import dotenv from 'dotenv';
import { pool } from './db.js';

dotenv.config();

const app = express();
const port = Number(process.env.PORT || 8080);
const apiKey = process.env.API_KEY || '';
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

app.get('/api/progress/:username', async (req, res) => {
  const username = String(req.params.username || '').trim();
  if (!username) return res.status(400).json({ error: 'username required' });

  try {
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

app.listen(port, () => {
  console.log(`Server listening on :${port}`);
});
