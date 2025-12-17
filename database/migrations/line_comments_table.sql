-- ============================================
-- LINE COMMENTS SYSTEM - MIGRATION SCRIPT
-- ============================================
-- This script creates the file_line_comments table for storing
-- both shared (public) and personal (private) line-level comments
-- ============================================

-- Create the file_line_comments table
CREATE TABLE IF NOT EXISTS file_line_comments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    repository_id UUID NOT NULL REFERENCES repositories(id) ON DELETE CASCADE,
    file_id UUID NOT NULL REFERENCES repository_files(id) ON DELETE CASCADE,
    line_number INTEGER NOT NULL CHECK (line_number > 0),
    comment_text TEXT NOT NULL CHECK (length(comment_text) > 0 AND length(comment_text) <= 5000),
    is_shared BOOLEAN NOT NULL DEFAULT false,
    created_by_user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Ensure we don't have duplicate personal comments on the same line by the same user
    CONSTRAINT unique_personal_comment_per_user_line 
        UNIQUE (file_id, line_number, created_by_user_id, is_shared) 
        WHERE is_shared = false
);

-- Create indexes for efficient queries
CREATE INDEX IF NOT EXISTS idx_file_line_comments_file_line 
    ON file_line_comments(file_id, line_number);

CREATE INDEX IF NOT EXISTS idx_file_line_comments_user_file 
    ON file_line_comments(created_by_user_id, file_id);

CREATE INDEX IF NOT EXISTS idx_file_line_comments_shared 
    ON file_line_comments(file_id, is_shared) 
    WHERE is_shared = true;

-- Create updated_at trigger function if it doesn't exist
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Add trigger to automatically update updated_at timestamp
DROP TRIGGER IF EXISTS update_file_line_comments_updated_at ON file_line_comments;
CREATE TRIGGER update_file_line_comments_updated_at
    BEFORE UPDATE ON file_line_comments
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Verify the table was created successfully
DO $$
BEGIN
    IF EXISTS (
        SELECT FROM information_schema.tables 
        WHERE table_schema = 'public' 
        AND table_name = 'file_line_comments'
    ) THEN
        RAISE NOTICE 'Table file_line_comments created successfully!';
    ELSE
        RAISE EXCEPTION 'Failed to create table file_line_comments';
    END IF;
END $$;
