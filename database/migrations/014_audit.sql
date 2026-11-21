-- ============================================================================
-- SIGM CLOUD - 014 - Auditoria, accesos e incidentes de seguridad
-- ============================================================================
BEGIN;

-- ----------------------------------------------------------------------------
-- Auditoria general. tenant_id NULL = evento de plataforma visible solo para
-- el rol de plataforma; los tenants nunca ven registros ajenos.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.audit_logs (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   uuid REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    user_id     uuid REFERENCES sigm.users(id) ON DELETE SET NULL,
    action      varchar(80) NOT NULL,
    entity_type varchar(80) NOT NULL,
    entity_id   uuid,
    result      varchar(20) NOT NULL DEFAULT 'SUCCESS'
                CHECK (result IN ('SUCCESS', 'FAILURE', 'DENIED')),
    reason      text,
    metadata    jsonb NOT NULL DEFAULT '{}'::jsonb,
    ip_address  inet,
    user_agent  text,
    occurred_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_audit_logs_tenant_date
    ON sigm.audit_logs (tenant_id, occurred_at DESC);

CREATE INDEX IF NOT EXISTS ix_audit_logs_entity
    ON sigm.audit_logs (tenant_id, entity_type, entity_id, occurred_at DESC);

CREATE INDEX IF NOT EXISTS ix_audit_logs_user
    ON sigm.audit_logs (tenant_id, user_id, occurred_at DESC);

COMMENT ON TABLE sigm.audit_logs IS
  'Registro append-only. La aplicacion no puede actualizar ni eliminar filas.';

-- ----------------------------------------------------------------------------
-- Accesos a informacion clinica (lecturas, descargas, impresiones, exportes)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.access_logs (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id      uuid NOT NULL DEFAULT sigm.current_tenant_id()
                   REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    user_id        uuid REFERENCES sigm.users(id) ON DELETE SET NULL,
    patient_id     uuid,
    resource_type  varchar(80) NOT NULL,
    resource_id    uuid,
    action         varchar(30) NOT NULL
                   CHECK (action IN ('VIEW', 'CREATE', 'UPDATE', 'DELETE', 'DOWNLOAD', 'PRINT', 'EXPORT', 'SHARE')),
    justification  text,
    metadata       jsonb NOT NULL DEFAULT '{}'::jsonb,
    ip_address     inet,
    user_agent     text,
    occurred_at    timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_access_logs_patient_date
    ON sigm.access_logs (tenant_id, patient_id, occurred_at DESC);

CREATE INDEX IF NOT EXISTS ix_access_logs_user_date
    ON sigm.access_logs (tenant_id, user_id, occurred_at DESC);

-- ----------------------------------------------------------------------------
-- Incidentes de seguridad
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.security_incidents (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id    uuid REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    incident_type varchar(80) NOT NULL,
    severity     varchar(20) NOT NULL DEFAULT 'MEDIUM'
                 CHECK (severity IN ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL')),
    description  text NOT NULL,
    status       varchar(20) NOT NULL DEFAULT 'OPEN'
                 CHECK (status IN ('OPEN', 'INVESTIGATING', 'CONTAINED', 'RESOLVED', 'CLOSED')),
    detected_at  timestamptz NOT NULL DEFAULT now(),
    resolved_at  timestamptz,
    reported_to  varchar(200),
    metadata     jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_security_incidents_status
    ON sigm.security_incidents (tenant_id, status, severity);

COMMIT;
