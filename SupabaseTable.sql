-- Run this SQL in the Supabase SQL Editor to create the notes table
create table public.notes (
  id uuid primary key default gen_random_uuid(),
  title text not null,
  content text not null,
  created_at timestamptz not null default now()
);
