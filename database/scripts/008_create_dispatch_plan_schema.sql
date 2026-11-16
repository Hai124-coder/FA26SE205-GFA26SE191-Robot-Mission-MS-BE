-- Migration 008: persistent draft dispatch plans for Section 8.
-- Run once, manually, against the intended PostgreSQL database.
BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '30s';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typnamespace = 'public'::regnamespace AND typname = 'dispatch_plan_status') THEN
        CREATE TYPE public.dispatch_plan_status AS ENUM ('DRAFT', 'RELEASED', 'DISCARDED');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typnamespace = 'public'::regnamespace AND typname = 'dispatch_plan_source') THEN
        CREATE TYPE public.dispatch_plan_source AS ENUM ('AI', 'FALLBACK');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typnamespace = 'public'::regnamespace AND typname = 'dispatch_plan_task_status') THEN
        CREATE TYPE public.dispatch_plan_task_status AS ENUM ('ASSIGNED', 'WAITING');
    END IF;
END $$;

CREATE TABLE public."DispatchPlans"
(
    "Id" uuid NOT NULL,
    "ZoneId" uuid NOT NULL,
    "Status" public.dispatch_plan_status NOT NULL DEFAULT 'DRAFT'::public.dispatch_plan_status,
    "Source" public.dispatch_plan_source NOT NULL,
    "Reason" varchar(1000) NULL,
    "CreatedByUserId" uuid NULL,
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    "ReleasedByUserId" uuid NULL,
    "ReleasedAt" timestamptz NULL,
    "DiscardedAt" timestamptz NULL,
    "ManualEditCount" integer NOT NULL DEFAULT 0,
    "EstimatedDistanceM" numeric(12,2) NULL,
    "BaselineDistanceM" numeric(12,2) NULL,
    "EstimatedDurationSec" integer NULL,
    "BaselineDurationSec" integer NULL,
    "Version" bigint NOT NULL DEFAULT 1,
    CONSTRAINT "DispatchPlans_pkey" PRIMARY KEY ("Id"),
    CONSTRAINT "DispatchPlans_ManualEditCount_check" CHECK ("ManualEditCount" >= 0),
    CONSTRAINT "DispatchPlans_EstimatedDistanceM_check" CHECK ("EstimatedDistanceM" IS NULL OR "EstimatedDistanceM" >= 0),
    CONSTRAINT "DispatchPlans_BaselineDistanceM_check" CHECK ("BaselineDistanceM" IS NULL OR "BaselineDistanceM" >= 0),
    CONSTRAINT "DispatchPlans_EstimatedDurationSec_check" CHECK ("EstimatedDurationSec" IS NULL OR "EstimatedDurationSec" >= 0),
    CONSTRAINT "DispatchPlans_BaselineDurationSec_check" CHECK ("BaselineDurationSec" IS NULL OR "BaselineDurationSec" >= 0),
    CONSTRAINT "DispatchPlans_Version_check" CHECK ("Version" > 0),
    CONSTRAINT "DispatchPlans_Released_state_check" CHECK (("Status" <> 'RELEASED'::public.dispatch_plan_status) OR ("ReleasedAt" IS NOT NULL)),
    CONSTRAINT "DispatchPlans_Discarded_state_check" CHECK (("Status" <> 'DISCARDED'::public.dispatch_plan_status) OR ("DiscardedAt" IS NOT NULL)),
    CONSTRAINT "DispatchPlans_ZoneId_fkey" FOREIGN KEY ("ZoneId") REFERENCES public."Zones" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "DispatchPlans_CreatedByUserId_fkey" FOREIGN KEY ("CreatedByUserId") REFERENCES public."Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "DispatchPlans_ReleasedByUserId_fkey" FOREIGN KEY ("ReleasedByUserId") REFERENCES public."Users" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "DispatchPlans_ZoneId_OneDraft_key" ON public."DispatchPlans" ("ZoneId") WHERE "Status" = 'DRAFT'::public.dispatch_plan_status;
CREATE INDEX "DispatchPlans_ZoneId_Status_CreatedAt_idx" ON public."DispatchPlans" ("ZoneId", "Status", "CreatedAt" DESC);

CREATE TABLE public."DispatchPlanTrips"
(
    "Id" uuid NOT NULL,
    "PlanId" uuid NOT NULL,
    "RobotId" uuid NOT NULL,
    "MapVersionId" uuid NOT NULL,
    "PickupLocationId" uuid NULL,
    "Seq" integer NOT NULL,
    "EstimatedStartAt" timestamptz NULL,
    "EstimatedEndAt" timestamptz NULL,
    "EstimatedDistanceM" numeric(12,2) NULL,
    CONSTRAINT "DispatchPlanTrips_pkey" PRIMARY KEY ("Id"),
    CONSTRAINT "DispatchPlanTrips_Id_PlanId_key" UNIQUE ("Id", "PlanId"),
    CONSTRAINT "DispatchPlanTrips_PlanId_RobotId_key" UNIQUE ("PlanId", "RobotId"),
    CONSTRAINT "DispatchPlanTrips_PlanId_Seq_key" UNIQUE ("PlanId", "Seq"),
    CONSTRAINT "DispatchPlanTrips_Seq_check" CHECK ("Seq" > 0),
    CONSTRAINT "DispatchPlanTrips_EstimatedDistanceM_check" CHECK ("EstimatedDistanceM" IS NULL OR "EstimatedDistanceM" >= 0),
    CONSTRAINT "DispatchPlanTrips_PlanId_fkey" FOREIGN KEY ("PlanId") REFERENCES public."DispatchPlans" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "DispatchPlanTrips_RobotId_fkey" FOREIGN KEY ("RobotId") REFERENCES public."Robots" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "DispatchPlanTrips_MapVersionId_fkey" FOREIGN KEY ("MapVersionId") REFERENCES public."MapVersions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "DispatchPlanTrips_PickupLocationId_fkey" FOREIGN KEY ("PickupLocationId") REFERENCES public."Locations" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "DispatchPlanTrips_RobotId_idx" ON public."DispatchPlanTrips" ("RobotId");

CREATE TABLE public."DispatchPlanStops"
(
    "Id" uuid NOT NULL,
    "PlanTripId" uuid NOT NULL,
    "Seq" integer NOT NULL,
    "LocationId" uuid NOT NULL,
    "Action" public.trip_stop_action NOT NULL,
    "EstimatedArrivedAt" timestamptz NULL,
    "EstimatedDepartedAt" timestamptz NULL,
    "CumulativeDistanceM" numeric(12,2) NULL,
    CONSTRAINT "DispatchPlanStops_pkey" PRIMARY KEY ("Id"),
    CONSTRAINT "DispatchPlanStops_Id_PlanTripId_key" UNIQUE ("Id", "PlanTripId"),
    CONSTRAINT "DispatchPlanStops_PlanTripId_Seq_key" UNIQUE ("PlanTripId", "Seq"),
    CONSTRAINT "DispatchPlanStops_Seq_check" CHECK ("Seq" > 0),
    CONSTRAINT "DispatchPlanStops_CumulativeDistanceM_check" CHECK ("CumulativeDistanceM" IS NULL OR "CumulativeDistanceM" >= 0),
    CONSTRAINT "DispatchPlanStops_PlanTripId_fkey" FOREIGN KEY ("PlanTripId") REFERENCES public."DispatchPlanTrips" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "DispatchPlanStops_LocationId_fkey" FOREIGN KEY ("LocationId") REFERENCES public."Locations" ("Id") ON DELETE RESTRICT
);

CREATE TABLE public."DispatchPlanTasks"
(
    "Id" uuid NOT NULL,
    "PlanId" uuid NOT NULL,
    "MissionId" uuid NOT NULL,
    "PlanTripId" uuid NULL,
    "DeliveryPlanStopId" uuid NULL,
    "Status" public.dispatch_plan_task_status NOT NULL,
    "SlotNo" integer NULL,
    "Priority" public.mission_priority NOT NULL,
    "EstimatedDeliveryAt" timestamptz NULL,
    "WaitReason" varchar(500) NULL,
    "LateReason" varchar(500) NULL,
    CONSTRAINT "DispatchPlanTasks_pkey" PRIMARY KEY ("Id"),
    CONSTRAINT "DispatchPlanTasks_PlanId_MissionId_key" UNIQUE ("PlanId", "MissionId"),
    CONSTRAINT "DispatchPlanTasks_SlotNo_check" CHECK ("SlotNo" IS NULL OR "SlotNo" > 0),
    CONSTRAINT "DispatchPlanTasks_Assignment_check" CHECK (
        ("Status" = 'ASSIGNED'::public.dispatch_plan_task_status AND "PlanTripId" IS NOT NULL AND "DeliveryPlanStopId" IS NOT NULL AND "SlotNo" IS NOT NULL AND "WaitReason" IS NULL)
        OR
        ("Status" = 'WAITING'::public.dispatch_plan_task_status AND "PlanTripId" IS NULL AND "DeliveryPlanStopId" IS NULL AND "SlotNo" IS NULL AND "WaitReason" IS NOT NULL)
    ),
    CONSTRAINT "DispatchPlanTasks_PlanId_fkey" FOREIGN KEY ("PlanId") REFERENCES public."DispatchPlans" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "DispatchPlanTasks_MissionId_fkey" FOREIGN KEY ("MissionId") REFERENCES public."Missions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "DispatchPlanTasks_PlanTripId_PlanId_fkey" FOREIGN KEY ("PlanTripId", "PlanId") REFERENCES public."DispatchPlanTrips" ("Id", "PlanId") ON DELETE RESTRICT,
    CONSTRAINT "DispatchPlanTasks_DeliveryPlanStopId_PlanTripId_fkey" FOREIGN KEY ("DeliveryPlanStopId", "PlanTripId") REFERENCES public."DispatchPlanStops" ("Id", "PlanTripId") ON DELETE RESTRICT
);

CREATE INDEX "DispatchPlanTasks_PlanId_Status_idx" ON public."DispatchPlanTasks" ("PlanId", "Status");
CREATE INDEX "DispatchPlanTasks_MissionId_idx" ON public."DispatchPlanTasks" ("MissionId");

COMMENT ON TABLE public."DispatchPlans" IS 'Draft and released dispatch plans. At most one DRAFT plan exists per zone.';
COMMENT ON TABLE public."DispatchPlanTrips" IS 'A robot route proposed in a dispatch plan; not an operational Trip until release.';
COMMENT ON TABLE public."DispatchPlanStops" IS 'Ordered proposed pickup, drop, and return stops for a dispatch-plan trip.';
COMMENT ON TABLE public."DispatchPlanTasks" IS 'A mission assigned to a proposed trip and slot, or held waiting with a reason.';

COMMIT;
