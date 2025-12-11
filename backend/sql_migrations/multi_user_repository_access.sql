-- =====================================================
-- Multi-User Repository Access Migration
-- =====================================================
-- This migration creates a junction table to allow multiple
-- developers to access the same analyzed repository.
-- 
-- Execute this in Supabase SQL Editor
-- =====================================================

-- Step 1: Create the repository_user_access junction table
CREATE TABLE IF NOT EXISTS public.repository_user_access (
    repository_id UUID NOT NULL,
    user_id UUID NOT NULL,
    granted_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    granted_by_user_id UUID,
    
    CONSTRAINT repository_user_access_pkey 
        PRIMARY KEY (repository_id, user_id),
    
    CONSTRAINT repository_user_access_repository_id_fkey 
        FOREIGN KEY (repository_id) 
        REFERENCES public.repositories(id) 
        ON DELETE CASCADE,
    
    CONSTRAINT repository_user_access_user_id_fkey 
        FOREIGN KEY (user_id) 
        REFERENCES public.users(id) 
        ON DELETE CASCADE,
    
    CONSTRAINT repository_user_access_granted_by_user_id_fkey 
        FOREIGN KEY (granted_by_user_id) 
        REFERENCES public.users(id) 
        ON DELETE SET NULL
);

-- Step 2: Create indexes for faster lookups
CREATE INDEX IF NOT EXISTS idx_repository_user_access_user_id 
    ON public.repository_user_access(user_id);

CREATE INDEX IF NOT EXISTS idx_repository_user_access_repository_id 
    ON public.repository_user_access(repository_id);

-- Step 3: Migrate existing data from repositories table
-- For each repository with a connected_by_user_id, grant that user access
INSERT INTO public.repository_user_access (repository_id, user_id, granted_at, granted_by_user_id)
SELECT 
    id AS repository_id,
    connected_by_user_id AS user_id,
    created_at AS granted_at,
    connected_by_user_id AS granted_by_user_id
FROM public.repositories
WHERE connected_by_user_id IS NOT NULL
ON CONFLICT (repository_id, user_id) DO NOTHING;

-- Step 4: Verify migration
-- This should return the number of access grants created
SELECT COUNT(*) AS total_access_grants FROM public.repository_user_access;

-- =====================================================
-- ROLLBACK SCRIPT (if needed)
-- =====================================================
-- Uncomment and run if you need to rollback this migration:
-- DROP TABLE IF EXISTS public.repository_user_access CASCADE;
-- =====================================================
