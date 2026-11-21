import { useCallback, useEffect, useState } from 'react';
import './App.css';
import { api, clearSession, getSession, setSession } from './api';
import PasswordRecovery from './PasswordRecovery';
import SecurityPanel from './SecurityPanel';

const icons: Record<string, string> = {
  home: 'M3 11.2 12 4l9 7.2M5.5 9.8V20h13V9.8M10 20v-5.5h4V20',
  calendar: 'M4 6.5A1.5 1.5 0 0 1 5.5 5h13A1.5 1.5 0 0 1 20 6.5V19a1.5 1.5 0 0 1-1.5 1.5h-13A1.5 1.5 0 0 1 4 19zM4 10h16M8.5 3v4M15.5 3v4',
  folder: 'M3 6.8A1.8 1.8 0 0 1 4.8 5h4.4L11.5 7.5h7.7A1.8 1.8 0 0 1 21 9.3v8A1.8 1.8 0 0 1 19.2 19H4.8A1.8 1.8 0 0 1 3 17.3zM3 9.5h18',
  flask: 'M9.5 3h5M10.5 3v5.2l-5.3 9.2A2.4 2.4 0 0 0 7.2 21h9.6a2.4 2.4 0 0 0 2-3.6l-5.3-9.2V3M8 15h8',
  pill: 'M8.8 15.2 5.4 18.6a4.3 4.3 0 0 1-6-6l7.2-7.2a4.3 4.3 0 0 1 6 6l-3.4 3.4zM9.5 14.5l5-5',
  user: 'M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM4.5 20.5c.9-3.8 3.9-6 7.5-6s6.6 2.2 7.5 6',
  logout: 'M9.5 4H6A1.5 1.5 0 0 0 4.5 5.5v13A1.5 1.5 0 0 0 6 20h3.5M15.5 16l4-4-4-4M19 12H9',
  cross: 'M10 3h4v7h7v4h-7v7h-4v-7H3v-4h7z',
  check: 'M4.5 12.5 10 18 19.5 7',
  bell: 'M6 9.5a6 6 0 0 1 12 0c0 4.6 1.8 5.8 1.8 5.8H4.2S6 14.1 6 9.5M10.3 19.5a2 2 0 0 0 3.4 0',
  shield: 'M12 3 5 5.6v5.5c0 4.6 3 7.8 7 9.9 4-2.1 7-5.3 7-9.9V5.6zM9.2 12l2 2 3.7-3.9',
  download: 'M12 4v10m0 0-4-4m4 4 4-4M5 19h14',
};

type Section = 'inicio' | 'citas' | 'documentos' | 'resultados' | 'formulas' | 'datos';

const SECTIONS: { id: Section; label: string; icon: string }[] = [
  { id: 'inicio', label: 'Inicio', icon: 'home' },
  { id: 'citas', label: 'Mis citas', icon: 'calendar' },
  { id: 'documentos', label: 'Documentos', icon: 'folder' },
  { id: 'resultados', label: 'Resultados', icon: 'flask' },
  { id: 'formulas', label: 'Fórmulas médicas', icon: 'pill' },
  { id: 'datos', label: 'Mis datos', icon: 'user' },
];

function Ico({ name, size = 17 }: { name: string; size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor"
      strokeWidth={1.7} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d={icons[name] || icons.home} />
    </svg>
  );
}

function initials(n?: string) {
  return (n || '?').split(/\s+/).filter(Boolean).slice(0, 2).map(x => x[0]?.toUpperCase()).join('') || '?';
}
function shortId(v?: string) { return v ? String(v).slice(0, 8) + '…' : '—'; }
function dt(v?: string | null) {
  if (!v) return '—';
  const date = new Date(v);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString('es-CO', { dateStyle: 'medium', timeStyle: 'short' });
}
function day(v?: string | null) {
  if (!v) return '—';
  const date = new Date(v);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString('es-CO', { dateStyle: 'long' });
}
function tone(status?: string) {
  const s = String(status || '').toUpperCase();
  if (/(PAID|COMPLETED|SIGNED|ACTIVE|CONFIRMED|FINALIZED|ISSUED|DELIVERED|SENT|RESULTED|AVAILABLE)/.test(s)) return 'green';
  if (/(CANCEL|REJECT|EXPIR|BLOCK|CRITICAL|SUSPEND|FAILED|DENIED|OVERDUE)/.test(s)) return 'red';
  if (/(PEND|REQUEST|IN_PROGRESS|DRAFT|SCHEDULED|WAITING|RECEIVED|PROCESSING|QUEUED|OPEN)/.test(s)) return 'amber';
  return 'slate';
}
function Badge({ value }: { value?: string | null }) {
  if (!value) return <span className="muted">—</span>;
  const label = String(value).toLowerCase().replace(/_/g, ' ');
  return <span className={`badge ${tone(value)}`}>{label.charAt(0).toUpperCase() + label.slice(1)}</span>;
}
function size(bytes?: number) {
  if (!bytes || bytes <= 0) return '—';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
}

type Patient = {
  id: string;
  patientNumber?: string | null;
  fullName?: string | null;
  documentType?: string | null;
  documentNumber?: string | null;
  birthDate?: string | null;
  sexAtBirth?: string | null;
  email?: string | null;
  phone?: string | null;
  mobilePhone?: string | null;
  addressLine?: string | null;
  city?: string | null;
  department?: string | null;
  tenantName?: string | null;
  tenantCode?: string | null;
};

export default function App() {
  const brand = import.meta.env.VITE_BRAND || 'SIGM Pacientes';
  const product = import.meta.env.VITE_PRODUCT || 'SIGM Cloud';
  const [session, setSessionState] = useState(getSession());
  const [section, setSection] = useState<Section>('inicio');
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [mfaNeeded, setMfaNeeded] = useState(false);
  const [recover, setRecover] = useState(false);

  const [patient, setPatient] = useState<Patient | null>(null);
  const [appointments, setAppointments] = useState<any[]>([]);
  const [documents, setDocuments] = useState<any[]>([]);
  const [prescriptions, setPrescriptions] = useState<any[]>([]);
  const [labOrders, setLabOrders] = useState<any[]>([]);
  const [consents, setConsents] = useState<any[]>([]);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    const results = await Promise.allSettled([
      api.portalMe(),
      api.portalAppointments(),
      api.portalDocuments(),
      api.portalPrescriptions(),
      api.portalLabResults(),
      api.portalConsents(),
    ]);
    const [me, appts, docs, rx, labs, consentRows] = results;
    if (me.status === 'fulfilled') setPatient(me.value);
    else setError(me.reason instanceof Error ? me.reason.message : 'No fue posible cargar su información.');
    if (appts.status === 'fulfilled') setAppointments(appts.value);
    if (docs.status === 'fulfilled') setDocuments(docs.value);
    if (rx.status === 'fulfilled') setPrescriptions(rx.value);
    if (labs.status === 'fulfilled') setLabOrders(labs.value);
    if (consentRows.status === 'fulfilled') setConsents(consentRows.value);
    setLoading(false);
  }, []);

  useEffect(() => {
    if (session?.accessToken) void load();
  }, [session?.accessToken, load]);

  async function onLogin(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError('');
    setBusy(true);
    const fd = new FormData(e.currentTarget);
    try {
      const res = await api.login({
        email: fd.get('email'),
        password: fd.get('password'),
        tenantId: (fd.get('tenantId') as string) || undefined,
        mfaCode: (fd.get('mfaCode') as string) || undefined,
      });
      setSession(res);
      setSessionState(res);
    } catch (err: any) {
      if (err?.status === 401 && err?.data?.mfaRequired) {
        setMfaNeeded(true);
        setError('');
      } else {
        setError(err.message);
      }
    } finally {
      setBusy(false);
    }
  }

  async function download(document: any) {
    setBusy(true);
    setError('');
    try {
      const blob = await api.portalDownloadDocument(document.id);
      const url = URL.createObjectURL(blob);
      const link = window.document.createElement('a');
      link.href = url;
      link.download = document.title || 'documento';
      link.click();
      setTimeout(() => URL.revokeObjectURL(url), 30_000);
    } catch (err: any) {
      setError(err.message || String(err));
    } finally {
      setBusy(false);
    }
  }

  async function updateConsent(consentType: string, granted: boolean) {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      await api.portalUpdateConsent(consentType, granted);
      setNotice(granted ? 'Consentimiento registrado. Gracias.' : 'Consentimiento revocado.');
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No fue posible registrar el consentimiento.');
    } finally {
      setBusy(false);
    }
  }

  async function saveContact(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    setBusy(true);
    setError('');
    setNotice('');
    try {
      await api.portalUpdateContact({
        phone: (fd.get('phone') as string) || null,
        mobilePhone: (fd.get('mobilePhone') as string) || null,
        addressLine: (fd.get('addressLine') as string) || null,
        city: (fd.get('city') as string) || null,
        department: (fd.get('department') as string) || null,
      });
      setNotice('Datos de contacto actualizados. Los datos clínicos solo los modifica su institución.');
      await load();
    } catch (err: any) {
      setError(err.message || String(err));
    } finally {
      setBusy(false);
    }
  }

  if (!session?.accessToken) {
    return <div className="auth">
      <aside className="auth-brand">
        <div className="auth-brand-head">
          <div className="auth-brand-mark"><Ico name="cross" size={22} /></div>
          <div><div className="auth-brand-title">{brand}</div><div className="auth-brand-sub">{product}</div></div>
        </div>
        <div className="auth-hero">
          <h1>Su salud,<br />en sus manos</h1>
          <p>Consulte sus citas, documentos y resultados autorizados desde cualquier lugar, con la seguridad de que sus datos clínicos están protegidos.</p>
          <div className="auth-feats">
            <div className="auth-feat"><div className="auth-feat-ico"><Ico name="check" size={14} /></div><div><b>Acceso seguro y privado</b><span>Sus registros clínicos no son editables desde el portal.</span></div></div>
            <div className="auth-feat"><div className="auth-feat-ico"><Ico name="check" size={14} /></div><div><b>Información siempre disponible</b><span>Citas, fórmulas y resultados en un solo lugar.</span></div></div>
            <div className="auth-feat"><div className="auth-feat-ico"><Ico name="check" size={14} /></div><div><b>Aislamiento por organización</b><span>Sus datos pertenecen únicamente a su institución.</span></div></div>
          </div>
        </div>
        <div className="auth-foot">© {new Date().getFullYear()} {product} · Portal del paciente</div>
      </aside>
      <main className="auth-panel">
        <div className="auth-card">
          {recover ? <PasswordRecovery onBack={() => { setRecover(false); setError(''); }} /> : <>
          {error && <div className="alert error"><Ico name="logout" size={16} /> {error}</div>}
          <h2>Acceso de paciente</h2>
          <p className="sub">Ingrese con las credenciales de su cuenta de paciente.</p>
          <form onSubmit={onLogin}>
            <label className="field"><span>Correo electrónico</span><input name="email" type="email" placeholder="correo@ejemplo.com" required /></label>
            <label className="field"><span>Contraseña</span><input name="password" type="password" placeholder="••••••••" required /></label>
            <label className="field"><span>Identificador de su organización</span><input name="tenantId" placeholder="CLINIC-DEMO" required /></label>
            {mfaNeeded && <label className="field"><span>Código de verificación o de recuperación</span><input name="mfaCode" autoComplete="one-time-code" placeholder="123456" autoFocus required /></label>}
            <button className="btn" disabled={busy}>{busy ? 'Verificando…' : mfaNeeded ? 'Verificar e ingresar' : 'Entrar'}</button>
          </form>
          <p className="auth-alt"><a href="#" onClick={e => { e.preventDefault(); setRecover(true); setError(''); }}>¿Olvidó su contraseña?</a></p>
          <p className="auth-alt">La activación de cuentas la gestiona su clínica o consultorio.</p>
          <p className="auth-alt"><a href="/">← Volver al inicio</a></p>
          </>}
        </div>
      </main>
    </div>;
  }

  const now = Date.now();
  const upcoming = appointments
    .filter((a) => new Date(a.startsAt).getTime() >= now - 60 * 60 * 1000 && String(a.status).toUpperCase() !== 'CANCELLED')
    .sort((a, b) => new Date(a.startsAt).getTime() - new Date(b.startsAt).getTime());
  const past = appointments.filter((a) => new Date(a.startsAt).getTime() < now - 60 * 60 * 1000);

  const counts: Record<Section, number | null> = {
    inicio: null,
    citas: upcoming.length,
    documentos: documents.length,
    resultados: labOrders.length,
    formulas: prescriptions.length,
    datos: null,
  };

  return <div className="app">
    <aside className="sidebar">
      <div className="brand">
        <div className="brand-mark"><Ico name="cross" size={20} /></div>
        <div><div className="brand-name">{brand}</div><div className="brand-sub">{product}</div></div>
      </div>
      <nav className="nav">
        <div className="nav-group">
          <div className="nav-label">Mi portal</div>
          {SECTIONS.map((item) => (
            <button
              key={item.id}
              type="button"
              className={'nav-item' + (section === item.id ? ' active' : '')}
              onClick={() => { setSection(item.id); setNotice(''); }}
            >
              <Ico name={item.icon} />
              {item.label}
              {counts[item.id] != null && counts[item.id]! > 0 && (
                <span className="muted" style={{ marginLeft: 'auto', fontSize: 11.5 }}>{counts[item.id]}</span>
              )}
            </button>
          ))}
        </div>
      </nav>
      <div className="sidebar-foot">SIGM Cloud · v1.0 · Datos ficticios</div>
    </aside>

    <div className="main">
      <header className="topbar">
        <div className="crumb">{product} / {SECTIONS.find((s) => s.id === section)?.label}<b>Mi portal</b></div>
        <div className="topbar-right">
          <span className="tenant-chip" title={patient?.tenantCode ?? session.tenantId}>
            <Ico name="shield" size={12} /> {patient?.tenantCode ?? shortId(session.tenantId)}
          </span>
          <div className="user">
            <div className="avatar">{initials(session.fullName)}</div>
            <div><div className="user-name">{session.fullName}</div><div className="user-role">Paciente</div></div>
          </div>
          <button className="btn ghost sm" onClick={() => { clearSession(); setSessionState(null); setPatient(null); }}>
            <Ico name="logout" size={14} /> Salir
          </button>
        </div>
      </header>

      <div className="content">
        {error && <div className="alert error">{error}</div>}
        {notice && <div className="alert">{notice}</div>}

        {section === 'inicio' && (
          <>
            <section className="panel">
              <div className="panel-h">
                <div>
                  <div className="panel-t"><Ico name="user" /> Bienvenido, {(patient?.fullName || session.fullName).split(' ')[0]}</div>
                  <div className="panel-d">
                    {patient
                      ? `Historia ${patient.patientNumber ?? '—'} · ${patient.tenantName ?? product}`
                      : 'Cargando su información…'}
                  </div>
                </div>
                {patient && <span className="badge green">Verificado</span>}
              </div>
              <div className="panel-b">
                <div className="tiles">
                  <button className="tile" type="button" onClick={() => setSection('citas')}>
                    <div className="t-ico"><Ico name="calendar" size={20} /></div>
                    <div><b>Mis citas</b><br /><span>{upcoming.length} próxima(s) · {appointments.length} en total</span></div>
                  </button>
                  <button className="tile" type="button" onClick={() => setSection('documentos')}>
                    <div className="t-ico"><Ico name="folder" size={20} /></div>
                    <div><b>Documentos</b><br /><span>{documents.length} publicados para usted</span></div>
                  </button>
                  <button className="tile" type="button" onClick={() => setSection('resultados')}>
                    <div className="t-ico"><Ico name="flask" size={20} /></div>
                    <div><b>Resultados</b><br /><span>{labOrders.length} orden(es) de laboratorio</span></div>
                  </button>
                  <button className="tile" type="button" onClick={() => setSection('formulas')}>
                    <div className="t-ico"><Ico name="pill" size={20} /></div>
                    <div><b>Fórmulas médicas</b><br /><span>{prescriptions.length} prescripción(es)</span></div>
                  </button>
                  <button className="tile" type="button" onClick={() => setSection('datos')}>
                    <div className="t-ico"><Ico name="user" size={20} /></div>
                    <div><b>Mis datos</b><br /><span>Actualice sus datos de contacto</span></div>
                  </button>
                </div>
              </div>
            </section>

            {patient && (
              <section className="panel">
                <div className="panel-h">
                  <div><div className="panel-t">Identificación</div><div className="panel-d">Estos datos los administra su institución.</div></div>
                </div>
                <div className="panel-b">
                  <div className="fields-grid">
                    <label className="field"><span>Nombre completo</span><input value={patient.fullName ?? ''} readOnly /></label>
                    <label className="field"><span>Documento</span><input value={`${patient.documentType ?? ''} ${patient.documentNumber ?? ''}`.trim()} readOnly /></label>
                    <label className="field"><span>Nacimiento</span><input value={patient.birthDate ?? ''} readOnly /></label>
                    <label className="field"><span>Sexo</span><input value={patient.sexAtBirth ?? ''} readOnly /></label>
                  </div>
                </div>
              </section>
            )}
          </>
        )}

        {section === 'citas' && (
          <>
            <section className="panel">
              <div className="panel-h">
                <div><div className="panel-t"><Ico name="calendar" /> Próximas citas</div><div className="panel-d">Ordenadas por fecha. Para reprogramar contacte a su institución.</div></div>
                <button className="btn ghost sm" type="button" onClick={() => void load()} disabled={loading}>
                  {loading ? 'Actualizando…' : 'Actualizar'}
                </button>
              </div>
              <div className="table-wrap">
                <table>
                  <thead><tr><th>Fecha</th><th>Profesional</th><th>Especialidad</th><th>Sede</th><th>Estado</th><th>Motivo</th></tr></thead>
                  <tbody>
                    {upcoming.length === 0 && <tr><td colSpan={6}><div className="empty">No tiene citas próximas.</div></td></tr>}
                    {upcoming.map((a) => (
                      <tr key={a.id}>
                        <td className="strong">{day(a.startsAt)}<div className="muted" style={{ fontSize: 11.5 }}>{dt(a.startsAt)}</div></td>
                        <td>{a.professional ?? '—'}</td>
                        <td>{a.specialty ?? '—'}</td>
                        <td>{a.site ?? '—'}</td>
                        <td><Badge value={a.status} /></td>
                        <td>{a.reason ?? '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>

            <section className="panel">
              <div className="panel-h"><div><div className="panel-t">Historial de citas</div><div className="panel-d">{past.length} cita(s) anteriores</div></div></div>
              <div className="table-wrap">
                <table>
                  <thead><tr><th>Fecha</th><th>Profesional</th><th>Especialidad</th><th>Estado</th></tr></thead>
                  <tbody>
                    {past.length === 0 && <tr><td colSpan={4}><div className="empty">Sin citas anteriores.</div></td></tr>}
                    {past.map((a) => (
                      <tr key={a.id}>
                        <td className="strong">{dt(a.startsAt)}</td>
                        <td>{a.professional ?? '—'}</td>
                        <td>{a.specialty ?? '—'}</td>
                        <td><Badge value={a.status} /></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          </>
        )}

        {section === 'documentos' && (
          <section className="panel">
            <div className="panel-h">
              <div><div className="panel-t"><Ico name="folder" /> Documentos compartidos</div><div className="panel-d">Solo se muestran los documentos que su institución publicó para usted.</div></div>
              <button className="btn ghost sm" type="button" onClick={() => void load()} disabled={loading}>Actualizar</button>
            </div>
            <div className="table-wrap">
              <table>
                <thead><tr><th>Título</th><th>Descripción</th><th>Tipo</th><th>Tamaño</th><th>Publicado</th><th></th></tr></thead>
                <tbody>
                  {documents.length === 0 && <tr><td colSpan={6}><div className="empty">Aún no hay documentos publicados para usted.</div></td></tr>}
                  {documents.map((doc) => (
                    <tr key={doc.id}>
                      <td className="strong">{doc.title}<div className="muted" style={{ fontSize: 11.5 }}>v{doc.version}</div></td>
                      <td>{doc.description ?? '—'}</td>
                      <td>{doc.mimeType}</td>
                      <td>{size(doc.sizeBytes)}</td>
                      <td>{dt(doc.createdAt)}</td>
                      <td>
                        <button className="btn subtle sm" type="button" disabled={busy} onClick={() => void download(doc)}>
                          <Ico name="download" size={13} /> Descargar
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        )}

        {section === 'resultados' && (
          <section className="panel">
            <div className="panel-h">
              <div><div className="panel-t"><Ico name="flask" /> Resultados de laboratorio</div><div className="panel-d">Resultados entregados por su institución.</div></div>
              <button className="btn ghost sm" type="button" onClick={() => void load()} disabled={loading}>Actualizar</button>
            </div>
            <div className="panel-b">
              {labOrders.length === 0 && <div className="empty">No hay órdenes de laboratorio registradas para usted.</div>}
              {labOrders.map((order) => {
                const tests: any[] = safeParse(order.tests);
                return (
                  <div key={order.id} style={{ marginBottom: 18 }}>
                    <div className="panel-sec-t" style={{ borderTop: 0, paddingTop: 0 }}>
                      Orden {order.orderNumber ?? shortId(order.id)} · {dt(order.orderedAt)} <Badge value={order.status} />
                    </div>
                    <div className="table-wrap">
                      <table>
                        <thead><tr><th>Prueba</th><th>Resultado</th><th>Unidad</th><th>Referencia</th><th>Estado</th></tr></thead>
                        <tbody>
                          {tests.length === 0 && <tr><td colSpan={5}><div className="empty">Sin pruebas registradas.</div></td></tr>}
                          {tests.map((t, i) => (
                            <tr key={i}>
                              <td className="strong">{t.test ?? '—'}</td>
                              <td>{t.result ?? 'Pendiente'}{t.flag ? ` (${t.flag})` : ''}</td>
                              <td>{t.unit ?? '—'}</td>
                              <td>{t.reference ?? '—'}</td>
                              <td><Badge value={t.status} /></td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                );
              })}
            </div>
          </section>
        )}

        {section === 'formulas' && (
          <section className="panel">
            <div className="panel-h">
              <div><div className="panel-t"><Ico name="pill" /> Fórmulas médicas</div><div className="panel-d">Prescripciones emitidas a su nombre.</div></div>
              <button className="btn ghost sm" type="button" onClick={() => void load()} disabled={loading}>Actualizar</button>
            </div>
            <div className="panel-b">
              {prescriptions.length === 0 && <div className="empty">No tiene fórmulas registradas.</div>}
              {prescriptions.map((rx) => {
                const items: any[] = safeParse(rx.items);
                return (
                  <div key={rx.id} style={{ marginBottom: 18 }}>
                    <div className="panel-sec-t" style={{ borderTop: 0, paddingTop: 0 }}>
                      {rx.number ?? shortId(rx.id)} · {dt(rx.issuedAt)} <Badge value={rx.status} />
                    </div>
                    <div className="table-wrap">
                      <table>
                        <thead><tr><th>Medicamento</th><th>Dosis</th><th>Frecuencia</th><th>Vía</th><th>Duración</th><th>Indicaciones</th></tr></thead>
                        <tbody>
                          {items.length === 0 && <tr><td colSpan={6}><div className="empty">Sin ítems.</div></td></tr>}
                          {items.map((item, i) => (
                            <tr key={i}>
                              <td className="strong">{item.name}</td>
                              <td>{item.dose ?? '—'}</td>
                              <td>{item.frequency ?? '—'}</td>
                              <td>{item.route ?? '—'}</td>
                              <td>{item.durationDays != null ? `${item.durationDays} días` : '—'}</td>
                              <td>{item.instructions ?? '—'}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                    {rx.notes && <div className="muted" style={{ marginTop: 6 }}>Notas: {rx.notes}</div>}
                  </div>
                );
              })}
            </div>
          </section>
        )}

        {section === 'datos' && (
          <section className="panel">
            <div className="panel-h">
              <div><div className="panel-t"><Ico name="user" /> Mis datos</div><div className="panel-d">Puede actualizar sus datos de contacto. La información clínica solo la modifica su institución.</div></div>
            </div>
            <div className="panel-b">
              {patient ? (
                <form onSubmit={saveContact}>
                  <div className="fields-grid">
                    <label className="field"><span>Nombre completo</span><input value={patient.fullName ?? ''} readOnly /></label>
                    <label className="field"><span>Documento</span><input value={`${patient.documentType ?? ''} ${patient.documentNumber ?? ''}`.trim()} readOnly /></label>
                    <label className="field"><span>Correo (cuenta)</span><input value={patient.email ?? ''} readOnly /></label>
                    <label className="field"><span>Teléfono</span><input name="phone" defaultValue={patient.phone ?? ''} /></label>
                    <label className="field"><span>Celular</span><input name="mobilePhone" defaultValue={patient.mobilePhone ?? ''} /></label>
                    <label className="field"><span>Ciudad</span><input name="city" defaultValue={patient.city ?? ''} /></label>
                    <label className="field"><span>Departamento</span><input name="department" defaultValue={patient.department ?? ''} /></label>
                    <label className="field" style={{ gridColumn: '1 / -1' }}><span>Dirección</span><input name="addressLine" defaultValue={patient.addressLine ?? ''} /></label>
                  </div>
                  <div className="toolbar" style={{ paddingLeft: 0, paddingRight: 0 }}>
                    <button className="btn" disabled={busy}>{busy ? 'Guardando…' : 'Guardar cambios'}</button>
                  </div>
                </form>
              ) : (
                <div className="empty">No se encontró un registro de paciente asociado a su cuenta.</div>
              )}
            </div>
            <div style={{ marginTop: 24, borderTop: '1px solid #e2e8f0', paddingTop: 20 }}>
              <div className="panel-t"><Ico name="shield" /> Consentimientos informados</div>
              <div className="panel-d">
                Usted decide sobre el uso de sus datos personales y de su historia clínica. Puede aceptar o revocar cada
                consentimiento; la fecha, la versión y la evidencia quedan registradas.
              </div>
              <div style={{ marginTop: 14 }}>
                {consents.length === 0 && <div className="empty">No hay consentimientos disponibles.</div>}
                {consents.map((c) => (
                  <div
                    key={c.consentType}
                    style={{ display: 'flex', gap: 16, alignItems: 'flex-start', padding: '12px 0', borderTop: '1px solid #eef2f7' }}
                  >
                    <div style={{ flex: 1 }}>
                      <div className="strong">{c.label}</div>
                      <div className="muted" style={{ marginTop: 2 }}>{c.description}</div>
                      <div className="muted" style={{ marginTop: 4, fontSize: 12 }}>
                        Versión {c.version} · {c.granted
                          ? `Aceptado${c.grantedAt ? ` el ${dt(c.grantedAt)}` : ''}`
                          : c.revokedAt ? `Revocado el ${dt(c.revokedAt)}` : 'No otorgado'}
                      </div>
                    </div>
                    <button
                      type="button"
                      className={c.granted ? 'btn ghost sm' : 'btn sm'}
                      disabled={busy}
                      onClick={() => void updateConsent(c.consentType, !c.granted)}
                    >
                      {c.granted ? 'Revocar' : 'Aceptar'}
                    </button>
                  </div>
                ))}
              </div>
            </div>
            <SecurityPanel />
          </section>
        )}
      </div>
    </div>
  </div>;
}

function safeParse(value: unknown): any[] {
  if (Array.isArray(value)) return value;
  if (typeof value !== 'string') return [];
  try {
    const parsed = JSON.parse(value);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}
