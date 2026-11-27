-- Migration 014: section-2 account, temporary-password and reset-request workflow.
-- Database First: run deliberately against the selected development database.

BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '30s';

ALTER TABLE public."Users"
    ADD COLUMN IF NOT EXISTS "MustChangePassword" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "LastLoginAt" timestamptz NULL;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typnamespace = 'public'::regnamespace AND typname = 'password_reset_request_status') THEN
        CREATE TYPE public.password_reset_request_status AS ENUM ('OPEN', 'DONE', 'REJECTED', 'CANCELLED');
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS public."PasswordResetRequests"
(
    "Id" uuid NOT NULL,
    "RequestCode" varchar(50) NOT NULL,
    "UserId" uuid NOT NULL,
    "Note" varchar(120) NULL,
    "Status" public.password_reset_request_status NOT NULL DEFAULT 'OPEN'::public.password_reset_request_status,
    "RequestedAt" timestamptz NOT NULL DEFAULT now(),
    "ProcessedAt" timestamptz NULL,
    "ProcessedByUserId" uuid NULL,
    CONSTRAINT "PasswordResetRequests_pkey" PRIMARY KEY ("Id"),
    CONSTRAINT "PasswordResetRequests_RequestCode_key" UNIQUE ("RequestCode"),
    CONSTRAINT "PasswordResetRequests_UserId_fkey" FOREIGN KEY ("UserId") REFERENCES public."Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "PasswordResetRequests_ProcessedByUserId_fkey" FOREIGN KEY ("ProcessedByUserId") REFERENCES public."Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "PasswordResetRequests_Note_check" CHECK ("Note" IS NULL OR length("Note") <= 120),
    CONSTRAINT "PasswordResetRequests_Processed_check" CHECK (("Status" = 'OPEN'::public.password_reset_request_status AND "ProcessedAt" IS NULL AND "ProcessedByUserId" IS NULL) OR ("Status" <> 'OPEN'::public.password_reset_request_status AND "ProcessedAt" IS NOT NULL))
);

CREATE UNIQUE INDEX IF NOT EXISTS "PasswordResetRequests_UserId_Open_key"
    ON public."PasswordResetRequests" ("UserId")
    WHERE "Status" = 'OPEN'::public.password_reset_request_status;
CREATE INDEX IF NOT EXISTS "PasswordResetRequests_Status_RequestedAt_idx"
    ON public."PasswordResetRequests" ("Status", "RequestedAt" DESC);

ALTER TABLE public."PasswordResetRequests" ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public."PasswordResetRequests" FROM PUBLIC, anon, authenticated;

COMMIT;
