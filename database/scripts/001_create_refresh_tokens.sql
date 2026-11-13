-- RMMS: authentication session storage.
-- Manual Database First change. Does not modify Users or insert data.
-- Intentionally fails if RefreshTokens already exists, rather than silently
-- accepting an unknown table structure. Run the entire script together.

BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '30s';

CREATE TABLE public."RefreshTokens"
(
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "TokenHash" varchar(64) NOT NULL,
    "FamilyId" uuid NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    "ExpiresAt" timestamptz NOT NULL,
    "RevokedAt" timestamptz NULL,
    "ReplacedByTokenId" uuid NULL,

    CONSTRAINT "RefreshTokens_pkey" PRIMARY KEY ("Id"),
    CONSTRAINT "RefreshTokens_TokenHash_key" UNIQUE ("TokenHash"),
    CONSTRAINT "RefreshTokens_UserId_fkey"
        FOREIGN KEY ("UserId") REFERENCES public."Users" ("Id")
        ON DELETE RESTRICT,
    CONSTRAINT "RefreshTokens_ReplacedByTokenId_fkey"
        FOREIGN KEY ("ReplacedByTokenId") REFERENCES public."RefreshTokens" ("Id")
        ON DELETE RESTRICT,
    CONSTRAINT "RefreshTokens_TokenHash_check"
        CHECK ("TokenHash" ~ '^[0-9A-F]{64}$'),
    CONSTRAINT "RefreshTokens_ExpiresAt_check"
        CHECK ("ExpiresAt" > "CreatedAt"),
    CONSTRAINT "RefreshTokens_ReplacementRevoked_check"
        CHECK ("ReplacedByTokenId" IS NULL OR "RevokedAt" IS NOT NULL)
);

CREATE INDEX "RefreshTokens_UserId_FamilyId_idx"
    ON public."RefreshTokens" ("UserId", "FamilyId");
CREATE INDEX "RefreshTokens_ExpiresAt_idx"
    ON public."RefreshTokens" ("ExpiresAt");

COMMENT ON TABLE public."RefreshTokens" IS
    'Refresh token hashes and rotation history. Each FamilyId identifies one login session.';
COMMENT ON COLUMN public."RefreshTokens"."TokenHash" IS
    'SHA-256 uppercase hexadecimal hash only. Never store the original refresh token.';
COMMENT ON COLUMN public."RefreshTokens"."FamilyId" IS
    'Shared by all rotated tokens in one session. A replacement must belong to the same user and family.';
COMMENT ON COLUMN public."RefreshTokens"."ExpiresAt" IS
    'Absolute session expiry; preserved when rotating a refresh token.';

-- Supabase browser roles must not access authentication material directly.
-- The trusted backend uses its configured database connection.
ALTER TABLE public."RefreshTokens" ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public."RefreshTokens" FROM PUBLIC, anon, authenticated;

COMMIT;
