-- Migration 013: durable, at-least-once immediate robot control commands.
-- Existing DISPATCH_TRIP rows remain valid and still require a trip and plan.
BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '30s';

ALTER TABLE public."RobotCommandOutbox"
  ALTER COLUMN "TripId" DROP NOT NULL,
  ALTER COLUMN "PlanId" DROP NOT NULL;

ALTER TABLE public."RobotCommandOutbox"
  DROP CONSTRAINT IF EXISTS "RobotCommandOutbox_CommandType_check";

ALTER TABLE public."RobotCommandOutbox"
  ADD CONSTRAINT "RobotCommandOutbox_CommandType_check"
  CHECK ("CommandType" IN ('DISPATCH_TRIP', 'STOP', 'RESUME'));

ALTER TABLE public."RobotCommandOutbox"
  DROP CONSTRAINT IF EXISTS "RobotCommandOutbox_CommandShape_check";

ALTER TABLE public."RobotCommandOutbox"
  ADD CONSTRAINT "RobotCommandOutbox_CommandShape_check"
  CHECK (
    (
      "CommandType" = 'DISPATCH_TRIP'
      AND "TripId" IS NOT NULL
      AND "PlanId" IS NOT NULL
      AND "Topic" ~ '^rmms/v1/[^/]+/order$'
    )
    OR
    (
      "CommandType" IN ('STOP', 'RESUME')
      AND "TripId" IS NULL
      AND "PlanId" IS NULL
      AND "Topic" ~ '^rmms/v1/[^/]+/command$'
      AND jsonb_typeof("PayloadJson" -> 'commandId') = 'string'
      AND jsonb_typeof("PayloadJson" -> 'command') = 'string'
      AND jsonb_typeof("PayloadJson" -> 'issuedAt') = 'string'
      AND "PayloadJson" ->> 'commandId' = "Id"::text
      AND "PayloadJson" ->> 'command' = "CommandType"
    )
  );

COMMENT ON TABLE public."RobotCommandOutbox" IS
  'Durable at-least-once robot commands. DISPATCH_TRIP targets /order; STOP and RESUME target /command and use Id as commandId.';

COMMIT;
