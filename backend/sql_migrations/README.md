# Multi-User Repository Access - SQL Migration

## Execute this SQL in Supabase SQL Editor

**File Location:** `D:\ups\foresite\ups_foresite_backend\backend\sql_migrations\multi_user_repository_access.sql`

### What this migration does:

1. Creates `repository_user_access` junction table
2. Adds indexes for performance
3. Migrates existing data from `repositories.connected_by_user_id`
4. Provides rollback script

### To execute:

1. Open Supabase Dashboard
2. Go to SQL Editor
3. Copy and paste the contents of the migration file
4. Click "Run"
5. Verify the count matches your expectations

The migration is idempotent (safe to run multiple times).
