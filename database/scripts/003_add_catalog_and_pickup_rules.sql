-- Migration 003: complete the schema required by catalogue capacity rules and
-- mission-type pickup configuration. This script only adds structure; it does
-- not infer product weights or pickup locations from existing data.
--
-- Prerequisite: run 002_alter_robot_types_add_specs.sql successfully first.

BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '30s';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'RobotTypes' AND column_name = 'MaxLoadPerSlotKg'
    ) THEN
        RAISE EXCEPTION 'Migration 002 is required: RobotTypes.MaxLoadPerSlotKg is missing.';
    END IF;

    IF to_regclass('public."MissionTypePickupLocations"') IS NOT NULL THEN
        RAISE EXCEPTION 'MissionTypePickupLocations already exists. Inspect its schema before treating migration 003 as applied.';
    END IF;
END $$;

ALTER TABLE public."Items"
    ADD COLUMN IF NOT EXISTS "Packaging" varchar(100),
    ADD COLUMN IF NOT EXISTS "KgPerUnit" numeric(8,3),
    ADD COLUMN IF NOT EXISTS "CapacityLimitReason" varchar(20);

ALTER TABLE public."MissionTypes"
    ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Items_KgPerUnit_check') THEN
        ALTER TABLE public."Items"
            ADD CONSTRAINT "Items_KgPerUnit_check" CHECK ("KgPerUnit" IS NULL OR "KgPerUnit" >= 0);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Items_CapacityLimitReason_check') THEN
        ALTER TABLE public."Items"
            ADD CONSTRAINT "Items_CapacityLimitReason_check"
            CHECK ("CapacityLimitReason" IS NULL OR "CapacityLimitReason" IN ('WEIGHT', 'VOLUME', 'POLICY'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Locations_Id_ZoneId_key') THEN
        ALTER TABLE public."Locations"
            ADD CONSTRAINT "Locations_Id_ZoneId_key" UNIQUE ("Id", "ZoneId");
    END IF;
END $$;

CREATE TABLE public."MissionTypePickupLocations"
(
    "Id" uuid NOT NULL,
    "MissionTypeId" uuid NOT NULL,
    "ZoneId" uuid NOT NULL,
    "LocationId" uuid NOT NULL,
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "UpdatedByUserId" uuid NULL,

    CONSTRAINT "MissionTypePickupLocations_pkey" PRIMARY KEY ("Id"),
    CONSTRAINT "MissionTypePickupLocations_MissionTypeId_ZoneId_key" UNIQUE ("MissionTypeId", "ZoneId"),
    CONSTRAINT "MissionTypePickupLocations_MissionTypeId_fkey"
        FOREIGN KEY ("MissionTypeId") REFERENCES public."MissionTypes" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "MissionTypePickupLocations_ZoneId_fkey"
        FOREIGN KEY ("ZoneId") REFERENCES public."Zones" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "MissionTypePickupLocations_LocationId_ZoneId_fkey"
        FOREIGN KEY ("LocationId", "ZoneId") REFERENCES public."Locations" ("Id", "ZoneId") ON DELETE RESTRICT,
    CONSTRAINT "MissionTypePickupLocations_UpdatedByUserId_fkey"
        FOREIGN KEY ("UpdatedByUserId") REFERENCES public."Users" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "MissionTypePickupLocations_LocationId_idx"
    ON public."MissionTypePickupLocations" ("LocationId");

COMMENT ON COLUMN public."Items"."Packaging" IS 'Packaging or presentation, e.g. box, bottle, bag.';
COMMENT ON COLUMN public."Items"."KgPerUnit" IS 'Mass in kilograms for one Item.Unit.';
COMMENT ON COLUMN public."Items"."CapacityLimitReason" IS 'Declared limiting factor for MaxQtyPerSlot: WEIGHT, VOLUME, or POLICY.';
COMMENT ON COLUMN public."MissionTypes"."IsActive" IS 'Whether new manual requests and imports may use this mission type.';
COMMENT ON TABLE public."MissionTypePickupLocations" IS
    'One configured pickup location for each mission type on each floor (Zone).';

COMMIT;
