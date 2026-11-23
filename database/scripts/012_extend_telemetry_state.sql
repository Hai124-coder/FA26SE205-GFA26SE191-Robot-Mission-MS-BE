-- Migration 012: retain protocol details for inbound robot state telemetry.
BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '30s';

ALTER TABLE public."TelemetrySnapshots"
  ADD COLUMN IF NOT EXISTS "HeadingRad" double precision NULL,
  ADD COLUMN IF NOT EXISTS "ReportedStatus" varchar(20) NULL,
  ADD COLUMN IF NOT EXISTS "OrderId" varchar(100) NULL,
  ADD COLUMN IF NOT EXISTS "NextNodeCode" varchar(100) NULL,
  ADD COLUMN IF NOT EXISTS "AtStopSeq" integer NULL,
  ADD COLUMN IF NOT EXISTS "ReceivedAt" timestamptz NOT NULL DEFAULT now();

DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'TelemetrySnapshots_BatteryLevel_check') THEN
    ALTER TABLE public."TelemetrySnapshots" ADD CONSTRAINT "TelemetrySnapshots_BatteryLevel_check" CHECK ("BatteryLevel" BETWEEN 0 AND 100);
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'TelemetrySnapshots_Speed_check') THEN
    ALTER TABLE public."TelemetrySnapshots" ADD CONSTRAINT "TelemetrySnapshots_Speed_check" CHECK ("Speed" IS NULL OR "Speed" >= 0);
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'TelemetrySnapshots_AtStopSeq_check') THEN
    ALTER TABLE public."TelemetrySnapshots" ADD CONSTRAINT "TelemetrySnapshots_AtStopSeq_check" CHECK ("AtStopSeq" IS NULL OR "AtStopSeq" > 0);
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'TelemetrySnapshots_ReportedStatus_check') THEN
    ALTER TABLE public."TelemetrySnapshots" ADD CONSTRAINT "TelemetrySnapshots_ReportedStatus_check" CHECK ("ReportedStatus" IS NULL OR "ReportedStatus" IN ('IDLE','MOVING','AT_STOP','CHARGING','ERROR'));
  END IF;
END $$;

COMMENT ON COLUMN public."TelemetrySnapshots"."Timestamp" IS 'Timestamp supplied by robot in MQTT state payload.';
COMMENT ON COLUMN public."TelemetrySnapshots"."ReceivedAt" IS 'UTC timestamp when backend accepted the MQTT state payload.';
COMMENT ON COLUMN public."TelemetrySnapshots"."ReportedStatus" IS 'Protocol status reported by robot; distinct from Robots.State domain state.';

COMMIT;
