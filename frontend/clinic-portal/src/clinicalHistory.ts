/** Construcción de la línea de tiempo clínica a partir del snapshot oficial de historia. */

export type TimelineKind =
  | 'encounter' | 'note' | 'diagnosis' | 'vital' | 'allergy'
  | 'history' | 'prescription' | 'lab';

export type TimelineEvent = {
  id: string;
  kind: TimelineKind;
  /** ISO para ordenar (acepta también fechas tipo DateOnly). */
  date: string;
  label: string;
  title: string;
  detail: string;
  meta?: string;
  status?: string;
  tone: string;
};

const CODE_LABELS: Record<string, string> = {
  CONSULTATION: 'Consulta', EVOLUTION: 'Evolución', ADMISSION: 'Ingreso', DISCHARGE: 'Egreso',
  PROCEDURE: 'Procedimiento', EMERGENCY: 'Urgencias', TELEMEDICINE: 'Telemedicina',
  NURSING: 'Enfermería', INTERCONSULTATION: 'Interconsulta', OTHER: 'Otra nota',
  PRIMARY: 'Principal', SECONDARY: 'Secundaria', DIFFERENTIAL: 'Diferencial',
  CONFIRMED: 'Confirmado', SUSPECTED: 'Sospechado', RULED_OUT: 'Descartado',
  MEDICATION: 'Medicamento', FOOD: 'Alimento', ENVIRONMENTAL: 'Ambiental', LATEX: 'Látex',
  PERSONAL: 'Personal', SURGICAL: 'Quirúrgico', TOXICOLOGICAL: 'Toxicológico',
  GYNECOLOGICAL: 'Ginecológico', PSYCHIATRIC: 'Psiquiátrico', HOSPITALIZATION: 'Hospitalización',
  TRAUMATIC: 'Traumático', SIGNED: 'Firmada', DRAFT: 'Borrador', AMENDED: 'Modificada', VOID: 'Anulada',
};

export function humanize(value?: string | null): string {
  if (!value) return '—';
  const key = String(value).trim();
  if (CODE_LABELS[key]) return CODE_LABELS[key];
  return key.replace(/_/g, ' ').toLowerCase().replace(/\b\w/g, (c) => c.toUpperCase());
}

export const TIMELINE_FILTERS: { id: TimelineKind | 'all'; label: string }[] = [
  { id: 'all', label: 'Todas' },
  { id: 'encounter', label: 'Atenciones' },
  { id: 'note', label: 'Notas' },
  { id: 'diagnosis', label: 'Diagnósticos' },
  { id: 'vital', label: 'Signos vitales' },
  { id: 'allergy', label: 'Alergias' },
  { id: 'history', label: 'Antecedentes' },
  { id: 'prescription', label: 'Fórmulas' },
  { id: 'lab', label: 'Laboratorio' },
];

export const KIND_LABELS: Record<TimelineKind, string> = {
  encounter: 'Atención', note: 'Nota clínica', diagnosis: 'Diagnóstico', vital: 'Signos vitales',
  allergy: 'Alergia', history: 'Antecedente', prescription: 'Fórmula médica', lab: 'Laboratorio',
};

function asDate(value: any): string | null {
  if (!value) return null;
  if (value instanceof Date) return value.toISOString();
  const raw = String(value);
  const parsed = new Date(raw);
  if (!Number.isNaN(parsed.getTime())) return parsed.toISOString();
  if (/^\d{4}-\d{2}-\d{2}$/.test(raw)) return `${raw}T00:00:00`;
  return null;
}

function clip(value: unknown, max = 260): string {
  const text = String(value ?? '').trim();
  return text.length > max ? `${text.slice(0, max)}…` : text;
}

function join(...parts: unknown[]): string {
  return parts.filter((p) => p !== null && p !== undefined && String(p).trim() !== '').map((p) => String(p).trim()).join(' · ');
}

/** Convierte el snapshot de historia clínica en una línea de tiempo cronológica (más reciente primero). */
export function buildClinicalTimeline(snapshot: any): TimelineEvent[] {
  const events: TimelineEvent[] = [];
  if (!snapshot) return events;

  (snapshot.encounters ?? []).forEach((e: any) => {
    const encounterDate = asDate(e.encounterDate) ?? '1900-01-01T00:00:00';
    events.push({
      id: `enc-${e.id ?? Math.random()}`,
      kind: 'encounter',
      date: encounterDate,
      label: 'Atención',
      title: humanize(e.encounterType),
      detail: join(e.chiefComplaint ? `Motivo: ${e.chiefComplaint}` : '', e.professionalName, e.specialtyName) || 'Sin detalle',
      status: e.status,
      meta: e.status ? humanize(e.status) : undefined,
      tone: 'blue',
    });

    (e.notes ?? []).forEach((n: any) => {
      const date = asDate(n.signedAt) ?? asDate(n.createdAt) ?? encounterDate;
      events.push({
        id: `note-${n.id ?? Math.random()}`,
        kind: 'note',
        date,
        label: n.status === 'SIGNED' ? 'Nota firmada' : n.status === 'AMENDED' ? 'Nota modificada' : 'Nota en borrador',
        title: humanize(n.noteType),
        detail: clip(n.content, 320) || '(sin contenido)',
        status: n.status,
        meta: join(`v${n.currentVersion ?? 1}`, n.signedAt ? `firmada ${formatTimelineDate(n.signedAt)}` : ''),
        tone: n.status === 'SIGNED' ? 'green' : n.status === 'AMENDED' ? 'violet' : 'amber',
      });
      (n.amendments ?? []).forEach((a: any) => events.push({
        id: `amd-${n.id ?? Math.random()}-${a.number ?? Math.random()}`,
        kind: 'note',
        date,
        label: 'Adenda',
        title: `Adenda ${a.number ?? ''} · ${a.reason ?? 'sin motivo'}`,
        detail: clip(a.content, 320),
        tone: 'violet',
      }));
    });
  });

  (snapshot.diagnoses ?? []).forEach((x: any) => events.push({
    id: `dx-${x.id ?? Math.random()}`, kind: 'diagnosis', date: asDate(x.createdAt) ?? '1900-01-01T00:00:00',
    label: 'Diagnóstico', title: join(x.code, x.description),
    detail: join(x.diagnosisType ? humanize(x.diagnosisType) : '', x.certainty ? humanize(x.certainty) : '') || 'Sin detalle',
    status: x.certainty, tone: 'violet',
  }));

  (snapshot.vitals ?? []).forEach((v: any) => events.push({
    id: `vit-${v.id ?? Math.random()}`, kind: 'vital', date: asDate(v.measuredAt) ?? '1900-01-01T00:00:00',
    label: 'Signos vitales', title: 'Registro de signos vitales',
    detail: join(
      v.temperatureC != null ? `Temp ${v.temperatureC} °C` : '',
      v.heartRate != null ? `FC ${v.heartRate}` : '',
      v.respiratoryRate != null ? `FR ${v.respiratoryRate}` : '',
      v.systolicBp != null || v.diastolicBp != null ? `PA ${v.systolicBp ?? '—'}/${v.diastolicBp ?? '—'}` : '',
      v.oxygenSaturation != null ? `SpO₂ ${v.oxygenSaturation}%` : '',
      v.weightKg != null ? `Peso ${v.weightKg} kg` : '',
      v.heightCm != null ? `Talla ${v.heightCm} cm` : '',
    ) || 'Sin valores',
    tone: 'amber',
  }));

  (snapshot.allergies ?? []).forEach((a: any) => events.push({
    id: `alg-${a.id ?? Math.random()}`, kind: 'allergy', date: asDate(a.createdAt) ?? asDate(a.onsetDate) ?? '1900-01-01T00:00:00',
    label: 'Alergia', title: a.allergen ?? 'Alérgeno',
    detail: join(a.allergenType ? humanize(a.allergenType) : '', a.severity ? `Severidad: ${humanize(a.severity)}` : '', a.reaction) || 'Sin detalle',
    status: a.status, tone: 'red',
  }));

  (snapshot.history ?? []).forEach((h: any) => events.push({
    id: `his-${h.id ?? Math.random()}`, kind: 'history', date: asDate(h.onsetDate) ?? asDate(h.createdAt) ?? '1900-01-01T00:00:00',
    label: 'Antecedente', title: humanize(h.historyType),
    detail: clip(h.description, 320) || 'Sin detalle',
    status: h.status, tone: 'slate',
  }));

  (snapshot.prescriptions ?? []).forEach((p: any) => {
    const date = asDate(p.issuedAt) ?? '1900-01-01T00:00:00';
    const meds = (p.items ?? []).map((i: any) => join(i.medicationName, i.dose, i.frequency, i.route)).filter(Boolean);
    events.push({
      id: `rx-${p.id ?? Math.random()}`, kind: 'prescription', date,
      label: 'Fórmula médica', title: p.number ? `Fórmula ${p.number}` : 'Fórmula médica',
      detail: meds.length ? clip(meds.join(' · '), 320) : 'Sin medicamentos',
      status: p.status, tone: 'green',
    });
  });

  (snapshot.labOrders ?? []).forEach((o: any) => {
    const date = asDate(o.orderedAt) ?? '1900-01-01T00:00:00';
    const tests = (o.tests ?? []).map((t: any) => join(t.name, t.result ? `Resultado: ${t.result}` : '', t.unit)).filter(Boolean);
    events.push({
      id: `lab-${o.id ?? Math.random()}`, kind: 'lab', date,
      label: 'Laboratorio', title: o.orderNumber ? `Orden ${o.orderNumber}` : 'Orden de laboratorio',
      detail: tests.length ? clip(tests.join(' · '), 320) : 'Sin resultados',
      status: o.status, tone: 'violet',
    });
  });

  return events.sort((a, b) => b.date.localeCompare(a.date));
}

export function formatTimelineDate(value: any, withTime = true): string {
  const iso = asDate(value);
  if (!iso) return 'Sin fecha';
  if (iso.startsWith('1900-01-01')) return 'Sin fecha';
  const date = new Date(iso);
  const dateOnly = /^\d{4}-\d{2}-\d{2}T00:00:00/.test(iso) && !String(value).includes('T');
  try {
    return dateOnly
      ? date.toLocaleDateString('es-CO', { year: 'numeric', month: 'short', day: 'numeric' })
      : date.toLocaleString('es-CO', withTime
        ? { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }
        : { year: 'numeric', month: 'short', day: 'numeric' });
  } catch { return iso.slice(0, 10); }
}

export function groupTimelineByMonth(events: TimelineEvent[]): { key: string; label: string; events: TimelineEvent[] }[] {
  const groups = new Map<string, TimelineEvent[]>();
  events.forEach((e) => {
    const key = e.date.startsWith('1900-01-01') ? 'sin-fecha' : e.date.slice(0, 7);
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key)!.push(e);
  });
  return Array.from(groups, ([key, items]) => {
    let label = 'Sin fecha';
    if (key !== 'sin-fecha') {
      const [year, month] = key.split('-').map(Number);
      label = new Date(Date.UTC(year, month - 1, 1)).toLocaleDateString('es-CO', { month: 'long', year: 'numeric' });
      label = label.charAt(0).toUpperCase() + label.slice(1);
    }
    return { key, label, events: items };
  });
}
