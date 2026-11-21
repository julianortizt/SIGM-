-- ============================================================================
-- SIGM CLOUD - RUN ALL
-- Ejecutar con psql:
--   psql -U postgres -d sigm_cloud -v ON_ERROR_STOP=1 -f run_all.sql
--
-- Orden deliberado: la migracion 016 (RLS y privilegios) se ejecuta despues
-- de los seeds para que los catalogos globales puedan sembrarse sin contexto
-- de tenant. En produccion, ejecute los seeds como sigm_platform si necesita
-- volver a sembrar despues de habilitar RLS.
--
-- Los seeds 006-008 contienen los catalogos oficiales completos de CIE-10,
-- CUPS e IUM descargados de SISPRO por import/import_catalogs.py. Son
-- idempotentes: puede volver a ejecutar este archivo sin duplicar datos.
-- ============================================================================
\set ON_ERROR_STOP on

\echo 'SIGM Cloud :: migraciones 001-015'
\ir migrations/001_extensions_and_schemas.sql
\ir migrations/002_tenancy_and_subscriptions.sql
\ir migrations/003_organization.sql
\ir migrations/004_identity_access.sql
\ir migrations/005_specialties_professionals.sql
\ir migrations/006_patients.sql
\ir migrations/007_clinical_catalogs.sql
\ir migrations/008_scheduling.sql
\ir migrations/009_clinical_records.sql
\ir migrations/010_documents.sql
\ir migrations/011_laboratory.sql
\ir migrations/012_billing_eps.sql
\ir migrations/013_interoperability.sql
\ir migrations/014_audit.sql
\ir migrations/015_integrity_triggers.sql

\echo 'SIGM Cloud :: roles, permisos, especialidades y catalogos base'
\ir seeds/001_roles_permissions.sql
\ir seeds/002_specialties_and_catalogs.sql

\echo 'SIGM Cloud :: catalogos oficiales completos (SISPRO)'
\ir seeds/003_laboratory_tests_sample.sql
\ir seeds/006_catalogs_cie10.sql
\ir seeds/007_catalogs_cups.sql
\ir seeds/008_catalogs_ium.sql

\echo 'SIGM Cloud :: planes y formularios'
\ir seeds/004_plans.sql
\ir seeds/005_specialty_forms_sample.sql

\echo 'SIGM Cloud :: RLS y privilegios'
\ir migrations/016_rls_and_grants.sql

\echo 'SIGM Cloud :: endurecimiento de privilegios'
\ir migrations/017_security_hardening.sql

\echo 'SIGM Cloud :: superadmin de plataforma y preferencias de usuario'
\ir migrations/018_platform_admin.sql

\echo 'SIGM Cloud :: configuracion global de plataforma (bienvenida)'
\ir migrations/019_platform_settings.sql

\echo 'SIGM Cloud :: consola de base de datos de plataforma'
\ir migrations/020_platform_database.sql

\echo 'SIGM Cloud :: endurecimiento de autenticacion (MFA, sesiones, contrasenas)'
\ir migrations/021_auth_hardening.sql

\echo 'SIGM Cloud :: bloqueo MFA y doble aprobacion de scripts'
\ir migrations/022_mfa_lockout_script_approval.sql

\echo 'SIGM Cloud :: endurecimiento de privilegios y almacenamiento durable'
\ir migrations/023_hardening_and_file_blobs.sql

\echo 'SIGM Cloud :: campos de formularios globales por tenant'
\ir migrations/024_form_field_tenant_trigger.sql

\echo 'SIGM Cloud :: base de datos creada correctamente'
