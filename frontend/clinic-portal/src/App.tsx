import { useEffect, useRef, useState } from 'react';
import './App.css';
import { API, api, clearSession, getSession, setSession } from './api';
import PatientPicker from './components/PatientPicker';
import SecurityModal from './components/SecurityModal';
import PasswordRecovery from './components/PasswordRecovery';
import { openClinicalRecordPdf, printClinicalRecord } from './clinicalRecord';

type View = 'login' | 'register' | 'dashboard';
type Tab = 'home'|'specialties'|'users'|'sites'|'offices'|'professionals'|'schedules'|'patients'|'appointments'|'waiting'|'reminders'|'encounters'|'clinical'|'lab'|'prescriptions'|'billing'|'eps'|'documents'|'interop'|'audit';

const nav: { label: string; items: { id: Tab; label: string; icon: string }[] }[] = [
  { label: 'General', items: [
    { id: 'home', label: 'Inicio', icon: 'home' },
    { id: 'specialties', label: 'Especialidades', icon: 'grid' },
  ]},
  { label: 'Organización', items: [
    { id: 'users', label: 'Usuarios', icon: 'users' },
    { id: 'sites', label: 'Sedes', icon: 'building' },
    { id: 'offices', label: 'Consultorios', icon: 'door' },
  ]},
  { label: 'Agenda', items: [
    { id: 'professionals', label: 'Profesionales', icon: 'steth' },
    { id: 'schedules', label: 'Horarios', icon: 'clock' },
    { id: 'appointments', label: 'Citas', icon: 'calendar' },
    { id: 'waiting', label: 'Lista de espera', icon: 'list' },
    { id: 'reminders', label: 'Recordatorios', icon: 'bell' },
  ]},
  { label: 'Asistencial', items: [
    { id: 'encounters', label: 'Atenciones', icon: 'activity' },
    { id: 'clinical', label: 'Clínico', icon: 'clipboard' },
    { id: 'lab', label: 'Laboratorio', icon: 'flask' },
    { id: 'prescriptions', label: 'Recetas', icon: 'pill' },
  ]},
  { label: 'Facturación', items: [
    { id: 'billing', label: 'Facturación', icon: 'invoice' },
    { id: 'eps', label: 'EPS y Pagos', icon: 'card' },
  ]},
  { label: 'Gestión', items: [
    { id: 'documents', label: 'Documentos', icon: 'folder' },
    { id: 'interop', label: 'Interoperabilidad', icon: 'share' },
    { id: 'audit', label: 'Auditoría', icon: 'shield' },
  ]},
];

const labels: Record<Tab, string> = Object.fromEntries(
  nav.flatMap(g => g.items.map(i => [i.id, i.label]))
) as Record<Tab, string>;

const icons: Record<string, string> = {
  home: 'M3 11.2 12 4l9 7.2M5.5 9.8V20h13V9.8M10 20v-5.5h4V20',
  grid: 'M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z',
  users: 'M9.5 11.5a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7zM2.8 20c.7-3.4 3.4-5.3 6.7-5.3s6 1.9 6.7 5.3M17.5 9.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5zM16.3 14.9c2.6.4 4.6 2 5.2 4.6',
  building: 'M5 21V4.5A1.5 1.5 0 0 1 6.5 3h11A1.5 1.5 0 0 1 19 4.5V21M2.5 21h19M9 7h1.5M13.5 7H15M9 10.5h1.5M13.5 10.5H15M9 14h1.5M13.5 14H15M11 21v-3.5h2.5V21',
  door: 'M6.5 21V4.8A1.8 1.8 0 0 1 8.3 3h7.4a1.8 1.8 0 0 1 1.8 1.8V21M3.5 21h17M14 12.2h.01',
  steth: 'M10 11a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7zM3 20c.7-3.3 3.4-5.1 7-5.1 1.3 0 2.5.3 3.5.8M18.5 12.5v3.7a2.6 2.6 0 0 1-2.6 2.6h-.7M18.5 7.5v3',
  clock: 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM12 7.5V12l3.2 2',
  calendar: 'M4 6.5A1.5 1.5 0 0 1 5.5 5h13A1.5 1.5 0 0 1 20 6.5V19a1.5 1.5 0 0 1-1.5 1.5h-13A1.5 1.5 0 0 1 4 19zM4 10h16M8.5 3v4M15.5 3v4',
  list: 'M9 6.5h11.5M9 12h11.5M9 17.5h11.5M4 6.5h.01M4 12h.01M4 17.5h.01',
  bell: 'M6 9.5a6 6 0 0 1 12 0c0 4.6 1.8 5.8 1.8 5.8H4.2S6 14.1 6 9.5M10.3 19.5a2 2 0 0 0 3.4 0',
  activity: 'M3 12h3.8L9.5 6l4.5 12 2.7-6H21',
  clipboard: 'M5.5 6.5A1.5 1.5 0 0 1 7 5h10a1.5 1.5 0 0 1 1.5 1.5V19.5A1.5 1.5 0 0 1 17 21H7a1.5 1.5 0 0 1-1.5-1.5zM9 3.5h6V6H9zM9.5 11h5M9.5 14.5h5',
  flask: 'M9.5 3h5M10.5 3v5.2l-5.3 9.2A2.4 2.4 0 0 0 7.2 21h9.6a2.4 2.4 0 0 0 2-3.6l-5.3-9.2V3M8 15h8',
  pill: 'M8.8 15.2 5.4 18.6a4.3 4.3 0 0 1-6-6l7.2-7.2a4.3 4.3 0 0 1 6 6l-3.4 3.4zM9.5 14.5l5-5',
  invoice: 'M6 2.5h7.5L18 6.8V21.5H6zM13 2.8V7h4.5M9 12h6M9 15.5h6M9 8.5h1.5',
  card: 'M2.5 7A2.5 2.5 0 0 1 5 4.5h14A2.5 2.5 0 0 1 21.5 7v10a2.5 2.5 0 0 1-2.5 2.5H5A2.5 2.5 0 0 1 2.5 17zM2.5 9.8h19M6 14.8h4',
  folder: 'M3 6.8A1.8 1.8 0 0 1 4.8 5h4.4L11.5 7.5h7.7A1.8 1.8 0 0 1 21 9.3v8A1.8 1.8 0 0 1 19.2 19H4.8A1.8 1.8 0 0 1 3 17.3zM3 9.5h18',
  share: 'M6 14.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5zM17.5 7.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5zM17.5 21a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5zM8.3 10.9l7-3.4M8.3 13.6l7 3.3',
  shield: 'M12 3 5 5.6v5.5c0 4.6 3 7.8 7 9.9 4-2.1 7-5.3 7-9.9V5.6zM9.2 12l2 2 3.7-3.9',
  logout: 'M9.5 4H6A1.5 1.5 0 0 0 4.5 5.5v13A1.5 1.5 0 0 0 6 20h3.5M15.5 16l4-4-4-4M19 12H9',
  search: 'M11 18a7 7 0 1 0 0-14 7 7 0 0 0 0 14zM20.5 20.5 15.6 15.6',
  check: 'M4.5 12.5 10 18 19.5 7',
  cross: 'M10 3h4v7h7v4h-7v7h-4v-7H3v-4h7z',
  edit: 'M4 20h4l10.5-10.5a2.1 2.1 0 0 0-3-3L5 17zM14.5 6.5l3 3',
  trash: 'M4 7h16M9 7V4.5h6V7M6.5 7l.8 12.2A1.5 1.5 0 0 0 8.8 20.5h6.4a1.5 1.5 0 0 0 1.5-1.3L17.5 7M10 11v5M14 11v5',
  copy: 'M9 9V5.5A1.5 1.5 0 0 1 10.5 4h8A1.5 1.5 0 0 1 20 5.5v8a1.5 1.5 0 0 1-1.5 1.5H15M5.5 9h8A1.5 1.5 0 0 1 15 10.5v8A1.5 1.5 0 0 1 13.5 20h-8A1.5 1.5 0 0 1 4 18.5v-8A1.5 1.5 0 0 1 5.5 9z',
};

function Ico({ name, size = 17 }: { name: string; size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor"
      strokeWidth={1.7} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d={icons[name] || icons.grid} />
    </svg>
  );
}

/* ---------- helpers de presentación ---------- */
function pretty(v: string) {
  return String(v).toLowerCase().replace(/_/g, ' ').replace(/^\b\w/, c => c.toUpperCase());
}
function badgeCls(v?: string) {
  const s = String(v || '').toUpperCase();
  if (/(PAID|COMPLETED|SIGNED|ACTIVE|CONFIRMED|CLOSED|FINALIZED|ISSUED|APPROVED|DELIVERED|SENT|SUCCEEDED)/.test(s)) return 'green';
  if (/(CANCEL|REJECT|EXPIR|BLOCK|CRITICAL|SUSPEND|FAILED|DENIED|OVERDUE|STAT)/.test(s)) return 'red';
  if (/(PEND|REQUEST|IN_PROGRESS|DRAFT|SCHEDULED|WAITING|RECEIVED|PROCESSING|QUEUED|VALIDATION|OPEN|URGENT|HIGH)/.test(s)) return 'amber';
  if (/(LAB|IMAGING|PHYSICIAN|SPECIALIST|CONSULTATION|TELE)/.test(s)) return 'blue';
  return 'slate';
}
function Badge({ v }: { v?: string | null }) {
  if (v == null || v === '') return <span className="muted">—</span>;
  return <span className={`badge ${badgeCls(v)}`}>{pretty(String(v))}</span>;
}
function money(v: any) {
  const n = Number(v ?? 0);
  return new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(Number.isFinite(n) ? n : 0);
}
function dt(v: any) { return v ? new Date(v).toLocaleString('es-CO', { dateStyle: 'medium', timeStyle: 'short' }) : '—'; }
function d(v: any) { return v ? new Date(v).toLocaleDateString('es-CO', { dateStyle: 'medium' }) : '—'; }
function initials(n?: string) {
  return (n || '?').split(/\s+/).filter(Boolean).slice(0, 2).map(x => x[0]?.toUpperCase()).join('') || '?';
}
function shortId(v?: string) { return v ? String(v).slice(0, 8) + '…' : '—'; }
const wd = ['Dom', 'Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb'];
function greeting() {
  const h = new Date().getHours();
  return h < 12 ? 'Buenos días' : h < 19 ? 'Buenas tardes' : 'Buenas noches';
}
function isToday(v: any) {
  if (!v) return false;
  const d1 = new Date(v), n = new Date();
  return d1.getFullYear() === n.getFullYear() && d1.getMonth() === n.getMonth() && d1.getDate() === n.getDate();
}
function hhmm(v: any) { return v ? new Date(v).toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit' }) : '—'; }



export default function App() {
  const brand = import.meta.env.VITE_BRAND || 'SIGM Clínicas';
  const product = import.meta.env.VITE_PRODUCT || 'SIGM Cloud';
  const [view, setView] = useState<View>(getSession()?.accessToken ? 'dashboard' : 'login');
  const [tab, setTab] = useState<Tab>('home');
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  const [specialties, setSpecialties] = useState<any[]>([]); const [selected, setSelected] = useState<string[]>([]);
  const [users, setUsers] = useState<any[]>([]); const [sites, setSites] = useState<any[]>([]); const [offices, setOffices] = useState<any[]>([]);
  const [pros, setPros] = useState<any[]>([]); const [schedules, setSchedules] = useState<any[]>([]); const [rules, setRules] = useState<any[]>([]); const [exceptions, setExceptions] = useState<any[]>([]); const [apptTypes, setApptTypes] = useState<any[]>([]);
  const [patients, setPatients] = useState<any[]>([]); const [appts, setAppts] = useState<any[]>([]); const [encounters, setEncounters] = useState<any[]>([]);
  const [summary, setSummary] = useState<any>(null); const [summaryPatient, setSummaryPatient] = useState('');
  const [services, setServices] = useState<any[]>([]); const [invoices, setInvoices] = useState<any[]>([]); const [ar, setAr] = useState<any[]>([]); const [docs, setDocs] = useState<any[]>([]); const [slots, setSlots] = useState<any>(null);
  const [waiting, setWaiting] = useState<any[]>([]); const [reminders, setReminders] = useState<any[]>([]); const [labOrders, setLabOrders] = useState<any[]>([]); const [labTests, setLabTests] = useState<any[]>([]);
  const [rx, setRx] = useState<any[]>([]); const [insurers, setInsurers] = useState<any[]>([]); const [contracts, setContracts] = useState<any[]>([]); const [auths, setAuths] = useState<any[]>([]); const [payments, setPayments] = useState<any[]>([]);
  const [interopEvents, setInteropEvents] = useState<any[]>([]); const [transmissions, setTransmissions] = useState<any[]>([]); const [rda, setRda] = useState<any[]>([]); const [audit, setAudit] = useState<any[]>([]); const [access, setAccess] = useState<any[]>([]);
  const session = getSession();
  const [patientQ, setPatientQ] = useState('');
  const [patTotal, setPatTotal] = useState(0); const [patPage, setPatPage] = useState(1);
  const [patientEdit, setPatientEdit] = useState<any>(null);
  const [mfa, setMfa] = useState<{ email: string; password: string; tenant?: string } | null>(null);
  const [copied, setCopied] = useState(false);
  const patQInit = useRef(true);
  const [showSuperadmin, setShowSuperadmin] = useState(false);
  const [showSecurity, setShowSecurity] = useState(false);
  const [recovering, setRecovering] = useState(false);
  const [showSidebar, setShowSidebar] = useState(true); /* true por defecto en desktop, false en móvil inicial */
  // Cargar/guardar preferencias de dashboard desde localStorage (sincronizado después con backend)
  const [hiddenModules, setHiddenModules] = useState<string[]>(() => {
    if (typeof window !== 'undefined') {
      try { const ls = window.localStorage.getItem('sigm_dashboard_hidden'); if (ls) return JSON.parse(ls); } catch { }
    }
    return [];
  });
  useEffect(() => {
    async function sync() {
      try {
        const token = localStorage.getItem('sigm_token');
        if (!token) return;
        const h = new Headers({ 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' });
        const t = localStorage.getItem('sigm_tenant'); if (t) h.append('X-Tenant-Id', t);
        const res = await fetch(`${API}/api/v1/tenant-config/dashboard`, { method: 'GET', headers: h });
        const data = await res.json();
        const hidden = (data?.hidden ?? []).filter((x: any) => typeof x === 'string');
        setHiddenModules(hidden);
        window.localStorage.setItem('sigm_dashboard_hidden', JSON.stringify(hidden));
      } catch (e) { /* silencioso */ }
    }
    sync();
  }, []);

  async function loadData() {
    const safe = async <T,>(p: Promise<T>, fb: T) => { try { return await p; } catch { return fb; } };
    const [all, mine] = await Promise.all([safe(api.specialties(), []), safe(api.tenantSpecialties(), [])]);
    setSpecialties(all); setSelected((mine || []).map((x: any) => String(x.id)));
    setUsers(await safe(api.users(), [])); setSites(await safe(api.sites(), [])); setOffices(await safe(api.offices(), []));
    setPros(await safe(api.professionals(), [])); setSchedules(await safe(api.schedules(), [])); setRules(await safe(api.rules(), [])); setExceptions(await safe(api.exceptions(), [])); setApptTypes(await safe(api.appointmentTypes(), []));
    await loadPatientsPage(1, patientQ); setAppts(await safe(api.appointments(), [])); setEncounters(await safe(api.encounters(), []));
    setServices(await safe(api.billingServices(), [])); setInvoices(await safe(api.invoices(), [])); setAr(await safe(api.ar(), [])); setDocs(await safe(api.documents(), []));
    setWaiting(await safe(api.waitingList(), [])); setReminders(await safe(api.reminders(), [])); setLabOrders(await safe(api.labOrders(), [])); setLabTests(await safe(api.labTests(), []));
    setRx(await safe(api.prescriptions(), [])); setInsurers(await safe(api.insurers(), [])); setContracts(await safe(api.contracts(), [])); setAuths(await safe(api.authorizations(), [])); setPayments(await safe(api.payments(), []));
    setInteropEvents(await safe(api.interopEvents(), [])); setTransmissions(await safe(api.transmissions(), [])); setRda(await safe(api.rda(), [])); setAudit(await safe(api.auditLogs(), [])); setAccess(await safe(api.accessLogs(), []));
  }
  useEffect(() => { if (view === 'dashboard') loadData(); }, [view]);

  // La búsqueda de pacientes se resuelve en el servidor (con retardo para no saturar).
  useEffect(() => {
    if (view !== 'dashboard') return;
    if (patQInit.current) { patQInit.current = false; return; }
    const timer = setTimeout(() => { loadPatientsPage(1, patientQ); }, 350);
    return () => clearTimeout(timer);
  }, [patientQ, view]);

  async function loadPatientsPage(page: number, q = patientQ) {
    try {
      const r = await api.patientsPage({ page, take: 50, q: q.trim() || undefined });
      setPatTotal(r.total); setPatPage(page);
      setPatients(prev => page === 1 ? r.items : [...prev, ...r.items.filter((x: any) => !prev.some((p: any) => p.id === x.id))]);
    } catch (e: any) { setError(e.message || String(e)); }
  }

  async function openPatientEdit(id: string) {
    setBusy(true); setError('');
    try {
      const p = await api.patient(id);
      setPatientEdit({ ...p, birthDate: p.birthDate ? String(p.birthDate).slice(0, 10) : '' });
    } catch (e: any) { setError(e.message || String(e)); } finally { setBusy(false); }
  }

  async function savePatient(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault(); if (!patientEdit) return;
    const fd = new FormData(e.currentTarget);
    const body = {
      firstName: fd.get('firstName'), lastName: fd.get('lastName'), middleName: fd.get('middleName') || null, secondLastName: fd.get('secondLastName') || null,
      documentType: fd.get('documentType') || null, documentNumber: fd.get('documentNumber') || null, birthDate: fd.get('birthDate') || null,
      sexAtBirth: fd.get('sexAtBirth') || 'UNKNOWN', email: fd.get('email') || null, phone: fd.get('phone') || null, mobilePhone: fd.get('mobilePhone') || null,
      addressLine: fd.get('addressLine') || null, city: fd.get('city') || null, department: fd.get('department') || null,
    };
    setBusy(true); setError('');
    try { await api.updatePatient(patientEdit.id, body); setPatientEdit(null); await loadPatientsPage(patPage, patientQ); }
    catch (err: any) { setError(err.message || String(err)); } finally { setBusy(false); }
  }

  async function deactivatePatient(p: any) {
    const nombre = p.fullName || `${p.firstName} ${p.lastName}`;
    if (!window.confirm(`¿Desactivar a ${nombre}? La historia clínica se conserva, solo deja de aparecer como activo.`)) return;
    setBusy(true); setError('');
    try { await api.deactivatePatient(p.id); await loadPatientsPage(1, patientQ); }
    catch (err: any) { setError(err.message || String(err)); } finally { setBusy(false); }
  }

  async function run(fn: () => Promise<any>, reset?: HTMLFormElement) {
    setBusy(true); setError('');
    try { await fn(); if (reset) reset.reset(); await loadData(); } catch (e: any) { setError(e.message || String(e)); } finally { setBusy(false); }
  }

  async function openRecordPdf(patientId: string) {
    setBusy(true); setError('');
    try { await openClinicalRecordPdf(patientId); } catch (e: any) { setError(e.message || String(e)); } finally { setBusy(false); }
  }

  async function printRecord(patientId: string) {
    setBusy(true); setError('');
    try { await printClinicalRecord(patientId); } catch (e: any) { setError(e.message || String(e)); } finally { setBusy(false); }
  }

  async function onLogin(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault(); setBusy(true); setError(''); const fd = new FormData(e.currentTarget);
    const tenant = ((fd.get('tenantId') as string) || '').trim() || undefined;
    try { const res = await api.login({ email: fd.get('email'), password: fd.get('password'), tenantId: tenant });
      if (res?.requiresTenantSelection) { setError('Su usuario pertenece a varias organizaciones: escriba el código o el ID de la organización.'); return; } setSession(res); setView('dashboard'); }
    catch (err: any) {
      // El backend pide el segundo factor en el mismo endpoint: se conserva lo escrito y se pide el código.
      if (err?.status === 401 && err?.data?.mfaRequired) {
        setMfa({ email: String(fd.get('email') || ''), password: String(fd.get('password') || ''), tenant });
        setError('Verificación en dos pasos: escriba el código de su aplicación autenticadora.');
      } else setError(err.message);
    } finally { setBusy(false); }
  }
  async function onMfa(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault(); if (!mfa) return;
    setBusy(true); setError('');
    const mfaCode = String(new FormData(e.currentTarget).get('mfaCode') || '').trim();
    try {
      const res = await api.login({ email: mfa.email, password: mfa.password, tenantId: mfa.tenant, mfaCode });
      setSession(res); setMfa(null); setView('dashboard');
    } catch (err: any) { setError(err.message); } finally { setBusy(false); }
  }
  async function onRegister(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault(); setBusy(true); setError(''); const fd = new FormData(e.currentTarget);
    const ids = (fd.getAll('specialties') as string[]).filter(Boolean);
    try {
      const created = await api.registerOrg({ tenantCode: fd.get('tenantCode'), legalName: fd.get('legalName'), commercialName: fd.get('commercialName') || fd.get('legalName'), taxId: fd.get('taxId') || null, productType: 'CLINIC', adminEmail: fd.get('adminEmail'), adminFullName: fd.get('adminFullName'), adminPassword: fd.get('adminPassword'), specialtyIds: ids, planId: null });
      const login = await api.login({ email: fd.get('adminEmail'), password: fd.get('adminPassword'), tenantId: created.tenantId }); setSession(login); setView('dashboard');
    } catch (err: any) { setError(err.message); } finally { setBusy(false); }
  }

  /* ============ LOGIN / REGISTRO ============ */
  if (view !== 'dashboard') {
    return <div className="auth">
      <aside className="auth-brand">
        <div className="auth-brand-head">
          <div className="auth-brand-mark"><Ico name="cross" size={22} /></div>
          <div><div className="auth-brand-title">{brand}</div><div className="auth-brand-sub">{product}</div></div>
        </div>
        <div className="auth-hero">
          <h1>El sistema operativo<br />de su institución médica</h1>
          <p>Historia clínica, agenda médica, laboratorio, facturación y trazabilidad de accesos en una sola plataforma, con aislamiento total por organización.</p>
          <div className="auth-feats">
            <div className="auth-feat"><div className="auth-feat-ico"><Ico name="check" size={14} /></div><div><b>Historia clínica estructurada</b><span>Atenciones, signos vitales, alergias, antecedentes y diagnósticos.</span></div></div>
            <div className="auth-feat"><div className="auth-feat-ico"><Ico name="check" size={14} /></div><div><b>Agenda y disponibilidad inteligente</b><span>Reglas, excepciones, lista de espera y recordatorios multicanal.</span></div></div>
            <div className="auth-feat"><div className="auth-feat-ico"><Ico name="check" size={14} /></div><div><b>Facturación y cartera controladas</b><span>Tarifarios, facturas, pagos y cuentas por cobrar en tiempo real.</span></div></div>
            <div className="auth-feat"><div className="auth-feat-ico"><Ico name="check" size={14} /></div><div><b>Seguridad auditable</b><span>Control de acceso por roles y bitácora completa de operaciones.</span></div></div>
          </div>
        </div>
        <div className="auth-foot">© {new Date().getFullYear()} {product} · Plataforma SaaS de gestión médica</div>
      </aside>
      <main className="auth-panel">
        <div className="auth-card">
          {error && <div className="alert error"><Ico name="shield" size={16} /> {error}</div>}
          {recovering ? <PasswordRecovery onBack={() => { setRecovering(false); setError(''); }} /> : view === 'login' ? (mfa ? <>
            <h2>Verificación en dos pasos</h2>
            <p className="sub">Ingrese el código de 6 dígitos o uno de sus códigos de recuperación.</p>
            <form onSubmit={onMfa}>
              <label className="field"><span>Código de verificación</span><input name="mfaCode" autoComplete="one-time-code" placeholder="123456" autoFocus required /></label>
              <button className="btn" disabled={busy}>{busy ? 'Verificando…' : 'Verificar y entrar'}</button>
            </form>
            <p className="auth-alt"><a href="#" onClick={e => { e.preventDefault(); setMfa(null); setError(''); }}>← Usar otra cuenta</a></p>
          </> : <>
            <h2>Iniciar sesión</h2>
            <p className="sub">Acceda con sus credenciales institucionales.</p>
            <form onSubmit={onLogin}>
              <label className="field"><span>Correo electrónico</span><input name="email" type="email" placeholder="usuario@organizacion.com" required /></label>
              <label className="field"><span>Contraseña</span><input name="password" type="password" placeholder="••••••••" required /></label>
              <label className="field"><span>Organización <span className="muted" style={{ fontWeight: 400 }}>(opcional)</span></span><input name="tenantId" placeholder="Código o ID, p. ej. CLINIC-DEMO" autoComplete="organization" /></label>
              <button className="btn" disabled={busy}>{busy ? 'Verificando…' : 'Entrar'}</button>
            </form>
            <p className="auth-alt">¿Primera vez? <a href="#" onClick={e => { e.preventDefault(); setView('register'); api.specialties().then(setSpecialties).catch(() => {}); }}>Cree la cuenta de su clínica</a></p>
            <p className="auth-alt"><a href="#" onClick={e => { e.preventDefault(); setRecovering(true); setError(''); }}>¿Olvidó su contraseña?</a></p>
          </>) : <>
            <h2>Crear organización</h2>
            <p className="sub">Registre su clínica y configure el administrador inicial.</p>
            <form onSubmit={onRegister}>
              <div className="fields-grid">
                <label className="field"><span>Código</span><input name="tenantCode" placeholder="mi-clinica" required /></label>
                <label className="field"><span>NIT</span><input name="taxId" placeholder="900000000-1" /></label>
                <label className="field" style={{ gridColumn: '1 / -1' }}><span>Razón social</span><input name="legalName" placeholder="Clínica Ejemplo S.A.S." required /></label>
                <label className="field" style={{ gridColumn: '1 / -1' }}><span>Nombre comercial</span><input name="commercialName" placeholder="Clínica Ejemplo" /></label>
                <label className="field"><span>Correo del admin</span><input name="adminEmail" type="email" placeholder="admin@clinica.com" required /></label>
                <label className="field"><span>Nombre del admin</span><input name="adminFullName" placeholder="Nombre completo" required /></label>
                <label className="field" style={{ gridColumn: '1 / -1' }}><span>Contraseña</span><input name="adminPassword" type="password" placeholder="Mínimo 8 caracteres" required minLength={8} /></label>
              </div>
              <label className="field"><span>Especialidades que ofrecerá</span></label>
              <div className="chips">{specialties.map(s => <label key={s.id} className={'chip' + (selected.includes(String(s.id)) ? ' on' : '')}><input type="checkbox" checked={selected.includes(String(s.id))} onChange={e => setSelected(p => e.target.checked ? [...p, String(s.id)] : p.filter(x => x !== String(s.id)))} /> {s.name}</label>)}</div>
              <button className="btn" disabled={busy}>{busy ? 'Creando…' : 'Crear y continuar'}</button>
            </form>
            <p className="auth-alt"><a href="#" onClick={e => { e.preventDefault(); setView('login'); }}>← Volver a iniciar sesión</a></p>
          </>}
        </div>
      </main>
    </div>;
  }

  /* ============ DASHBOARD ============ */
  const pendingInvoices = invoices.filter(i => !/PAID|CANCEL/i.test(String(i.status)));
  const arTotal = ar.reduce((s, r: any) => s + Number(r.balance_amount || 0), 0);
  const todayStr = new Date().toLocaleDateString('es-CO', { weekday: 'long', day: 'numeric', month: 'long' });
  const dow = (new Date().getDay() + 6) % 7; // lunes = 0
  const monday = new Date(); monday.setHours(0, 0, 0, 0); monday.setDate(monday.getDate() - dow);
  const week = Array.from({ length: 7 }, (_, i) => {
    const d = new Date(monday); d.setDate(monday.getDate() + i);
    const count = appts.filter(a => !/CANCEL/i.test(String(a.status)) && new Date(a.startsAt).toDateString() === d.toDateString()).length;
    return { date: d, label: wd[(i + 1) % 7], count, today: isToday(d) };
  });
  const apptsToday = appts.filter(a => isToday(a.startsAt) && !/CANCEL/i.test(String(a.status)))
    .sort((a: any, b: any) => new Date(a.startsAt).getTime() - new Date(b.startsAt).getTime());
  const weekEnd = new Date(monday.getTime() + 7 * 864e5);
  const apptsThisWeek = appts.filter(a => { const t = new Date(a.startsAt); return !/CANCEL/i.test(String(a.status)) && t >= monday && t < weekEnd; }).length;
  const dist = (() => {
    const counts: Record<string, number> = {};
    appts.forEach(a => { const s = String(a.status || '—').toUpperCase(); counts[s] = (counts[s] || 0) + 1; });
    const entries = Object.entries(counts).sort((x, y) => y[1] - x[1]);
    const max = Math.max(1, ...entries.map(e => e[1]));
    return entries.map(([status, count]) => ({ status, count, pct: Math.round(count / max * 100) }));
  })();

  async function setApptStatus(id: string, status: string) {
    let reason: string | undefined;
    if (status === 'CANCELLED') { const r = window.prompt('Motivo de cancelación (opcional):'); if (r === null) return; reason = r || undefined; }
    await run(() => api.updateAppointmentStatus(id, status, reason));
  }

  return <div className="app">
    <aside className="sidebar">
      <div className="brand">
        <div className="brand-mark"><Ico name="cross" size={20} /></div>
        <div><div className="brand-name">{brand}</div><div className="brand-sub">{product}</div></div>
      </div>
      <nav className="nav">
        {nav.map(g => {
          // Determinar si este grupo de navegación tiene un módulo asociado que pudiera estar oculto.
          // Mapeo simple: los ids de tab que contienen el nombre del módulo se ocultan si ese módulo está en hiddenModules.
          const moduleMap: Record<Tab, string> = {
            home: '', specialties: '', users: 'users', sites: 'sites', offices: 'offices',
            professionals: '', schedules: '', patients: '', appointments: 'appointments', waiting: 'waiting_list', reminders: 'reminders',
            encounters: 'clinical', clinical: 'clinical', lab: 'laboratory', prescriptions: 'prescriptions',
            billing: 'billing', eps: 'billing',
            documents: 'documents', interop: 'interoperability', audit: 'audit',
          };
          const gItems = g.items.map(i => {
            const modCode = moduleMap[i.id as Tab];
            if (modCode && hiddenModules.includes(modCode)) return null; // ocultar ítem
            return i;
          }).filter(i => i !== null);
          return gItems.length > 0 ? (<div className="nav-group" key={g.label}>
            <div className="nav-label">{g.label}</div>
            {gItems.map(i => <button key={i.id} className={'nav-item' + (tab === i.id ? ' active' : '')} onClick={() => setTab(i.id)}>
              <Ico name={i.icon} />{i.label}</button>)}
          </div>) : null;
        })}
      </nav>
      <div className="sidebar-foot">SIGM Cloud · v1.0 · Todos los datos son ficticios</div>
    </aside>

    <div className="main">
      <header className="topbar">
        <div className="crumb">{product} / {labels[tab]}<b>{labels[tab]}</b></div>
        <div className="topbar-right">
          <span className="tenant-chip" title={[session?.tenantName, session?.tenantId].filter(Boolean).join(' · ')}>
            <Ico name="building" size={12} />
            <span className="tenant-chip-txt">{session?.tenantCode || shortId(session?.tenantId)}{session?.tenantName ? ` · ${session.tenantName}` : ''}</span>
            <button type="button" className="chip-copy" title="Copiar código de la organización"
              onClick={async () => { const v = session?.tenantCode || session?.tenantId || ''; if (!v) return; try { await navigator.clipboard.writeText(v); setCopied(true); setTimeout(() => setCopied(false), 1600); } catch { /* el navegador puede negar el portapapeles */ } }}>
              <Ico name="copy" size={11} /> {copied ? 'Copiado' : 'Copiar'}
            </button>
          </span>
          {session?.permissions?.includes('platform.tenants.manage') && (
            <button className="btn ghost sm" title="Panel superadmin" onClick={() => setShowSuperadmin(true)}><Ico name="shield" size={14} /> Soporte</button>
          )}
          <button className="btn ghost sm" title="Seguridad de la cuenta" onClick={() => setShowSecurity(true)}><Ico name="shield" size={14} /> Seguridad</button>
          <div className="user">
            <div className="avatar">{initials(session?.fullName)}</div>
            <div><div className="user-name">{session?.fullName}</div><div className="user-role">{session?.permissions?.length ?? 0} permisos</div></div>
          </div>
          <button className="btn ghost sm" onClick={() => { clearSession(); setView('login'); }}><Ico name="logout" size={14} /> Salir</button>
          {/* Botón menú móvil - visible solo en < 960px */}
          <div className="mobile-menu-btn" onClick={() => setShowSidebar(!showSidebar)}>
            <div className="bar"></div>
            <div className="bar"></div>
            <div className="bar"></div>
          </div>
        </div>
      </header>

      {showSuperadmin && (
        <div className="modal-backdrop" onClick={() => setShowSuperadmin(false)}>
          <div className="modal-card" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true" aria-label="Soporte y administración">
            <div className="modal-head">
              <div><h3>Soporte y administración</h3><p className="muted">La gestión de clientes, módulos y base de datos se realiza desde el panel de plataforma.</p></div>
              <button type="button" className="btn ghost sm" onClick={() => setShowSuperadmin(false)} aria-label="Cerrar">&times;</button>
            </div>
            <div className="modal-body">
              <p>Abra <b>SIGM Admin</b> (puerto 3002) con una cuenta de plataforma para administrar organizaciones, módulos, bienvenida y la consola de base de datos.</p>
              <ul className="muted" style={{marginTop:8,paddingLeft:18}}>
                <li>Clientes y licencias: sección Clientes / Módulos.</li>
                <li>Base de datos: sección «Base de datos» del panel de plataforma.</li>
              </ul>
            </div>
            <div className="modal-foot"><button className="btn secondary" onClick={() => setShowSuperadmin(false)}>Cerrar</button></div>
          </div>
        </div>
      )}

      {showSecurity && <SecurityModal onClose={() => setShowSecurity(false)} />}

      {patientEdit && (
        <div className="modal-backdrop" onClick={() => setPatientEdit(null)}>
          <div className="modal-card" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true" aria-label="Editar paciente">
            <form onSubmit={savePatient}>
              <div className="modal-head">
                <div><h3>Editar paciente</h3><p className="muted">{patientEdit.patientNumber} · {patientEdit.fullName}</p></div>
                <button type="button" className="btn ghost sm" onClick={() => setPatientEdit(null)} aria-label="Cerrar">&times;</button>
              </div>
              <div className="modal-body">
                {error && <div className="alert error"><Ico name="shield" size={16} /> {error}</div>}
                <div className="fields-grid">
                  <label className="field"><span>Nombres</span><input name="firstName" defaultValue={patientEdit.firstName || ''} required /></label>
                  <label className="field"><span>Segundo nombre</span><input name="middleName" defaultValue={patientEdit.middleName || ''} /></label>
                  <label className="field"><span>Apellidos</span><input name="lastName" defaultValue={patientEdit.lastName || ''} required /></label>
                  <label className="field"><span>Segundo apellido</span><input name="secondLastName" defaultValue={patientEdit.secondLastName || ''} /></label>
                  <label className="field"><span>Tipo doc.</span><select name="documentType" defaultValue={patientEdit.documentType || 'CC'}><option value="CC">CC</option><option value="TI">TI</option><option value="CE">CE</option><option value="PA">PA</option><option value="RC">RC</option><option value="NIT">NIT</option></select></label>
                  <label className="field"><span>N.º documento</span><input name="documentNumber" defaultValue={patientEdit.documentNumber || ''} /></label>
                  <label className="field"><span>Fecha nac.</span><input name="birthDate" type="date" defaultValue={patientEdit.birthDate || ''} max={new Date().toISOString().slice(0, 10)} /></label>
                  <label className="field"><span>Sexo</span><select name="sexAtBirth" defaultValue={patientEdit.sexAtBirth || 'UNKNOWN'}><option value="UNKNOWN">Sin especificar</option><option value="F">Femenino</option><option value="M">Masculino</option><option value="INTERSEX">Intersexual</option></select></label>
                  <label className="field"><span>Correo</span><input name="email" type="email" defaultValue={patientEdit.email || ''} /></label>
                  <label className="field"><span>Teléfono</span><input name="phone" defaultValue={patientEdit.phone || ''} /></label>
                  <label className="field"><span>Celular</span><input name="mobilePhone" defaultValue={patientEdit.mobilePhone || ''} /></label>
                  <label className="field"><span>Ciudad</span><input name="city" defaultValue={patientEdit.city || ''} /></label>
                  <label className="field" style={{ gridColumn: '1 / -1' }}><span>Dirección</span><input name="addressLine" defaultValue={patientEdit.addressLine || ''} /></label>
                  <label className="field" style={{ gridColumn: '1 / -1' }}><span>Departamento</span><input name="department" defaultValue={patientEdit.department || ''} /></label>
                </div>
              </div>
              <div className="modal-foot">
                <button type="button" className="btn ghost" onClick={() => setPatientEdit(null)}>Cancelar</button>
                <button className="btn" disabled={busy}>{busy ? 'Guardando…' : 'Guardar cambios'}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      <div className="content">
        {error && <div className="alert error"><Ico name="shield" size={16} /> {error}</div>}

        {tab === 'home' && <>
          <div style={{ marginBottom: 18 }}>
            <h2 style={{ fontSize: 21, fontWeight: 700 }}>{greeting()}, {session?.fullName?.split(' ')[0] || 'equipo'} 👋</h2>
            <p className="muted" style={{ marginTop: 4 }}>Esto es lo que está pasando hoy en {brand} · {new Date().toLocaleDateString('es-CO', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })}</p>
          </div>

          <div className="stats">
            <div className="stat"><div className="stat-ico blue"><Ico name="users" size={20} /></div>
              <div><div className="stat-val">{patients.length}</div><div className="stat-lbl">Pacientes registrados</div></div></div>
            <div className="stat"><div className="stat-ico violet"><Ico name="calendar" size={20} /></div>
              <div style={{ flex: 1 }}><div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'flex-start' }}>
                <div><div className="stat-val">{apptsToday.length}</div><div className="stat-lbl">Citas para hoy</div></div>
                <span className="trend up">↗ {apptsThisWeek} esta semana</span></div>
                <div className="mini-bars">{week.map((d, i) => <i key={i} className={d.count > 0 ? 'hot' : ''} style={{ height: `${Math.max(12, Math.min(100, d.count * 22 + 12))}%` }} title={`${d.label} ${d.count} citas`} />)}</div>
              </div></div>
            <div className="stat"><div className="stat-ico amber"><Ico name="invoice" size={20} /></div>
              <div><div className="stat-val" style={{ fontSize: 19 }}>{money(arTotal)}</div><div className="stat-lbl">Cartera por cobrar · {pendingInvoices.length} facturas</div></div></div>
            <div className="stat"><div className="stat-ico green"><Ico name="activity" size={20} /></div>
              <div><div className="stat-val">{encounters.length}</div><div className="stat-lbl">Atenciones · {pros.length} profesionales</div></div></div>
          </div>

          <div className="panel">
            <div className="panel-h"><div><div className="panel-t"><Ico name="calendar" /> Calendario semanal</div><div className="panel-d">{todayStr} · citas activas por día</div></div>
              <button className="btn subtle sm" onClick={() => setTab('appointments')}>Ver agenda completa</button></div>
            <div className="panel-b">
              <div className="week-strip">
                {week.map((d, i) => <div key={i} className={'ws-day' + (d.today ? ' today' : '')}>
                  <span>{d.label}</span><b>{d.date.getDate()}</b>
                  <em>{d.count > 0 ? `${d.count} cita${d.count > 1 ? 's' : ''}` : '—'}</em>
                </div>)}
              </div>
            </div>
          </div>

          <div className="grid2" style={{ alignItems: 'start' }}>
            <section className="panel">
              <div className="panel-h"><div><div className="panel-t"><Ico name="clock" /> Citas para hoy</div><div className="panel-d">{apptsToday.length > 0 ? `${apptsToday.filter(a => /CONFIRMED/i.test(String(a.status))).length} confirmadas de ${apptsToday.length}` : 'Ninguna cita programada para hoy'}</div></div>
                <button className="btn subtle sm" onClick={() => setTab('appointments')}><Ico name="calendar" size={13} /> Nueva cita</button></div>
              <div className="panel-b" style={{ paddingTop: 12 }}>
                {apptsToday.length === 0 ? <div className="empty">No hay citas para hoy. ¡Disfrute de su día libre!<br /><small>Las próximas citas aparecen en la agenda completa.</small></div> :
                  <div className="list-cards">
                    {apptsToday.map(a => {
                      const s = String(a.status).toUpperCase();
                      return <div className="list-card" key={a.id}>
                        <b style={{ width: 46, flex: 'none' }}>{hhmm(a.startsAt)}</b>
                        <div className="grow"><b>{patients.find(p => p.id === a.patientId)?.fullName || 'Paciente'}</b>
                          <div className="meta">{pros.find(p => p.id === a.professionalId)?.displayName || ''}</div></div>
                        <Badge v={a.status} />
                        <div className="status-actions">
                          {s === 'PENDING' && <button className="chip-btn" disabled={busy} onClick={() => setApptStatus(a.id, 'CONFIRMED')}>Confirmar</button>}
                          {s === 'CONFIRMED' && <button className="chip-btn" disabled={busy} onClick={() => setApptStatus(a.id, 'IN_PROGRESS')}>Atender</button>}
                          {s === 'IN_PROGRESS' && <><button className="chip-btn" disabled={busy} onClick={() => setApptStatus(a.id, 'ATTENDED')}>Atendida</button>
                            <button className="chip-btn" disabled={busy} onClick={() => setApptStatus(a.id, 'NO_SHOW')}>No asistió</button></>}
                          {!/CANCEL|ATTENDED|NO_SHOW/i.test(s) && <button className="chip-btn danger" disabled={busy} onClick={() => setApptStatus(a.id, 'CANCELLED')}>Cancelar</button>}
                        </div>
                      </div>;
                    })}
                  </div>}
              </div>
            </section>

            <section className="panel">
              <div className="panel-h"><div><div className="panel-t"><Ico name="check" /> Acciones rápidas</div><div className="panel-d">Ir directo a la operación del día</div></div></div>
              <div className="panel-b" style={{ paddingBottom: 12 }}>
                <div className="quick-actions">
                  <button className="quick" onClick={() => setTab('patients')}><span className="q-ico blue"><Ico name="users" size={19} /></span>Nuevo paciente</button>
                  <button className="quick" onClick={() => setTab('appointments')}><span className="q-ico violet"><Ico name="calendar" size={19} /></span>Nueva cita</button>
                  <button className="quick" onClick={() => setTab('prescriptions')}><span className="q-ico green"><Ico name="pill" size={19} /></span>Receta</button>
                  <button className="quick" onClick={() => setTab('eps')}><span className="q-ico amber"><Ico name="card" size={19} /></span>Registrar pago</button>
                </div>
              </div>
              <div className="panel-b" style={{ borderTop: '1px solid var(--line-soft)', paddingTop: 12 }}>
                <div className="panel-sec-t" style={{ paddingTop: 0 }}>Citas por estado</div>
                {dist.map(d2 => <div className="dist-row" key={d2.status}>
                  <span className="d-label">{pretty(d2.status)}</span>
                  <span className="d-bar"><i style={{ width: `${d2.pct}%` }} /></span>
                  <span className="d-val">{d2.count}</span>
                </div>)}
                {dist.length === 0 && <div className="empty">Sin citas registradas todavía.</div>}
              </div>
            </section>
          </div>

          <section className="panel">
            <div className="panel-h"><div><div className="panel-t"><Ico name="invoice" /> Facturación reciente</div><div className="panel-d">{pendingInvoices.length} facturas con saldo pendiente</div></div>
              <button className="btn subtle sm" onClick={() => setTab('billing')}>Ver facturación</button></div>
            <div className="panel-b" style={{ paddingTop: 12 }}>
              {invoices.length === 0 ? <div className="empty">Aún no hay facturas emitidas.</div> :
                <div className="list-cards">{invoices.slice(0, 6).map(i => <div className="list-card" key={i.id}>
                  <div className="grow"><b>{i.invoiceNumber}</b><div className="meta">{money(i.totalAmount)}</div></div>
                  <Badge v={i.status} /></div>)}</div>}
            </div>
          </section>
        </>}

        {tab === 'specialties' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="grid" /> Especialidades habilitadas</div><div className="panel-d">Seleccione las especialidades que ofrece la organización y guarde los cambios.</div></div>
            <button className="btn" onClick={() => run(() => api.saveTenantSpecialties(selected))} disabled={busy}><Ico name="check" size={14} /> Guardar cambios</button></div>
          <div className="panel-b">
            <div className="chips" style={{ marginTop: 0 }}>{specialties.map(s => <label key={s.id} className={'chip' + (selected.includes(String(s.id)) ? ' on' : '')}><input type="checkbox" checked={selected.includes(String(s.id))} onChange={e => setSelected(p => e.target.checked ? [...p, String(s.id)] : p.filter(x => x !== String(s.id)))} /> {s.name}</label>)}</div>
          </div>
        </section>}

        {tab === 'users' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="users" /> Usuarios</div><div className="panel-d">{users.length} usuarios en la organización</div></div></div>
          <div className="panel-b" style={{ paddingBottom: 10 }}>
            <form className="toolbar" onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.createUser({ email: fd.get('email'), fullName: fd.get('fullName'), phone: fd.get('phone') || null, password: fd.get('password'), roleCodes: [fd.get('role') || 'DOCTOR'] }), f); }}>
              <label className="field"><span>Nombre</span><input name="fullName" placeholder="Nombre completo" required /></label>
              <label className="field"><span>Correo</span><input name="email" type="email" placeholder="correo@organizacion.com" required /></label>
              <label className="field"><span>Contraseña</span><input name="password" type="password" placeholder="Contraseña" required /></label>
              <label className="field"><span>Rol</span><select name="role"><option>DOCTOR</option><option>RECEPTIONIST</option><option>NURSE</option><option>BILLING</option><option>TENANT_ADMIN</option><option>LAB_STAFF</option></select></label>
              <button className="btn" disabled={busy}><Ico name="users" size={14} /> Agregar</button>
            </form>
          </div>
          <div className="table-wrap">
            <table><thead><tr><th>Nombre</th><th>Correo</th><th>Estado</th></tr></thead><tbody>
              {users.length === 0 ? <tr><td colSpan={3}><div className="empty">Sin usuarios todavía.</div></td></tr> :
                users.map(u => <tr key={u.id}><td className="strong">{u.fullName}</td><td>{u.email}</td><td><Badge v={u.status} /></td></tr>)}
            </tbody></table>
          </div>
        </section>}

        {tab === 'sites' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="building" /> Sedes</div><div className="panel-d">{sites.length} sedes registradas</div></div></div>
          <div className="panel-b" style={{ paddingBottom: 10 }}>
            <form className="toolbar" onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.createSite({ name: fd.get('name'), code: fd.get('code') || null, city: fd.get('city') || null, department: fd.get('department') || null, isMain: fd.get('isMain') === 'on' }), f); }}>
              <label className="field"><span>Nombre</span><input name="name" placeholder="Sede principal" required /></label>
              <label className="field"><span>Código</span><input name="code" placeholder="Código" /></label>
              <label className="field"><span>Ciudad</span><input name="city" placeholder="Ciudad" /></label>
              <label className="field"><span>Departamento</span><input name="department" placeholder="Departamento" /></label>
              <label className="check" style={{ marginBottom: 8 }}><input name="isMain" type="checkbox" /> Sede principal</label>
              <button className="btn" disabled={busy}><Ico name="building" size={14} /> Crear sede</button>
            </form>
          </div>
          <div className="table-wrap">
            <table><thead><tr><th>Nombre</th><th>Código</th><th>Ciudad</th><th>Principal</th></tr></thead><tbody>
              {sites.length === 0 ? <tr><td colSpan={4}><div className="empty">Sin sedes registradas.</div></td></tr> :
                sites.map(s => <tr key={s.id}><td className="strong">{s.name}</td><td>{s.code || '—'}</td><td>{s.city || '—'}</td><td>{s.isMain ? <span className="badge blue">Principal</span> : <span className="muted">—</span>}</td></tr>)}
            </tbody></table>
          </div>
        </section>}

        {tab === 'offices' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="door" /> Consultorios y recursos físicos</div><div className="panel-d">{offices.length} consultorios</div></div></div>
          <div className="panel-b" style={{ paddingBottom: 10 }}>
            <form className="toolbar" onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.createOffice({ siteId: fd.get('siteId'), name: fd.get('name'), code: fd.get('code') || null, floor: fd.get('floor') || null, capacity: fd.get('capacity') ? Number(fd.get('capacity')) : null, officeType: fd.get('officeType') || 'CONSULTATION' }), f); }}>
              <label className="field"><span>Sede</span><select name="siteId" required><option value="">Seleccionar…</option>{sites.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
              <label className="field"><span>Nombre</span><input name="name" placeholder="Consultorio 101" required /></label>
              <label className="field"><span>Código</span><input name="code" placeholder="Código" /></label>
              <label className="field"><span>Piso</span><input name="floor" placeholder="Piso" /></label>
              <label className="field"><span>Capacidad</span><input name="capacity" type="number" placeholder="Capacidad" /></label>
              <label className="field"><span>Tipo</span><select name="officeType"><option>CONSULTATION</option><option>PROCEDURE</option><option>LAB</option><option>OTHER</option></select></label>
              <button className="btn" disabled={busy}><Ico name="door" size={14} /> Crear</button>
            </form>
          </div>
          <div className="table-wrap">
            <table><thead><tr><th>Nombre</th><th>Sede</th><th>Tipo</th><th>Estado</th></tr></thead><tbody>
              {offices.length === 0 ? <tr><td colSpan={4}><div className="empty">Sin consultorios registrados.</div></td></tr> :
                offices.map(o => <tr key={o.id}><td className="strong">{o.name}</td><td>{sites.find(s => s.id === o.siteId)?.name || shortId(o.siteId)}</td><td><Badge v={o.officeType} /></td><td><Badge v={o.isActive ? 'ACTIVE' : 'INACTIVE'} /></td></tr>)}
            </tbody></table>
          </div>
        </section>}

        {tab === 'professionals' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="steth" /> Profesionales</div><div className="panel-d">{pros.length} profesionales vinculados</div></div></div>
          <div className="panel-b" style={{ paddingBottom: 10 }}>
            <form className="toolbar" onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.createProfessional({ userId: fd.get('userId'), displayName: fd.get('displayName'), professionalType: fd.get('professionalType') || 'PHYSICIAN', professionalLicense: fd.get('license') || null, primarySiteId: (fd.get('primarySiteId') as string) || null, specialtyIds: (fd.getAll('specialtyIds') as string[]).filter(Boolean) }), f); }}>
              <label className="field"><span>Usuario</span><select name="userId" required><option value="">Seleccionar…</option>{users.map(u => <option key={u.id} value={u.id}>{u.fullName}</option>)}</select></label>
              <label className="field"><span>Nombre a mostrar</span><input name="displayName" placeholder="Dr. / Dra. …" required /></label>
              <label className="field"><span>Tipo</span><select name="professionalType"><option>PHYSICIAN</option><option>SPECIALIST</option><option>NURSE</option><option>OTHER</option></select></label>
              <label className="field"><span>Sede principal</span><select name="primarySiteId"><option value="">Sin sede fija</option>{sites.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
              <label className="field"><span>Registro médico</span><input name="license" placeholder="N.º de registro" /></label>
              <label className="field"><span>Especialidades</span><select name="specialtyIds" multiple size={3}>{specialties.filter(s => selected.includes(String(s.id))).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
              <button className="btn" disabled={busy}><Ico name="steth" size={14} /> Crear</button>
            </form>
          </div>
          <div className="table-wrap">
            <table><thead><tr><th>Nombre</th><th>Tipo</th><th>Especialidades</th></tr></thead><tbody>
              {pros.length === 0 ? <tr><td colSpan={3}><div className="empty">Sin profesionales registrados.</div></td></tr> :
                pros.map(p => <tr key={p.id}><td className="strong">{p.displayName}</td><td><Badge v={p.professionalType} /></td><td>{(p.specialties || []).map((s: any) => <span key={s.id || s} className="badge slate" style={{ marginRight: 4 }}>{typeof s === 'string' ? pretty(s) : s.name}</span>) || '—'}</td></tr>)}
            </tbody></table>
          </div>
        </section>}

        {tab === 'schedules' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="clock" /> Horarios y disponibilidad</div><div className="panel-d">Agendas, reglas, excepciones y consulta de slots</div></div></div>
          <div className="panel-b">
            <div className="grid2">
              <form onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.createSchedule({ professionalId: fd.get('professionalId'), siteId: fd.get('siteId'), medicalOfficeId: (fd.get('medicalOfficeId') as string) || null, name: fd.get('name'), timezone: 'America/Bogota' }), f); }}>
                <div className="panel-sec-t" style={{ paddingTop: 0 }}>Nueva agenda</div>
                <div className="fields-grid">
                  <label className="field" style={{ gridColumn: '1 / -1' }}><span>Profesional</span><select name="professionalId" required><option value="">Seleccionar…</option>{pros.map(p => <option key={p.id} value={p.id}>{p.displayName}</option>)}</select></label>
                  <label className="field"><span>Sede</span><select name="siteId" required><option value="">Seleccionar…</option>{sites.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
                  <label className="field"><span>Consultorio</span><select name="medicalOfficeId"><option value="">Opcional</option>{offices.map(o => <option key={o.id} value={o.id}>{o.name}</option>)}</select></label>
                  <label className="field" style={{ gridColumn: '1 / -1' }}><span>Nombre de la agenda</span><input name="name" placeholder="Agenda mañana — Dr. Pérez" required /></label>
                </div>
                <button className="btn sm" disabled={busy} style={{ marginTop: 12 }}>Crear agenda</button>
              </form>
              <form onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.addRule({ scheduleId: fd.get('scheduleId'), weekday: Number(fd.get('weekday')), startTime: fd.get('startTime'), endTime: fd.get('endTime'), slotDurationMinutes: Number(fd.get('slot') || 30), specialtyId: (fd.get('specialtyId') as string) || null }), f); }}>
                <div className="panel-sec-t" style={{ paddingTop: 0 }}>Regla de disponibilidad</div>
                <div className="fields-grid">
                  <label className="field" style={{ gridColumn: '1 / -1' }}><span>Agenda</span><select name="scheduleId" required><option value="">Seleccionar…</option>{schedules.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
                  <label className="field"><span>Día</span><select name="weekday">{[1, 2, 3, 4, 5, 6, 7].map(n => <option key={n} value={n}>{wd[n % 7]}</option>)}</select></label>
                  <label className="field"><span>Duración de slot (min)</span><input name="slot" type="number" defaultValue={30} /></label>
                  <label className="field"><span>Inicio</span><input name="startTime" type="time" required /></label>
                  <label className="field"><span>Fin</span><input name="endTime" type="time" required /></label>
                  <label className="field" style={{ gridColumn: '1 / -1' }}><span>Especialidad</span><select name="specialtyId"><option value="">Opcional</option>{specialties.filter(s => selected.includes(String(s.id))).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
                </div>
                <button className="btn sm" disabled={busy} style={{ marginTop: 12 }}>Agregar regla</button>
              </form>
              <form onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.addException({ scheduleId: (fd.get('scheduleId') as string) || null, professionalId: fd.get('professionalId'), exceptionDate: fd.get('exceptionDate'), startTime: fd.get('startTime') || null, endTime: fd.get('endTime') || null, isAllDay: fd.get('isAllDay') === 'on', exceptionType: fd.get('exceptionType') || 'BLOCK', reason: fd.get('reason') || null }), f); }}>
                <div className="panel-sec-t" style={{ paddingTop: 0 }}>Excepción / bloqueo</div>
                <div className="fields-grid">
                  <label className="field"><span>Profesional</span><select name="professionalId" required><option value="">Seleccionar…</option>{pros.map(p => <option key={p.id} value={p.id}>{p.displayName}</option>)}</select></label>
                  <label className="field"><span>Agenda</span><select name="scheduleId"><option value="">Opcional</option>{schedules.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
                  <label className="field"><span>Fecha</span><input name="exceptionDate" type="date" required /></label>
                  <label className="field"><span>Inicio</span><input name="startTime" type="time" /></label>
                  <label className="field"><span>Fin</span><input name="endTime" type="time" /></label>
                  <label className="field"><span>Tipo</span><select name="exceptionType"><option>BLOCK</option><option>EXTRA</option></select></label>
                  <label className="field" style={{ gridColumn: '1 / -1' }}><span>Motivo</span><input name="reason" placeholder="Motivo" /></label>
                  <label className="check"><input name="isAllDay" type="checkbox" /> Todo el día</label>
                </div>
                <button className="btn sm" disabled={busy} style={{ marginTop: 12 }}>Agregar excepción</button>
              </form>
              <form onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.createAppointmentType({ code: fd.get('code'), name: fd.get('name'), durationMinutes: Number(fd.get('duration') || 30), specialtyId: (fd.get('specialtyId') as string) || null }), f); }}>
                <div className="panel-sec-t" style={{ paddingTop: 0 }}>Tipo de cita</div>
                <div className="fields-grid">
                  <label className="field"><span>Código</span><input name="code" placeholder="Código" required /></label>
                  <label className="field"><span>Nombre</span><input name="name" placeholder="Nombre" required /></label>
                  <label className="field"><span>Duración (min)</span><input name="duration" type="number" defaultValue={30} /></label>
                  <label className="field" style={{ gridColumn: '1 / -1' }}><span>Especialidad</span><select name="specialtyId"><option value="">Opcional</option>{specialties.filter(s => selected.includes(String(s.id))).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
                </div>
                <button className="btn sm" disabled={busy} style={{ marginTop: 12 }}>Crear tipo</button>
              </form>
            </div>
          </div>
          <div className="panel-sec">
            <div className="panel-sec-t">Agendas</div>
            <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
              <table><thead><tr><th>Nombre</th><th>Profesional</th><th>Estado</th></tr></thead><tbody>
                {schedules.length === 0 ? <tr><td colSpan={3}><div className="empty">Sin agendas creadas.</div></td></tr> :
                  schedules.map(s => <tr key={s.id}><td className="strong">{s.name}</td><td>{pros.find(p => p.id === s.professionalId)?.displayName || shortId(s.professionalId)}</td><td><Badge v={s.isActive ? 'ACTIVE' : 'INACTIVE'} /></td></tr>)}
              </tbody></table>
            </div>
            <div className="panel-sec-t">Reglas</div>
            <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
              <table><thead><tr><th>Día</th><th>Inicio</th><th>Fin</th><th>Slot</th></tr></thead><tbody>
                {rules.length === 0 ? <tr><td colSpan={4}><div className="empty">Sin reglas de disponibilidad.</div></td></tr> :
                  rules.map(r => <tr key={r.id}><td className="strong">{wd[r.weekday % 7]}</td><td>{r.startTime}</td><td>{r.endTime}</td><td>{r.slotDurationMinutes} min</td></tr>)}
              </tbody></table>
            </div>
            <div className="panel-sec-t">Excepciones</div>
            <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
              <table><thead><tr><th>Fecha</th><th>Tipo</th><th>Motivo</th></tr></thead><tbody>
                {exceptions.length === 0 ? <tr><td colSpan={3}><div className="empty">Sin excepciones registradas.</div></td></tr> :
                  exceptions.map(x => <tr key={x.id}><td className="strong">{d(x.exceptionDate)}</td><td><Badge v={x.exceptionType} /></td><td>{x.reason || '—'}</td></tr>)}
              </tbody></table>
            </div>
            <div className="panel-sec-t">Tipos de cita</div>
            <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
              <table><thead><tr><th>Código</th><th>Nombre</th><th>Duración</th></tr></thead><tbody>
                {apptTypes.length === 0 ? <tr><td colSpan={3}><div className="empty">Sin tipos de cita.</div></td></tr> :
                  apptTypes.map(t => <tr key={t.id}><td className="strong">{t.code}</td><td>{t.name}</td><td>{t.durationMinutes} min</td></tr>)}
              </tbody></table>
            </div>
            <div className="panel-sec-t">Consultar disponibilidad (slots)</div>
            <form className="toolbar" onSubmit={e => { e.preventDefault(); const fd = new FormData(e.currentTarget); run(async () => setSlots(await api.availability(fd.get('professionalId') as string, fd.get('date') as string))); }}>
              <label className="field"><span>Profesional</span><select name="professionalId" required><option value="">Seleccionar…</option>{pros.map(p => <option key={p.id} value={p.id}>{p.displayName}</option>)}</select></label>
              <label className="field"><span>Fecha</span><input name="date" type="date" required /></label>
              <button className="btn" disabled={busy}><Ico name="clock" size={14} /> Consultar</button>
            </form>
            {slots && (() => {
              const arr: any[] = Array.isArray(slots) ? slots : (slots?.slots || slots?.available || slots?.items || []);
              if (Array.isArray(arr) && arr.length > 0) return <div className="slotgrid">{arr.map((s: any, i: number) => {
                const t = typeof s === 'string' ? s : (s.startsAt || s.start || s.time || s.startTime);
                return <span className="slot" key={i}>{t ? new Date(t).toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit' }) : JSON.stringify(s)}</span>;
              })}</div>;
              return <div className="empty" style={{ marginTop: 10 }}>No hay slots disponibles para esa fecha y profesional.</div>;
            })()}
          </div>
        </section>}

        {tab === 'patients' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="users" /> Pacientes</div><div className="panel-d">{patTotal} paciente{patTotal === 1 ? '' : 's'} en la organización{patientQ.trim() ? ` · filtro “${patientQ.trim()}”` : ''}</div></div>
            <div className="search-box"><Ico name="search" size={14} /><input placeholder="Buscar por nombre, documento o número…" value={patientQ} onChange={e => setPatientQ(e.target.value)} /></div></div>
          <div className="panel-b" style={{ paddingBottom: 10 }}>
            <form className="toolbar" onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.createPatient({ firstName: fd.get('firstName'), lastName: fd.get('lastName'), documentType: fd.get('documentType') || null, documentNumber: fd.get('documentNumber') || null, birthDate: fd.get('birthDate') || null, email: fd.get('email') || null, phone: fd.get('phone') || null, city: fd.get('city') || null, sexAtBirth: fd.get('sex') || 'UNKNOWN' }), f); }}>
              <label className="field"><span>Nombres</span><input name="firstName" placeholder="Nombres" required /></label>
              <label className="field"><span>Apellidos</span><input name="lastName" placeholder="Apellidos" required /></label>
              <label className="field"><span>Tipo doc.</span><select name="documentType"><option value="CC">CC</option><option value="TI">TI</option><option value="CE">CE</option><option value="PA">PA</option></select></label>
              <label className="field"><span>N.º documento</span><input name="documentNumber" placeholder="Documento" /></label>
              <label className="field"><span>Fecha nac.</span><input name="birthDate" type="date" /></label>
              <label className="field"><span>Sexo</span><select name="sex"><option value="UNKNOWN">Sin especificar</option><option value="M">Masculino</option><option value="F">Femenino</option></select></label>
              <label className="field"><span>Correo</span><input name="email" placeholder="Correo" /></label>
              <label className="field"><span>Teléfono</span><input name="phone" placeholder="Teléfono" /></label>
              <button className="btn" disabled={busy}><Ico name="users" size={14} /> Registrar</button>
            </form>
          </div>
          <div className="table-wrap">
            <table><thead><tr><th>Paciente</th><th>Documento</th><th>Contacto</th><th>Estado</th><th></th></tr></thead><tbody>
              {patients.length === 0 ? <tr><td colSpan={5}><div className="empty">Sin pacientes registrados.</div></td></tr> :
                patients.map((p: any) => <tr key={p.id}>
                  <td className="strong">{p.fullName || `${p.firstName} ${p.lastName}`} <small>({p.patientNumber})</small></td>
                  <td>{p.documentType} {p.documentNumber || '—'}</td>
                  <td>{p.email || p.phone || '—'}</td>
                  <td><span className={'badge ' + (p.isActive === false ? 'red' : 'green')}>{p.isActive === false ? 'Inactivo' : 'Activo'}</span></td>
                  <td className="row-actions">
                    <button type="button" className="chip-btn" disabled={busy} onClick={() => openPatientEdit(p.id)}><Ico name="edit" size={12} /> Editar</button>
                    {p.isActive !== false && <button type="button" className="chip-btn danger" disabled={busy} onClick={() => deactivatePatient(p)}><Ico name="trash" size={12} /> Desactivar</button>}
                  </td>
                </tr>)}
            </tbody></table>
          </div>
          {patients.length < patTotal && <div style={{ padding: '14px 0', textAlign: 'center' }}>
            <button className="btn ghost sm" disabled={busy} onClick={() => loadPatientsPage(patPage + 1)}>Cargar más ({patients.length} de {patTotal})</button>
          </div>}
        </section>}

        {tab === 'appointments' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="calendar" /> Agenda de citas</div><div className="panel-d">{appts.length} citas registradas</div></div></div>
          <div className="panel-b" style={{ paddingBottom: 10 }}>
            <form className="toolbar" onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.createAppointment({ patientId: fd.get('patientId'), professionalId: fd.get('professionalId'), specialtyId: fd.get('specialtyId'), siteId: fd.get('siteId'), medicalOfficeId: (fd.get('medicalOfficeId') as string) || null, startsAt: new Date(fd.get('startsAt') as string).toISOString(), endsAt: new Date(fd.get('endsAt') as string).toISOString(), reason: fd.get('reason') || null }), f); }}>
              <PatientPicker name="patientId" required />
              <label className="field"><span>Profesional</span><select name="professionalId" required><option value="">Seleccionar…</option>{pros.map(p => <option key={p.id} value={p.id}>{p.displayName}</option>)}</select></label>
              <label className="field"><span>Especialidad</span><select name="specialtyId" required><option value="">Seleccionar…</option>{specialties.filter(s => selected.includes(String(s.id))).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
              <label className="field"><span>Sede</span><select name="siteId" required><option value="">Seleccionar…</option>{sites.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
              <label className="field"><span>Consultorio</span><select name="medicalOfficeId"><option value="">Opcional</option>{offices.map(o => <option key={o.id} value={o.id}>{o.name}</option>)}</select></label>
              <label className="field"><span>Inicio</span><input name="startsAt" type="datetime-local" required /></label>
              <label className="field"><span>Fin</span><input name="endsAt" type="datetime-local" required /></label>
              <label className="field"><span>Motivo</span><input name="reason" placeholder="Motivo de la cita" /></label>
              <button className="btn" disabled={busy}><Ico name="calendar" size={14} /> Crear cita</button>
            </form>
          </div>
          <div className="table-wrap">
            <table><thead><tr><th>Número</th><th>Inicio</th><th>Fin</th><th>Estado</th><th>Acciones</th></tr></thead><tbody>
              {appts.length === 0 ? <tr><td colSpan={5}><div className="empty">Sin citas programadas.</div></td></tr> :
                appts.map(a => { const s = String(a.status).toUpperCase();
                  return <tr key={a.id}><td className="strong">{a.appointmentNumber}</td><td>{dt(a.startsAt)}</td><td>{dt(a.endsAt)}</td><td><Badge v={a.status} /></td>
                  <td><div className="status-actions">
                    {s === 'PENDING' && <button className="chip-btn" disabled={busy} onClick={() => setApptStatus(a.id, 'CONFIRMED')}>Confirmar</button>}
                    {s === 'CONFIRMED' && <button className="chip-btn" disabled={busy} onClick={() => setApptStatus(a.id, 'IN_PROGRESS')}>Atender</button>}
                    {s === 'IN_PROGRESS' && <><button className="chip-btn" disabled={busy} onClick={() => setApptStatus(a.id, 'ATTENDED')}>Atendida</button>
                      <button className="chip-btn" disabled={busy} onClick={() => setApptStatus(a.id, 'NO_SHOW')}>No asistió</button></>}
                    {s === 'WAITING' && <button className="chip-btn" disabled={busy} onClick={() => setApptStatus(a.id, 'IN_PROGRESS')}>Atender</button>}
                    {!/CANCEL|ATTENDED|NO_SHOW/i.test(s) && <button className="chip-btn danger" disabled={busy} onClick={() => setApptStatus(a.id, 'CANCELLED')}>Cancelar</button>}
                  </div></td></tr>; })}
            </tbody></table>
          </div>
        </section>}

        {tab === 'encounters' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="activity" /> Atenciones</div><div className="panel-d">{encounters.length} atenciones clínicas</div></div></div>
          <div className="panel-b" style={{ paddingBottom: 10 }}>
            <form className="toolbar" onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.createEncounter({ patientId: fd.get('patientId'), professionalId: fd.get('professionalId'), specialtyId: fd.get('specialtyId'), siteId: fd.get('siteId'), appointmentId: (fd.get('appointmentId') as string) || null, encounterType: fd.get('encounterType') || 'CONSULTATION', chiefComplaint: fd.get('chiefComplaint') || null }), f); }}>
              <PatientPicker name="patientId" required />
              <label className="field"><span>Profesional</span><select name="professionalId" required><option value="">Seleccionar…</option>{pros.map(p => <option key={p.id} value={p.id}>{p.displayName}</option>)}</select></label>
              <label className="field"><span>Especialidad</span><select name="specialtyId" required><option value="">Seleccionar…</option>{specialties.filter(s => selected.includes(String(s.id))).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
              <label className="field"><span>Sede</span><select name="siteId" required><option value="">Seleccionar…</option>{sites.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
              <label className="field"><span>Tipo</span><select name="encounterType"><option>CONSULTATION</option><option>FOLLOW_UP</option><option>EMERGENCY</option><option>PROCEDURE</option><option>TELEMEDICINE</option></select></label>
              <label className="field" style={{ flex: '2 1 240px', maxWidth: 320 }}><span>Motivo</span><input name="chiefComplaint" placeholder="Motivo de consulta" /></label>
              <button className="btn" disabled={busy}><Ico name="activity" size={14} /> Abrir atención</button>
            </form>
          </div>
          <div className="table-wrap">
            <table><thead><tr><th>Fecha</th><th>Tipo</th><th>Estado</th><th>Motivo</th></tr></thead><tbody>
              {encounters.length === 0 ? <tr><td colSpan={4}><div className="empty">Sin atenciones registradas.</div></td></tr> :
                encounters.map(e2 => <tr key={e2.id}><td className="strong">{dt(e2.encounterDate)}</td><td><Badge v={e2.encounterType} /></td><td><Badge v={e2.status} /></td><td>{e2.chiefComplaint || '—'}</td></tr>)}
            </tbody></table>
          </div>
        </section>}

        {tab === 'clinical' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="clipboard" /> Resumen clínico y registros</div><div className="panel-d">Alergias, antecedentes, signos vitales y resumen integrado del paciente</div></div></div>
          <div className="panel-b" style={{ paddingBottom: 12 }}>
            <form className="toolbar" onSubmit={e => { e.preventDefault(); run(async () => setSummary(await api.clinicalSummary(summaryPatient))); }}>
              <PatientPicker value={summaryPatient} onChange={setSummaryPatient} />
              <button className="btn" disabled={!summaryPatient || busy}><Ico name="clipboard" size={14} /> Cargar resumen</button>
              <button className="btn ghost" type="button" disabled={!summaryPatient || busy} onClick={() => void openRecordPdf(summaryPatient)}><Ico name="folder" size={14} /> Historia clínica (PDF)</button>
              <button className="btn ghost" type="button" disabled={!summaryPatient || busy} onClick={() => void printRecord(summaryPatient)}><Ico name="copy" size={14} /> Imprimir</button>
            </form>
            {summary && (() => {
              const al: any[] = summary.allergies || [], dx: any[] = summary.diagnoses || [], vit: any[] = summary.vitals || [], hist: any[] = summary.history || [];
              const timeline = [
                ...vit.map((v: any) => ({ date: v.recordedAt || v.createdAt, cls: 'tl-vitals', title: 'Signos vitales', detail: `Temp ${v.temperatureC ?? '—'} °C · FC ${v.heartRate ?? '—'} · PA ${v.systolicBp ?? '—'}/${v.diastolicBp ?? '—'} · SpO₂ ${v.oxygenSaturation ?? '—'}%` })),
                ...hist.map((h: any) => ({ date: h.onsetDate || h.createdAt, cls: '', title: `Antecedente · ${pretty(h.historyType || '')}`, detail: h.description || '—' })),
                ...dx.map((x: any) => ({ date: x.diagnosedAt || x.createdAt, cls: 'tl-lab', title: `Diagnóstico${x.code ? ` · ${x.code}` : ''}`, detail: x.description || x.name || '—' })),
              ].filter((e: any) => e.date).sort((a: any, b: any) => new Date(b.date).getTime() - new Date(a.date).getTime()).slice(0, 12);
              return <>
                <div className="panel-sec-t">Línea de tiempo clínica ({timeline.length})</div>
                {timeline.length === 0 ? <div className="empty">Sin eventos fechados todavía. Registre signos vitales, antecedentes o diagnósticos para construir la línea de tiempo.</div> :
                  <div className="timeline">
                    {timeline.map((e: any, i: number) => <div className={`tl-item ${e.cls}`} key={i}>
                      <div className="tl-date">{dt(e.date)}</div>
                      <div className="tl-title">{e.title}</div>
                      <div className="tl-detail">{e.detail}</div>
                    </div>)}
                  </div>}
                <div className="panel-sec-t">Alergias ({al.length})</div>
                {al.length === 0 ? <div className="empty">Sin alergias registradas.</div> :
                  <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
                    <table><thead><tr><th>Alérgeno</th><th>Tipo</th><th>Severidad</th><th>Reacción</th></tr></thead><tbody>
                      {al.map((a: any, i: number) => <tr key={i}><td className="strong">{a.allergen}</td><td><Badge v={a.allergenType} /></td><td><Badge v={a.severity} /></td><td>{a.reaction || '—'}</td></tr>)}
                    </tbody></table>
                  </div>}
                <div className="panel-sec-t">Diagnósticos ({dx.length})</div>
                {dx.length === 0 ? <div className="empty">Sin diagnósticos registrados.</div> :
                  <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
                    <table><thead><tr><th>Código</th><th>Descripción</th><th>Tipo</th></tr></thead><tbody>
                      {dx.map((x: any, i: number) => <tr key={i}><td className="strong">{x.code || '—'}</td><td>{x.description || x.name || '—'}</td><td><Badge v={x.diagnosisType || x.type} /></td></tr>)}
                    </tbody></table>
                  </div>}
                <div className="panel-sec-t">Signos vitales ({vit.length})</div>
                {vit.length === 0 ? <div className="empty">Sin signos vitales registrados.</div> :
                  <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
                    <table><thead><tr><th>Fecha</th><th>Temp °C</th><th>FC</th><th>FR</th><th>PA</th><th>SpO₂ %</th><th>Peso kg</th><th>Talla cm</th></tr></thead><tbody>
                      {vit.map((v: any, i: number) => <tr key={i}><td className="strong">{dt(v.recordedAt || v.createdAt)}</td><td className="num">{v.temperatureC ?? '—'}</td><td className="num">{v.heartRate ?? '—'}</td><td className="num">{v.respiratoryRate ?? '—'}</td><td className="num">{v.systolicBp ?? '—'}/{v.diastolicBp ?? '—'}</td><td className="num">{v.oxygenSaturation ?? '—'}</td><td className="num">{v.weightKg ?? '—'}</td><td className="num">{v.heightCm ?? '—'}</td></tr>)}
                    </tbody></table>
                  </div>}
                <div className="panel-sec-t">Antecedentes ({hist.length})</div>
                {hist.length === 0 ? <div className="empty">Sin antecedentes registrados.</div> :
                  <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
                    <table><thead><tr><th>Tipo</th><th>Descripción</th><th>Desde</th><th>Estado</th></tr></thead><tbody>
                      {hist.map((h: any, i: number) => <tr key={i}><td className="strong"><Badge v={h.historyType} /></td><td>{h.description}</td><td>{d(h.onsetDate)}</td><td><Badge v={h.status} /></td></tr>)}
                    </tbody></table>
                  </div>}
              </>;
            })()}
          </div>
          <div className="panel-sec">
            <div className="grid2">
              <form onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.addAllergy({ patientId: fd.get('patientId'), allergen: fd.get('allergen'), allergenType: fd.get('allergenType'), reaction: fd.get('reaction') || null, severity: fd.get('severity') || 'MEDIUM' }), f); }}>
                <div className="panel-sec-t" style={{ paddingTop: 0 }}>Registrar alergia</div>
                <div className="fields-grid">
                  <PatientPicker name="patientId" required style={{ gridColumn: '1 / -1' }} />
                  <label className="field"><span>Alérgeno</span><input name="allergen" placeholder="Alérgeno" required /></label>
                  <label className="field"><span>Tipo</span><select name="allergenType"><option>MEDICATION</option><option>FOOD</option><option>ENVIRONMENTAL</option><option>OTHER</option></select></label>
                  <label className="field"><span>Severidad</span><select name="severity"><option>LOW</option><option>MEDIUM</option><option>HIGH</option><option>CRITICAL</option></select></label>
                  <label className="field"><span>Reacción</span><input name="reaction" placeholder="Reacción" /></label>
                </div>
                <button className="btn sm" disabled={busy} style={{ marginTop: 12 }}>Agregar alergia</button>
              </form>
              <form onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.addHistory({ patientId: fd.get('patientId'), encounterId: (fd.get('encounterId') as string) || null, historyType: fd.get('historyType'), description: fd.get('description'), onsetDate: fd.get('onsetDate') || null, status: 'ACTIVE' }), f); }}>
                <div className="panel-sec-t" style={{ paddingTop: 0 }}>Registrar antecedente</div>
                <div className="fields-grid">
                  <PatientPicker name="patientId" required style={{ gridColumn: '1 / -1' }} />
                  <label className="field"><span>Tipo</span><select name="historyType"><option>PERSONAL</option><option>SURGICAL</option><option>TOXICOLOGICAL</option><option>GYNECOLOGICAL</option><option>OTHER</option></select></label>
                  <label className="field"><span>Desde</span><input name="onsetDate" type="date" /></label>
                  <label className="field" style={{ gridColumn: '1 / -1' }}><span>Descripción</span><input name="description" placeholder="Descripción" required /></label>
                </div>
                <button className="btn sm" disabled={busy} style={{ marginTop: 12 }}>Agregar antecedente</button>
              </form>
            </div>
            <div className="panel-sec-t">Registrar signos vitales</div>
            <form className="toolbar" onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.addVitals({ encounterId: fd.get('encounterId'), patientId: fd.get('patientId'), temperatureC: num(fd.get('temperatureC')), heartRate: int(fd.get('heartRate')), respiratoryRate: int(fd.get('respiratoryRate')), systolicBp: int(fd.get('systolicBp')), diastolicBp: int(fd.get('diastolicBp')), oxygenSaturation: num(fd.get('oxygenSaturation')), weightKg: num(fd.get('weightKg')), heightCm: num(fd.get('heightCm')), glucoseMgDl: num(fd.get('glucoseMgDl')), painScale: int(fd.get('painScale')), notes: fd.get('notes') || null }), f); }}>
              <PatientPicker name="patientId" required />
              <label className="field" style={{ flex: '2 1 260px', maxWidth: 340 }}><span>Atención</span><select name="encounterId" required><option value="">Seleccionar…</option>{encounters.map(e2 => <option key={e2.id} value={e2.id}>{dt(e2.encounterDate)} · {pretty(e2.encounterType)}</option>)}</select></label>
              <label className="field"><span>Temp °C</span><input name="temperatureC" placeholder="36.8" /></label>
              <label className="field"><span>FC</span><input name="heartRate" placeholder="72" /></label>
              <label className="field"><span>FR</span><input name="respiratoryRate" placeholder="16" /></label>
              <label className="field"><span>PAS</span><input name="systolicBp" placeholder="120" /></label>
              <label className="field"><span>PAD</span><input name="diastolicBp" placeholder="80" /></label>
              <label className="field"><span>SpO₂ %</span><input name="oxygenSaturation" placeholder="98" /></label>
              <label className="field"><span>Peso kg</span><input name="weightKg" placeholder="70" /></label>
              <label className="field"><span>Talla cm</span><input name="heightCm" placeholder="170" /></label>
              <button className="btn" disabled={busy}><Ico name="activity" size={14} /> Registrar</button>
            </form>
          </div>
        </section>}

        {tab === 'billing' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="invoice" /> Facturación</div><div className="panel-d">{invoices.length} facturas · {services.length} servicios en tarifario</div></div></div>
          <div className="panel-b">
            <div className="grid2">
              <form onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.createBillingService({ code: fd.get('code') || null, name: fd.get('name'), description: fd.get('description') || null }), f); }}>
                <div className="panel-sec-t" style={{ paddingTop: 0 }}>Servicio / tarifario</div>
                <div className="fields-grid">
                  <label className="field"><span>Código</span><input name="code" placeholder="Autogenerado si se omite" /></label>
                  <label className="field"><span>Nombre</span><input name="name" placeholder="Nombre del servicio" required /></label>
                  <label className="field" style={{ gridColumn: '1 / -1' }}><span>Descripción</span><input name="description" placeholder="Descripción" /></label>
                </div>
                <button className="btn sm" disabled={busy} style={{ marginTop: 12 }}>Crear servicio</button>
              </form>
              <form onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.createInvoice({ patientId: (fd.get('patientId') as string) || null, encounterId: (fd.get('encounterId') as string) || null, dueDate: fd.get('dueDate') || null, items: [{ description: fd.get('desc'), quantity: Number(fd.get('qty') || 1), unitPrice: Number(fd.get('price') || 0), discountAmount: 0, taxAmount: Number(fd.get('tax') || 0) }] }), f); }}>
                <div className="panel-sec-t" style={{ paddingTop: 0 }}>Nueva factura (borrador)</div>
                <div className="fields-grid">
                  <PatientPicker name="patientId" allowEmpty emptyLabel="Sin paciente asociado" style={{ gridColumn: '1 / -1' }} />
                  <label className="field" style={{ gridColumn: '1 / -1' }}><span>Descripción del ítem</span><input name="desc" placeholder="Descripción del ítem" required /></label>
                  <label className="field"><span>Cantidad</span><input name="qty" type="number" defaultValue={1} /></label>
                  <label className="field"><span>Precio unitario</span><input name="price" type="number" placeholder="Precio" /></label>
                  <label className="field"><span>Impuesto</span><input name="tax" type="number" placeholder="Impuesto" /></label>
                  <label className="field"><span>Vencimiento</span><input name="dueDate" type="date" /></label>
                </div>
                <button className="btn sm" disabled={busy} style={{ marginTop: 12 }}>Crear factura</button>
              </form>
            </div>
          </div>
          <div className="panel-sec">
            <div className="panel-sec-t">Facturas</div>
            <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
              <table><thead><tr><th>Número</th><th>Estado</th><th className="num">Total</th><th className="num">Saldo</th></tr></thead><tbody>
                {invoices.length === 0 ? <tr><td colSpan={4}><div className="empty">Sin facturas emitidas.</div></td></tr> :
                  invoices.map(i => {
                    const arRow = ar.find((r: any) => r.invoice_number === i.invoiceNumber);
                    const saldo = arRow ? Number(arRow.balance_amount ?? 0) : (String(i.status).toUpperCase() === 'PAID' ? 0 : null);
                    return <tr key={i.id}><td className="strong">{i.invoiceNumber}</td><td><Badge v={i.status} /></td><td className="num">{money(i.totalAmount)}</td><td className="num">{saldo == null ? '—' : money(saldo)}</td></tr>;
                  })}
              </tbody></table>
            </div>
            <div className="panel-sec-t">Cartera (cuentas por cobrar)</div>
            <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
              <table><thead><tr><th>Factura</th><th>Estado</th><th className="num">Saldo</th></tr></thead><tbody>
                {ar.length === 0 ? <tr><td colSpan={3}><div className="empty">Sin cartera pendiente.</div></td></tr> :
                  ar.map((r: any, i: number) => <tr key={i}><td className="strong">{r.invoice_number}</td><td><Badge v={r.status} /></td><td className="num">{money(r.balance_amount)}</td></tr>)}
              </tbody></table>
            </div>
            <div className="panel-sec-t">Servicios del tarifario</div>
            <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
              <table><thead><tr><th>Código</th><th>Nombre</th></tr></thead><tbody>
                {services.length === 0 ? <tr><td colSpan={2}><div className="empty">Sin servicios en el tarifario.</div></td></tr> :
                  services.map(s => <tr key={s.id}><td className="strong">{s.code}</td><td>{s.name}</td></tr>)}
              </tbody></table>
            </div>
          </div>
        </section>}

        {tab === 'waiting' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="list" /> Lista de espera</div><div className="panel-d">{waiting.length} pacientes en espera</div></div></div>
          <div className="panel-b" style={{ paddingBottom: 10 }}>
            <form className="toolbar" onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.createWaiting({ patientId: fd.get('patientId'), specialtyId: fd.get('specialtyId'), professionalId: (fd.get('professionalId') as string) || null, siteId: (fd.get('siteId') as string) || null, priority: Number(fd.get('priority') || 3), desiredFrom: fd.get('desiredFrom') || null, desiredTo: fd.get('desiredTo') || null, notes: fd.get('notes') || null }), f); }}>
              <PatientPicker name="patientId" required />
              <label className="field"><span>Especialidad</span><select name="specialtyId" required><option value="">Seleccionar…</option>{specialties.filter(s => selected.includes(String(s.id))).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
              <label className="field"><span>Prioridad</span><select name="priority"><option value="1">1 · Alta</option><option value="3">3 · Media</option><option value="5">5 · Baja</option></select></label>
              <label className="field"><span>Desde</span><input name="desiredFrom" type="date" /></label>
              <label className="field"><span>Hasta</span><input name="desiredTo" type="date" /></label>
              <label className="field"><span>Notas</span><input name="notes" placeholder="Notas" /></label>
              <button className="btn" disabled={busy}><Ico name="list" size={14} /> Agregar</button>
            </form>
          </div>
          <div className="table-wrap">
            <table><thead><tr><th>Prioridad</th><th>Estado</th><th>Desde</th><th>Notas</th></tr></thead><tbody>
              {waiting.length === 0 ? <tr><td colSpan={4}><div className="empty">Sin pacientes en lista de espera.</div></td></tr> :
                waiting.map(w => <tr key={w.id}><td><span className={'badge ' + (w.priority <= 2 ? 'red' : w.priority <= 3 ? 'amber' : 'slate')}>Prioridad {w.priority}</span></td><td><Badge v={w.status} /></td><td>{w.desiredFrom ? d(w.desiredFrom) : '—'}</td><td>{w.notes || '—'}</td></tr>)}
            </tbody></table>
          </div>
        </section>}

        {tab === 'reminders' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="bell" /> Recordatorios</div><div className="panel-d">{reminders.length} recordatorios programados</div></div></div>
          <div className="panel-b" style={{ paddingBottom: 10 }}>
            <form className="toolbar" onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.createReminder({ appointmentId: fd.get('appointmentId'), channel: fd.get('channel') || 'EMAIL', scheduledAt: new Date(fd.get('scheduledAt') as string).toISOString(), recipient: fd.get('recipient') || null }), f); }}>
              <label className="field" style={{ flex: '2 1 240px', maxWidth: 320 }}><span>Cita</span><select name="appointmentId" required><option value="">Seleccionar…</option>{appts.map(a => <option key={a.id} value={a.id}>{a.appointmentNumber} · {dt(a.startsAt)}</option>)}</select></label>
              <label className="field"><span>Canal</span><select name="channel"><option>EMAIL</option><option>SMS</option><option>WHATSAPP</option><option>PUSH</option></select></label>
              <label className="field"><span>Programado</span><input name="scheduledAt" type="datetime-local" required /></label>
              <label className="field"><span>Destinatario</span><input name="recipient" placeholder="Destinatario" /></label>
              <button className="btn" disabled={busy}><Ico name="bell" size={14} /> Programar</button>
            </form>
          </div>
          <div className="table-wrap">
            <table><thead><tr><th>Canal</th><th>Programado</th><th>Estado</th><th>Destinatario</th></tr></thead><tbody>
              {reminders.length === 0 ? <tr><td colSpan={4}><div className="empty">Sin recordatorios programados.</div></td></tr> :
                reminders.map(r => <tr key={r.id}><td><Badge v={r.channel} /></td><td>{dt(r.scheduledAt)}</td><td><Badge v={r.status} /></td><td>{r.recipient || '—'}</td></tr>)}
            </tbody></table>
          </div>
          <div className="panel-b" style={{ borderTop: '1px solid var(--line-soft)' }}><p className="muted">El envío se procesa de forma asíncrona (worker/cola) según la configuración del tenant.</p></div>
        </section>}

        {tab === 'lab' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="flask" /> Laboratorio</div><div className="panel-d">{labOrders.length} órdenes · {labTests.length} pruebas en catálogo</div></div></div>
          <div className="panel-b" style={{ paddingBottom: 10 }}>
            <form className="toolbar" onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.createLabOrder({ patientId: fd.get('patientId'), encounterId: (fd.get('encounterId') as string) || null, notes: fd.get('notes') || null }), f); }}>
              <PatientPicker name="patientId" required />
              <label className="field" style={{ flex: '2 1 240px', maxWidth: 320 }}><span>Atención</span><select name="encounterId"><option value="">Opcional</option>{encounters.map(e2 => <option key={e2.id} value={e2.id}>{dt(e2.encounterDate)}</option>)}</select></label>
              <label className="field"><span>Notas</span><input name="notes" placeholder="Notas" /></label>
              <button className="btn" disabled={busy}><Ico name="flask" size={14} /> Crear orden</button>
            </form>
          </div>
          <div className="panel-sec">
            <div className="panel-sec-t">Órdenes</div>
            <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
              <table><thead><tr><th>Fecha</th><th>Estado</th><th>Notas</th></tr></thead><tbody>
                {labOrders.length === 0 ? <tr><td colSpan={3}><div className="empty">Sin órdenes de laboratorio.</div></td></tr> :
                  labOrders.map(o => <tr key={o.id}><td className="strong">{dt(o.orderedAt)}</td><td><Badge v={o.status} /></td><td>{o.notes || '—'}</td></tr>)}
              </tbody></table>
            </div>
            <div className="panel-sec-t">Catálogo de pruebas</div>
            <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
              <table><thead><tr><th>Código</th><th>Nombre</th><th>Muestra</th></tr></thead><tbody>
                {labTests.length === 0 ? <tr><td colSpan={3}><div className="empty">Catálogo no disponible.</div></td></tr> :
                  labTests.slice(0, 50).map(t => <tr key={t.id}><td className="strong">{t.code}</td><td>{t.name}</td><td>{t.sampleType || '—'}</td></tr>)}
              </tbody></table>
            </div>
          </div>
        </section>}

        {tab === 'prescriptions' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="pill" /> Recetas y fórmulas</div><div className="panel-d">{rx.length} recetas emitidas</div></div></div>
          <div className="panel-b">
            <form onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.createPrescription({ encounterId: fd.get('encounterId'), patientId: fd.get('patientId'), notes: fd.get('notes') || null, items: [{ medicationName: fd.get('medicationName'), dose: fd.get('dose') || null, frequency: fd.get('frequency') || null, route: fd.get('route') || null, duration: fd.get('duration') || null, quantity: Number(fd.get('quantity') || 1), instructions: fd.get('instructions') || null }] }), f); }}>
              <div className="fields-grid">
                <PatientPicker name="patientId" required />
                <label className="field" style={{ gridColumn: 'span 2' }}><span>Atención</span><select name="encounterId" required><option value="">Seleccionar…</option>{encounters.map(e2 => <option key={e2.id} value={e2.id}>{dt(e2.encounterDate)} · {pretty(e2.encounterType)}</option>)}</select></label>
                <label className="field"><span>Medicamento</span><input name="medicationName" placeholder="Medicamento" required /></label>
                <label className="field"><span>Dosis</span><input name="dose" placeholder="500 mg" /></label>
                <label className="field"><span>Frecuencia</span><input name="frequency" placeholder="c/8 h" /></label>
                <label className="field"><span>Vía</span><input name="route" placeholder="Oral" /></label>
                <label className="field"><span>Duración</span><input name="duration" placeholder="7 días" /></label>
                <label className="field"><span>Cantidad</span><input name="quantity" type="number" defaultValue={1} /></label>
                <label className="field" style={{ gridColumn: '1 / -1' }}><span>Indicaciones</span><input name="instructions" placeholder="Indicaciones para el paciente" /></label>
              </div>
              <button className="btn" disabled={busy} style={{ marginTop: 12 }}><Ico name="pill" size={14} /> Emitir receta</button>
            </form>
          </div>
          <div className="table-wrap">
            <table><thead><tr><th>Número</th><th>Estado</th><th>Fecha</th></tr></thead><tbody>
              {rx.length === 0 ? <tr><td colSpan={3}><div className="empty">Sin recetas emitidas.</div></td></tr> :
                rx.map(r => <tr key={r.id}><td className="strong">{r.prescriptionNumber}</td><td><Badge v={r.status} /></td><td>{dt(r.issuedAt)}</td></tr>)}
            </tbody></table>
          </div>
        </section>}

        {tab === 'eps' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="card" /> EPS, contratos y pagos</div><div className="panel-d">{insurers.length} aseguradoras · {contracts.length} contratos · {auths.length} autorizaciones</div></div></div>
          <div className="panel-b" style={{ paddingBottom: 10 }}>
            <form className="toolbar" onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.createPayment({ invoiceId: (fd.get('invoiceId') as string) || null, amount: Number(fd.get('amount')), paymentMethod: fd.get('paymentMethod') || 'CASH', reference: fd.get('reference') || null }), f); }}>
              <label className="field"><span>Factura</span><select name="invoiceId"><option value="">Seleccionar…</option>{invoices.map(i => <option key={i.id} value={i.id}>{i.invoiceNumber}</option>)}</select></label>
              <label className="field"><span>Monto</span><input name="amount" type="number" step="0.01" placeholder="Monto" required /></label>
              <label className="field"><span>Método</span><select name="paymentMethod"><option>CASH</option><option>CARD</option><option>TRANSFER</option><option>OTHER</option></select></label>
              <label className="field"><span>Referencia</span><input name="reference" placeholder="Referencia" /></label>
              <button className="btn" disabled={busy}><Ico name="card" size={14} /> Registrar pago</button>
            </form>
          </div>
          <div className="panel-sec">
            <div className="panel-sec-t">Pagos</div>
            <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
              <table><thead><tr><th>Fecha</th><th className="num">Monto</th><th>Método</th><th>Estado</th></tr></thead><tbody>
                {payments.length === 0 ? <tr><td colSpan={4}><div className="empty">Sin pagos registrados.</div></td></tr> :
                  payments.map(p => <tr key={p.id}><td className="strong">{d(p.paymentDate)}</td><td className="num">{money(p.amount)}</td><td><Badge v={p.paymentMethod} /></td><td><Badge v={p.status} /></td></tr>)}
              </tbody></table>
            </div>
            <div className="panel-sec-t">Autorizaciones</div>
            <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
              <table><thead><tr><th>Número</th><th>Estado</th><th>Solicitada</th></tr></thead><tbody>
                {auths.length === 0 ? <tr><td colSpan={3}><div className="empty">Sin autorizaciones registradas.</div></td></tr> :
                  auths.map(a => <tr key={a.id}><td className="strong">{a.authorizationNumber}</td><td><Badge v={a.status} /></td><td>{a.requestedAt ? dt(a.requestedAt) : '—'}</td></tr>)}
              </tbody></table>
            </div>
          </div>
        </section>}

        {tab === 'interop' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="share" /> Interoperabilidad</div><div className="panel-d">Eventos, transmisiones y documentos RDA · integraciones vía adaptadores y cola asíncrona</div></div></div>
          <div className="panel-sec">
            <div className="panel-sec-t">Eventos ({interopEvents.length})</div>
            <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
              <table><thead><tr><th>Tipo</th><th>Entidad</th><th>Estado</th><th>Fecha</th></tr></thead><tbody>
                {interopEvents.length === 0 ? <tr><td colSpan={4}><div className="empty">Sin eventos registrados.</div></td></tr> :
                  interopEvents.map(e2 => <tr key={e2.id}><td className="strong">{pretty(e2.eventType)}</td><td>{pretty(e2.entityType)}</td><td><Badge v={e2.status} /></td><td>{dt(e2.createdAt)}</td></tr>)}
              </tbody></table>
            </div>
            <div className="panel-sec-t">Transmisiones ({transmissions.length})</div>
            <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
              <table><thead><tr><th>Tipo</th><th>Destino</th><th>Estado</th><th>Referencia</th></tr></thead><tbody>
                {transmissions.length === 0 ? <tr><td colSpan={4}><div className="empty">Sin transmisiones registradas.</div></td></tr> :
                  transmissions.map(t => <tr key={t.id}><td className="strong">{pretty(t.transmissionType)}</td><td>{t.targetSystem || '—'}</td><td><Badge v={t.status} /></td><td>{shortId(t.referenceId)}</td></tr>)}
              </tbody></table>
            </div>
            <div className="panel-sec-t">Documentos RDA ({rda.length})</div>
            <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
              <table><thead><tr><th>Tipo</th><th>Estado</th><th>Generado</th></tr></thead><tbody>
                {rda.length === 0 ? <tr><td colSpan={3}><div className="empty">Sin documentos RDA generados.</div></td></tr> :
                  rda.map(r => <tr key={r.id}><td className="strong">{pretty(r.documentType)}</td><td><Badge v={r.status} /></td><td>{r.generatedAt ? dt(r.generatedAt) : '—'}</td></tr>)}
              </tbody></table>
            </div>
          </div>
        </section>}

        {tab === 'audit' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="shield" /> Auditoría y accesos</div><div className="panel-d">{audit.length} eventos de auditoría · {access.length} accesos registrados</div></div></div>
          <div className="panel-sec">
            <div className="panel-sec-t">Bitácora de auditoría</div>
            <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
              <table><thead><tr><th>Fecha</th><th>Entidad</th><th>Acción</th></tr></thead><tbody>
                {audit.length === 0 ? <tr><td colSpan={3}><div className="empty">Sin eventos de auditoría.</div></td></tr> :
                  audit.map(a => <tr key={a.id}><td className="strong">{dt(a.occurredAt)}</td><td>{pretty(a.entityType)}</td><td><Badge v={a.action} /></td></tr>)}
              </tbody></table>
            </div>
            <div className="panel-sec-t">Accesos</div>
            <div className="table-wrap" style={{ border: '1px solid var(--line-soft)', borderRadius: 10 }}>
              <table><thead><tr><th>Fecha</th><th>Recurso</th><th>Acción</th></tr></thead><tbody>
                {access.length === 0 ? <tr><td colSpan={3}><div className="empty">Sin accesos registrados.</div></td></tr> :
                  access.map(a => <tr key={a.id}><td className="strong">{dt(a.occurredAt || a.accessedAt)}</td><td>{pretty(a.resourceType)}</td><td><Badge v={a.action} /></td></tr>)}
              </tbody></table>
            </div>
          </div>
        </section>}

        {tab === 'documents' && <section className="panel">
          <div className="panel-h"><div><div className="panel-t"><Ico name="folder" /> Documentos</div><div className="panel-d">{docs.length} documentos registrados</div></div></div>
          <div className="panel-b" style={{ paddingBottom: 10 }}>
            <form className="toolbar" onSubmit={e => { e.preventDefault(); const f = e.currentTarget, fd = new FormData(f); run(() => api.createDocument({ patientId: (fd.get('patientId') as string) || null, encounterId: null, title: fd.get('title'), description: fd.get('description') || null, storageKey: fd.get('storageKey') || null, mimeType: fd.get('mimeType') || 'application/pdf', sizeBytes: Number(fd.get('sizeBytes') || 0), isPublicToPatient: fd.get('isPublic') === 'on' }), f); }}>
              <PatientPicker name="patientId" allowEmpty emptyLabel="Sin asociar" />
              <label className="field"><span>Título</span><input name="title" placeholder="Título" required /></label>
              <label className="field"><span>Tipo MIME</span><input name="mimeType" defaultValue="application/pdf" /></label>
              <label className="field"><span>Tamaño (bytes)</span><input name="sizeBytes" type="number" defaultValue={0} /></label>
              <label className="field"><span>Clave de almacenamiento</span><input name="storageKey" placeholder="storage_key" /></label>
              <label className="check" style={{ marginBottom: 8 }}><input name="isPublic" type="checkbox" /> Visible al paciente</label>
              <button className="btn" disabled={busy}><Ico name="folder" size={14} /> Registrar</button>
            </form>
          </div>
          <div className="table-wrap">
            <table><thead><tr><th>Título</th><th>Tipo</th><th>Estado</th><th>Fecha</th></tr></thead><tbody>
              {docs.length === 0 ? <tr><td colSpan={4}><div className="empty">Sin documentos registrados.</div></td></tr> :
                docs.map(d2 => <tr key={d2.id}><td className="strong">{d2.title}</td><td>{d2.mimeType}</td><td><Badge v={d2.status} /></td><td>{dt(d2.createdAt)}</td></tr>)}
            </tbody></table>
          </div>
          <div className="panel-b" style={{ borderTop: '1px solid var(--line-soft)' }}><p className="muted">Los archivos físicos se almacenan en un almacenamiento privado compatible con S3; aquí se gestionan metadatos, versiones y permisos.</p></div>
        </section>}
      </div>
    </div>
  </div>;
}

function num(v: FormDataEntryValue | null) { if (v === null || v === '') return null; const n = Number(v); return Number.isFinite(n) ? n : null; }
function int(v: FormDataEntryValue | null) { if (v === null || v === '') return null; const n = parseInt(String(v), 10); return Number.isFinite(n) ? n : null; }
