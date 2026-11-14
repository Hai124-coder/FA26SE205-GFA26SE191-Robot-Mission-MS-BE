-- Migration 004: reviewed reference data for catalogue capacity rules and
-- mission-type pickup locations. Source: RMMS configuration data document dated
-- 2026-11-14. Run only after 002 and 003 have completed successfully.
--
-- This script deliberately refuses to overwrite existing pickup configuration.

BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '30s';

DO $$
DECLARE
    expected_item_count integer := 16;
    actual_item_count integer;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'Items' AND column_name = 'KgPerUnit'
    ) THEN
        RAISE EXCEPTION 'Migration 003 is required: Items.KgPerUnit is missing.';
    END IF;

    SELECT count(*) INTO actual_item_count
    FROM public."Items"
    WHERE "Code" IN (
        'TH-PARA', 'TH-AMOX', 'TH-CEFT', 'TH-NACL', 'TH-INSU', 'TH-NORA', 'TH-SALB',
        'XN-MAU', 'XN-KHIMAU', 'XN-NTIEU', 'VT-GANG-S', 'VT-GANG-M', 'VT-GAC',
        'VT-BOM5', 'VT-TBANG', 'VT-KTRANG'
    );
    IF actual_item_count <> expected_item_count THEN
        RAISE EXCEPTION 'Expected % reference items but found %.', expected_item_count, actual_item_count;
    END IF;

    IF EXISTS (SELECT 1 FROM public."MissionTypePickupLocations") THEN
        RAISE EXCEPTION 'Pickup mappings already exist. Review them manually instead of overwriting configuration.';
    END IF;
END $$;

UPDATE public."RobotTypes"
SET "SlotLengthCm" = CASE "TypeCode" WHEN 'MED_CART' THEN 40 WHEN 'SUPPLY_CART' THEN 60 END,
    "SlotWidthCm" = CASE "TypeCode" WHEN 'MED_CART' THEN 30 WHEN 'SUPPLY_CART' THEN 40 END,
    "SlotHeightCm" = CASE "TypeCode" WHEN 'MED_CART' THEN 20 WHEN 'SUPPLY_CART' THEN 30 END,
    "MaxLoadPerSlotKg" = CASE "TypeCode" WHEN 'MED_CART' THEN 5.00 WHEN 'SUPPLY_CART' THEN 10.00 END,
    "MaxPayloadKg" = CASE "TypeCode" WHEN 'MED_CART' THEN 20.00 WHEN 'SUPPLY_CART' THEN 30.00 END
WHERE "TypeCode" IN ('MED_CART', 'SUPPLY_CART');

WITH reference_data ("Code", "Packaging", "KgPerUnit", "CapacityLimitReason") AS (
    VALUES
        ('TH-PARA',    'hộp 10 vỉ × 10 viên',       0.100::numeric, 'VOLUME'),
        ('TH-AMOX',    'hộp 10 vỉ × 10 viên',       0.120::numeric, 'VOLUME'),
        ('TH-CEFT',    'lọ bột pha tiêm',           0.030::numeric, 'POLICY'),
        ('TH-NACL',    'chai nhựa 500 ml',          0.550::numeric, 'WEIGHT'),
        ('TH-INSU',    'bút 3 ml kèm túi giữ lạnh', 0.040::numeric, 'POLICY'),
        ('TH-NORA',    'ống thuỷ tinh 4 ml',        0.010::numeric, 'POLICY'),
        ('TH-SALB',    'hộp 20 ống 2,5 ml',         0.150::numeric, 'VOLUME'),
        ('XN-MAU',     'ống 4 ml có nắp',           0.010::numeric, 'POLICY'),
        ('XN-KHIMAU',  'ống trong túi đá',           0.150::numeric, 'POLICY'),
        ('XN-NTIEU',   'lọ 60 ml có nắp',           0.080::numeric, 'POLICY'),
        ('VT-GANG-S',  'hộp 100 chiếc',              0.550::numeric, 'VOLUME'),
        ('VT-GANG-M',  'hộp 100 chiếc',              0.550::numeric, 'VOLUME'),
        ('VT-GAC',     'gói 10 miếng',               0.050::numeric, 'VOLUME'),
        ('VT-BOM5',    'hộp 100 cái',                0.900::numeric, 'WEIGHT'),
        ('VT-TBANG',   'bộ tiệt khuẩn đóng túi',     0.400::numeric, 'POLICY'),
        ('VT-KTRANG',  'hộp 50 cái',                 0.250::numeric, 'VOLUME')
)
UPDATE public."Items" item
SET "Packaging" = reference_data."Packaging",
    "KgPerUnit" = reference_data."KgPerUnit",
    "CapacityLimitReason" = reference_data."CapacityLimitReason"
FROM reference_data
WHERE item."Code" = reference_data."Code";

UPDATE public."MissionTypes"
SET "IsActive" = true
WHERE "Code" IN ('MEDICINE_DELIVERY', 'SUPPLY_DELIVERY', 'SPECIMEN_TRANSPORT');

INSERT INTO public."MissionTypePickupLocations"
    ("Id", "MissionTypeId", "ZoneId", "LocationId")
SELECT
    (
        substr(md5(mission_type."Code" || ':' || zone."Code"), 1, 8) || '-' ||
        substr(md5(mission_type."Code" || ':' || zone."Code"), 9, 4) || '-' ||
        substr(md5(mission_type."Code" || ':' || zone."Code"), 13, 4) || '-' ||
        substr(md5(mission_type."Code" || ':' || zone."Code"), 17, 4) || '-' ||
        substr(md5(mission_type."Code" || ':' || zone."Code"), 21, 12)
    )::uuid,
    mission_type."Id", zone."Id", location."Id"
FROM (VALUES
    ('MEDICINE_DELIVERY', 'F2', 'F2-PHARM'),
    ('MEDICINE_DELIVERY', 'F3', 'F3-PHARM'),
    ('SUPPLY_DELIVERY', 'F2', 'F2-STORE'),
    ('SUPPLY_DELIVERY', 'F3', 'F3-STORE'),
    ('SPECIMEN_TRANSPORT', 'F2', 'F2-BLOOD')
) AS expected("MissionTypeCode", "ZoneCode", "LocationCode")
JOIN public."MissionTypes" mission_type ON mission_type."Code" = expected."MissionTypeCode"
JOIN public."Zones" zone ON zone."Code" = expected."ZoneCode" AND zone."IsActive"
JOIN public."Locations" location ON location."Code" = expected."LocationCode" AND location."ZoneId" = zone."Id" AND location."IsActive";

DO $$
DECLARE
    invalid_capacity_count integer;
    inserted_mapping_count integer;
BEGIN
    SELECT count(*) INTO invalid_capacity_count
    FROM public."Items" item
    JOIN public."MissionTypes" mission_type ON mission_type."Id" = item."MissionTypeId"
    JOIN public."RobotTypes" robot_type ON robot_type."Id" = mission_type."RobotTypeId"
    WHERE item."Code" IN (
        'TH-PARA', 'TH-AMOX', 'TH-CEFT', 'TH-NACL', 'TH-INSU', 'TH-NORA', 'TH-SALB',
        'XN-MAU', 'XN-KHIMAU', 'XN-NTIEU', 'VT-GANG-S', 'VT-GANG-M', 'VT-GAC',
        'VT-BOM5', 'VT-TBANG', 'VT-KTRANG'
    )
      AND (item."KgPerUnit" IS NULL OR robot_type."MaxLoadPerSlotKg" IS NULL
           OR item."MaxQtyPerSlot" * item."KgPerUnit" > robot_type."MaxLoadPerSlotKg");
    IF invalid_capacity_count <> 0 THEN
        RAISE EXCEPTION 'Capacity validation failed for % reference items.', invalid_capacity_count;
    END IF;

    SELECT count(*) INTO inserted_mapping_count FROM public."MissionTypePickupLocations";
    IF inserted_mapping_count <> 5 THEN
        RAISE EXCEPTION 'Expected 5 pickup mappings but found %.', inserted_mapping_count;
    END IF;
END $$;

COMMIT;
