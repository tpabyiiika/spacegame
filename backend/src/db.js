import pg from 'pg';
import dotenv from 'dotenv';

dotenv.config();

const { Pool } = pg;

if (!process.env.DATABASE_URL) {
  throw new Error('DATABASE_URL is required');
}

const connectionString = process.env.DATABASE_URL;
const sslDisabledByConnString = /sslmode=disable/i.test(connectionString);
const sslRejectUnauthorized = process.env.DB_SSL_REJECT_UNAUTHORIZED !== 'false';

export const pool = new Pool({
  connectionString,
  ssl: sslDisabledByConnString ? false : { rejectUnauthorized: sslRejectUnauthorized }
});
