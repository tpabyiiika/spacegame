create table if not exists user_progress (
  username text primary key,
  data jsonb not null,
  updated_at timestamptz not null default now()
);

create index if not exists idx_user_progress_updated_at on user_progress(updated_at desc);
