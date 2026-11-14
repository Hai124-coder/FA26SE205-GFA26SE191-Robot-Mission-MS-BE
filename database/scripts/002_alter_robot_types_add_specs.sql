-- Migration 002: Add technical specification columns to RobotTypes
-- Run on Supabase before deploying backend changes.
-- All columns are nullable to stay compatible with existing seed rows;
-- values should be updated via PUT /robot-types/{type} or a data patch.

ALTER TABLE "RobotTypes"
    ADD COLUMN IF NOT EXISTS "SlotLengthCm"      integer,
    ADD COLUMN IF NOT EXISTS "SlotWidthCm"        integer,
    ADD COLUMN IF NOT EXISTS "SlotHeightCm"       integer,
    ADD COLUMN IF NOT EXISTS "MaxLoadPerSlotKg"   numeric(6,2),
    ADD COLUMN IF NOT EXISTS "MaxPayloadKg"       numeric(6,2),
    ADD COLUMN IF NOT EXISTS "SpeedMs"            numeric(4,2),
    ADD COLUMN IF NOT EXISTS "HomeStationType"    varchar(30);

COMMENT ON COLUMN "RobotTypes"."SlotLengthCm"    IS 'Slot interior length in centimetres';
COMMENT ON COLUMN "RobotTypes"."SlotWidthCm"     IS 'Slot interior width in centimetres';
COMMENT ON COLUMN "RobotTypes"."SlotHeightCm"    IS 'Slot interior height in centimetres';
COMMENT ON COLUMN "RobotTypes"."MaxLoadPerSlotKg" IS 'Maximum payload per slot in kilograms';
COMMENT ON COLUMN "RobotTypes"."MaxPayloadKg"    IS 'Maximum total robot payload (must be >= Slots * MaxLoadPerSlotKg)';
COMMENT ON COLUMN "RobotTypes"."SpeedMs"         IS 'Nominal speed in metres per second';
COMMENT ON COLUMN "RobotTypes"."HomeStationType" IS 'Station type the robot returns to by default (e.g. STANDARD, CLEAN)';
