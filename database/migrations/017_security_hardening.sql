-- ============================================================================
-- SIGM CLOUD - 017 - Endurecimiento de privilegios
-- Cierra brechas detectadas en la validacion:
--   1. Catalogos globales eran escribibles por el rol de aplicacion.
--   2. role_permissions no tenia RLS y permitia escalar privilegios.
--   3. Un administrador de tenant podia asignar el rol PLATFORM_ADMIN.
-- ============================================================================
BEGIN;

-- ----------------------------------------------------------------------------
-- 1. Catalogos globales: solo lectura para sigm_app.
-- Solo sigm_platform (aprovisionamiento) puede modificarlos.
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sigm_app') THEN
        EXECUTE 'REVOKE INSERT, UPDATE, DELETE ON
            sigm.plans,
            sigm.specialties,
            sigm.permissions,
            sigm.document_types,
            sigm.diagnosis_catalog,
            sigm.procedure_catalog,
            sigm.medication_catalog,
            sigm.laboratory_tests,
            sigm.clinical_note_types,
            sigm.insurers
        FROM sigm_app';
    ELSE
        RAISE NOTICE 'Rol sigm_app ausente: se omite el endurecimiento de catalogos globales.';
    END IF;
END $$;

-- ----------------------------------------------------------------------------
-- 2. RLS sobre role_permissions.
-- Lectura: roles globales y roles del tenant actual.
-- Escritura: solo roles propios del tenant actual.
-- ----------------------------------------------------------------------------
ALTER TABLE sigm.role_permissions ENABLE ROW LEVEL SECURITY;
ALTER TABLE sigm.role_permissions FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS role_permissions_read ON sigm.role_permissions;
DROP POLICY IF EXISTS role_permissions_write ON sigm.role_permissions;

CREATE POLICY role_permissions_read ON sigm.role_permissions
    FOR SELECT
    USING (
        EXISTS (
            SELECT 1
            FROM sigm.roles r
            WHERE r.id = role_id
              AND (r.tenant_id IS NULL OR r.tenant_id = sigm.current_tenant_id())
        )
    );

CREATE POLICY role_permissions_write ON sigm.role_permissions
    FOR ALL
    USING (
        EXISTS (
            SELECT 1
            FROM sigm.roles r
            WHERE r.id = role_id
              AND r.tenant_id = sigm.current_tenant_id()
        )
    )
    WITH CHECK (
        EXISTS (
            SELECT 1
            FROM sigm.roles r
            WHERE r.id = role_id
              AND r.tenant_id = sigm.current_tenant_id()
        )
    );

-- ----------------------------------------------------------------------------
-- 3. Un rol de tenant no puede recibir permisos de plataforma.
-- Los permisos de rol global solo los administra sigm_platform.
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION sigm.fn_validate_role_permission()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_role_tenant uuid;
    v_module      varchar(60);
BEGIN
    SELECT tenant_id INTO v_role_tenant
    FROM sigm.roles
    WHERE id = NEW.role_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'El rol % no existe', NEW.role_id
            USING ERRCODE = '23503';
    END IF;

    SELECT module INTO v_module
    FROM sigm.permissions
    WHERE id = NEW.permission_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'El permiso % no existe', NEW.permission_id
            USING ERRCODE = '23503';
    END IF;

    IF v_role_tenant IS NOT NULL AND v_module = 'platform' THEN
        RAISE EXCEPTION 'Un rol de tenant no puede recibir el permiso de plataforma %', NEW.permission_id
            USING ERRCODE = '42501';
    END IF;

    IF v_role_tenant IS NULL AND current_user <> 'sigm_platform'
       AND NOT EXISTS (
           SELECT 1 FROM pg_roles
           WHERE rolname = current_user AND rolsuper
       ) THEN
        RAISE EXCEPTION 'Solo sigm_platform puede modificar los permisos de un rol global'
            USING ERRCODE = '42501';
    END IF;

    RETURN NEW;
END;
$$;

REVOKE ALL ON FUNCTION sigm.fn_validate_role_permission() FROM PUBLIC;

DROP TRIGGER IF EXISTS trg_validate_role_permission ON sigm.role_permissions;
CREATE TRIGGER trg_validate_role_permission
    BEFORE INSERT OR UPDATE ON sigm.role_permissions
    FOR EACH ROW EXECUTE FUNCTION sigm.fn_validate_role_permission();

-- ----------------------------------------------------------------------------
-- 4. Un tenant no puede asignar el rol de plataforma.
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION sigm.fn_validate_role_tenant()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_role_tenant uuid;
    v_role_code   varchar(60);
BEGIN
    IF NEW.role_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT tenant_id, code INTO v_role_tenant, v_role_code
    FROM sigm.roles
    WHERE id = NEW.role_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'El rol % no existe', NEW.role_id
            USING ERRCODE = '23503';
    END IF;

    IF v_role_tenant IS NOT NULL AND v_role_tenant <> NEW.tenant_id THEN
        RAISE EXCEPTION 'El rol % no pertenece al tenant %', NEW.role_id, NEW.tenant_id
            USING ERRCODE = '23514';
    END IF;

    IF v_role_code = 'PLATFORM_ADMIN' AND current_user <> 'sigm_platform'
       AND NOT EXISTS (
           SELECT 1 FROM pg_roles
           WHERE rolname = current_user AND rolsuper
       ) THEN
        RAISE EXCEPTION 'Solo sigm_platform puede asignar el rol PLATFORM_ADMIN'
            USING ERRCODE = '42501';
    END IF;

    RETURN NEW;
END;
$$;

COMMIT;
