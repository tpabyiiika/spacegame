create table if not exists user_flags (
  username text primary key,
  banned boolean not null default false,
  reason text not null default '',
  updated_at timestamptz not null default now()
);

create index if not exists idx_user_flags_banned on user_flags(banned);
