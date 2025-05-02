-- Add updated_at column for Yandex Notes-like functionality
alter table public.notes add column updated_at timestamptz;
update public.notes set updated_at = created_at where updated_at is null;
alter table public.notes alter column updated_at set not null;
