import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { pool } from './db.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

async function run() {
  const sqlPath = path.resolve(__dirname, '../sql/001_init.sql');
  const sql = fs.readFileSync(sqlPath, 'utf8');
  await pool.query(sql);
  await pool.end();
  console.log('Migration complete');
}

run().catch(async (err) => {
  console.error('Migration failed:', err);
  try { await pool.end(); } catch {}
  process.exit(1);
});
