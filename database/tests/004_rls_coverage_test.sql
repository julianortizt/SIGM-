-- ============================================================================
-- SIGM CLOUD - Prueba de cobertura RLS y claves foraneas multi-tenant
-- Verifica que toda tabla con tenant_id tenga RLS, FORCE y al menos una
-- politica, y que las FK entre tablas de tenant incluyan tenant_id en ambos
-- lados. Las cuatro excepciones documentadas estan protegidas por triggers.
--
-- Ejecutar despues de run_all.sql:
--   psql -U postgres -d sigm_cloud -v ON_ERROR_STOP=1 -f tests/004_rls_coverage_test.sql
-- ============================================================================
\set ON_ERROR_STOP on
BEGIN;

DO $$
DECLARE
    v_list  text;
    v_count integer;
BEGIN
    -- 1) Toda tabla con tenant_id debe tener RLS habilitado.
    SELECT string_agg(c.relname, ', ' ORDER BY c.relname), count(*)
      INTO v_list, v_count
    FROM pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'sigm'
      AND c.relkind = 'r'
      AND EXISTS (
          SELECT 1 FROM pg_attribute a
          WHERE a.attrelid = c.oid AND a.attname = 'tenant_id' AND NOT a.attisdropped
      )
      AND NOT c.relrowsecurity;

    IF v_count > 0 THEN
        RAISE EXCEPTION 'Tablas con tenant_id sin RLS: %', v_list;
    END IF;

    -- 2) RLS forzado. audit_logs es la unica excepcion: su trigger
    --    SECURITY DEFINER debe escribir auditoria sin contexto de tenant.
    SELECT string_agg(c.relname, ', ' ORDER BY c.relname), count(*)
      INTO v_list, v_count
    FROM pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'sigm'
      AND c.relkind = 'r'
      AND c.relname <> 'audit_logs'
      AND EXISTS (
          SELECT 1 FROM pg_attribute a
          WHERE a.attrelid = c.oid AND a.attname = 'tenant_id' AND NOT a.attisdropped
      )
      AND NOT c.relforcerowsecurity;

    IF v_count > 0 THEN
        RAISE EXCEPTION 'Tablas con tenant_id sin FORCE ROW LEVEL SECURITY: %', v_list;
    END IF;

    -- 3) Toda tabla de tenant debe tener al menos una politica.
    SELECT string_agg(c.relname, ', ' ORDER BY c.relname), count(*)
      INTO v_list, v_count
    FROM pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'sigm'
      AND c.relkind = 'r'
      AND EXISTS (
          SELECT 1 FROM pg_attribute a
          WHERE a.attrelid = c.oid AND a.attname = 'tenant_id' AND NOT a.attisdropped
      )
      AND NOT EXISTS (
          SELECT 1 FROM pg_policies p
          WHERE p.schemaname = 'sigm' AND p.tablename = c.relname
      );

    IF v_count > 0 THEN
        RAISE EXCEPTION 'Tablas de tenant sin politicas RLS: %', v_list;
    END IF;

    -- 4) Las politicas de tablas de tenant deben referenciar el aislamiento.
    SELECT string_agg(p.tablename || '.' || p.policyname, ', ' ORDER BY p.tablename, p.policyname),
           count(*)
      INTO v_list, v_count
    FROM pg_policies p
    WHERE p.schemaname = 'sigm'
      AND EXISTS (
          SELECT 1
          FROM pg_class c
          JOIN pg_namespace n ON n.oid = c.relnamespace
          JOIN pg_attribute a ON a.attrelid = c.oid
          WHERE n.nspname = 'sigm'
            AND c.relkind = 'r'
            AND c.relname = p.tablename
            AND a.attname = 'tenant_id'
            AND NOT a.attisdropped
      )
      AND coalesce(p.qual, '') NOT ILIKE '%tenant%'
      AND coalesce(p.with_check, '') NOT ILIKE '%tenant%';

    IF v_count > 0 THEN
        RAISE EXCEPTION 'Politicas de tenant sin referencia a tenant_id: %', v_list;
    END IF;

    -- 5) Las FK entre tablas de tenant deben ser compuestas (tenant_id, id).
    --    Excepciones documentadas: referencias a roles, formularios y
    --    categorias que pueden ser globales (tenant_id NULL); cada una tiene
    --    un trigger de validacion que verifica la compatibilidad de tenant.
    WITH tenant_tables AS (
        SELECT c.oid
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'sigm'
          AND c.relkind = 'r'
          AND EXISTS (
              SELECT 1 FROM pg_attribute a
              WHERE a.attrelid = c.oid AND a.attname = 'tenant_id' AND NOT a.attisdropped
          )
    ),
    tenant_attnums AS (
        SELECT c.oid AS relid, a.attnum
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        JOIN pg_attribute a ON a.attrelid = c.oid
        WHERE n.nspname = 'sigm'
          AND c.relkind = 'r'
          AND a.attname = 'tenant_id'
          AND NOT a.attisdropped
    )
    SELECT string_agg(con.conname || ' (' || con.conrelid::regclass || ')', ', '
                      ORDER BY con.conname),
           count(*)
      INTO v_list, v_count
    FROM pg_constraint con
    JOIN tenant_tables ct ON ct.oid = con.conrelid
    JOIN tenant_tables pt ON pt.oid = con.confrelid
    JOIN tenant_attnums ca ON ca.relid = con.conrelid
    JOIN tenant_attnums pa ON pa.relid = con.confrelid
    WHERE con.contype = 'f'
      AND con.connamespace = 'sigm'::regnamespace
      AND (
          NOT (con.conkey @> ARRAY[ca.attnum])
          OR NOT (con.confkey @> ARRAY[pa.attnum])
      )
      AND con.conname NOT IN (
          'user_roles_role_id_fkey',
          'document_permissions_role_id_fkey',
          'specialty_form_fields_form_id_fkey',
          'documents_category_id_fkey'
      );

    IF v_count > 0 THEN
        RAISE EXCEPTION 'FK entre tablas de tenant sin tenant_id compuesto: %', v_list;
    END IF;

    -- 6) Los cuatro triggers que protegen las excepciones deben existir.
    SELECT string_agg(required.trigger_name, ', ' ORDER BY required.trigger_name),
           count(*)
      INTO v_list, v_count
    FROM (VALUES
        ('trg_validate_role_tenant',           'user_roles'),
        ('trg_validate_role_tenant_docperm',   'document_permissions'),
        ('trg_validate_form_field_tenant',     'specialty_form_fields'),
        ('trg_validate_document_category',     'documents')
    ) AS required(trigger_name, table_name)
    WHERE NOT EXISTS (
        SELECT 1
        FROM pg_trigger t
        JOIN pg_class c ON c.oid = t.tgrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'sigm'
          AND c.relname = required.table_name
          AND t.tgname = required.trigger_name
          AND NOT t.tgisinternal
    );

    IF v_count > 0 THEN
        RAISE EXCEPTION 'Faltan triggers de validacion de tenant: %', v_list;
    END IF;

    RAISE NOTICE 'Cobertura RLS y FK multi-tenant validada';
END $$;

SELECT
    count(*) FILTER (WHERE c.relrowsecurity)      AS tablas_tenant_con_rls,
    count(*) FILTER (WHERE c.relforcerowsecurity) AS tablas_tenant_con_force,
    count(*)                                      AS tablas_tenant
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'sigm'
  AND c.relkind = 'r'
  AND EXISTS (
      SELECT 1 FROM pg_attribute a
      WHERE a.attrelid = c.oid AND a.attname = 'tenant_id' AND NOT a.attisdropped
  );

\echo 'RLS_COVERAGE=0'
ROLLBACK;
