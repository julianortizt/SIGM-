import { useEffect, useMemo, useState } from 'react';
import { api } from './api';
import { openClinicalRecordPdf, printClinicalRecord } from './clinicalRecord';
import {
  buildClinicalTimeline, formatTimelineDate, groupTimelineByMonth, humanize,
  KIND_LABELS, TIMELINE_FILTERS, type TimelineEvent, type TimelineKind,
} from './clinicalHistory';

type Props = {
  open: boolean;
  onClose: () => void;
  patients: any[];
  initialPatientId?: string;
  onOpenClinical?: (patientId: string) => void;
};

function patientLabel(p: any) {
  return p?.fullName || [p?.firstName, p?.lastName].filter(Boolean).join(' ') || p?.patientNumber || 'Paciente';
}

export default function ClinicalHistoryDrawer({ open, onClose, patients, initialPatientId, onOpenClinical }: Props) {
  const [patientId, setPatientId] = useState('');
  const [query, setQuery] = useState('');
  const [snapshot, setSnapshot] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [filter, setFilter] = useState<TimelineKind | 'all'>('all');
  const [text, setText] = useState('');

  useEffect(() => {
    if (!open) return;
    const saved = localStorage.getItem('sigm_history_patient') || '';
    const start = initialPatientId || (patients.some((p) => p.id === saved) ? saved : '') || patients[0]?.id || '';
    setPatientId(start);
    const chosen = patients.find((p) => p.id === start);
    setQuery(chosen ? patientLabel(chosen) : '');
    setFilter('all'); setText(''); setError('');
  }, [open, initialPatientId]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (!open || !patientId) { setSnapshot(null); return; }
    let active = true;
    setLoading(true); setError('');
    api.clinicalRecord(patientId)
      .then((s) => { if (active) setSnapshot(s); })
      .catch((e) => { if (active) { setError(e?.message || 'No fue posible cargar la historia clínica.'); setSnapshot(null); } })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [open, patientId]);

  useEffect(() => {
    if (patientId) localStorage.setItem('sigm_history_patient', patientId);
  }, [patientId]);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  const events = useMemo(() => buildClinicalTimeline(snapshot), [snapshot]);
  const counts = useMemo(() => {
    const acc: Record<string, number> = { all: events.length };
    events.forEach((e) => { acc[e.kind] = (acc[e.kind] || 0) + 1; });
    return acc;
  }, [events]);
  const visible = useMemo(() => {
    const needle = text.trim().toLowerCase();
    return events.filter((e) =>
      (filter === 'all' || e.kind === filter) &&
      (!needle || `${e.label} ${e.title} ${e.detail} ${e.meta || ''}`.toLowerCase().includes(needle)));
  }, [events, filter, text]);
  const groups = useMemo(() => groupTimelineByMonth(visible), [visible]);
  const patient = snapshot?.patient || patients.find((p) => p.id === patientId) || {};
  const matches = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return patients.filter((p) => !needle || [patientLabel(p), p?.patientNumber, p?.documentNumber]
      .filter(Boolean).some((v) => String(v).toLowerCase().includes(needle))).slice(0, 8);
  }, [patients, query]);

  async function withAction(fn: () => Promise<void>) {
    setError('');
    try { await fn(); } catch (e: any) { setError(e?.message || String(e)); }
  }

  if (!open) return null;

  return <div className="ch-overlay" onMouseDown={onClose}>
    <aside className="ch-drawer" role="dialog" aria-modal="true" aria-label="Historia clínica" onMouseDown={(e) => e.stopPropagation()}>
      <div className="ch-head">
        <div>
          <div className="ch-kicker">Historia clínica · línea de tiempo</div>
          <h3>Histórico del paciente</h3>
        </div>
        <button className="btn ghost sm" onClick={onClose} title="Cerrar">✕</button>
      </div>

      <div className="ch-body">
        <div className="ch-picker">
          <label className="field"><span>Paciente</span>
            <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Buscar por nombre, historia o documento" />
          </label>
          {query.trim() !== patientLabel(patient) && matches.length > 0 && <div className="ch-options">
            {matches.map((p) => <button key={p.id} onClick={() => { setPatientId(p.id); setQuery(patientLabel(p)); }}>
              <b>{patientLabel(p)}</b><small>{p.patientNumber || p.documentNumber || ''}</small>
            </button>)}
          </div>}
        </div>

        {error && <div className="alert error" style={{ marginTop: 12 }}>{error}</div>}

        <div className="ch-patient-card">
          <div>
            <b>{patient.fullName || patientLabel(patient) || '—'}</b>
            <div className="muted">
              {patient.patientNumber ? `Historia ${patient.patientNumber} · ` : ''}
              {patient.documentType ? `${patient.documentType} ` : ''}{patient.documentNumber || '—'}
            </div>
          </div>
          <div className="ch-actions">
            <button className="btn ghost sm" disabled={!patientId || loading} onClick={() => void withAction(() => openClinicalRecordPdf(patientId))}>PDF</button>
            <button className="btn ghost sm" disabled={!patientId || loading} onClick={() => void withAction(() => printClinicalRecord(patientId))}>Imprimir</button>
            {onOpenClinical && <button className="btn subtle sm" disabled={!patientId} onClick={() => { onOpenClinical(patientId); onClose(); }}>Abrir en Clínico</button>}
          </div>
        </div>

        {(snapshot?.allergies || []).length > 0 && <div className="alert ch-alert" style={{ marginTop: 12 }}>
          <b>Alertas clínicas:</b> {(snapshot.allergies as any[]).map((a) => `${a.allergen} (${humanize(a.severity)})`).join(' · ')}
        </div>}

        <div className="ch-filter-bar">
          <div className="ch-chips">
            {TIMELINE_FILTERS.map((f) => <button key={f.id}
              className={'chip' + (filter === f.id ? ' on' : '')}
              disabled={f.id !== 'all' && !counts[f.id]}
              onClick={() => setFilter(f.id)}>
              {f.label} <small>{counts[f.id] || 0}</small>
            </button>)}
          </div>
          <input className="ch-search" value={text} onChange={(e) => setText(e.target.value)} placeholder="Filtrar el histórico…" />
        </div>

        {loading && <div className="empty" style={{ marginTop: 14 }}>Cargando historia clínica…</div>}
        {!loading && !snapshot && !error && <div className="empty" style={{ marginTop: 14 }}>Seleccione un paciente para ver su histórico clínico.</div>}
        {!loading && snapshot && visible.length === 0 && <div className="empty" style={{ marginTop: 14 }}>No hay eventos que coincidan con el filtro.</div>}

        {!loading && groups.map((g) => <div className="ch-group" key={g.key}>
          <div className="ch-month">{g.label} <span>{g.events.length}</span></div>
          <div className="timeline ch-timeline">
            {g.events.map((e: TimelineEvent) => <div className={`tl-item ${e.tone === 'blue' ? '' : `tl-${e.tone}`}`} key={e.id}>
              <div className="tl-date">{formatTimelineDate(e.date)} <span className="ch-kind">{e.label} · {KIND_LABELS[e.kind]}</span></div>
              <div className="tl-title">{e.title}{e.status && <span className="badge slate ch-badge">{humanize(e.status)}</span>}</div>
              <div className="tl-detail">{e.detail}</div>
              {e.meta && <div className="ch-meta">{e.meta}</div>}
            </div>)}
          </div>
        </div>)}

        <div className="ch-foot">
          Fuente: historia clínica oficial de SIGM Cloud. El PDF incluye hash SHA-256 para verificar su integridad.
        </div>
      </div>
    </aside>
  </div>;
}
