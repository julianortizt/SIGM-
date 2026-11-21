-- ============================================================================
-- SIGM CLOUD - 016 - Row Level Security, roles de base de datos y privilegios
-- ============================================================================
BEGIN;

-- ----------------------------------------------------------------------------
-- Roles de base de datos
-- sigm_app      : rol de la aplicacion. No es propietario ni tiene BYPASSRLS.
-- sigm_platform : rol de aprovisionamiento, autenticacion y tareas globales.
-- La creacion de roles requiere privilegios de administrador.
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sigm_app') THEN
        EXECUTE 'CREATE ROLE sigm_app NOLOGIN';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sigm_platform') THEN
        EXECUTE 'CREATE ROLE sigm_platform NOLOGIN BYPASSRLS';
    END IF;
EXCEPTION
    WHEN insufficient_privilege THEN
        RAISE NOTICE 'No se pudieron crear los roles sigm_app/sigm_platform. Creelos manualmente antes de desplegar.';
END $$;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sigm_app') THEN
        EXECUTE 'GRANT USAGE ON SCHEMA sigm TO sigm_app';
        EXECUTE 'GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA sigm TO sigm_app';
        EXECUTE 'GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA sigm TO sigm_app';
        EXECUTE 'GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA sigm TO sigm_app';

        -- El rol de aplicacion nunca debe modificar evidencia clinica ni auditoria.
        EXECUTE 'REVOKE UPDATE, DELETE ON sigm.audit_logs FROM sigm_app';
        EXECUTE 'REVOKE UPDATE, DELETE ON sigm.access_logs FROM sigm_app';
        EXECUTE 'REVOKE UPDATE, DELETE ON sigm.authentication_events FROM sigm_app';
        EXECUTE 'REVOKE UPDATE, DELETE ON sigm.transmission_logs FROM sigm_app';
        EXECUTE 'REVOKE UPDATE, DELETE ON sigm.clinical_note_versions FROM sigm_app';
        EXECUTE 'REVOKE UPDATE, DELETE ON sigm.clinical_amendments FROM sigm_app';
        EXECUTE 'REVOKE UPDATE, DELETE ON sigm.document_signatures FROM sigm_app';
        EXECUTE 'REVOKE DELETE ON sigm.clinical_notes FROM sigm_app';
        EXECUTE 'REVOKE DELETE ON sigm.encounters FROM sigm_app';
        EXECUTE 'REVOKE DELETE ON sigm.patients FROM sigm_app';
        EXECUTE 'REVOKE DELETE ON sigm.invoices FROM sigm_app';
        EXECUTE 'REVOKE DELETE ON sigm.payments FROM sigm_app';

        EXECUTE 'REVOKE ALL ON FUNCTION sigm.fn_audit_trigger() FROM sigm_app';
    ELSE
        RAISE NOTICE 'Rol sigm_app ausente: se omiten privilegios. Creelo y vuelva a ejecutar 016.';
    END IF;
END $$;

-- Funciones que no debe ejecutar ningun rol de aplicacion
REVOKE ALL ON FUNCTION sigm.fn_audit_trigger() FROM PUBLIC;

-- ----------------------------------------------------------------------------
-- Helpers de aplicacion de RLS
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION sigm.enable_tenant_rls(p_table regclass)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    EXECUTE format('ALTER TABLE %s ENABLE ROW LEVEL SECURITY', p_table);
    EXECUTE format('ALTER TABLE %s FORCE ROW LEVEL SECURITY', p_table);
    EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %s', p_table);
    EXECUTE format(
        'CREATE POLICY tenant_isolation ON %s
         FOR ALL
         USING (tenant_id = sigm.current_tenant_id())
         WITH CHECK (tenant_id = sigm.current_tenant_id())',
        p_table
    );
END;
$$;

CREATE OR REPLACE FUNCTION sigm.enable_global_tenant_rls(p_table regclass)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    EXECUTE format('ALTER TABLE %s ENABLE ROW LEVEL SECURITY', p_table);
    EXECUTE format('ALTER TABLE %s FORCE ROW LEVEL SECURITY', p_table);
    EXECUTE format('DROP POLICY IF EXISTS global_or_tenant_select ON %s', p_table);
    EXECUTE format('DROP POLICY IF EXISTS tenant_write ON %s', p_table);
    EXECUTE format(
        'CREATE POLICY global_or_tenant_select ON %s
         FOR SELECT
         USING (tenant_id IS NULL OR tenant_id = sigm.current_tenant_id())',
        p_table
    );
    EXECUTE format(
        'CREATE POLICY tenant_write ON %s
         FOR ALL
         USING (tenant_id = sigm.current_tenant_id())
         WITH CHECK (tenant_id = sigm.current_tenant_id())',
        p_table
    );
END;
$$;

REVOKE ALL ON FUNCTION sigm.enable_tenant_rls(regclass) FROM PUBLIC;
REVOKE ALL ON FUNCTION sigm.enable_global_tenant_rls(regclass) FROM PUBLIC;

-- ----------------------------------------------------------------------------
-- Tablas estrictamente aisladas por tenant
-- ----------------------------------------------------------------------------
SELECT sigm.enable_tenant_rls('sigm.tenant_subscriptions');
SELECT sigm.enable_tenant_rls('sigm.tenant_features');
SELECT sigm.enable_tenant_rls('sigm.tenant_settings');
SELECT sigm.enable_tenant_rls('sigm.tenant_domains');
SELECT sigm.enable_tenant_rls('sigm.tenant_branding');
SELECT sigm.enable_tenant_rls('sigm.subscription_payments');
SELECT sigm.enable_tenant_rls('sigm.organizations');
SELECT sigm.enable_tenant_rls('sigm.sites');
SELECT sigm.enable_tenant_rls('sigm.departments');
SELECT sigm.enable_tenant_rls('sigm.medical_offices');
SELECT sigm.enable_tenant_rls('sigm.resources');
SELECT sigm.enable_tenant_rls('sigm.user_tenants');
SELECT sigm.enable_tenant_rls('sigm.user_roles');
SELECT sigm.enable_tenant_rls('sigm.user_sessions');
SELECT sigm.enable_tenant_rls('sigm.authentication_events');
SELECT sigm.enable_tenant_rls('sigm.tenant_specialties');
SELECT sigm.enable_tenant_rls('sigm.professionals');
SELECT sigm.enable_tenant_rls('sigm.professional_sites');
SELECT sigm.enable_tenant_rls('sigm.professional_specialties');
SELECT sigm.enable_tenant_rls('sigm.patients');
SELECT sigm.enable_tenant_rls('sigm.patient_identifiers');
SELECT sigm.enable_tenant_rls('sigm.patient_contacts');
SELECT sigm.enable_tenant_rls('sigm.patient_addresses');
SELECT sigm.enable_tenant_rls('sigm.patient_relatives');
SELECT sigm.enable_tenant_rls('sigm.patient_alerts');
SELECT sigm.enable_tenant_rls('sigm.patient_consents');
SELECT sigm.enable_tenant_rls('sigm.personal_data_requests');
SELECT sigm.enable_tenant_rls('sigm.patient_merge_log');
SELECT sigm.enable_tenant_rls('sigm.appointment_types');
SELECT sigm.enable_tenant_rls('sigm.schedules');
SELECT sigm.enable_tenant_rls('sigm.availability_rules');
SELECT sigm.enable_tenant_rls('sigm.schedule_exceptions');
SELECT sigm.enable_tenant_rls('sigm.appointments');
SELECT sigm.enable_tenant_rls('sigm.appointment_status_history');
SELECT sigm.enable_tenant_rls('sigm.appointment_resources');
SELECT sigm.enable_tenant_rls('sigm.waiting_list');
SELECT sigm.enable_tenant_rls('sigm.reminders');
SELECT sigm.enable_tenant_rls('sigm.clinical_records');
SELECT sigm.enable_tenant_rls('sigm.encounters');
SELECT sigm.enable_tenant_rls('sigm.clinical_notes');
SELECT sigm.enable_tenant_rls('sigm.clinical_note_versions');
SELECT sigm.enable_tenant_rls('sigm.clinical_amendments');
SELECT sigm.enable_tenant_rls('sigm.medical_history');
SELECT sigm.enable_tenant_rls('sigm.family_history');
SELECT sigm.enable_tenant_rls('sigm.allergies');
SELECT sigm.enable_tenant_rls('sigm.vital_signs');
SELECT sigm.enable_tenant_rls('sigm.diagnoses');
SELECT sigm.enable_tenant_rls('sigm.medical_orders');
SELECT sigm.enable_tenant_rls('sigm.procedure_orders');
SELECT sigm.enable_tenant_rls('sigm.patient_medications');
SELECT sigm.enable_tenant_rls('sigm.prescriptions');
SELECT sigm.enable_tenant_rls('sigm.prescription_items');
SELECT sigm.enable_tenant_rls('sigm.documents');
SELECT sigm.enable_tenant_rls('sigm.document_versions');
SELECT sigm.enable_tenant_rls('sigm.document_permissions');
SELECT sigm.enable_tenant_rls('sigm.document_signatures');
SELECT sigm.enable_tenant_rls('sigm.laboratory_orders');
SELECT sigm.enable_tenant_rls('sigm.laboratory_order_items');
SELECT sigm.enable_tenant_rls('sigm.laboratory_results');
SELECT sigm.enable_tenant_rls('sigm.diagnostic_reports');
SELECT sigm.enable_tenant_rls('sigm.patient_insurances');
SELECT sigm.enable_tenant_rls('sigm.contracts');
SELECT sigm.enable_tenant_rls('sigm.service_catalog');
SELECT sigm.enable_tenant_rls('sigm.price_lists');
SELECT sigm.enable_tenant_rls('sigm.price_list_items');
SELECT sigm.enable_tenant_rls('sigm.contract_services');
SELECT sigm.enable_tenant_rls('sigm.authorizations');
SELECT sigm.enable_tenant_rls('sigm.invoices');
SELECT sigm.enable_tenant_rls('sigm.invoice_items');
SELECT sigm.enable_tenant_rls('sigm.payments');
SELECT sigm.enable_tenant_rls('sigm.credit_debit_notes');
SELECT sigm.enable_tenant_rls('sigm.rips_reports');
SELECT sigm.enable_tenant_rls('sigm.rips_report_details');
SELECT sigm.enable_tenant_rls('sigm.interoperability_messages');
SELECT sigm.enable_tenant_rls('sigm.interoperability_events');
SELECT sigm.enable_tenant_rls('sigm.transmission_logs');
SELECT sigm.enable_tenant_rls('sigm.rda_documents');
SELECT sigm.enable_tenant_rls('sigm.audit_logs');
SELECT sigm.enable_tenant_rls('sigm.access_logs');
SELECT sigm.enable_tenant_rls('sigm.security_incidents');

-- audit_logs debe permitir que el trigger SECURITY DEFINER escriba aun sin
-- contexto de tenant. El rol de aplicacion sigue sujeto a RLS porque no es
-- propietario de la tabla.
ALTER TABLE sigm.audit_logs NO FORCE ROW LEVEL SECURITY;

-- ----------------------------------------------------------------------------
-- Tablas con filas globales + filas por tenant
-- ----------------------------------------------------------------------------
SELECT sigm.enable_global_tenant_rls('sigm.roles');
SELECT sigm.enable_global_tenant_rls('sigm.specialty_forms');
SELECT sigm.enable_global_tenant_rls('sigm.specialty_form_fields');
SELECT sigm.enable_global_tenant_rls('sigm.document_categories');
SELECT sigm.enable_global_tenant_rls('sigm.fhir_mappings');

-- ----------------------------------------------------------------------------
-- Tenants: el cliente solo ve y actualiza su propia fila.
-- La creacion de tenants es una operacion de plataforma.
-- ----------------------------------------------------------------------------
ALTER TABLE sigm.tenants ENABLE ROW LEVEL SECURITY;
ALTER TABLE sigm.tenants FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_self_select ON sigm.tenants;
DROP POLICY IF EXISTS tenant_self_update ON sigm.tenants;
CREATE POLICY tenant_self_select ON sigm.tenants
    FOR SELECT USING (id = sigm.current_tenant_id());
CREATE POLICY tenant_self_update ON sigm.tenants
    FOR UPDATE USING (id = sigm.current_tenant_id())
    WITH CHECK (id = sigm.current_tenant_id());

-- ----------------------------------------------------------------------------
-- Users: identidad global. La aplicacion ve su propio usuario y los usuarios
-- que pertenecen al tenant actual. El login previo al contexto usa el rol
-- sigm_platform o un endpoint de autenticacion con privilegios controlados.
-- ----------------------------------------------------------------------------
ALTER TABLE sigm.users ENABLE ROW LEVEL SECURITY;
ALTER TABLE sigm.users FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS users_tenant_read ON sigm.users;
DROP POLICY IF EXISTS users_self_update ON sigm.users;
CREATE POLICY users_tenant_read ON sigm.users
    FOR SELECT USING (
        id = sigm.current_user_id()
        OR EXISTS (
            SELECT 1
            FROM sigm.user_tenants ut
            WHERE ut.user_id = sigm.users.id
              AND ut.tenant_id = sigm.current_tenant_id()
        )
    );
CREATE POLICY users_self_update ON sigm.users
    FOR UPDATE USING (id = sigm.current_user_id())
    WITH CHECK (id = sigm.current_user_id());

-- ----------------------------------------------------------------------------
-- Privilegios del rol de plataforma
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sigm_platform') THEN
        GRANT USAGE ON SCHEMA sigm TO sigm_platform;
        GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA sigm TO sigm_platform;
        GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA sigm TO sigm_platform;
        GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA sigm TO sigm_platform;
    END IF;
END $$;

-- Privilegios por defecto para objetos futuros
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sigm_app') THEN
        EXECUTE 'ALTER DEFAULT PRIVILEGES IN SCHEMA sigm
                 GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO sigm_app';
        EXECUTE 'ALTER DEFAULT PRIVILEGES IN SCHEMA sigm
                 GRANT USAGE, SELECT ON SEQUENCES TO sigm_app';
    END IF;
END $$;

COMMIT;
