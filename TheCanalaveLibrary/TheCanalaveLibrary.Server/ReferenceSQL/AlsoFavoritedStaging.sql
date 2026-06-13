-- Begin a transaction to ensure the swap is atomic
BEGIN;

-- Drop the old backup table if it exists from a previous run
DROP TABLE IF EXISTS also_favorited_scores_old;

-- Rename the live table to become the old backup
ALTER TABLE also_favorited_scores RENAME TO also_favorited_scores_old;

-- Rename the new staging table to become the new live table
ALTER TABLE also_favorited_scores_staging RENAME TO also_favorited_scores;

-- Commit the transaction. The swap is now complete and live.
COMMIT;

-- Clean up the old table (can be done outside the transaction)
DROP TABLE also_favorited_scores_old;
