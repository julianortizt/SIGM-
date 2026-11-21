import { useEffect, useRef, useState } from 'react';
import type { CSSProperties } from 'react';
import { api } from '../api';

type PatientOption = { id: string; fullName?: string; lastName?: string; documentNumber?: string; patientNumber?: string };

type Props = {
  /** Nombre del campo oculto para formularios con FormData. */
  name?: string;
  label?: string;
  required?: boolean;
  allowEmpty?: boolean;
  emptyLabel?: string;
  placeholder?: string;
  style?: CSSProperties;
  /** Modo controlado: id seleccionado. */
  value?: string;
  /** Etiqueta del id controlado cuando el paciente no está en la página cargada. */
  valueLabel?: string;
  onChange?: (id: string) => void;
};

/**
 * Selector de pacientes con búsqueda asíncrona en el servidor.
 * Evita cargar toda la base de pacientes en un <select> y permite buscar por
 * nombre, documento o número de historia mientras se escribe.
 */
export default function PatientPicker({
  name,
  label = 'Paciente',
  required,
  allowEmpty,
  emptyLabel = 'Sin paciente',
  placeholder = 'Buscar por nombre o documento…',
  style,
  value,
  valueLabel,
  onChange,
}: Props) {
  const [query, setQuery] = useState('');
  const [items, setItems] = useState<PatientOption[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [picked, setPicked] = useState<PatientOption | null>(null);
  const wrapRef = useRef<HTMLDivElement | null>(null);
  const inputRef = useRef<HTMLInputElement | null>(null);

  const controlled = value !== undefined;
  const selectedId = controlled ? value : picked?.id ?? '';
  const display = controlled
    ? (picked && picked.id === value ? labelFor(picked) : valueLabel ?? (value ? 'Paciente seleccionado' : ''))
    : picked ? labelFor(picked) : '';

  // Limpia la selección cuando el formulario se reinicia.
  useEffect(() => {
    const form = inputRef.current?.form;
    if (!form) return;
    const onReset = () => {
      setPicked(null);
      setQuery('');
      if (controlled) onChange?.('');
    };
    form.addEventListener('reset', onReset);
    return () => form.removeEventListener('reset', onReset);
  }, [controlled, onChange]);

  // Cierra el desplegable al hacer clic fuera.
  useEffect(() => {
    const onDocClick = (event: MouseEvent) => {
      if (!wrapRef.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, []);

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    const timer = setTimeout(async () => {
      try {
        const result = await api.patientsPage({ page: 1, take: 20, q: query.trim() || undefined });
        if (!cancelled) setItems(result.items ?? []);
      } catch {
        if (!cancelled) setItems([]);
      } finally {
        if (!cancelled) setLoading(false);
      }
    }, 280);
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [query, open]);

  const choose = (patient: PatientOption) => {
    setPicked(patient);
    setQuery('');
    setOpen(false);
    onChange?.(patient.id);
  };

  const clear = () => {
    setPicked(null);
    setQuery('');
    onChange?.('');
  };

  return (
    <div className="field" style={style}>
      <span>{label}</span>
      {name && <input type="hidden" name={name} value={selectedId} />}
      <div ref={wrapRef} style={{ position: 'relative' }}>
        <input
          ref={inputRef}
          type="text"
          value={open ? query : display}
          placeholder={selectedId ? display || 'Paciente seleccionado' : placeholder}
          required={required && !selectedId}
          autoComplete="off"
          onFocus={() => setOpen(true)}
          onChange={(e) => {
            setQuery(e.target.value);
            setOpen(true);
            if (picked) setPicked(null);
            if (controlled && value) onChange?.('');
          }}
        />
        {selectedId && (
          <button
            type="button"
            aria-label="Quitar paciente"
            onClick={clear}
            style={{
              position: 'absolute',
              right: 8,
              top: '50%',
              transform: 'translateY(-50%)',
              border: 0,
              background: 'transparent',
              cursor: 'pointer',
              fontSize: 16,
              color: 'var(--muted)',
            }}
          >
            ×
          </button>
        )}
        {open && (
          <div
            style={{
              position: 'absolute',
              zIndex: 40,
              top: 'calc(100% + 4px)',
              left: 0,
              right: 0,
              background: '#fff',
              border: '1px solid var(--line-soft)',
              borderRadius: 10,
              boxShadow: '0 12px 28px rgba(15,23,42,.14)',
              maxHeight: 260,
              overflowY: 'auto',
            }}
          >
            {loading && <div className="muted" style={{ padding: 10, fontSize: 12 }}>Buscando…</div>}
            {!loading && items.length === 0 && (
              <div className="muted" style={{ padding: 10, fontSize: 12 }}>Sin coincidencias.</div>
            )}
            {items.map((patient) => (
              <button
                key={patient.id}
                type="button"
                onClick={() => choose(patient)}
                style={{
                  display: 'block',
                  width: '100%',
                  textAlign: 'left',
                  padding: '8px 10px',
                  border: 0,
                  borderBottom: '1px solid var(--line-soft)',
                  background: patient.id === selectedId ? 'var(--accent-soft)' : 'transparent',
                  cursor: 'pointer',
                  fontSize: 13,
                }}
              >
                <strong>{labelFor(patient)}</strong>
                <span className="muted" style={{ marginLeft: 6, fontSize: 12 }}>
                  {[patient.documentNumber, patient.patientNumber].filter(Boolean).join(' · ')}
                </span>
              </button>
            ))}
            {allowEmpty && (
              <button
                type="button"
                onClick={() => {
                  clear();
                  setOpen(false);
                }}
                style={{
                  display: 'block',
                  width: '100%',
                  textAlign: 'left',
                  padding: '8px 10px',
                  border: 0,
                  background: 'transparent',
                  cursor: 'pointer',
                  fontSize: 12.5,
                  color: 'var(--muted)',
                }}
              >
                {emptyLabel}
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

function labelFor(patient: PatientOption) {
  return patient.fullName || patient.lastName || 'Paciente';
}
