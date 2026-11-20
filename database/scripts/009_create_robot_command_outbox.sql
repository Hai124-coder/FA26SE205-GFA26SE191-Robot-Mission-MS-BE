-- Migration 009: durable MQTT command outbox for released robot trips.
-- Run once, manually, against the intended PostgreSQL database.
BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '30s';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typnamespace = 'public'::regnamespace AND typname = 'robot_command_outbox_status') THEN
        CREATE TYPE public.robot_command_outbox_status AS ENUM ('PENDING', 'PROCESSING', 'SENT', 'FAILED');
    END IF;
END $$;

CREATE TABLE public."RobotCommandOutbox"
(
    "Id" uuid NOT NULL,
    "CommandType" varchar(50) NOT NULL,
    "RobotId" uuid NOT NULL,
    "TripId" uuid NOT NULL,
    "PlanId" uuid NOT NULL,
    "Topic" varchar(200) NOT NULL,
    "PayloadJson" jsonb NOT NULL,
    "Status" public.robot_command_outbox_status NOT NULL DEFAULT 'PENDING'::public.robot_command_outbox_status,
    "AttemptCount" integer NOT NULL DEFAULT 0,
    "NextAttemptAt" timestamptz NOT NULL DEFAULT now(),
    "LockedUntil" timestamptz NULL,
    "LockedBy" varchar(100) NULL,
    "SentAt" timestamptz NULL,
    "LastError" varchar(2000) NULL,
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "RobotCommandOutbox_pkey" PRIMARY KEY ("Id"),
    CONSTRAINT "RobotCommandOutbox_CommandType_check" CHECK ("CommandType" = 'DISPATCH_TRIP'),
    CONSTRAINT "RobotCommandOutbox_AttemptCount_check" CHECK ("AttemptCount" >= 0),
    CONSTRAINT "RobotCommandOutbox_Topic_check" CHECK (length(btrim("Topic")) > 0),
    CONSTRAINT "RobotCommandOutbox_PayloadJson_check" CHECK (jsonb_typeof("PayloadJson") = 'object'),
    CONSTRAINT "RobotCommandOutbox_Lock_check" CHECK (
        ("Status" = 'PROCESSING'::public.robot_command_outbox_status AND "LockedUntil" IS NOT NULL AND "LockedBy" IS NOT NULL)
        OR
        ("Status" <> 'PROCESSING'::public.robot_command_outbox_status AND "LockedUntil" IS NULL AND "LockedBy" IS NULL)
    ),
    CONSTRAINT "RobotCommandOutbox_Sent_check" CHECK ("Status" <> 'SENT'::public.robot_command_outbox_status OR "SentAt" IS NOT NULL),
    CONSTRAINT "RobotCommandOutbox_Failed_check" CHECK ("Status" <> 'FAILED'::public.robot_command_outbox_status OR "LastError" IS NOT NULL),
    CONSTRAINT "RobotCommandOutbox_RobotId_fkey" FOREIGN KEY ("RobotId") REFERENCES public."Robots" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "RobotCommandOutbox_TripId_fkey" FOREIGN KEY ("TripId") REFERENCES public."Trips" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "RobotCommandOutbox_PlanId_fkey" FOREIGN KEY ("PlanId") REFERENCES public."DispatchPlans" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "RobotCommandOutbox_TripId_CommandType_key" ON public."RobotCommandOutbox" ("TripId", "CommandType");
CREATE INDEX "RobotCommandOutbox_Status_NextAttemptAt_idx" ON public."RobotCommandOutbox" ("Status", "NextAttemptAt");
CREATE INDEX "RobotCommandOutbox_RobotId_CreatedAt_idx" ON public."RobotCommandOutbox" ("RobotId", "CreatedAt" DESC);

COMMENT ON TABLE public."RobotCommandOutbox" IS 'Durable at-least-once robot commands. Payload is an immutable dispatch snapshot; SENT means broker publish succeeded, not robot acknowledgement.';

COMMIT;
