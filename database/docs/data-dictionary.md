# Diccionario de datos - SIGM Cloud

Generado desde el catalogo real de PostgreSQL 18 (`pg_class`, `pg_attribute`).

- Esquema: `sigm`
- Tablas: 98
- Vistas: 1 (`sigm.v_accounts_receivable`)
- Indices: 286
- Restricciones: 1337
- Politicas RLS: 96
- Triggers: 134

> Los tipos y defaults corresponden exactamente a las migraciones de `database/migrations`.

> Los catálogos `diagnosis_catalog`, `procedure_catalog` y `medication_catalog`
> se cargan completos desde SISPRO. Volúmenes y hashes en
> [catalogs.md](catalogs.md).

#### `sigm.access_logs`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `user_id` | uuid | no |  |
| `patient_id` | uuid | no |  |
| `resource_type` | character varying(80) | si |  |
| `resource_id` | uuid | no |  |
| `action` | character varying(30) | si |  |
| `justification` | text | no |  |
| `metadata` | jsonb | si | `'{}'::jsonb` |
| `ip_address` | inet | no |  |
| `user_agent` | text | no |  |
| `occurred_at` | timestamp with time zone | si | `now()` |

#### `sigm.allergies`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | si |  |
| `allergen` | character varying(200) | si |  |
| `allergen_type` | character varying(30) | si | `'MEDICATION'::character varying` |
| `reaction` | text | no |  |
| `severity` | character varying(20) | si | `'MEDIUM'::character varying` |
| `onset_date` | date | no |  |
| `status` | character varying(20) | si | `'ACTIVE'::character varying` |
| `recorded_by` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.appointment_resources`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `appointment_id` | uuid | si |  |
| `resource_id` | uuid | si |  |
| `starts_at` | timestamp with time zone | si |  |
| `ends_at` | timestamp with time zone | si |  |
| `quantity` | integer | si | `1` |
| `created_at` | timestamp with time zone | si | `now()` |

#### `sigm.appointment_status_history`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `appointment_id` | uuid | si |  |
| `from_status` | character varying(20) | no |  |
| `to_status` | character varying(20) | si |  |
| `changed_by` | uuid | no |  |
| `reason` | text | no |  |
| `changed_at` | timestamp with time zone | si | `now()` |

#### `sigm.appointment_types`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `code` | character varying(40) | si |  |
| `name` | character varying(120) | si |  |
| `description` | text | no |  |
| `duration_minutes` | integer | si | `30` |
| `specialty_id` | uuid | no |  |
| `requires_authorization` | boolean | si | `false` |
| `color` | character varying(20) | no |  |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.appointments`

Citas medicas. Las restricciones de exclusion impiden reservas dobles por profesional y consultorio.

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `appointment_number` | character varying(50) | si | `('A'::text \|\| lpad((nextval('sigm.appointment_number_seq'::regclass))::text, 10, '0'::text))` |
| `patient_id` | uuid | si |  |
| `professional_id` | uuid | si |  |
| `specialty_id` | uuid | si |  |
| `site_id` | uuid | si |  |
| `medical_office_id` | uuid | no |  |
| `appointment_type_id` | uuid | no |  |
| `starts_at` | timestamp with time zone | si |  |
| `ends_at` | timestamp with time zone | si |  |
| `status` | character varying(20) | si | `'PENDING'::character varying` |
| `reason` | text | no |  |
| `notes` | text | no |  |
| `booked_by` | uuid | no |  |
| `confirmed_at` | timestamp with time zone | no |  |
| `cancelled_at` | timestamp with time zone | no |  |
| `cancellation_reason` | text | no |  |
| `rescheduled_from_id` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.audit_logs`

Registro append-only. La aplicacion no puede actualizar ni eliminar filas.

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | no |  |
| `user_id` | uuid | no |  |
| `action` | character varying(80) | si |  |
| `entity_type` | character varying(80) | si |  |
| `entity_id` | uuid | no |  |
| `result` | character varying(20) | si | `'SUCCESS'::character varying` |
| `reason` | text | no |  |
| `metadata` | jsonb | si | `'{}'::jsonb` |
| `ip_address` | inet | no |  |
| `user_agent` | text | no |  |
| `occurred_at` | timestamp with time zone | si | `now()` |

#### `sigm.authentication_events`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | no |  |
| `user_id` | uuid | no |  |
| `event_type` | character varying(40) | si |  |
| `success` | boolean | si |  |
| `ip_address` | inet | no |  |
| `user_agent` | text | no |  |
| `detail` | jsonb | si | `'{}'::jsonb` |
| `occurred_at` | timestamp with time zone | si | `now()` |

#### `sigm.authorizations`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | si |  |
| `encounter_id` | uuid | no |  |
| `insurer_id` | uuid | si |  |
| `contract_id` | uuid | no |  |
| `service_id` | uuid | no |  |
| `authorization_number` | character varying(80) | si |  |
| `status` | character varying(20) | si | `'REQUESTED'::character varying` |
| `requested_at` | timestamp with time zone | si | `now()` |
| `approved_at` | timestamp with time zone | no |  |
| `valid_from` | date | no |  |
| `valid_until` | date | no |  |
| `approved_amount` | numeric(14,2) | no |  |
| `notes` | text | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.availability_rules`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `schedule_id` | uuid | si |  |
| `weekday` | smallint | si |  |
| `start_time` | time without time zone | si |  |
| `end_time` | time without time zone | si |  |
| `slot_duration_minutes` | integer | si | `30` |
| `specialty_id` | uuid | no |  |
| `medical_office_id` | uuid | no |  |
| `appointment_type_id` | uuid | no |  |
| `valid_from` | date | no |  |
| `valid_to` | date | no |  |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.clinical_amendments`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `note_id` | uuid | si |  |
| `amendment_number` | integer | si |  |
| `reason` | text | si |  |
| `content` | jsonb | si |  |
| `author_user_id` | uuid | si |  |
| `signed_at` | timestamp with time zone | no |  |
| `created_at` | timestamp with time zone | si | `now()` |

#### `sigm.clinical_note_types`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `code` | character varying(40) | si |  |
| `name` | character varying(120) | si |  |
| `description` | text | no |  |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |

#### `sigm.clinical_note_versions`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `note_id` | uuid | si |  |
| `version_number` | integer | si |  |
| `content` | jsonb | si |  |
| `author_user_id` | uuid | si |  |
| `signed_at` | timestamp with time zone | no |  |
| `signature_hash` | character varying(128) | no |  |
| `created_at` | timestamp with time zone | si | `now()` |

#### `sigm.clinical_notes`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `encounter_id` | uuid | si |  |
| `patient_id` | uuid | si |  |
| `author_user_id` | uuid | si |  |
| `note_type` | character varying(40) | si |  |
| `status` | character varying(20) | si | `'DRAFT'::character varying` |
| `current_version` | integer | si | `1` |
| `signed_at` | timestamp with time zone | no |  |
| `signed_by` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.clinical_records`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | si |  |
| `status` | character varying(20) | si | `'OPEN'::character varying` |
| `opened_at` | timestamp with time zone | si | `now()` |
| `closed_at` | timestamp with time zone | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.contract_services`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `contract_id` | uuid | si |  |
| `service_id` | uuid | si |  |
| `agreed_price` | numeric(14,2) | si |  |
| `copay_percent` | numeric(5,2) | si | `0` |
| `requires_authorization` | boolean | si | `false` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.contracts`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `insurer_id` | uuid | si |  |
| `contract_number` | character varying(80) | si |  |
| `name` | character varying(200) | si |  |
| `billing_type` | character varying(30) | si | `'FEE_FOR_SERVICE'::character varying` |
| `valid_from` | date | no |  |
| `valid_to` | date | no |  |
| `status` | character varying(20) | si | `'ACTIVE'::character varying` |
| `notes` | text | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.credit_debit_notes`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `invoice_id` | uuid | si |  |
| `note_number` | character varying(60) | si |  |
| `note_type` | character varying(10) | si |  |
| `reason` | text | si |  |
| `amount` | numeric(14,2) | si |  |
| `issued_at` | timestamp with time zone | si | `now()` |
| `status` | character varying(20) | si | `'DRAFT'::character varying` |
| `cufe` | character varying(120) | no |  |
| `created_by` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.departments`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `site_id` | uuid | no |  |
| `code` | character varying(40) | si |  |
| `name` | character varying(150) | si |  |
| `description` | text | no |  |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.diagnoses`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `encounter_id` | uuid | si |  |
| `patient_id` | uuid | si |  |
| `diagnosis_catalog_id` | uuid | si |  |
| `diagnosis_type` | character varying(20) | si | `'PRIMARY'::character varying` |
| `certainty` | character varying(20) | si | `'SUSPECTED'::character varying` |
| `onset_date` | date | no |  |
| `resolution_date` | date | no |  |
| `notes` | text | no |  |
| `recorded_by` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.diagnosis_catalog`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `code` | character varying(20) | si |  |
| `description` | text | si |  |
| `chapter` | character varying(150) | no |  |
| `chapter_code` | character varying(10) | no |  |
| `group_code` | character varying(20) | no |  |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.diagnostic_reports`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | si |  |
| `encounter_id` | uuid | no |  |
| `report_type` | character varying(30) | si | `'LABORATORY'::character varying` |
| `title` | character varying(250) | si |  |
| `content` | text | no |  |
| `document_id` | uuid | no |  |
| `status` | character varying(20) | si | `'DRAFT'::character varying` |
| `reported_by` | uuid | no |  |
| `reported_at` | timestamp with time zone | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.document_categories`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | no |  |
| `code` | character varying(40) | si |  |
| `name` | character varying(120) | si |  |
| `description` | text | no |  |
| `is_system` | boolean | si | `false` |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.document_permissions`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `document_id` | uuid | si |  |
| `user_id` | uuid | no |  |
| `role_id` | uuid | no |  |
| `permission_level` | character varying(20) | si | `'READ'::character varying` |
| `granted_by` | uuid | no |  |
| `granted_at` | timestamp with time zone | si | `now()` |
| `expires_at` | timestamp with time zone | no |  |

#### `sigm.document_signatures`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `document_id` | uuid | si |  |
| `signer_user_id` | uuid | si |  |
| `signature_type` | character varying(20) | si | `'ELECTRONIC'::character varying` |
| `signature_hash` | character varying(128) | si |  |
| `certificate_serial` | character varying(120) | no |  |
| `signed_at` | timestamp with time zone | si | `now()` |
| `metadata` | jsonb | si | `'{}'::jsonb` |

#### `sigm.document_types`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `code` | character varying(10) | si |  |
| `name` | character varying(80) | si |  |
| `is_active` | boolean | si | `true` |

#### `sigm.document_versions`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `document_id` | uuid | si |  |
| `version_number` | integer | si |  |
| `storage_key` | text | si |  |
| `mime_type` | character varying(120) | si |  |
| `size_bytes` | bigint | si |  |
| `checksum_sha256` | character varying(64) | no |  |
| `uploaded_by` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |

#### `sigm.documents`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | no |  |
| `encounter_id` | uuid | no |  |
| `category_id` | uuid | no |  |
| `title` | character varying(250) | si |  |
| `description` | text | no |  |
| `storage_key` | text | si |  |
| `mime_type` | character varying(120) | si |  |
| `size_bytes` | bigint | si |  |
| `checksum_sha256` | character varying(64) | no |  |
| `version` | integer | si | `1` |
| `status` | character varying(20) | si | `'ACTIVE'::character varying` |
| `is_public_to_patient` | boolean | si | `false` |
| `uploaded_by` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.encounters`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | si |  |
| `professional_id` | uuid | si |  |
| `specialty_id` | uuid | si |  |
| `site_id` | uuid | si |  |
| `medical_office_id` | uuid | no |  |
| `appointment_id` | uuid | no |  |
| `encounter_type` | character varying(30) | si | `'CONSULTATION'::character varying` |
| `encounter_date` | timestamp with time zone | si | `now()` |
| `started_at` | timestamp with time zone | no |  |
| `ended_at` | timestamp with time zone | no |  |
| `chief_complaint` | text | no |  |
| `status` | character varying(20) | si | `'IN_PROGRESS'::character varying` |
| `created_by` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.family_history`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | si |  |
| `relative_relationship` | character varying(80) | si |  |
| `condition_description` | text | si |  |
| `diagnosis_catalog_id` | uuid | no |  |
| `age_at_onset` | integer | no |  |
| `status` | character varying(20) | si | `'ACTIVE'::character varying` |
| `recorded_by` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.fhir_mappings`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | no |  |
| `resource_type` | character varying(60) | si |  |
| `local_table` | character varying(80) | si |  |
| `local_field` | character varying(80) | no |  |
| `fhir_path` | character varying(200) | si |  |
| `mapping` | jsonb | si | `'{}'::jsonb` |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.insurers`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `code` | character varying(30) | si |  |
| `name` | character varying(200) | si |  |
| `nit` | character varying(30) | no |  |
| `insurer_type` | character varying(30) | si | `'EPS'::character varying` |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.interoperability_events`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `event_type` | character varying(80) | si |  |
| `entity_type` | character varying(80) | si |  |
| `entity_id` | uuid | no |  |
| `status` | character varying(20) | si | `'PENDING'::character varying` |
| `detail` | jsonb | si | `'{}'::jsonb` |
| `occurred_at` | timestamp with time zone | si | `now()` |

#### `sigm.interoperability_messages`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `message_type` | character varying(60) | si |  |
| `direction` | character varying(10) | si |  |
| `fhir_resource_type` | character varying(60) | no |  |
| `resource_id` | uuid | no |  |
| `external_id` | character varying(120) | no |  |
| `payload` | jsonb | si | `'{}'::jsonb` |
| `status` | character varying(20) | si | `'PENDING'::character varying` |
| `sent_at` | timestamp with time zone | no |  |
| `response_payload` | jsonb | si | `'{}'::jsonb` |
| `error_message` | text | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.invoice_items`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `invoice_id` | uuid | si |  |
| `service_id` | uuid | no |  |
| `description` | character varying(250) | si |  |
| `quantity` | numeric(12,2) | si | `1` |
| `unit_price` | numeric(14,2) | si |  |
| `discount_amount` | numeric(14,2) | si | `0` |
| `tax_rate` | numeric(5,2) | si | `0` |
| `tax_amount` | numeric(14,2) | si | `0` |
| `line_total` | numeric(14,2) | no | `round((((quantity * unit_price) - discount_amount) + tax_amount), 2)` |
| `encounter_id` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |

#### `sigm.invoices`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `invoice_number` | character varying(60) | si |  |
| `patient_id` | uuid | si |  |
| `insurer_id` | uuid | no |  |
| `contract_id` | uuid | no |  |
| `encounter_id` | uuid | no |  |
| `issue_date` | date | si | `CURRENT_DATE` |
| `due_date` | date | no |  |
| `currency` | character(3) | si | `'COP'::bpchar` |
| `subtotal` | numeric(14,2) | si | `0` |
| `discount_total` | numeric(14,2) | si | `0` |
| `tax_total` | numeric(14,2) | si | `0` |
| `total` | numeric(14,2) | si | `0` |
| `status` | character varying(20) | si | `'DRAFT'::character varying` |
| `electronic_status` | character varying(20) | si | `'PENDING'::character varying` |
| `cufe` | character varying(120) | no |  |
| `xml_storage_key` | text | no |  |
| `dian_response` | jsonb | si | `'{}'::jsonb` |
| `created_by` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.laboratory_order_items`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `laboratory_order_id` | uuid | si |  |
| `test_id` | uuid | si |  |
| `status` | character varying(20) | si | `'REQUESTED'::character varying` |
| `notes` | text | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.laboratory_orders`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `encounter_id` | uuid | si |  |
| `patient_id` | uuid | si |  |
| `order_number` | character varying(50) | si | `('L'::text \|\| lpad((nextval('sigm.lab_order_number_seq'::regclass))::text, 10, '0'::text))` |
| `ordered_by` | uuid | no |  |
| `status` | character varying(20) | si | `'REQUESTED'::character varying` |
| `priority` | character varying(20) | si | `'ROUTINE'::character varying` |
| `ordered_at` | timestamp with time zone | si | `now()` |
| `notes` | text | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.laboratory_results`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `order_item_id` | uuid | si |  |
| `result_value` | text | si |  |
| `unit` | character varying(40) | no |  |
| `reference_range` | character varying(150) | no |  |
| `result_flag` | character varying(20) | si | `'NORMAL'::character varying` |
| `resulted_at` | timestamp with time zone | si | `now()` |
| `validated_by` | uuid | no |  |
| `validated_at` | timestamp with time zone | no |  |
| `created_at` | timestamp with time zone | si | `now()` |

#### `sigm.laboratory_tests`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `code` | character varying(30) | si |  |
| `name` | character varying(250) | si |  |
| `sample_type` | character varying(80) | no |  |
| `unit` | character varying(40) | no |  |
| `reference_range` | character varying(150) | no |  |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.medical_history`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | si |  |
| `encounter_id` | uuid | no |  |
| `history_type` | character varying(30) | si | `'PERSONAL'::character varying` |
| `category` | character varying(80) | no |  |
| `description` | text | si |  |
| `onset_date` | date | no |  |
| `resolution_date` | date | no |  |
| `status` | character varying(20) | si | `'ACTIVE'::character varying` |
| `recorded_by` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.medical_offices`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `site_id` | uuid | si |  |
| `code` | character varying(40) | si |  |
| `name` | character varying(120) | si |  |
| `floor` | character varying(40) | no |  |
| `capacity` | integer | no |  |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.medical_orders`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `encounter_id` | uuid | si |  |
| `patient_id` | uuid | si |  |
| `order_type` | character varying(30) | si |  |
| `description` | text | si |  |
| `priority` | character varying(20) | si | `'ROUTINE'::character varying` |
| `status` | character varying(20) | si | `'REQUESTED'::character varying` |
| `ordered_by` | uuid | no |  |
| `ordered_at` | timestamp with time zone | si | `now()` |
| `notes` | text | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.medication_catalog`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `code` | character varying(30) | si |  |
| `name` | character varying(250) | si |  |
| `presentation` | character varying(150) | no |  |
| `concentration` | character varying(80) | no |  |
| `pharmaceutical_form` | character varying(80) | no |  |
| `route` | character varying(80) | no |  |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.organizations`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `legal_name` | character varying(200) | si |  |
| `commercial_name` | character varying(200) | si |  |
| `tax_id` | character varying(30) | no |  |
| `organization_type` | character varying(20) | si | `'CLINIC'::character varying` |
| `is_primary` | boolean | si | `false` |
| `status` | character varying(20) | si | `'ACTIVE'::character varying` |
| `address_line` | text | no |  |
| `city` | character varying(120) | no |  |
| `department` | character varying(120) | no |  |
| `country` | character varying(80) | si | `'Colombia'::character varying` |
| `phone` | character varying(40) | no |  |
| `email` | citext | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.patient_addresses`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | si |  |
| `address_type` | character varying(30) | si | `'HOME'::character varying` |
| `line1` | character varying(200) | si |  |
| `line2` | character varying(200) | no |  |
| `city` | character varying(120) | no |  |
| `department` | character varying(120) | no |  |
| `country` | character varying(80) | si | `'Colombia'::character varying` |
| `postal_code` | character varying(20) | no |  |
| `is_primary` | boolean | si | `false` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.patient_alerts`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | si |  |
| `alert_type` | character varying(30) | si | `'MEDICAL'::character varying` |
| `severity` | character varying(20) | si | `'MEDIUM'::character varying` |
| `description` | text | si |  |
| `is_active` | boolean | si | `true` |
| `created_by` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.patient_consents`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | si |  |
| `consent_type` | character varying(60) | si |  |
| `consent_version` | character varying(20) | si | `'1'::character varying` |
| `granted` | boolean | si |  |
| `granted_at` | timestamp with time zone | no |  |
| `revoked_at` | timestamp with time zone | no |  |
| `document_id` | uuid | no |  |
| `recorded_by` | uuid | no |  |
| `notes` | text | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.patient_contacts`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | si |  |
| `contact_type` | character varying(30) | si | `'FAMILY'::character varying` |
| `name` | character varying(200) | si |  |
| `relationship` | character varying(80) | no |  |
| `phone` | character varying(40) | no |  |
| `email` | citext | no |  |
| `is_primary` | boolean | si | `false` |
| `is_emergency` | boolean | si | `false` |
| `notes` | text | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.patient_identifiers`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | si |  |
| `identifier_type` | character varying(40) | si |  |
| `identifier_value` | character varying(80) | si |  |
| `issuing_authority` | character varying(120) | no |  |
| `is_primary` | boolean | si | `false` |
| `valid_from` | date | no |  |
| `valid_to` | date | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.patient_insurances`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | si |  |
| `insurer_id` | uuid | si |  |
| `affiliate_number` | character varying(60) | no |  |
| `plan_name` | character varying(120) | no |  |
| `is_primary` | boolean | si | `false` |
| `valid_from` | date | no |  |
| `valid_to` | date | no |  |
| `status` | character varying(20) | si | `'ACTIVE'::character varying` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.patient_medications`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | si |  |
| `medication_catalog_id` | uuid | no |  |
| `medication_name` | character varying(250) | si |  |
| `dose` | character varying(80) | no |  |
| `frequency` | character varying(80) | no |  |
| `route` | character varying(80) | no |  |
| `start_date` | date | no |  |
| `end_date` | date | no |  |
| `status` | character varying(20) | si | `'ACTIVE'::character varying` |
| `notes` | text | no |  |
| `recorded_by` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.patient_merge_log`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `source_patient_id` | uuid | si |  |
| `target_patient_id` | uuid | si |  |
| `merged_by` | uuid | no |  |
| `merged_at` | timestamp with time zone | si | `now()` |
| `reason` | text | no |  |
| `metadata` | jsonb | si | `'{}'::jsonb` |

#### `sigm.patient_relatives`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | si |  |
| `relative_patient_id` | uuid | no |  |
| `name` | character varying(200) | si |  |
| `relationship` | character varying(80) | no |  |
| `document_type` | character varying(10) | no |  |
| `document_number` | character varying(40) | no |  |
| `phone` | character varying(40) | no |  |
| `is_guardian` | boolean | si | `false` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.patients`

Paciente dentro de un tenant. La identidad de un paciente no se comparte automaticamente entre organizaciones.

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_number` | character varying(50) | si | `('P'::text \|\| lpad((nextval('sigm.patient_number_seq'::regclass))::text, 8, '0'::text))` |
| `document_type` | character varying(10) | no |  |
| `document_number` | character varying(40) | no |  |
| `first_name` | character varying(100) | si |  |
| `middle_name` | character varying(100) | no |  |
| `last_name` | character varying(100) | si |  |
| `second_last_name` | character varying(100) | no |  |
| `full_name` | text | no | `btrim((((((((COALESCE(first_name, ''::character varying))::text \|\| ' '::text) \|\| (COALESCE(middle_name, ''::character varying))::text) \|\| ' '::text) \|\| (COALESCE(last_name, ''::character varying))::text) \|\| ' '::text) \|\| (COALESCE(second_last_name, ''::character varying))::text))` |
| `birth_date` | date | no |  |
| `sex_at_birth` | character varying(20) | si | `'UNKNOWN'::character varying` |
| `gender_identity` | character varying(60) | no |  |
| `marital_status` | character varying(30) | no |  |
| `occupation` | character varying(120) | no |  |
| `blood_type` | character varying(10) | no |  |
| `email` | citext | no |  |
| `phone` | character varying(40) | no |  |
| `mobile_phone` | character varying(40) | no |  |
| `address_line` | text | no |  |
| `city` | character varying(120) | no |  |
| `department` | character varying(120) | no |  |
| `country` | character varying(80) | si | `'Colombia'::character varying` |
| `notes` | text | no |  |
| `is_active` | boolean | si | `true` |
| `deceased_at` | timestamp with time zone | no |  |
| `created_by` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.payments`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `invoice_id` | uuid | si |  |
| `payment_date` | timestamp with time zone | si | `now()` |
| `amount` | numeric(14,2) | si |  |
| `payment_method` | character varying(40) | si | `'CASH'::character varying` |
| `reference` | character varying(120) | no |  |
| `received_by` | uuid | no |  |
| `notes` | text | no |  |
| `created_at` | timestamp with time zone | si | `now()` |

#### `sigm.permissions`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `code` | character varying(100) | si |  |
| `module` | character varying(60) | si |  |
| `name` | character varying(150) | si |  |
| `description` | text | no |  |
| `created_at` | timestamp with time zone | si | `now()` |

#### `sigm.personal_data_requests`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | si |  |
| `request_type` | character varying(30) | si |  |
| `description` | text | si |  |
| `status` | character varying(20) | si | `'RECEIVED'::character varying` |
| `requested_at` | timestamp with time zone | si | `now()` |
| `resolved_at` | timestamp with time zone | no |  |
| `resolution` | text | no |  |
| `handled_by` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.plans`

Catalogo global de planes comerciales. Los precios son parametricos y no representan una decision comercial definitiva.

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `code` | character varying(40) | si |  |
| `name` | character varying(120) | si |  |
| `product_type` | character varying(20) | si | `'BOTH'::character varying` |
| `description` | text | no |  |
| `price` | numeric(14,2) | si | `0` |
| `currency` | character(3) | si | `'COP'::bpchar` |
| `billing_period` | character varying(20) | si | `'MONTHLY'::character varying` |
| `max_users` | integer | no |  |
| `max_sites` | integer | no |  |
| `storage_limit_mb` | integer | no |  |
| `features` | jsonb | si | `'{}'::jsonb` |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.prescription_items`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `prescription_id` | uuid | si |  |
| `medication_catalog_id` | uuid | no |  |
| `medication_name` | character varying(250) | si |  |
| `dose` | character varying(80) | si |  |
| `frequency` | character varying(80) | si |  |
| `route` | character varying(80) | no |  |
| `duration_days` | integer | no |  |
| `quantity` | character varying(40) | no |  |
| `instructions` | text | no |  |
| `created_at` | timestamp with time zone | si | `now()` |

#### `sigm.prescriptions`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `encounter_id` | uuid | si |  |
| `patient_id` | uuid | si |  |
| `prescription_number` | character varying(50) | si | `('R'::text \|\| lpad((nextval('sigm.prescription_number_seq'::regclass))::text, 10, '0'::text))` |
| `status` | character varying(20) | si | `'ISSUED'::character varying` |
| `issued_at` | timestamp with time zone | si | `now()` |
| `issued_by` | uuid | no |  |
| `notes` | text | no |  |
| `document_id` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.price_list_items`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `price_list_id` | uuid | si |  |
| `service_id` | uuid | si |  |
| `price` | numeric(14,2) | si |  |
| `discount_percent` | numeric(5,2) | si | `0` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.price_lists`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `name` | character varying(150) | si |  |
| `insurer_id` | uuid | no |  |
| `contract_id` | uuid | no |  |
| `valid_from` | date | no |  |
| `valid_to` | date | no |  |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.procedure_catalog`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `code` | character varying(30) | si |  |
| `name` | text | si |  |
| `chapter` | character varying(150) | no |  |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.procedure_orders`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `encounter_id` | uuid | si |  |
| `patient_id` | uuid | si |  |
| `procedure_catalog_id` | uuid | si |  |
| `quantity` | integer | si | `1` |
| `status` | character varying(20) | si | `'REQUESTED'::character varying` |
| `ordered_by` | uuid | no |  |
| `performed_at` | timestamp with time zone | no |  |
| `notes` | text | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.professional_sites`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `professional_id` | uuid | si |  |
| `site_id` | uuid | si |  |
| `is_primary` | boolean | si | `false` |
| `valid_from` | date | no |  |
| `valid_to` | date | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.professional_specialties`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `professional_id` | uuid | si |  |
| `specialty_id` | uuid | si |  |
| `site_id` | uuid | no |  |
| `professional_license` | character varying(80) | no |  |
| `verification_status` | character varying(20) | si | `'PENDING'::character varying` |
| `verified_at` | timestamp with time zone | no |  |
| `verified_by` | uuid | no |  |
| `valid_from` | date | no |  |
| `valid_to` | date | no |  |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.professionals`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `user_id` | uuid | si |  |
| `organization_id` | uuid | no |  |
| `primary_site_id` | uuid | no |  |
| `professional_type` | character varying(40) | si | `'PHYSICIAN'::character varying` |
| `professional_license` | character varying(80) | no |  |
| `display_name` | character varying(200) | si |  |
| `email` | citext | no |  |
| `phone` | character varying(40) | no |  |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.rda_documents`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `encounter_id` | uuid | si |  |
| `patient_id` | uuid | si |  |
| `rda_uuid` | character varying(120) | si |  |
| `status` | character varying(20) | si | `'DRAFT'::character varying` |
| `generated_at` | timestamp with time zone | no |  |
| `transmitted_at` | timestamp with time zone | no |  |
| `payload` | jsonb | si | `'{}'::jsonb` |
| `document_id` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.reminders`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `appointment_id` | uuid | si |  |
| `channel` | character varying(20) | si |  |
| `scheduled_at` | timestamp with time zone | si |  |
| `sent_at` | timestamp with time zone | no |  |
| `status` | character varying(20) | si | `'PENDING'::character varying` |
| `recipient` | character varying(200) | no |  |
| `payload` | jsonb | si | `'{}'::jsonb` |
| `error_message` | text | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.resources`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `site_id` | uuid | no |  |
| `code` | character varying(40) | si |  |
| `name` | character varying(150) | si |  |
| `resource_type` | character varying(30) | si | `'EQUIPMENT'::character varying` |
| `description` | text | no |  |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.rips_report_details`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `rips_report_id` | uuid | si |  |
| `invoice_id` | uuid | no |  |
| `patient_id` | uuid | si |  |
| `service_id` | uuid | no |  |
| `payload` | jsonb | si | `'{}'::jsonb` |
| `validation_status` | character varying(20) | si | `'PENDING'::character varying` |
| `validation_errors` | jsonb | si | `'[]'::jsonb` |
| `created_at` | timestamp with time zone | si | `now()` |

#### `sigm.rips_reports`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `period_start` | date | si |  |
| `period_end` | date | si |  |
| `report_type` | character varying(40) | si | `'CONSULTATION'::character varying` |
| `status` | character varying(20) | si | `'DRAFT'::character varying` |
| `total_records` | integer | si | `0` |
| `total_amount` | numeric(14,2) | si | `0` |
| `file_key` | text | no |  |
| `generated_at` | timestamp with time zone | no |  |
| `transmitted_at` | timestamp with time zone | no |  |
| `response` | jsonb | si | `'{}'::jsonb` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.role_permissions`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `role_id` | uuid | si |  |
| `permission_id` | uuid | si |  |

#### `sigm.roles`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | no |  |
| `code` | character varying(60) | si |  |
| `name` | character varying(120) | si |  |
| `description` | text | no |  |
| `is_system` | boolean | si | `false` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.schedule_exceptions`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `schedule_id` | uuid | no |  |
| `professional_id` | uuid | si |  |
| `exception_date` | date | si |  |
| `start_time` | time without time zone | no |  |
| `end_time` | time without time zone | no |  |
| `is_all_day` | boolean | si | `false` |
| `exception_type` | character varying(20) | si | `'BLOCK'::character varying` |
| `reason` | character varying(250) | no |  |
| `created_by` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.schedules`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `professional_id` | uuid | si |  |
| `site_id` | uuid | si |  |
| `medical_office_id` | uuid | no |  |
| `name` | character varying(120) | si |  |
| `timezone` | character varying(60) | si | `'America/Bogota'::character varying` |
| `valid_from` | date | no |  |
| `valid_to` | date | no |  |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.security_incidents`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | no |  |
| `incident_type` | character varying(80) | si |  |
| `severity` | character varying(20) | si | `'MEDIUM'::character varying` |
| `description` | text | si |  |
| `status` | character varying(20) | si | `'OPEN'::character varying` |
| `detected_at` | timestamp with time zone | si | `now()` |
| `resolved_at` | timestamp with time zone | no |  |
| `reported_to` | character varying(200) | no |  |
| `metadata` | jsonb | si | `'{}'::jsonb` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.service_catalog`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `code` | character varying(40) | si |  |
| `cups_code` | character varying(30) | no |  |
| `name` | character varying(250) | si |  |
| `description` | text | no |  |
| `category` | character varying(80) | no |  |
| `unit` | character varying(40) | no | `'UNIDAD'::character varying` |
| `base_price` | numeric(14,2) | si | `0` |
| `tax_rate` | numeric(5,2) | si | `0` |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.sites`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `organization_id` | uuid | si |  |
| `code` | character varying(40) | si |  |
| `name` | character varying(150) | si |  |
| `address_line` | text | no |  |
| `city` | character varying(120) | no |  |
| `department` | character varying(120) | no |  |
| `country` | character varying(80) | si | `'Colombia'::character varying` |
| `phone` | character varying(40) | no |  |
| `email` | citext | no |  |
| `timezone` | character varying(60) | si | `'America/Bogota'::character varying` |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.specialties`

Catalogo maestro de especialidades. Cada tenant decide cuales habilita.

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `code` | character varying(40) | si |  |
| `name` | character varying(150) | si |  |
| `description` | text | no |  |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.specialty_form_fields`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | no |  |
| `form_id` | uuid | si |  |
| `field_key` | character varying(80) | si |  |
| `label` | character varying(200) | si |  |
| `field_type` | character varying(40) | si | `'TEXT'::character varying` |
| `required` | boolean | si | `false` |
| `options` | jsonb | si | `'[]'::jsonb` |
| `config` | jsonb | si | `'{}'::jsonb` |
| `sort_order` | integer | si | `0` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.specialty_forms`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | no |  |
| `specialty_id` | uuid | si |  |
| `name` | character varying(150) | si |  |
| `version` | integer | si | `1` |
| `description` | text | no |  |
| `schema` | jsonb | si | `'{}'::jsonb` |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.subscription_payments`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `subscription_id` | uuid | si |  |
| `amount` | numeric(14,2) | si |  |
| `currency` | character(3) | si | `'COP'::bpchar` |
| `period_start` | date | no |  |
| `period_end` | date | no |  |
| `status` | character varying(20) | si | `'PENDING'::character varying` |
| `paid_at` | timestamp with time zone | no |  |
| `reference` | character varying(120) | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.tenant_branding`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `logo_key` | text | no |  |
| `favicon_key` | text | no |  |
| `primary_color` | character varying(20) | no |  |
| `secondary_color` | character varying(20) | no |  |
| `accent_color` | character varying(20) | no |  |
| `email_from_name` | character varying(120) | no |  |
| `email_from_address` | citext | no |  |
| `custom_css` | text | no |  |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.tenant_domains`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `domain` | citext | si |  |
| `is_primary` | boolean | si | `false` |
| `verified_at` | timestamp with time zone | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.tenant_features`

Permite habilitar modulos y limites por cliente sin duplicar el codigo base.

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `feature_code` | character varying(80) | si |  |
| `enabled` | boolean | si | `true` |
| `limit_value` | integer | no |  |
| `config` | jsonb | si | `'{}'::jsonb` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.tenant_settings`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `setting_key` | character varying(120) | si |  |
| `value` | jsonb | si |  |
| `updated_by` | uuid | no |  |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.tenant_specialties`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `specialty_id` | uuid | si |  |
| `custom_name` | character varying(150) | no |  |
| `is_active` | boolean | si | `true` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.tenant_subscriptions`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `plan_id` | uuid | si |  |
| `status` | character varying(20) | si | `'TRIAL'::character varying` |
| `started_at` | timestamp with time zone | si | `now()` |
| `current_period_start` | timestamp with time zone | no |  |
| `current_period_end` | timestamp with time zone | no |  |
| `trial_ends_at` | timestamp with time zone | no |  |
| `cancelled_at` | timestamp with time zone | no |  |
| `external_reference` | character varying(120) | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.tenants`

Cuenta comercial SaaS. Es la frontera de aislamiento logico de todos los datos clinicos y administrativos.

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_code` | character varying(40) | si |  |
| `legal_name` | character varying(200) | si |  |
| `commercial_name` | character varying(200) | si |  |
| `tax_id` | character varying(30) | no |  |
| `product_type` | character varying(20) | si |  |
| `status` | character varying(20) | si | `'ACTIVE'::character varying` |
| `timezone` | character varying(60) | si | `'America/Bogota'::character varying` |
| `locale` | character varying(10) | si | `'es-CO'::character varying` |
| `currency` | character(3) | si | `'COP'::bpchar` |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.transmission_logs`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `message_id` | uuid | no |  |
| `destination` | character varying(250) | si |  |
| `http_status` | integer | no |  |
| `request_payload` | jsonb | si | `'{}'::jsonb` |
| `response_payload` | jsonb | si | `'{}'::jsonb` |
| `attempted_at` | timestamp with time zone | si | `now()` |
| `success` | boolean | si |  |
| `error_message` | text | no |  |

#### `sigm.user_roles`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `user_id` | uuid | si |  |
| `role_id` | uuid | si |  |
| `granted_by` | uuid | no |  |
| `granted_at` | timestamp with time zone | si | `now()` |
| `expires_at` | timestamp with time zone | no |  |

#### `sigm.user_sessions`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `user_id` | uuid | si |  |
| `refresh_token_hash` | text | si |  |
| `ip_address` | inet | no |  |
| `user_agent` | text | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `last_seen_at` | timestamp with time zone | no |  |
| `expires_at` | timestamp with time zone | si |  |
| `revoked_at` | timestamp with time zone | no |  |
| `revoked_reason` | character varying(120) | no |  |

#### `sigm.user_tenants`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `user_id` | uuid | si |  |
| `status` | character varying(20) | si | `'ACTIVE'::character varying` |
| `default_site_id` | uuid | no |  |
| `invited_by` | uuid | no |  |
| `joined_at` | timestamp with time zone | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.users`

Identidad global del usuario. Los datos clinicos y de negocio siempre se relacionan con un tenant.

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `email` | citext | si |  |
| `password_hash` | text | no |  |
| `full_name` | character varying(200) | si |  |
| `phone` | character varying(40) | no |  |
| `is_active` | boolean | si | `true` |
| `email_verified_at` | timestamp with time zone | no |  |
| `mfa_enabled` | boolean | si | `false` |
| `mfa_secret_encrypted` | text | no |  |
| `failed_login_attempts` | integer | si | `0` |
| `locked_until` | timestamp with time zone | no |  |
| `last_login_at` | timestamp with time zone | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |

#### `sigm.vital_signs`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `encounter_id` | uuid | si |  |
| `patient_id` | uuid | si |  |
| `measured_at` | timestamp with time zone | si | `now()` |
| `temperature_c` | numeric(4,1) | no |  |
| `heart_rate` | integer | no |  |
| `respiratory_rate` | integer | no |  |
| `systolic_bp` | integer | no |  |
| `diastolic_bp` | integer | no |  |
| `oxygen_saturation` | numeric(5,2) | no |  |
| `weight_kg` | numeric(6,2) | no |  |
| `height_cm` | numeric(5,1) | no |  |
| `bmi` | numeric(5,2) | no |  |

#### `sigm.CASE`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `` |  |  |  |

#### `sigm.    WHEN ((weight_kg IS NOT NULL) AND (height_cm IS NOT NULL) AND (height_cm > (0)::numeric)) THEN round((weight_kg / ((height_cm / 100.0) * (height_cm / 100.0))), 2)`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `` |  |  |  |

#### `sigm.    ELSE NULL::numeric`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `` |  |  |  |

#### `sigm.END`

14

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `` |  |  |  |

#### `sigm.vital_signs`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `glucose_mg_dl` | numeric(7,2) | no |  |
| `pain_scale` | smallint | no |  |
| `notes` | text | no |  |
| `recorded_by` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |

#### `sigm.waiting_list`

| Columna | Tipo | Obligatorio | Default |
| --- | --- | --- | --- |
| `id` | uuid | si | `gen_random_uuid()` |
| `tenant_id` | uuid | si | `sigm.current_tenant_id()` |
| `patient_id` | uuid | si |  |
| `specialty_id` | uuid | si |  |
| `professional_id` | uuid | no |  |
| `site_id` | uuid | no |  |
| `priority` | smallint | si | `3` |
| `desired_from` | date | no |  |
| `desired_to` | date | no |  |
| `status` | character varying(20) | si | `'WAITING'::character varying` |
| `notes` | text | no |  |
| `created_by` | uuid | no |  |
| `created_at` | timestamp with time zone | si | `now()` |
| `updated_at` | timestamp with time zone | si | `now()` |
## Vistas

### `sigm.v_accounts_receivable`

Cartera por factura. Usa `security_invoker = true`, por lo que hereda las politicas RLS de `sigm.invoices` y `sigm.payments`.

| Columna | Tipo | Origen |
| --- | --- | --- |
| `tenant_id` | uuid | `invoices.tenant_id` |
| `invoice_id` | uuid | `invoices.id` |
| `invoice_number` | varchar(60) | `invoices.invoice_number` |
| `patient_id` | uuid | `invoices.patient_id` |
| `insurer_id` | uuid | `invoices.insurer_id` |
| `issue_date` | date | `invoices.issue_date` |
| `due_date` | date | `invoices.due_date` |
| `total` | numeric(14,2) | `invoices.total` |
| `paid_amount` | numeric | Suma de `payments.amount` |
| `balance` | numeric | `total - paid_amount` |
| `receivable_status` | text | `VOID`, `PAID`, `OVERDUE` o `PENDING` |

## Funciones del esquema

| Funcion | Proposito |
| --- | --- |
| `sigm.current_tenant_id()` | Lee `app.tenant_id` de la transaccion |
| `sigm.current_user_id()` | Lee `app.user_id` de la transaccion |
| `sigm.set_updated_at()` | Trigger de `updated_at` |
| `sigm.prevent_mutation()` | Bloquea `UPDATE`/`DELETE` en evidencia |
| `sigm.fn_audit_trigger()` | Auditoria generica `SECURITY DEFINER` |
| `sigm.fn_validate_role_tenant()` | Valida alcance de roles |
| `sigm.fn_validate_role_permission()` | Evita permisos de plataforma en roles de tenant |
| `sigm.fn_validate_document_category()` | Valida categoria documental global o del tenant |
| `sigm.fn_validate_form_field_tenant()` | Valida campos del formulario |
| `sigm.fn_appointment_status_history()` | Historial de estados de cita |
| `sigm.fn_clinical_note_version_after_insert()` | Mantiene `current_version` |
| `sigm.fn_clinical_note_sign_guard()` | Exige version firmada antes de firmar |
| `sigm.fn_prevent_signed_note_delete()` | Impide borrar notas firmadas |
| `sigm.recalculate_invoice_totals()` | Recalcula totales de factura |
| `sigm.fn_invoice_items_totals()` | Trigger de totales por item |
| `sigm.enable_tenant_rls()` | Helper de instalacion de RLS |
| `sigm.enable_global_tenant_rls()` | Helper de RLS global/tenant |