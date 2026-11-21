-- ============================================================================
-- SIGM CLOUD - 024 - Campos de formularios globales por tenant
-- ============================================================================
-- Los formularios de catálogo (specialty_forms.tenant_id IS NULL) son plantillas
-- globales: cada tenant debe poder añadir sus propios campos (por ejemplo,
-- "e2e_hardening_check") con su tenant_id. El trigger anterior exigía igualdad
-- estricta y rechazaba esos inserts con 23514, de modo que
-- POST /api/v1/specialty-forms/{id}/fields fallaba para TODOS los formularios
-- sembrados. Los formularios propios de un tenant siguen estrictamente aislados.
-- Idempotente.
-- ============================================================================
BEGIN;

CREATE OR REPLACE FUNCTION sigm.fn_validate_form_field_tenant()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
DECLARE
    v_form_tenant uuid;
BEGIN
    SELECT tenant_id INTO v_form_tenant
    FROM sigm.specialty_forms
    WHERE id = NEW.form_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'El formulario % no existe', NEW.form_id
            USING ERRCODE = '23503';
    END IF;

    -- Plantilla global: permite campos globales (NULL) o personalizaciones por tenant.
    -- Formulario de un tenant: el campo debe pertenecer exactamente a ese tenant.
    IF v_form_tenant IS NOT NULL AND NEW.tenant_id IS DISTINCT FROM v_form_tenant THEN
        RAISE EXCEPTION 'El campo % no pertenece al tenant del formulario %', NEW.id, NEW.form_id
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$function$;

COMMIT;
