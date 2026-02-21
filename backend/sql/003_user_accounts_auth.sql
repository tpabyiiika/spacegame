create table if not exists user_accounts (
  username text primary key,
  password_hash text not null,
  salt text not null,
  is_admin boolean not null default false,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create index if not exists idx_user_accounts_is_admin on user_accounts(is_admin);
