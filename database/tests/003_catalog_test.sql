-- ============================================================================
-- SIGM CLOUD - Prueba de catalogos oficiales
-- Verifica que los catalogos completos de SISPRO se cargaron correctamente.
-- Ejecutar despues de run_all.sql:
--   psql -U postgres -d sigm_cloud -v ON_ERROR_STOP=1 -f tests/003_catalog_test.sql
-- ============================================================================
\set ON_ERROR_STOP on
BEGIN;

DO $$
DECLARE
    v_cie  integer;
    v_cups integer;
    v_ium  integer;
    v_chapters integer;
BEGIN
    SELECT count(*) INTO v_cie  FROM sigm.diagnosis_catalog;
    SELECT count(*) INTO v_cups FROM sigm.procedure_catalog;
    SELECT count(*) INTO v_ium  FROM sigm.medication_catalog;

    IF v_cie < 12000 THEN
        RAISE EXCEPTION 'CIE-10 incompleto: % registros (esperado >= 12000)', v_cie;
    END IF;
    IF v_cups < 9500 THEN
        RAISE EXCEPTION 'CUPS incompleto: % registros (esperado >= 9500)', v_cups;
    END IF;
    IF v_ium < 33000 THEN
        RAISE EXCEPTION 'IUM incompleto: % registros (esperado >= 33000)', v_ium;
    END IF;

    IF EXISTS (SELECT 1 FROM sigm.diagnosis_catalog WHERE description IS NULL OR btrim(description) = '') THEN
        RAISE EXCEPTION 'CIE-10 contiene descripciones vacias';
    END IF;
    IF EXISTS (SELECT 1 FROM sigm.diagnosis_catalog WHERE chapter IS NULL OR btrim(chapter) = '') THEN
        RAISE EXCEPTION 'CIE-10 contiene capitulos vacios';
    END IF;
    IF EXISTS (SELECT 1 FROM sigm.procedure_catalog WHERE name IS NULL OR btrim(name) = '') THEN
        RAISE EXCEPTION 'CUPS contiene nombres vacios';
    END IF;
    IF EXISTS (SELECT 1 FROM sigm.medication_catalog WHERE name IS NULL OR btrim(name) = '') THEN
        RAISE EXCEPTION 'IUM contiene nombres vacios';
    END IF;

    SELECT count(DISTINCT chapter_code) INTO v_chapters
    FROM sigm.diagnosis_catalog
    WHERE chapter_code IS NOT NULL AND chapter_code <> '';
    IF v_chapters < 20 THEN
        RAISE EXCEPTION 'CIE-10 con mapeo de capitulos incompleto: %', v_chapters;
    END IF;

    -- Muestras de control de cada fuente.
    IF NOT EXISTS (
        SELECT 1 FROM sigm.diagnosis_catalog
        WHERE code = 'I10X' AND description ILIKE '%HIPERTENSION%'
    ) THEN
        RAISE EXCEPTION 'CIE-10 no contiene el codigo de control I10X';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM sigm.diagnosis_catalog
        WHERE code = 'J00X' AND description ILIKE '%RINOFARINGITIS%'
    ) THEN
        RAISE EXCEPTION 'CIE-10 no contiene el codigo de control J00X';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM sigm.procedure_catalog
        WHERE code = '890201' AND name ILIKE '%MEDICINA GENERAL%'
    ) THEN
        RAISE EXCEPTION 'CUPS no contiene el codigo de control 890201';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM sigm.medication_catalog
        WHERE code = '1A1000101000100'
    ) THEN
        RAISE EXCEPTION 'IUM no contiene el codigo de control 1A1000101000100';
    END IF;

    RAISE NOTICE 'Catalogos validados: CIE-10=%, CUPS=%, IUM=%', v_cie, v_cups, v_ium;
END $$;

SELECT 'CIE10' AS catalogo, count(*) AS registros,
       count(*) FILTER (WHERE is_active) AS activos
FROM sigm.diagnosis_catalog
UNION ALL
SELECT 'CUPS', count(*), count(*) FILTER (WHERE is_active)
FROM sigm.procedure_catalog
UNION ALL
SELECT 'IUM', count(*), count(*) FILTER (WHERE is_active)
FROM sigm.medication_catalog
ORDER BY catalogo;

\echo 'CATALOG=0'
ROLLBACK;
