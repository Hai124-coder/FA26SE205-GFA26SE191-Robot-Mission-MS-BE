-- Migration 005: request ownership, operator floor scope, and persisted Excel-import drafts.
-- Database First: run after migrations 001-004. This migration does not create files or infer
-- historical request/item snapshot data.

BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '30s';

DO $$
BEGIN
    IF to_regclass('public."Requests"') IS NOT NULL OR to_regclass('public."BatchRows"') IS NOT NULL THEN
        RAISE EXCEPTION 'Migration 005 structures already exist. Inspect schema before treating this migration as applied.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typnamespace = 'public'::regnamespace AND typname = 'batch_status') THEN
        CREATE TYPE public.batch_status AS ENUM ('DRAFT', 'COMMITTED', 'DISCARDED');
    END IF;
END $$;

ALTER TABLE public."Users"
    ADD COLUMN IF NOT EXISTS "ZoneId" uuid NULL;

ALTER TABLE public."Users"
    ADD CONSTRAINT "Users_ZoneId_fkey"
        FOREIGN KEY ("ZoneId") REFERENCES public."Zones" ("Id") ON DELETE RESTRICT;
CREATE INDEX "Users_ZoneId_idx" ON public."Users" ("ZoneId");

ALTER TABLE public."Batches"
    ADD COLUMN IF NOT EXISTS "Status" public.batch_status NOT NULL DEFAULT 'DRAFT',
    ADD COLUMN IF NOT EXISTS "TemplateVersion" varchar(30) NULL,
    ADD COLUMN IF NOT EXISTS "CommittedAt" timestamptz NULL,
    ADD COLUMN IF NOT EXISTS "CommittedByUserId" uuid NULL,
    ADD COLUMN IF NOT EXISTS "DiscardedAt" timestamptz NULL,
    ADD COLUMN IF NOT EXISTS "DiscardedByUserId" uuid NULL,
    ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamptz NOT NULL DEFAULT now();

UPDATE public."Batches"
SET "Status" = CASE WHEN "GeneratedMissions" > 0 THEN 'COMMITTED'::public.batch_status ELSE 'DRAFT'::public.batch_status END,
    "CommittedAt" = CASE WHEN "GeneratedMissions" > 0 THEN "CreatedAt" ELSE NULL END,
    "CommittedByUserId" = CASE WHEN "GeneratedMissions" > 0 THEN "CreatedByUserId" ELSE NULL END
WHERE "CommittedAt" IS NULL AND "DiscardedAt" IS NULL;

ALTER TABLE public."Batches"
    ADD CONSTRAINT "Batches_CommittedByUserId_fkey"
        FOREIGN KEY ("CommittedByUserId") REFERENCES public."Users" ("Id") ON DELETE RESTRICT,
    ADD CONSTRAINT "Batches_DiscardedByUserId_fkey"
        FOREIGN KEY ("DiscardedByUserId") REFERENCES public."Users" ("Id") ON DELETE RESTRICT,
    ADD CONSTRAINT "Batches_Status_Timestamps_check" CHECK (
        ("Status" = 'DRAFT' AND "CommittedAt" IS NULL AND "DiscardedAt" IS NULL)
        OR ("Status" = 'COMMITTED' AND "CommittedAt" IS NOT NULL AND "DiscardedAt" IS NULL)
        OR ("Status" = 'DISCARDED' AND "CommittedAt" IS NULL AND "DiscardedAt" IS NOT NULL)
    );
CREATE INDEX "Batches_Status_CreatedAt_idx" ON public."Batches" ("Status", "CreatedAt" DESC);

CREATE TABLE public."Requests"
(
    "Id" uuid NOT NULL,
    "RequestCode" varchar(50) NOT NULL,
    "ZoneId" uuid NOT NULL,
    "Source" public.batch_source NOT NULL,
    "BatchId" uuid NULL,
    "CreatedByUserId" uuid NOT NULL,
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    "Note" varchar(1000) NULL,

    CONSTRAINT "Requests_pkey" PRIMARY KEY ("Id"),
    CONSTRAINT "Requests_RequestCode_key" UNIQUE ("RequestCode"),
    CONSTRAINT "Requests_ZoneId_fkey" FOREIGN KEY ("ZoneId") REFERENCES public."Zones" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "Requests_BatchId_fkey" FOREIGN KEY ("BatchId") REFERENCES public."Batches" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "Requests_CreatedByUserId_fkey" FOREIGN KEY ("CreatedByUserId") REFERENCES public."Users" ("Id") ON DELETE RESTRICT
);
CREATE INDEX "Requests_ZoneId_CreatedAt_idx" ON public."Requests" ("ZoneId", "CreatedAt" DESC);
CREATE INDEX "Requests_BatchId_idx" ON public."Requests" ("BatchId");

ALTER TABLE public."Missions"
    ADD COLUMN IF NOT EXISTS "RequestId" uuid NULL,
    ADD COLUMN IF NOT EXISTS "ItemCodeSnapshot" varchar(50) NULL,
    ADD COLUMN IF NOT EXISTS "ItemNameSnapshot" varchar(200) NULL,
    ADD COLUMN IF NOT EXISTS "UnitSnapshot" varchar(30) NULL,
    ADD COLUMN IF NOT EXISTS "PackagingSnapshot" varchar(100) NULL,
    ADD COLUMN IF NOT EXISTS "KgPerUnitSnapshot" numeric(8,3) NULL,
    ADD COLUMN IF NOT EXISTS "WeightKgSnapshot" numeric(10,3) NULL;

ALTER TABLE public."Missions"
    ADD CONSTRAINT "Missions_RequestId_fkey" FOREIGN KEY ("RequestId") REFERENCES public."Requests" ("Id") ON DELETE RESTRICT,
    ADD CONSTRAINT "Missions_WeightKgSnapshot_check" CHECK ("WeightKgSnapshot" IS NULL OR "WeightKgSnapshot" >= 0);
CREATE INDEX "Missions_RequestId_idx" ON public."Missions" ("RequestId");

CREATE TABLE public."BatchRows"
(
    "Id" uuid NOT NULL,
    "BatchId" uuid NOT NULL,
    "RowNo" integer NOT NULL,
    "RequestCode" varchar(50) NULL,
    "DepartmentCode" varchar(50) NOT NULL,
    "ItemCode" varchar(50) NOT NULL,
    "Quantity" integer NULL,
    "Priority" public.mission_priority NULL,
    "DeadlineText" varchar(20) NULL,
    "Note" varchar(1000) NULL,
    "ZoneId" uuid NULL,
    "DepartmentLocationId" uuid NULL,
    "ItemId" uuid NULL,
    "MissionTypeId" uuid NULL,
    "IsValid" boolean NOT NULL DEFAULT false,
    "ErrorCode" varchar(50) NULL,
    "ErrorMessage" varchar(500) NULL,
    "WarningMessage" varchar(500) NULL,
    "SplitQuantities" jsonb NOT NULL DEFAULT '[]'::jsonb,
    "MissionsToCreate" integer NOT NULL DEFAULT 0,
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    "UpdatedAt" timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT "BatchRows_pkey" PRIMARY KEY ("Id"),
    CONSTRAINT "BatchRows_BatchId_RowNo_key" UNIQUE ("BatchId", "RowNo"),
    CONSTRAINT "BatchRows_RowNo_check" CHECK ("RowNo" > 0),
    CONSTRAINT "BatchRows_Quantity_check" CHECK ("Quantity" IS NULL OR "Quantity" BETWEEN 1 AND 600),
    CONSTRAINT "BatchRows_MissionsToCreate_check" CHECK ("MissionsToCreate" >= 0),
    CONSTRAINT "BatchRows_BatchId_fkey" FOREIGN KEY ("BatchId") REFERENCES public."Batches" ("Id") ON DELETE CASCADE,
    CONSTRAINT "BatchRows_ZoneId_fkey" FOREIGN KEY ("ZoneId") REFERENCES public."Zones" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "BatchRows_DepartmentLocationId_fkey" FOREIGN KEY ("DepartmentLocationId") REFERENCES public."Locations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "BatchRows_ItemId_fkey" FOREIGN KEY ("ItemId") REFERENCES public."Items" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "BatchRows_MissionTypeId_fkey" FOREIGN KEY ("MissionTypeId") REFERENCES public."MissionTypes" ("Id") ON DELETE RESTRICT
);
CREATE INDEX "BatchRows_BatchId_IsValid_idx" ON public."BatchRows" ("BatchId", "IsValid");

COMMENT ON COLUMN public."Users"."ZoneId" IS 'Required by application validation for OPERATOR users; null for ADMIN and SUPERVISOR.';
COMMENT ON TABLE public."Requests" IS 'One manual or imported transport request; one request can create multiple missions.';
COMMENT ON TABLE public."BatchRows" IS 'Persisted Excel-import row data and validation result while a Batch is DRAFT.';
COMMENT ON COLUMN public."Missions"."WeightKgSnapshot" IS 'Quantity multiplied by the item mass at mission creation time.';

COMMIT;
