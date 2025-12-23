-- ============================================
-- NOTES SYSTEM - Database Tables
-- Run this entire script in Supabase SQL Editor
-- ============================================

-- ============================================
-- FILE-LEVEL NOTES TABLES
-- ============================================

-- 1. File Sticky Notes
-- Shared notes attached to a specific file (text or document)
CREATE TABLE public.file_sticky_notes (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  repository_id uuid NOT NULL,
  file_id uuid NOT NULL,
  created_by_user_id uuid NOT NULL,
  note_type text NOT NULL CHECK (note_type IN ('text', 'document')),
  content text,
  document_url text,
  document_name text,
  document_size bigint,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT file_sticky_notes_pkey PRIMARY KEY (id),
  CONSTRAINT file_sticky_notes_repository_id_fkey FOREIGN KEY (repository_id) REFERENCES public.repositories(id) ON DELETE CASCADE,
  CONSTRAINT file_sticky_notes_file_id_fkey FOREIGN KEY (file_id) REFERENCES public.repository_files(id) ON DELETE CASCADE,
  CONSTRAINT file_sticky_notes_created_by_user_id_fkey FOREIGN KEY (created_by_user_id) REFERENCES public.users(id)
);

CREATE INDEX idx_file_sticky_notes_file ON public.file_sticky_notes(file_id);
CREATE INDEX idx_file_sticky_notes_repo ON public.file_sticky_notes(repository_id);

-- 2. File Discussion Threads
-- One discussion thread per file
CREATE TABLE public.file_discussion_threads (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  repository_id uuid NOT NULL,
  file_id uuid NOT NULL,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT file_discussion_threads_pkey PRIMARY KEY (id),
  CONSTRAINT file_discussion_threads_repository_id_fkey FOREIGN KEY (repository_id) REFERENCES public.repositories(id) ON DELETE CASCADE,
  CONSTRAINT file_discussion_threads_file_id_fkey FOREIGN KEY (file_id) REFERENCES public.repository_files(id) ON DELETE CASCADE,
  CONSTRAINT file_discussion_threads_file_unique UNIQUE (file_id)
);

-- 3. File Discussion Messages
-- Messages in a file's discussion thread
CREATE TABLE public.file_discussion_messages (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  thread_id uuid NOT NULL,
  user_id uuid NOT NULL,
  message text NOT NULL,
  mentioned_users uuid[],
  referenced_line_numbers integer[],
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT file_discussion_messages_pkey PRIMARY KEY (id),
  CONSTRAINT file_discussion_messages_thread_id_fkey FOREIGN KEY (thread_id) REFERENCES public.file_discussion_threads(id) ON DELETE CASCADE,
  CONSTRAINT file_discussion_messages_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id)
);

CREATE INDEX idx_file_discussion_messages_thread ON public.file_discussion_messages(thread_id);

-- 4. File Personal Notes
-- Private notes visible only to the creator
CREATE TABLE public.file_personal_notes (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  file_id uuid NOT NULL,
  user_id uuid NOT NULL,
  content text NOT NULL,
  line_number integer,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT file_personal_notes_pkey PRIMARY KEY (id),
  CONSTRAINT file_personal_notes_file_id_fkey FOREIGN KEY (file_id) REFERENCES public.repository_files(id) ON DELETE CASCADE,
  CONSTRAINT file_personal_notes_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE
);

CREATE INDEX idx_file_personal_notes_user_file ON public.file_personal_notes(user_id, file_id);

-- ============================================
-- REPOSITORY-LEVEL NOTES TABLES
-- ============================================

-- 5. Repo Sticky Notes
-- Shared notes attached to the entire repository
CREATE TABLE public.repo_sticky_notes (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  repository_id uuid NOT NULL,
  created_by_user_id uuid NOT NULL,
  note_type text NOT NULL CHECK (note_type IN ('text', 'document')),
  content text,
  document_url text,
  document_name text,
  document_size bigint,
  tagged_file_ids uuid[],
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT repo_sticky_notes_pkey PRIMARY KEY (id),
  CONSTRAINT repo_sticky_notes_repository_id_fkey FOREIGN KEY (repository_id) REFERENCES public.repositories(id) ON DELETE CASCADE,
  CONSTRAINT repo_sticky_notes_created_by_user_id_fkey FOREIGN KEY (created_by_user_id) REFERENCES public.users(id)
);

CREATE INDEX idx_repo_sticky_notes_repo ON public.repo_sticky_notes(repository_id);

-- 6. Repo Discussion Threads
-- One discussion thread per repository
CREATE TABLE public.repo_discussion_threads (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  repository_id uuid NOT NULL,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT repo_discussion_threads_pkey PRIMARY KEY (id),
  CONSTRAINT repo_discussion_threads_repository_id_fkey FOREIGN KEY (repository_id) REFERENCES public.repositories(id) ON DELETE CASCADE,
  CONSTRAINT repo_discussion_threads_repo_unique UNIQUE (repository_id)
);

-- 7. Repo Discussion Messages
-- Messages in a repo's discussion thread
CREATE TABLE public.repo_discussion_messages (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  thread_id uuid NOT NULL,
  user_id uuid NOT NULL,
  message text NOT NULL,
  mentioned_users uuid[],
  tagged_file_ids uuid[],
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT repo_discussion_messages_pkey PRIMARY KEY (id),
  CONSTRAINT repo_discussion_messages_thread_id_fkey FOREIGN KEY (thread_id) REFERENCES public.repo_discussion_threads(id) ON DELETE CASCADE,
  CONSTRAINT repo_discussion_messages_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id)
);

CREATE INDEX idx_repo_discussion_messages_thread ON public.repo_discussion_messages(thread_id);

-- 8. Repo Personal Notes
-- Private repo-level notes visible only to the creator
CREATE TABLE public.repo_personal_notes (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  repository_id uuid NOT NULL,
  user_id uuid NOT NULL,
  content text NOT NULL,
  tagged_file_ids uuid[],
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT repo_personal_notes_pkey PRIMARY KEY (id),
  CONSTRAINT repo_personal_notes_repository_id_fkey FOREIGN KEY (repository_id) REFERENCES public.repositories(id) ON DELETE CASCADE,
  CONSTRAINT repo_personal_notes_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE
);

CREATE INDEX idx_repo_personal_notes_user_repo ON public.repo_personal_notes(user_id, repository_id);

-- ============================================
-- NOTIFICATIONS TABLE
-- ============================================

-- 9. User Notifications
-- GitHub-style notifications for @mentions
CREATE TABLE public.user_notifications (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid NOT NULL,
  notification_type text NOT NULL CHECK (notification_type IN ('mention', 'reply', 'note_created')),
  title text NOT NULL,
  message text NOT NULL,
  link_url text,
  related_file_id uuid,
  related_repository_id uuid,
  is_read boolean DEFAULT false,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT user_notifications_pkey PRIMARY KEY (id),
  CONSTRAINT user_notifications_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE,
  CONSTRAINT user_notifications_related_file_id_fkey FOREIGN KEY (related_file_id) REFERENCES public.repository_files(id) ON DELETE SET NULL,
  CONSTRAINT user_notifications_related_repository_id_fkey FOREIGN KEY (related_repository_id) REFERENCES public.repositories(id) ON DELETE SET NULL
);

CREATE INDEX idx_user_notifications_user_unread ON public.user_notifications(user_id, is_read);
CREATE INDEX idx_user_notifications_user_created ON public.user_notifications(user_id, created_at DESC);

-- ============================================
-- DONE! All 9 tables created.
-- ============================================
