-- ============================================================================
-- SIGM CLOUD - 009 - Historia clinica, encuentros, notas y ordenes
-- ============================================================================
BEGIN;

CREATE SEQUENCE IF NOT EXISTS sigm.prescription_number_seq;

-- ----------------------------------------------------------------------------
-- Expediente longitudinal del paciente
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.clinical_records (
    id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id  uuid NOT NULL DEFAULT sigm.current_tenant_id()
               REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    patient_id uuid NOT NULL,
    status     varchar(20) NOT NULL DEFAULT 'OPEN'
               CHECK (status IN ('OPEN', 'CLOSED', 'ARCHIVED')),
    opened_at  timestamptz NOT NULL DEFAULT now(),
    closed_at  timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, patient_id),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE
);

-- ----------------------------------------------------------------------------
-- Encuentros / atenciones
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.encounters (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id        uuid NOT NULL DEFAULT sigm.current_tenant_id()
                     REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    patient_id       uuid NOT NULL,
    professional_id  uuid NOT NULL,
    specialty_id     uuid NOT NULL,
    site_id          uuid NOT NULL,
    medical_office_id uuid,
    appointment_id   uuid,
    encounter_type   varchar(30) NOT NULL DEFAULT 'CONSULTATION'
                     CHECK (encounter_type IN (
                         'CONSULTATION', 'FOLLOW_UP', 'EMERGENCY', 'PROCEDURE',
                         'TELEMEDICINE', 'HOME_VISIT', 'HOSPITALIZATION', 'OTHER'
                     )),
    encounter_date   timestamptz NOT NULL DEFAULT now(),
    started_at       timestamptz,
    ended_at         timestamptz,
    chief_complaint  text,
    status           varchar(20) NOT NULL DEFAULT 'IN_PROGRESS'
                     CHECK (status IN ('IN_PROGRESS', 'COMPLETED', 'CANCELLED', 'NO_SHOW')),
    created_by       uuid,
    created_at       timestamptz NOT NULL DEFAULT now(),
    updated_at       timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, id, patient_id),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, professional_id, specialty_id)
        REFERENCES sigm.professional_specialties (tenant_id, professional_id, specialty_id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, site_id)
        REFERENCES sigm.sites (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, medical_office_id)
        REFERENCES sigm.medical_offices (tenant_id, id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, appointment_id)
        REFERENCES sigm.appointments (tenant_id, id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, created_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL,
    CHECK (ended_at IS NULL OR started_at IS NULL OR ended_at >= started_at)
);

CREATE INDEX IF NOT EXISTS ix_encounters_patient_date
    ON sigm.encounters (tenant_id, patient_id, encounter_date DESC);

CREATE INDEX IF NOT EXISTS ix_encounters_professional_date
    ON sigm.encounters (tenant_id, professional_id, encounter_date DESC);

CREATE INDEX IF NOT EXISTS ix_encounters_status
    ON sigm.encounters (tenant_id, status);

-- ----------------------------------------------------------------------------
-- Notas clinicas (cabecera)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.clinical_notes (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id         uuid NOT NULL DEFAULT sigm.current_tenant_id()
                      REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    encounter_id      uuid NOT NULL,
    patient_id        uuid NOT NULL,
    author_user_id    uuid NOT NULL,
    note_type         varchar(40) NOT NULL REFERENCES sigm.clinical_note_types(code) ON DELETE RESTRICT,
    status            varchar(20) NOT NULL DEFAULT 'DRAFT'
                      CHECK (status IN ('DRAFT', 'SIGNED', 'AMENDED', 'VOID')),
    current_version   integer NOT NULL DEFAULT 1 CHECK (current_version > 0),
    signed_at         timestamptz,
    signed_by         uuid,
    created_at        timestamptz NOT NULL DEFAULT now(),
    updated_at        timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, encounter_id, patient_id)
        REFERENCES sigm.encounters (tenant_id, id, patient_id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, author_user_id)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, signed_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE RESTRICT,
    CHECK (status <> 'SIGNED' OR signed_at IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS ix_clinical_notes_encounter
    ON sigm.clinical_notes (tenant_id, encounter_id, created_at);

CREATE INDEX IF NOT EXISTS ix_clinical_notes_patient
    ON sigm.clinical_notes (tenant_id, patient_id, created_at DESC);

-- ----------------------------------------------------------------------------
-- Versiones inmutables del contenido de la nota
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.clinical_note_versions (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id      uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    note_id        uuid NOT NULL,
    version_number integer NOT NULL CHECK (version_number > 0),
    content        jsonb NOT NULL,
    author_user_id uuid NOT NULL,
    signed_at      timestamptz,
    signature_hash varchar(128),
    created_at     timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, note_id, version_number),
    FOREIGN KEY (tenant_id, note_id)
        REFERENCES sigm.clinical_notes (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, author_user_id)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_clinical_note_versions_note
    ON sigm.clinical_note_versions (tenant_id, note_id, version_number DESC);

-- ----------------------------------------------------------------------------
-- Adendas: corrigen o complementan sin sobrescribir la nota firmada
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.clinical_amendments (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id        uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    note_id          uuid NOT NULL,
    amendment_number integer NOT NULL CHECK (amendment_number > 0),
    reason           text NOT NULL,
    content          jsonb NOT NULL,
    author_user_id   uuid NOT NULL,
    signed_at        timestamptz,
    created_at       timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, note_id, amendment_number),
    FOREIGN KEY (tenant_id, note_id)
        REFERENCES sigm.clinical_notes (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, author_user_id)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE RESTRICT
);

-- ----------------------------------------------------------------------------
-- Antecedentes personales
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.medical_history (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id      uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    patient_id     uuid NOT NULL,
    encounter_id   uuid,
    history_type   varchar(30) NOT NULL DEFAULT 'PERSONAL'
                   CHECK (history_type IN (
                       'PERSONAL', 'SURGICAL', 'TOXICOLOGICAL', 'GYNECOLOGICAL',
                       'PSYCHIATRIC', 'HOSPITALIZATION', 'TRAUMATIC', 'OTHER'
                   )),
    category       varchar(80),
    description    text NOT NULL,
    onset_date     date,
    resolution_date date,
    status         varchar(20) NOT NULL DEFAULT 'ACTIVE'
                   CHECK (status IN ('ACTIVE', 'RESOLVED', 'UNKNOWN')),
    recorded_by    uuid,
    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, encounter_id, patient_id)
        REFERENCES sigm.encounters (tenant_id, id, patient_id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, recorded_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL,
    CHECK (resolution_date IS NULL OR onset_date IS NULL OR resolution_date >= onset_date)
);

-- ----------------------------------------------------------------------------
-- Antecedentes familiares
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.family_history (
    id                   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id            uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    patient_id           uuid NOT NULL,
    relative_relationship varchar(80) NOT NULL,
    condition_description text NOT NULL,
    diagnosis_catalog_id uuid REFERENCES sigm.diagnosis_catalog(id) ON DELETE SET NULL,
    age_at_onset         integer CHECK (age_at_onset IS NULL OR age_at_onset >= 0),
    status               varchar(20) NOT NULL DEFAULT 'ACTIVE'
                         CHECK (status IN ('ACTIVE', 'RESOLVED', 'UNKNOWN')),
    recorded_by          uuid,
    created_at           timestamptz NOT NULL DEFAULT now(),
    updated_at           timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, recorded_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL
);

-- ----------------------------------------------------------------------------
-- Alergias
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.allergies (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    patient_id    uuid NOT NULL,
    allergen      varchar(200) NOT NULL,
    allergen_type varchar(30) NOT NULL DEFAULT 'MEDICATION'
                  CHECK (allergen_type IN ('MEDICATION', 'FOOD', 'ENVIRONMENTAL', 'LATEX', 'OTHER')),
    reaction      text,
    severity      varchar(20) NOT NULL DEFAULT 'MEDIUM'
                  CHECK (severity IN ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL')),
    onset_date    date,
    status        varchar(20) NOT NULL DEFAULT 'ACTIVE'
                  CHECK (status IN ('ACTIVE', 'RESOLVED', 'UNKNOWN')),
    recorded_by   uuid,
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, recorded_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_allergies_patient
    ON sigm.allergies (tenant_id, patient_id, status);

-- ----------------------------------------------------------------------------
-- Signos vitales
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.vital_signs (
    id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id          uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    encounter_id       uuid NOT NULL,
    patient_id         uuid NOT NULL,
    measured_at        timestamptz NOT NULL DEFAULT now(),
    temperature_c      numeric(4,1) CHECK (temperature_c IS NULL OR temperature_c BETWEEN 25 AND 45),
    heart_rate         integer CHECK (heart_rate IS NULL OR heart_rate BETWEEN 0 AND 400),
    respiratory_rate   integer CHECK (respiratory_rate IS NULL OR respiratory_rate BETWEEN 0 AND 200),
    systolic_bp        integer CHECK (systolic_bp IS NULL OR systolic_bp BETWEEN 0 AND 400),
    diastolic_bp       integer CHECK (diastolic_bp IS NULL OR diastolic_bp BETWEEN 0 AND 300),
    oxygen_saturation  numeric(5,2) CHECK (oxygen_saturation IS NULL OR oxygen_saturation BETWEEN 0 AND 100),
    weight_kg          numeric(6,2) CHECK (weight_kg IS NULL OR weight_kg > 0),
    height_cm          numeric(5,1) CHECK (height_cm IS NULL OR height_cm > 0),
    bmi                numeric(5,2) GENERATED ALWAYS AS (
                           CASE
                               WHEN weight_kg IS NOT NULL AND height_cm IS NOT NULL AND height_cm > 0
                               THEN round(weight_kg / ((height_cm / 100.0) * (height_cm / 100.0)), 2)
                           END
                       ) STORED,
    glucose_mg_dl      numeric(7,2),
    pain_scale         smallint CHECK (pain_scale IS NULL OR pain_scale BETWEEN 0 AND 10),
    notes              text,
    recorded_by        uuid,
    created_at         timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, encounter_id, patient_id)
        REFERENCES sigm.encounters (tenant_id, id, patient_id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, recorded_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_vital_signs_encounter
    ON sigm.vital_signs (tenant_id, encounter_id, measured_at);

-- ----------------------------------------------------------------------------
-- Diagnosticos
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.diagnoses (
    id                   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id            uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    encounter_id         uuid NOT NULL,
    patient_id           uuid NOT NULL,
    diagnosis_catalog_id uuid NOT NULL REFERENCES sigm.diagnosis_catalog(id) ON DELETE RESTRICT,
    diagnosis_type       varchar(20) NOT NULL DEFAULT 'PRIMARY'
                         CHECK (diagnosis_type IN ('PRIMARY', 'SECONDARY', 'DIFFERENTIAL')),
    certainty            varchar(20) NOT NULL DEFAULT 'SUSPECTED'
                         CHECK (certainty IN ('CONFIRMED', 'SUSPECTED', 'RULED_OUT')),
    onset_date           date,
    resolution_date      date,
    notes                text,
    recorded_by          uuid,
    created_at           timestamptz NOT NULL DEFAULT now(),
    updated_at           timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, encounter_id, patient_id)
        REFERENCES sigm.encounters (tenant_id, id, patient_id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, recorded_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL,
    CHECK (resolution_date IS NULL OR onset_date IS NULL OR resolution_date >= onset_date)
);

CREATE INDEX IF NOT EXISTS ix_diagnoses_patient
    ON sigm.diagnoses (tenant_id, patient_id, created_at DESC);

-- ----------------------------------------------------------------------------
-- Ordenes medicas generales
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.medical_orders (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id    uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    encounter_id uuid NOT NULL,
    patient_id   uuid NOT NULL,
    order_type   varchar(30) NOT NULL
                 CHECK (order_type IN ('LABORATORY', 'IMAGING', 'PROCEDURE', 'REFERRAL', 'MEDICATION', 'OTHER')),
    description  text NOT NULL,
    priority     varchar(20) NOT NULL DEFAULT 'ROUTINE'
                 CHECK (priority IN ('ROUTINE', 'URGENT', 'STAT')),
    status       varchar(20) NOT NULL DEFAULT 'REQUESTED'
                 CHECK (status IN ('REQUESTED', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED')),
    ordered_by   uuid,
    ordered_at   timestamptz NOT NULL DEFAULT now(),
    notes        text,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, encounter_id, patient_id)
        REFERENCES sigm.encounters (tenant_id, id, patient_id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, ordered_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL
);

-- ----------------------------------------------------------------------------
-- Ordenes de procedimientos
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.procedure_orders (
    id                   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id            uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    encounter_id         uuid NOT NULL,
    patient_id           uuid NOT NULL,
    procedure_catalog_id uuid NOT NULL REFERENCES sigm.procedure_catalog(id) ON DELETE RESTRICT,
    quantity             integer NOT NULL DEFAULT 1 CHECK (quantity > 0),
    status               varchar(20) NOT NULL DEFAULT 'REQUESTED'
                         CHECK (status IN ('REQUESTED', 'SCHEDULED', 'PERFORMED', 'CANCELLED')),
    ordered_by           uuid,
    performed_at         timestamptz,
    notes                text,
    created_at           timestamptz NOT NULL DEFAULT now(),
    updated_at           timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, encounter_id, patient_id)
        REFERENCES sigm.encounters (tenant_id, id, patient_id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, ordered_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL
);

-- ----------------------------------------------------------------------------
-- Medicamentos actuales del paciente
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.patient_medications (
    id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id             uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    patient_id            uuid NOT NULL,
    medication_catalog_id uuid REFERENCES sigm.medication_catalog(id) ON DELETE SET NULL,
    medication_name       varchar(250) NOT NULL,
    dose                  varchar(80),
    frequency             varchar(80),
    route                 varchar(80),
    start_date            date,
    end_date              date,
    status                varchar(20) NOT NULL DEFAULT 'ACTIVE'
                          CHECK (status IN ('ACTIVE', 'SUSPENDED', 'COMPLETED', 'CANCELLED')),
    notes                 text,
    recorded_by           uuid,
    created_at            timestamptz NOT NULL DEFAULT now(),
    updated_at            timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, recorded_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL,
    CHECK (end_date IS NULL OR start_date IS NULL OR end_date >= start_date)
);

-- ----------------------------------------------------------------------------
-- Formulas medicas
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.prescriptions (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           uuid NOT NULL DEFAULT sigm.current_tenant_id()
                        REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    encounter_id        uuid NOT NULL,
    patient_id          uuid NOT NULL,
    prescription_number varchar(50) NOT NULL DEFAULT ('R' || lpad(nextval('sigm.prescription_number_seq')::text, 10, '0')),
    status              varchar(20) NOT NULL DEFAULT 'ISSUED'
                        CHECK (status IN ('ISSUED', 'DISPENSED', 'CANCELLED')),
    issued_at           timestamptz NOT NULL DEFAULT now(),
    issued_by           uuid,
    notes               text,
    document_id         uuid,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, prescription_number),
    FOREIGN KEY (tenant_id, encounter_id, patient_id)
        REFERENCES sigm.encounters (tenant_id, id, patient_id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, issued_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS sigm.prescription_items (
    id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id             uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    prescription_id       uuid NOT NULL,
    medication_catalog_id uuid REFERENCES sigm.medication_catalog(id) ON DELETE SET NULL,
    medication_name       varchar(250) NOT NULL,
    dose                  varchar(80) NOT NULL,
    frequency             varchar(80) NOT NULL,
    route                 varchar(80),
    duration_days         integer CHECK (duration_days IS NULL OR duration_days > 0),
    quantity              varchar(40),
    instructions          text,
    created_at            timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, prescription_id)
        REFERENCES sigm.prescriptions (tenant_id, id) ON DELETE CASCADE
);

COMMIT;
