-- Migration 006: persisted hospital-file inbox for import drafts.
BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '30s';

CREATE TABLE public."HospitalFiles"
(
    "Id" uuid NOT NULL,
    "FileName" varchar(255) NOT NULL,
    "ContentType" varchar(100) NOT NULL,
    "Content" bytea NOT NULL,
    "SizeBytes" integer NOT NULL,
    "SenderName" varchar(200) NULL,
    "ReceivedAt" timestamptz NOT NULL DEFAULT now(),
    "ImportedBatchId" uuid NULL,
    "ImportedAt" timestamptz NULL,
    "ImportedByUserId" uuid NULL,
    "Note" varchar(1000) NULL,
    CONSTRAINT "HospitalFiles_pkey" PRIMARY KEY ("Id"),
    CONSTRAINT "HospitalFiles_SizeBytes_check" CHECK ("SizeBytes" > 0 AND "SizeBytes" <= 10485760),
    CONSTRAINT "HospitalFiles_Imported_State_check" CHECK (
        ("ImportedBatchId" IS NULL AND "ImportedAt" IS NULL AND "ImportedByUserId" IS NULL)
        OR ("ImportedBatchId" IS NOT NULL AND "ImportedAt" IS NOT NULL AND "ImportedByUserId" IS NOT NULL)),
    CONSTRAINT "HospitalFiles_ImportedBatchId_fkey" FOREIGN KEY ("ImportedBatchId") REFERENCES public."Batches" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "HospitalFiles_ImportedByUserId_fkey" FOREIGN KEY ("ImportedByUserId") REFERENCES public."Users" ("Id") ON DELETE RESTRICT
);
CREATE UNIQUE INDEX "HospitalFiles_ImportedBatchId_key" ON public."HospitalFiles" ("ImportedBatchId") WHERE "ImportedBatchId" IS NOT NULL;
CREATE INDEX "HospitalFiles_ReceivedAt_idx" ON public."HospitalFiles" ("ReceivedAt" DESC);
COMMENT ON TABLE public."HospitalFiles" IS 'Inbox files received from hospital systems; an Excel file may create one draft Batch.';
COMMIT;
