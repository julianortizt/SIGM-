-- ============================================================================
-- SIGM CLOUD - 008 - Agendamiento, disponibilidad y recursos
-- ============================================================================
BEGIN;

CREATE SEQUENCE IF NOT EXISTS sigm.appointment_number_seq;

-- ----------------------------------------------------------------------------
-- Tipos de cita / servicio
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.appointment_types (
    id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id              uuid NOT NULL DEFAULT sigm.current_tenant_id()
                           REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    code                   varchar(40) NOT NULL,
    name                   varchar(120) NOT NULL,
    description            text,
    duration_minutes       integer NOT NULL DEFAULT 30 CHECK (duration_minutes > 0),
    specialty_id           uuid,
    requires_authorization boolean NOT NULL DEFAULT false,
    color                  varchar(20),
    is_active              boolean NOT NULL DEFAULT true,
    created_at             timestamptz NOT NULL DEFAULT now(),
    updated_at             timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, code),
    FOREIGN KEY (tenant_id, specialty_id)
        REFERENCES sigm.tenant_specialties (tenant_id, specialty_id) ON DELETE SET NULL
);

-- ----------------------------------------------------------------------------
-- Agenda base de un profesional en una sede
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.schedules (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id        uuid NOT NULL DEFAULT sigm.current_tenant_id()
                     REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    professional_id  uuid NOT NULL,
    site_id          uuid NOT NULL,
    medical_office_id uuid,
    name             varchar(120) NOT NULL,
    timezone         varchar(60) NOT NULL DEFAULT 'America/Bogota',
    valid_from       date,
    valid_to         date,
    is_active        boolean NOT NULL DEFAULT true,
    created_at       timestamptz NOT NULL DEFAULT now(),
    updated_at       timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, professional_id)
        REFERENCES sigm.professionals (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, site_id)
        REFERENCES sigm.sites (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, medical_office_id)
        REFERENCES sigm.medical_offices (tenant_id, id) ON DELETE SET NULL,
    CHECK (valid_to IS NULL OR valid_from IS NULL OR valid_to >= valid_from)
);

-- ----------------------------------------------------------------------------
-- Reglas de disponibilidad recurrentes
-- weekday: 1 = lunes ... 7 = domingo
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.availability_rules (
    id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id              uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    schedule_id            uuid NOT NULL,
    weekday                smallint NOT NULL CHECK (weekday BETWEEN 1 AND 7),
    start_time             time NOT NULL,
    end_time               time NOT NULL,
    slot_duration_minutes  integer NOT NULL DEFAULT 30 CHECK (slot_duration_minutes > 0),
    specialty_id           uuid,
    medical_office_id      uuid,
    appointment_type_id    uuid,
    valid_from             date,
    valid_to               date,
    is_active              boolean NOT NULL DEFAULT true,
    created_at             timestamptz NOT NULL DEFAULT now(),
    updated_at             timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, schedule_id)
        REFERENCES sigm.schedules (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, specialty_id)
        REFERENCES sigm.tenant_specialties (tenant_id, specialty_id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, medical_office_id)
        REFERENCES sigm.medical_offices (tenant_id, id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, appointment_type_id)
        REFERENCES sigm.appointment_types (tenant_id, id) ON DELETE SET NULL,
    CHECK (end_time > start_time),
    CHECK (valid_to IS NULL OR valid_from IS NULL OR valid_to >= valid_from)
);

CREATE INDEX IF NOT EXISTS ix_availability_rules_schedule
    ON sigm.availability_rules (tenant_id, schedule_id, weekday);

-- ----------------------------------------------------------------------------
-- Excepciones: bloqueos, vacaciones, horarios extra
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.schedule_exceptions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    schedule_id     uuid,
    professional_id uuid NOT NULL,
    exception_date  date NOT NULL,
    start_time      time,
    end_time        time,
    is_all_day      boolean NOT NULL DEFAULT false,
    exception_type  varchar(20) NOT NULL DEFAULT 'BLOCK'
                    CHECK (exception_type IN ('BLOCK', 'EXTRA')),
    reason          varchar(250),
    created_by      uuid,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, schedule_id)
        REFERENCES sigm.schedules (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, professional_id)
        REFERENCES sigm.professionals (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, created_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL,
    CHECK (is_all_day OR (start_time IS NOT NULL AND end_time IS NOT NULL AND end_time > start_time))
);

CREATE INDEX IF NOT EXISTS ix_schedule_exceptions_date
    ON sigm.schedule_exceptions (tenant_id, professional_id, exception_date);

-- ----------------------------------------------------------------------------
-- Citas
-- Estados: PENDING, CONFIRMED, RESCHEDULED, CANCELLED, WAITING,
--          IN_PROGRESS, ATTENDED, NO_SHOW
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.appointments (
    id                   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id            uuid NOT NULL DEFAULT sigm.current_tenant_id()
                         REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    appointment_number   varchar(50) NOT NULL DEFAULT ('A' || lpad(nextval('sigm.appointment_number_seq')::text, 10, '0')),
    patient_id           uuid NOT NULL,
    professional_id      uuid NOT NULL,
    specialty_id         uuid NOT NULL,
    site_id              uuid NOT NULL,
    medical_office_id    uuid,
    appointment_type_id  uuid,
    starts_at            timestamptz NOT NULL,
    ends_at              timestamptz NOT NULL,
    status               varchar(20) NOT NULL DEFAULT 'PENDING'
                         CHECK (status IN (
                             'PENDING', 'CONFIRMED', 'RESCHEDULED', 'CANCELLED',
                             'WAITING', 'IN_PROGRESS', 'ATTENDED', 'NO_SHOW'
                         )),
    reason               text,
    notes                text,
    booked_by            uuid,
    confirmed_at         timestamptz,
    cancelled_at         timestamptz,
    cancellation_reason  text,
    rescheduled_from_id  uuid,
    created_at           timestamptz NOT NULL DEFAULT now(),
    updated_at           timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, appointment_number),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, professional_id, specialty_id)
        REFERENCES sigm.professional_specialties (tenant_id, professional_id, specialty_id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, site_id)
        REFERENCES sigm.sites (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, medical_office_id)
        REFERENCES sigm.medical_offices (tenant_id, id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, appointment_type_id)
        REFERENCES sigm.appointment_types (tenant_id, id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, booked_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, rescheduled_from_id)
        REFERENCES sigm.appointments (tenant_id, id) ON DELETE SET NULL,
    CHECK (ends_at > starts_at),
    CHECK (cancelled_at IS NULL OR status = 'CANCELLED')
);

COMMENT ON TABLE sigm.appointments IS
  'Citas medicas. Las restricciones de exclusion impiden reservas dobles por profesional y consultorio.';

CREATE INDEX IF NOT EXISTS ix_appointments_tenant_date
    ON sigm.appointments (tenant_id, starts_at);

CREATE INDEX IF NOT EXISTS ix_appointments_patient_date
    ON sigm.appointments (tenant_id, patient_id, starts_at DESC);

CREATE INDEX IF NOT EXISTS ix_appointments_professional_date
    ON sigm.appointments (tenant_id, professional_id, starts_at);

CREATE INDEX IF NOT EXISTS ix_appointments_status
    ON sigm.appointments (tenant_id, status);

-- Evita dos citas activas del mismo profesional en el mismo rango de tiempo.
ALTER TABLE sigm.appointments
    DROP CONSTRAINT IF EXISTS ex_appointments_professional_overlap;
ALTER TABLE sigm.appointments
    ADD CONSTRAINT ex_appointments_professional_overlap
    EXCLUDE USING gist (
        tenant_id WITH =,
        professional_id WITH =,
        tstzrange(starts_at, ends_at, '[)') WITH &&
    )
    WHERE (status IN ('PENDING', 'CONFIRMED', 'RESCHEDULED', 'WAITING', 'IN_PROGRESS'));

-- Evita dos citas activas en el mismo consultorio.
ALTER TABLE sigm.appointments
    DROP CONSTRAINT IF EXISTS ex_appointments_office_overlap;
ALTER TABLE sigm.appointments
    ADD CONSTRAINT ex_appointments_office_overlap
    EXCLUDE USING gist (
        tenant_id WITH =,
        medical_office_id WITH =,
        tstzrange(starts_at, ends_at, '[)') WITH &&
    )
    WHERE (medical_office_id IS NOT NULL
           AND status IN ('PENDING', 'CONFIRMED', 'RESCHEDULED', 'WAITING', 'IN_PROGRESS'));

-- ----------------------------------------------------------------------------
-- Historial de estados de la cita
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.appointment_status_history (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id      uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    appointment_id uuid NOT NULL,
    from_status    varchar(20),
    to_status      varchar(20) NOT NULL,
    changed_by     uuid,
    reason         text,
    changed_at     timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, appointment_id)
        REFERENCES sigm.appointments (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, changed_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_appointment_status_history
    ON sigm.appointment_status_history (tenant_id, appointment_id, changed_at DESC);

-- ----------------------------------------------------------------------------
-- Reserva de recursos/equipos para una cita
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.appointment_resources (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id      uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    appointment_id uuid NOT NULL,
    resource_id    uuid NOT NULL,
    starts_at      timestamptz NOT NULL,
    ends_at        timestamptz NOT NULL,
    quantity       integer NOT NULL DEFAULT 1 CHECK (quantity > 0),
    created_at     timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, appointment_id)
        REFERENCES sigm.appointments (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, resource_id)
        REFERENCES sigm.resources (tenant_id, id) ON DELETE RESTRICT,
    CHECK (ends_at > starts_at)
);

ALTER TABLE sigm.appointment_resources
    DROP CONSTRAINT IF EXISTS ex_appointment_resources_overlap;
ALTER TABLE sigm.appointment_resources
    ADD CONSTRAINT ex_appointment_resources_overlap
    EXCLUDE USING gist (
        tenant_id WITH =,
        resource_id WITH =,
        tstzrange(starts_at, ends_at, '[)') WITH &&
    );

-- ----------------------------------------------------------------------------
-- Lista de espera
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.waiting_list (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    patient_id      uuid NOT NULL,
    specialty_id    uuid NOT NULL,
    professional_id uuid,
    site_id         uuid,
    priority        smallint NOT NULL DEFAULT 3 CHECK (priority BETWEEN 1 AND 5),
    desired_from    date,
    desired_to      date,
    status          varchar(20) NOT NULL DEFAULT 'WAITING'
                    CHECK (status IN ('WAITING', 'CONTACTED', 'SCHEDULED', 'CANCELLED', 'EXPIRED')),
    notes           text,
    created_by      uuid,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, specialty_id)
        REFERENCES sigm.tenant_specialties (tenant_id, specialty_id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, professional_id)
        REFERENCES sigm.professionals (tenant_id, id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, site_id)
        REFERENCES sigm.sites (tenant_id, id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, created_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL,
    CHECK (desired_to IS NULL OR desired_from IS NULL OR desired_to >= desired_from)
);

CREATE INDEX IF NOT EXISTS ix_waiting_list_status
    ON sigm.waiting_list (tenant_id, status, priority);

-- ----------------------------------------------------------------------------
-- Recordatorios y notificaciones de citas
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.reminders (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id      uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    appointment_id uuid NOT NULL,
    channel        varchar(20) NOT NULL
                   CHECK (channel IN ('EMAIL', 'SMS', 'WHATSAPP', 'PUSH', 'PHONE')),
    scheduled_at   timestamptz NOT NULL,
    sent_at        timestamptz,
    status         varchar(20) NOT NULL DEFAULT 'PENDING'
                   CHECK (status IN ('PENDING', 'SENT', 'FAILED', 'CANCELLED')),
    recipient      varchar(200),
    payload        jsonb NOT NULL DEFAULT '{}'::jsonb,
    error_message  text,
    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, appointment_id)
        REFERENCES sigm.appointments (tenant_id, id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_reminders_pending
    ON sigm.reminders (scheduled_at)
    WHERE status = 'PENDING';

COMMIT;
