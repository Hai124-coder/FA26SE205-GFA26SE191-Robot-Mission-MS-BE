-- Migration 015: persistent lifecycle markers for section 4.
-- Apply before deploying code that uses these columns.
-- Existing robot states are preserved. Ambiguous legacy rows require review.

BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '30s';

ALTER TABLE public."Robots"
    ADD COLUMN IF NOT EXISTS "AwaitingFirstConnection" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "SuspendRequestedAt" timestamptz NULL,
    ADD COLUMN IF NOT EXISTS "DecommissionedAt" timestamptz NULL;

COMMIT;
