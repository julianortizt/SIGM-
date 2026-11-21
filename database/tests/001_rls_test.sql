-- ============================================================================
-- SIGM CLOUD - PRUEBA DE AISLAMIENTO MULTI-TENANT
-- Ejecutar despues de run_all.sql.
-- Verifica que sigm_app solo vea datos del tenant establecido en la sesion.
-- ============================================================================
\set ON_ERROR_STOP on

BEGIN;

-- Datos creados como propietario/superusuario (bypass RLS).
INSERT INTO sigm.tenants (id, tenant_code, legal_name, commercial_name, product_type)
VALUES
    ('11111111-1111-1111-1111-111111111111', 'RLS_A', 'Tenant A S.A.S.', 'Tenant A', 'CLINIC'),
    ('22222222-2222-2222-2222-222222222222', 'RLS_B', 'Tenant B S.A.S.', 'Tenant B', 'PRACTICE');

INSERT INTO sigm.patients (tenant_id, first_name, last_name)
VALUES
    ('11111111-1111-1111-1111-111111111111', 'Ana', 'Tenant A'),
    ('22222222-2222-2222-2222-222222222222', 'Beto', 'Tenant B');

INSERT INTO sigm.clinical_records (tenant_id, patient_id)
SELECT tenant_id, id
FROM sigm.patients
WHERE tenant_id IN (
    '11111111-1111-1111-1111-111111111111',
    '22222222-2222-2222-2222-222222222222'
);

-- Datos adicionales para probar escalamiento de privilegios.
INSERT INTO sigm.users (id, email, full_name)
VALUES ('11111111-1111-1111-1111-111111111101', 'rls-a@test.local', 'Usuario RLS A');

INSERT INTO sigm.user_tenants (tenant_id, user_id, status)
VALUES ('11111111-1111-1111-1111-111111111111',
        '11111111-1111-1111-1111-111111111101', 'ACTIVE');

INSERT INTO sigm.roles (id, tenant_id, code, name, is_system)
VALUES ('11111111-1111-1111-1111-111111111102',
        '11111111-1111-1111-1111-111111111111', 'CUSTOM_RLS', 'Rol personalizado RLS', false);

-- A partir de aqui se prueba con el rol de aplicacion, sujeto a RLS.
SET ROLE sigm_app;
SET LOCAL app.tenant_id = '11111111-1111-1111-1111-111111111111';

DO $$
DECLARE
    v_count integer;
BEGIN
    SELECT count(*) INTO v_count FROM sigm.tenants;
    IF v_count <> 1 THEN
        RAISE EXCEPTION 'RLS fallo en tenants: se esperaban 1 fila y se obtuvieron %', v_count;
    END IF;

    SELECT count(*) INTO v_count FROM sigm.patients;
    IF v_count <> 1 THEN
        RAISE EXCEPTION 'RLS fallo en patients: se esperaba 1 fila y se obtuvieron %', v_count;
    END IF;

    SELECT count(*) INTO v_count
    FROM sigm.patients
    WHERE tenant_id = '22222222-2222-2222-2222-222222222222';
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'RLS fallo: se ven pacientes de otro tenant';
    END IF;

    SELECT count(*) INTO v_count FROM sigm.clinical_records;
    IF v_count <> 1 THEN
        RAISE EXCEPTION 'RLS fallo en clinical_records: se esperaba 1 fila y se obtuvieron %', v_count;
    END IF;

    BEGIN
        INSERT INTO sigm.patients (tenant_id, first_name, last_name)
        VALUES ('22222222-2222-2222-2222-222222222222', 'Intruso', 'Tenant B');
        RAISE EXCEPTION 'RLS fallo: se permitio insertar un paciente de otro tenant';
    EXCEPTION
        WHEN insufficient_privilege THEN
            NULL;
    END;

    -- RLS no lanza error en UPDATE: filtra las filas y deja ROW_COUNT en 0.
    UPDATE sigm.patients
    SET first_name = 'Alterado'
    WHERE tenant_id = '22222222-2222-2222-2222-222222222222';

    GET DIAGNOSTICS v_count = ROW_COUNT;
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'RLS fallo: se actualizaron % pacientes de otro tenant', v_count;
    END IF;

    -- Catalogos globales: la aplicacion no puede modificarlos.
    BEGIN
        INSERT INTO sigm.plans (code, name, price)
        VALUES ('HACK_PLAN', 'Plan no autorizado', 0);
        RAISE EXCEPTION 'Privilegios fallo: sigm_app pudo insertar en plans';
    EXCEPTION
        WHEN insufficient_privilege THEN
            NULL;
    END;

    -- Un rol de tenant no puede recibir permisos de plataforma.
    BEGIN
        INSERT INTO sigm.role_permissions (role_id, permission_id)
        SELECT '11111111-1111-1111-1111-111111111102', id
        FROM sigm.permissions
        WHERE code = 'platform.tenants.manage';
        RAISE EXCEPTION 'Privilegios fallo: se asigno un permiso de plataforma a un rol de tenant';
    EXCEPTION
        WHEN insufficient_privilege THEN
            NULL;
    END;

    -- Un tenant no puede asignarse el rol PLATFORM_ADMIN.
    BEGIN
        INSERT INTO sigm.user_roles (tenant_id, user_id, role_id)
        SELECT '11111111-1111-1111-1111-111111111111',
               '11111111-1111-1111-1111-111111111101',
               id
        FROM sigm.roles
        WHERE code = 'PLATFORM_ADMIN' AND tenant_id IS NULL;
        RAISE EXCEPTION 'Privilegios fallo: se asigno PLATFORM_ADMIN desde un tenant';
    EXCEPTION
        WHEN insufficient_privilege THEN
            NULL;
    END;

    SELECT count(*) INTO v_count
    FROM sigm.audit_logs
    WHERE tenant_id IS DISTINCT FROM '11111111-1111-1111-1111-111111111111';
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'RLS fallo en audit_logs: se ven eventos de otro tenant';
    END IF;

    RAISE NOTICE 'Prueba de aislamiento multi-tenant: OK';
END $$;

RESET ROLE;

-- Verificacion como propietario: el dato del otro tenant quedo intacto.
DO $$
DECLARE
    v_name varchar(100);
BEGIN
    SELECT first_name INTO v_name
    FROM sigm.patients
    WHERE tenant_id = '22222222-2222-2222-2222-222222222222';

    IF v_name IS DISTINCT FROM 'Beto' THEN
        RAISE EXCEPTION 'RLS fallo: el paciente del tenant B quedo con nombre %', v_name;
    END IF;
END $$;

ROLLBACK;
