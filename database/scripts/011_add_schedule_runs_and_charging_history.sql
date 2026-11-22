-- Migration 011: section-10 schedule run audit and charging queue history.
-- Enum additions commit before the main transaction so QUEUED can be used safely.
BEGIN;
ALTER TYPE public.charging_session_status ADD VALUE IF NOT EXISTS 'QUEUED';
COMMIT;

BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '30s';

DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typnamespace='public'::regnamespace AND typname='schedule_run_status') THEN
    CREATE TYPE public.schedule_run_status AS ENUM ('WAITING_FOR_ROBOT','MISSION_CREATED','LATE','FAILED','CANCELLED');
  END IF;
END $$;

ALTER TABLE public."MissionSchedules" ADD COLUMN IF NOT EXISTS "DeletedAt" timestamptz NULL;

CREATE TABLE IF NOT EXISTS public."MissionScheduleRuns" (
  "Id" uuid NOT NULL,
  "ScheduleId" uuid NOT NULL,
  "ScheduledFor" timestamptz NOT NULL,
  "IsManual" boolean NOT NULL DEFAULT false,
  "IdempotencyKey" varchar(128) NULL,
  "RequestedByUserId" uuid NULL,
  "Status" public.schedule_run_status NOT NULL DEFAULT 'WAITING_FOR_ROBOT'::public.schedule_run_status,
  "RouteSnapshot" jsonb NOT NULL,
  "MissionId" uuid NULL,
  "LateAlertId" uuid NULL,
  "FailureReason" varchar(1000) NULL,
  "CreatedAt" timestamptz NOT NULL DEFAULT now(),
  "LastEvaluatedAt" timestamptz NULL,
  "LateAt" timestamptz NULL,
  "CompletedAt" timestamptz NULL,
  CONSTRAINT "MissionScheduleRuns_pkey" PRIMARY KEY ("Id"),
  CONSTRAINT "MissionScheduleRuns_RouteSnapshot_check" CHECK (jsonb_typeof("RouteSnapshot")='object'),
  CONSTRAINT "MissionScheduleRuns_Manual_check" CHECK (("IsManual" AND "IdempotencyKey" IS NOT NULL AND "RequestedByUserId" IS NOT NULL) OR (NOT "IsManual" AND "IdempotencyKey" IS NULL AND "RequestedByUserId" IS NULL)),
  CONSTRAINT "MissionScheduleRuns_Failure_check" CHECK ("Status" <> 'FAILED'::public.schedule_run_status OR "FailureReason" IS NOT NULL),
  CONSTRAINT "MissionScheduleRuns_Late_check" CHECK ("Status" <> 'LATE'::public.schedule_run_status OR "LateAt" IS NOT NULL),
  CONSTRAINT "MissionScheduleRuns_MissionCreated_check" CHECK ("Status" <> 'MISSION_CREATED'::public.schedule_run_status OR "MissionId" IS NOT NULL),
  CONSTRAINT "MissionScheduleRuns_ScheduleId_fkey" FOREIGN KEY ("ScheduleId") REFERENCES public."MissionSchedules"("Id") ON DELETE RESTRICT,
  CONSTRAINT "MissionScheduleRuns_RequestedByUserId_fkey" FOREIGN KEY ("RequestedByUserId") REFERENCES public."Users"("Id") ON DELETE RESTRICT,
  CONSTRAINT "MissionScheduleRuns_MissionId_fkey" FOREIGN KEY ("MissionId") REFERENCES public."Missions"("Id") ON DELETE RESTRICT,
  CONSTRAINT "MissionScheduleRuns_LateAlertId_fkey" FOREIGN KEY ("LateAlertId") REFERENCES public."Alerts"("Id") ON DELETE RESTRICT
);

ALTER TABLE public."ChargingSessions"
  ADD COLUMN IF NOT EXISTS "QueuedAt" timestamptz NULL,
  ADD COLUMN IF NOT EXISTS "BatteryAtStart" integer NULL,
  ADD COLUMN IF NOT EXISTS "BatteryAtEnd" integer NULL;
UPDATE public."ChargingSessions" SET "QueuedAt" = "StartedAt" WHERE "QueuedAt" IS NULL;
ALTER TABLE public."ChargingSessions" ALTER COLUMN "QueuedAt" SET NOT NULL;
ALTER TABLE public."ChargingSessions" ALTER COLUMN "StartedAt" DROP NOT NULL;

DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ChargingSessions_Timeline_check') THEN
    ALTER TABLE public."ChargingSessions" ADD CONSTRAINT "ChargingSessions_Timeline_check" CHECK ("QueuedAt" <= COALESCE("StartedAt", "EndedAt", "QueuedAt"));
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ChargingSessions_State_check') THEN
    ALTER TABLE public."ChargingSessions" ADD CONSTRAINT "ChargingSessions_State_check" CHECK (("Status"='QUEUED'::public.charging_session_status AND "StartedAt" IS NULL AND "EndedAt" IS NULL) OR ("Status"='CHARGING'::public.charging_session_status AND "StartedAt" IS NOT NULL AND "EndedAt" IS NULL) OR ("Status" IN ('COMPLETED'::public.charging_session_status,'INTERRUPTED'::public.charging_session_status) AND "EndedAt" IS NOT NULL));
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ChargingSessions_Battery_check') THEN
    ALTER TABLE public."ChargingSessions" ADD CONSTRAINT "ChargingSessions_Battery_check" CHECK (("BatteryAtStart" IS NULL OR "BatteryAtStart" BETWEEN 0 AND 100) AND ("BatteryAtEnd" IS NULL OR "BatteryAtEnd" BETWEEN 0 AND 100));
  END IF;
END $$;

DROP INDEX IF EXISTS public."UX_ChargingSessions_OneActive";
CREATE UNIQUE INDEX IF NOT EXISTS "UX_ChargingSessions_OneActive" ON public."ChargingSessions"("RobotId") WHERE "Status" IN ('QUEUED'::public.charging_session_status,'CHARGING'::public.charging_session_status);
CREATE INDEX IF NOT EXISTS "MissionSchedules_Active_NotDeleted_idx" ON public."MissionSchedules"("RunTime") WHERE "IsActive" AND "DeletedAt" IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "MissionScheduleRuns_AutomaticSlot_key" ON public."MissionScheduleRuns"("ScheduleId","ScheduledFor") WHERE NOT "IsManual";
CREATE UNIQUE INDEX IF NOT EXISTS "MissionScheduleRuns_ManualIdempotency_key" ON public."MissionScheduleRuns"("ScheduleId","IdempotencyKey") WHERE "IsManual";
CREATE UNIQUE INDEX IF NOT EXISTS "MissionScheduleRuns_MissionId_key" ON public."MissionScheduleRuns"("MissionId") WHERE "MissionId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "MissionScheduleRuns_Status_ScheduledFor_idx" ON public."MissionScheduleRuns"("Status","ScheduledFor");
CREATE INDEX IF NOT EXISTS "MissionScheduleRuns_ScheduleId_CreatedAt_idx" ON public."MissionScheduleRuns"("ScheduleId","CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS "ChargingSessions_RobotId_QueuedAt_idx" ON public."ChargingSessions"("RobotId","QueuedAt" DESC);
CREATE INDEX IF NOT EXISTS "ChargingSessions_ChargingStationId_QueuedAt_idx" ON public."ChargingSessions"("ChargingStationId","QueuedAt" DESC);
COMMIT;
