-- Migration 007: explicit owner zone for import batches.
BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '30s';
ALTER TABLE public."Batches" ADD COLUMN "ZoneId" uuid NULL;
ALTER TABLE public."Batches" ADD CONSTRAINT "Batches_ZoneId_fkey" FOREIGN KEY ("ZoneId") REFERENCES public."Zones" ("Id") ON DELETE RESTRICT;
CREATE INDEX "Batches_ZoneId_CreatedAt_idx" ON public."Batches" ("ZoneId", "CreatedAt" DESC);
UPDATE public."Batches" b SET "ZoneId" = x."ZoneId" FROM (
 SELECT "BatchId", (array_agg("ZoneId"))[1] AS "ZoneId" FROM public."BatchRows" WHERE "ZoneId" IS NOT NULL GROUP BY "BatchId" HAVING count(DISTINCT "ZoneId") = 1
) x WHERE b."Id" = x."BatchId" AND b."ZoneId" IS NULL;
COMMENT ON COLUMN public."Batches"."ZoneId" IS 'Operator floor that owns this import batch; nullable only for unresolvable historical data.';
COMMIT;
