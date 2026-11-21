-- ============================================================================
-- SIGM CLOUD - 015 - Triggers de integridad, inmutabilidad y auditoria
-- ============================================================================
BEGIN;

-- ----------------------------------------------------------------------------
-- updated_at automatico en todas las tablas que lo tengan
-- ----------------------------------------------------------------------------
DO $$
DECLARE
    r record;
BEGIN
    FOR r IN
        SELECT c.oid::regclass AS table_name
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        JOIN pg_attribute a ON a.attrelid = c.oid AND a.attname = 'updated_at' AND NOT a.attisdropped
        WHERE n.nspname = 'sigm'
          AND c.relkind = 'r'
    LOOP
        EXECUTE format('DROP TRIGGER IF EXISTS trg_set_updated_at ON %s', r.table_name);
        EXECUTE format(
            'CREATE TRIGGER trg_set_updated_at BEFORE UPDATE ON %s
             FOR EACH ROW EXECUTE FUNCTION sigm.set_updated_at()',
            r.table_name
        );
    END LOOP;
END $$;

-- ----------------------------------------------------------------------------
-- Trigger de auditoria generico
-- Se ejecuta como SECURITY DEFINER para poder escribir en audit_logs.
-- No se concede EXECUTE al rol de aplicacion.
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION sigm.fn_audit_trigger()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = sigm, pg_temp
AS $$
DECLARE
    v_row     jsonb;
    v_old     jsonb;
    v_tenant  uuid;
    v_entity  uuid;
    v_user    uuid;
    v_changed jsonb := '[]'::jsonb;
BEGIN
    IF TG_OP = 'DELETE' THEN
        v_row := to_jsonb(OLD);
        v_old := to_jsonb(OLD);
    ELSE
        v_row := to_jsonb(NEW);
        v_old := CASE WHEN TG_OP = 'UPDATE' THEN to_jsonb(OLD) ELSE '{}'::jsonb END;
    END IF;

    v_tenant := NULLIF(v_row ->> 'tenant_id', '')::uuid;
    v_entity := NULLIF(v_row ->> 'id', '')::uuid;
    v_user   := sigm.current_user_id();

    IF TG_OP = 'UPDATE' THEN
        SELECT coalesce(jsonb_agg(n.key ORDER BY n.key), '[]'::jsonb)
        INTO v_changed
        FROM jsonb_each(v_row) AS n
        WHERE v_old -> n.key IS DISTINCT FROM n.value;
    END IF;

    INSERT INTO sigm.audit_logs (
        tenant_id, user_id, action, entity_type, entity_id, result, metadata
    ) VALUES (
        v_tenant,
        v_user,
        lower(TG_OP),
        TG_TABLE_NAME,
        v_entity,
        'SUCCESS',
        jsonb_build_object('changed_fields', v_changed)
    );

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$;

REVOKE ALL ON FUNCTION sigm.fn_audit_trigger() FROM PUBLIC;

-- ----------------------------------------------------------------------------
-- Validacion: un rol solo puede asignarse dentro de su tenant o si es global
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION sigm.fn_validate_role_tenant()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_role_tenant uuid;
BEGIN
    IF NEW.role_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT tenant_id INTO v_role_tenant
    FROM sigm.roles
    WHERE id = NEW.role_id;

    IF v_role_tenant IS NOT NULL AND v_role_tenant <> NEW.tenant_id THEN
        RAISE EXCEPTION 'El rol % no pertenece al tenant %', NEW.role_id, NEW.tenant_id
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

-- ----------------------------------------------------------------------------
-- Validacion: una categoria documental global o del mismo tenant
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION sigm.fn_validate_document_category()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_category_tenant uuid;
BEGIN
    IF NEW.category_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT tenant_id INTO v_category_tenant
    FROM sigm.document_categories
    WHERE id = NEW.category_id;

    IF v_category_tenant IS NOT NULL AND v_category_tenant <> NEW.tenant_id THEN
        RAISE EXCEPTION 'La categoria documental % no pertenece al tenant %', NEW.category_id, NEW.tenant_id
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

-- ----------------------------------------------------------------------------
-- Validacion: el campo de formulario debe pertenecer al mismo tenant del form
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION sigm.fn_validate_form_field_tenant()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_form_tenant uuid;
BEGIN
    SELECT tenant_id INTO v_form_tenant
    FROM sigm.specialty_forms
    WHERE id = NEW.form_id;

    IF v_form_tenant IS DISTINCT FROM NEW.tenant_id THEN
        RAISE EXCEPTION 'El campo % no pertenece al tenant del formulario %', NEW.id, NEW.form_id
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

-- ----------------------------------------------------------------------------
-- Historial automatico de estados de cita
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION sigm.fn_appointment_status_history()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_user uuid := sigm.current_user_id();
BEGIN
    IF NEW.status IS DISTINCT FROM OLD.status THEN
        IF v_user IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM sigm.user_tenants
            WHERE tenant_id = NEW.tenant_id AND user_id = v_user
        ) THEN
            v_user := NULL;
        END IF;

        INSERT INTO sigm.appointment_status_history (
            tenant_id, appointment_id, from_status, to_status, changed_by, reason
        ) VALUES (
            NEW.tenant_id, NEW.id, OLD.status, NEW.status, v_user, NEW.cancellation_reason
        );
    END IF;

    RETURN NEW;
END;
$$;

-- ----------------------------------------------------------------------------
-- Mantiene current_version en la cabecera de la nota
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION sigm.fn_clinical_note_version_after_insert()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_status varchar(20);
BEGIN
    SELECT status INTO v_status
    FROM sigm.clinical_notes
    WHERE tenant_id = NEW.tenant_id AND id = NEW.note_id;

    IF v_status IN ('SIGNED', 'AMENDED', 'VOID') THEN
        RAISE EXCEPTION 'La nota % ya esta firmada. Use una adenda para modificarla.', NEW.note_id
            USING ERRCODE = '55000';
    END IF;

    UPDATE sigm.clinical_notes
    SET current_version = greatest(current_version, NEW.version_number),
        updated_at = now()
    WHERE tenant_id = NEW.tenant_id AND id = NEW.note_id;

    RETURN NEW;
END;
$$;

-- ----------------------------------------------------------------------------
-- Exige una version firmada antes de marcar la nota como SIGNED
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION sigm.fn_clinical_note_sign_guard()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.status = 'SIGNED' AND OLD.status <> 'SIGNED' THEN
        IF NOT EXISTS (
            SELECT 1 FROM sigm.clinical_note_versions
            WHERE tenant_id = NEW.tenant_id
              AND note_id = NEW.id
              AND signed_at IS NOT NULL
        ) THEN
            RAISE EXCEPTION 'La nota % no puede firmarse sin una version firmada.', NEW.id
                USING ERRCODE = '23514';
        END IF;

        NEW.signed_at := coalesce(NEW.signed_at, now());
        NEW.signed_by := coalesce(NEW.signed_by, sigm.current_user_id());
    END IF;

    RETURN NEW;
END;
$$;

-- ----------------------------------------------------------------------------
-- Impide eliminar notas firmadas
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION sigm.fn_prevent_signed_note_delete()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.status IN ('SIGNED', 'AMENDED') THEN
        RAISE EXCEPTION 'No se puede eliminar una nota clinica firmada (%).', OLD.id
            USING ERRCODE = '55000';
    END IF;

    RETURN OLD;
END;
$$;

-- ----------------------------------------------------------------------------
-- Recalculo de totales de factura a partir de sus items
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION sigm.recalculate_invoice_totals(p_tenant_id uuid, p_invoice_id uuid)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE sigm.invoices i
    SET subtotal = coalesce(x.subtotal, 0),
        discount_total = coalesce(x.discount_total, 0),
        tax_total = coalesce(x.tax_total, 0),
        total = coalesce(x.total, 0),
        updated_at = now()
    FROM (
        SELECT
            sum(quantity * unit_price) AS subtotal,
            sum(discount_amount) AS discount_total,
            sum(tax_amount) AS tax_total,
            sum(line_total) AS total
        FROM sigm.invoice_items
        WHERE tenant_id = p_tenant_id AND invoice_id = p_invoice_id
    ) x
    WHERE i.tenant_id = p_tenant_id AND i.id = p_invoice_id;
END;
$$;

CREATE OR REPLACE FUNCTION sigm.fn_invoice_items_totals()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        PERFORM sigm.recalculate_invoice_totals(OLD.tenant_id, OLD.invoice_id);
        RETURN OLD;
    END IF;

    PERFORM sigm.recalculate_invoice_totals(NEW.tenant_id, NEW.invoice_id);

    IF TG_OP = 'UPDATE' AND NEW.invoice_id <> OLD.invoice_id THEN
        PERFORM sigm.recalculate_invoice_totals(OLD.tenant_id, OLD.invoice_id);
    END IF;

    RETURN NEW;
END;
$$;

-- ----------------------------------------------------------------------------
-- Instalacion de triggers
-- ----------------------------------------------------------------------------
DO $$
DECLARE
    t text;
    audit_tables text[] := ARRAY[
        'patients', 'patient_identifiers', 'patient_alerts', 'patient_consents',
        'clinical_records', 'encounters', 'clinical_notes', 'clinical_note_versions',
        'clinical_amendments', 'medical_history', 'family_history', 'allergies',
        'vital_signs', 'diagnoses', 'medical_orders', 'procedure_orders',
        'patient_medications', 'prescriptions', 'prescription_items',
        'documents', 'document_versions', 'document_permissions', 'document_signatures',
        'laboratory_orders', 'laboratory_order_items', 'laboratory_results', 'diagnostic_reports',
        'patient_insurances', 'contracts', 'service_catalog', 'price_lists',
        'price_list_items', 'contract_services', 'authorizations', 'invoices',
        'invoice_items', 'payments', 'credit_debit_notes', 'rips_reports',
        'rips_report_details', 'interoperability_messages', 'transmission_logs',
        'rda_documents', 'user_tenants', 'user_roles', 'appointments'
    ];
    immutable_tables text[] := ARRAY[
        'clinical_note_versions', 'clinical_amendments', 'audit_logs',
        'access_logs', 'document_signatures'
    ];
BEGIN
    FOREACH t IN ARRAY audit_tables LOOP
        EXECUTE format('DROP TRIGGER IF EXISTS trg_audit ON sigm.%I', t);
        EXECUTE format(
            'CREATE TRIGGER trg_audit AFTER INSERT OR UPDATE OR DELETE ON sigm.%I
             FOR EACH ROW EXECUTE FUNCTION sigm.fn_audit_trigger()',
            t
        );
    END LOOP;

    FOREACH t IN ARRAY immutable_tables LOOP
        EXECUTE format('DROP TRIGGER IF EXISTS trg_immutable ON sigm.%I', t);
        EXECUTE format(
            'CREATE TRIGGER trg_immutable BEFORE UPDATE OR DELETE ON sigm.%I
             FOR EACH ROW EXECUTE FUNCTION sigm.prevent_mutation()',
            t
        );
    END LOOP;
END $$;

-- Triggers especificos
DROP TRIGGER IF EXISTS trg_validate_role_tenant ON sigm.user_roles;
CREATE TRIGGER trg_validate_role_tenant
    BEFORE INSERT OR UPDATE ON sigm.user_roles
    FOR EACH ROW EXECUTE FUNCTION sigm.fn_validate_role_tenant();

DROP TRIGGER IF EXISTS trg_validate_role_tenant_docperm ON sigm.document_permissions;
CREATE TRIGGER trg_validate_role_tenant_docperm
    BEFORE INSERT OR UPDATE ON sigm.document_permissions
    FOR EACH ROW EXECUTE FUNCTION sigm.fn_validate_role_tenant();

DROP TRIGGER IF EXISTS trg_validate_document_category ON sigm.documents;
CREATE TRIGGER trg_validate_document_category
    BEFORE INSERT OR UPDATE ON sigm.documents
    FOR EACH ROW EXECUTE FUNCTION sigm.fn_validate_document_category();

DROP TRIGGER IF EXISTS trg_validate_form_field_tenant ON sigm.specialty_form_fields;
CREATE TRIGGER trg_validate_form_field_tenant
    BEFORE INSERT OR UPDATE ON sigm.specialty_form_fields
    FOR EACH ROW EXECUTE FUNCTION sigm.fn_validate_form_field_tenant();

DROP TRIGGER IF EXISTS trg_appointment_status_history ON sigm.appointments;
CREATE TRIGGER trg_appointment_status_history
    AFTER UPDATE OF status ON sigm.appointments
    FOR EACH ROW EXECUTE FUNCTION sigm.fn_appointment_status_history();

DROP TRIGGER IF EXISTS trg_clinical_note_version_after_insert ON sigm.clinical_note_versions;
CREATE TRIGGER trg_clinical_note_version_after_insert
    AFTER INSERT ON sigm.clinical_note_versions
    FOR EACH ROW EXECUTE FUNCTION sigm.fn_clinical_note_version_after_insert();

DROP TRIGGER IF EXISTS trg_clinical_note_sign_guard ON sigm.clinical_notes;
CREATE TRIGGER trg_clinical_note_sign_guard
    BEFORE UPDATE ON sigm.clinical_notes
    FOR EACH ROW EXECUTE FUNCTION sigm.fn_clinical_note_sign_guard();

DROP TRIGGER IF EXISTS trg_prevent_signed_note_delete ON sigm.clinical_notes;
CREATE TRIGGER trg_prevent_signed_note_delete
    BEFORE DELETE ON sigm.clinical_notes
    FOR EACH ROW EXECUTE FUNCTION sigm.fn_prevent_signed_note_delete();

DROP TRIGGER IF EXISTS trg_invoice_items_totals ON sigm.invoice_items;
CREATE TRIGGER trg_invoice_items_totals
    AFTER INSERT OR UPDATE OR DELETE ON sigm.invoice_items
    FOR EACH ROW EXECUTE FUNCTION sigm.fn_invoice_items_totals();

COMMIT;
