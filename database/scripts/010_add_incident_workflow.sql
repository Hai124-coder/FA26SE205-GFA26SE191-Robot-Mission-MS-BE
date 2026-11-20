-- Migration 010: persistent incident workflow for section 9.
-- This script is intentionally idempotent. It does not backfill legacy Alerts:
-- a NULL ScenarioCode means the alert predates the incident workflow.
BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '30s';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typnamespace = 'public'::regnamespace AND typname = 'incident_phase') THEN
        CREATE TYPE public.incident_phase AS ENUM ('SELF', 'DECIDE', 'HUMAN', 'DONE');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typnamespace = 'public'::regnamespace AND typname = 'incident_cargo_state') THEN
        CREATE TYPE public.incident_cargo_state AS ENUM ('ONBOARD', 'PICKUP', 'ROUTE', 'NONE');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typnamespace = 'public'::regnamespace AND typname = 'incident_action') THEN
        CREATE TYPE public.incident_action AS ENUM ('ACCEPT', 'GO_ON', 'WAIT_MORE', 'GO_PARTIAL', 'HUMAN', 'HUMAN_DONE', 'REASSIGN', 'REQUEUE', 'RESUME', 'WAIT_CONN', 'AS_FAULT', 'BROKER_UP', 'MAINTENANCE', 'CHARGE', 'NOTIFY');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typnamespace = 'public'::regnamespace AND typname = 'incident_timeline_event_type') THEN
        CREATE TYPE public.incident_timeline_event_type AS ENUM ('DETECTED', 'SUGGESTED', 'ACTION_REQUESTED', 'PHASE_CHANGED', 'AUTO_RESOLVED', 'RESOLVED');
    END IF;
END $$;

ALTER TABLE public."Alerts"
    ADD COLUMN IF NOT EXISTS "ScenarioCode" varchar(20) NULL,
    ADD COLUMN IF NOT EXISTS "Phase" public.incident_phase NULL,
    ADD COLUMN IF NOT EXISTS "CargoState" public.incident_cargo_state NULL,
    ADD COLUMN IF NOT EXISTS "SuggestedAction" public.incident_action NULL,
    ADD COLUMN IF NOT EXISTS "AutoResolvedAt" timestamptz NULL;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Alerts_ScenarioCode_check') THEN
        ALTER TABLE public."Alerts" ADD CONSTRAINT "Alerts_ScenarioCode_check"
            CHECK ("ScenarioCode" IS NULL OR length(btrim("ScenarioCode")) > 0) NOT VALID;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Alerts_AutoResolvedAt_check') THEN
        ALTER TABLE public."Alerts" ADD CONSTRAINT "Alerts_AutoResolvedAt_check"
            CHECK ("AutoResolvedAt" IS NULL OR "Status" = 'RESOLVED'::public.alert_status) NOT VALID;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Alerts_DonePhase_check') THEN
        ALTER TABLE public."Alerts" ADD CONSTRAINT "Alerts_DonePhase_check"
            CHECK ("Phase" IS NULL OR "Phase" <> 'DONE'::public.incident_phase OR "Status" = 'RESOLVED'::public.alert_status) NOT VALID;
    END IF;
END $$;

ALTER TABLE public."Alerts" VALIDATE CONSTRAINT "Alerts_ScenarioCode_check";
ALTER TABLE public."Alerts" VALIDATE CONSTRAINT "Alerts_AutoResolvedAt_check";
ALTER TABLE public."Alerts" VALIDATE CONSTRAINT "Alerts_DonePhase_check";

CREATE TABLE IF NOT EXISTS public."AlertTimelineEntries"
(
    "Id" uuid NOT NULL,
    "AlertId" uuid NOT NULL,
    "EventType" public.incident_timeline_event_type NOT NULL,
    "Action" public.incident_action NULL,
    "FromPhase" public.incident_phase NULL,
    "ToPhase" public.incident_phase NULL,
    "ActorUserId" uuid NULL,
    "Note" varchar(2000) NULL,
    "PayloadJson" jsonb NULL,
    "OccurredAt" timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "AlertTimelineEntries_pkey" PRIMARY KEY ("Id"),
    CONSTRAINT "AlertTimelineEntries_ActionRequested_check" CHECK ("EventType" <> 'ACTION_REQUESTED'::public.incident_timeline_event_type OR "Action" IS NOT NULL),
    CONSTRAINT "AlertTimelineEntries_PhaseChanged_check" CHECK ("EventType" <> 'PHASE_CHANGED'::public.incident_timeline_event_type OR "ToPhase" IS NOT NULL),
    CONSTRAINT "AlertTimelineEntries_PayloadJson_check" CHECK ("PayloadJson" IS NULL OR jsonb_typeof("PayloadJson") = 'object'),
    CONSTRAINT "AlertTimelineEntries_AlertId_fkey" FOREIGN KEY ("AlertId") REFERENCES public."Alerts" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "AlertTimelineEntries_ActorUserId_fkey" FOREIGN KEY ("ActorUserId") REFERENCES public."Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS public."AlertMissions"
(
    "AlertId" uuid NOT NULL,
    "MissionId" uuid NOT NULL,
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "AlertMissions_pkey" PRIMARY KEY ("AlertId", "MissionId"),
    CONSTRAINT "AlertMissions_AlertId_fkey" FOREIGN KEY ("AlertId") REFERENCES public."Alerts" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "AlertMissions_MissionId_fkey" FOREIGN KEY ("MissionId") REFERENCES public."Missions" ("Id") ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "Alerts_Status_CreatedAt_idx" ON public."Alerts" ("Status", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS "Alerts_ZoneId_Status_CreatedAt_idx" ON public."Alerts" ("ZoneId", "Status", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS "Alerts_TripId_Status_idx" ON public."Alerts" ("TripId", "Status");
CREATE INDEX IF NOT EXISTS "Alerts_ScenarioCode_Status_idx" ON public."Alerts" ("ScenarioCode", "Status");
CREATE INDEX IF NOT EXISTS "AlertTimelineEntries_AlertId_OccurredAt_Id_idx" ON public."AlertTimelineEntries" ("AlertId", "OccurredAt", "Id");
CREATE INDEX IF NOT EXISTS "AlertTimelineEntries_ActorUserId_OccurredAt_idx" ON public."AlertTimelineEntries" ("ActorUserId", "OccurredAt" DESC);
CREATE INDEX IF NOT EXISTS "AlertMissions_MissionId_CreatedAt_idx" ON public."AlertMissions" ("MissionId", "CreatedAt" DESC);

COMMENT ON TABLE public."AlertTimelineEntries" IS 'Immutable section-9 incident workflow audit trail; entries record detection, suggestions, user action, phase changes and resolution.';
COMMENT ON TABLE public."AlertMissions" IS 'Missions affected by an incident workflow or its selected resolution.';

COMMIT;
