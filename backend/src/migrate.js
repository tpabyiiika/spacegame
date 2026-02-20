import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { pool } from './db.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

async function run() {
  const dir = path.resolve(__dirname, '../sql');
  const files = fs.readdirSync(dir).filter((f) => f.endsWith('.sql')).sort();
  for (const file of files) {
    const sqlPath = path.join(dir, file);
    const sql = fs.readFileSync(sqlPath, 'utf8');
    await pool.query(sql);
    console.log(`Applied migration: ${file}`);
  }
  await pool.end();
  console.log('Migration complete');
}

run().catch(async (err) => {
  console.error('Migration failed:', err);
  try { await pool.end(); } catch {}
  process.exit(1);
});
