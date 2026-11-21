-- ============================================================================
-- SIGM CLOUD - 001 - Extensiones, esquema y funciones base
-- PostgreSQL 18
-- ============================================================================
BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS btree_gist;
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS citext;
CREATE EXTENSION IF NOT EXISTS unaccent;

CREATE SCHEMA IF NOT EXISTS sigm;

COMMENT ON SCHEMA sigm IS
  'Esquema principal de SIGM Cloud. Contiene el nucleo clinico, administrativo y de seguridad de la plataforma SaaS multi-tenant.';

-- ----------------------------------------------------------------------------
-- Contexto de sesion
-- El backend debe ejecutar SET LOCAL app.tenant_id = '<uuid>' dentro de cada
-- transaccion, despues de validar la identidad y la pertenencia del usuario.
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION sigm.current_tenant_id()
RETURNS uuid
LANGUAGE sql
STABLE
AS $$
  SELECT NULLIF(current_setting('app.tenant_id', true), '')::uuid;
$$;

COMMENT ON FUNCTION sigm.current_tenant_id() IS
  'Devuelve el tenant_id validado para la transaccion actual. NULL si no hay contexto.';

CREATE OR REPLACE FUNCTION sigm.current_user_id()
RETURNS uuid
LANGUAGE sql
STABLE
AS $$
  SELECT NULLIF(current_setting('app.user_id', true), '')::uuid;
$$;

COMMENT ON FUNCTION sigm.current_user_id() IS
  'Devuelve el user_id autenticado para la transaccion actual. NULL si no hay contexto.';

-- ----------------------------------------------------------------------------
-- Trigger generico para updated_at
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION sigm.set_updated_at()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  NEW.updated_at := now();
  RETURN NEW;
END;
$$;

COMMENT ON FUNCTION sigm.set_updated_at() IS
  'Actualiza automaticamente la columna updated_at en cada UPDATE.';

-- ----------------------------------------------------------------------------
-- Trigger generico para tablas inmutables
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION sigm.prevent_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  RAISE EXCEPTION 'La tabla %.% es inmutable y no admite %.', TG_TABLE_SCHEMA, TG_TABLE_NAME, TG_OP
    USING ERRCODE = '55000';
END;
$$;

COMMENT ON FUNCTION sigm.prevent_mutation() IS
  'Impide UPDATE/DELETE sobre tablas que representan evidencia clinica o auditoria.';

COMMIT;
