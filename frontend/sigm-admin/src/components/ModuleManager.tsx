import { useCallback, useEffect, useState } from 'react';
import {
  api,
  clearSession,
  getSession,
  type AuditEvent,
  type ModuleInfo,
  type PlatformStats,
  type Session,
  type Tenant,
  type TenantDetail,
} from '../api';
import LoginView from './LoginView';
import DatabaseView from './DatabaseView';
import SecurityModal from './SecurityModal';

type Section = 'resumen' | 'clientes' | 'modulos' | 'bienvenida' | 'auditoria' | 'base_datos';

const SECTIONS: { id: Section; label: string; hint: string }[] = [
  { id: 'resumen', label: 'Resumen', hint: 'Indicadores de la plataforma' },
  { id: 'clientes', label: 'Clientes', hint: 'Tenants y sus módulos' },
  { id: 'modulos', label: 'Módulos', hint: 'Catálogo y activación por cliente' },
  { id: 'bienvenida', label: 'Página de bienvenida', hint: 'Contenido público' },
  { id: 'auditoria', label: 'Auditoría', hint: 'Eventos de autenticación' },
  { id: 'base_datos', label: 'Base de datos', hint: 'Scripts SQL y respaldos' },
];

const STATUS_LABEL: Record<string, { label: string; tone: string }> = {
  ACTIVE: { label: 'Activo', tone: 'green' },
  TRIAL: { label: 'Prueba', tone: 'blue' },
  SUSPENDED: { label: 'Suspendido', tone: 'amber' },
  CANCELLED: { label: 'Cancelado', tone: 'red' },
};

function initials(name: string) {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('');
}

function formatDate(value?: string | null) {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString('es-CO', { dateStyle: 'medium', timeStyle: 'short' });
}

function StatusBadge({ status }: { status: string }) {
  const meta = STATUS_LABEL[status] ?? { label: status, tone: 'slate' };
  return <span className={`badge ${meta.tone}`}>{meta.label}</span>;
}

export default function ModuleManager() {
  const [session, setSession] = useState<Session | null>(() => getSession());
  const [section, setSection] = useState<Section>('resumen');

  const [stats, setStats] = useState<PlatformStats | null>(null);
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [catalog, setCatalog] = useState<ModuleInfo[]>([]);
  const [audit, setAudit] = useState<AuditEvent[]>([]);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [moduleModal, setModuleModal] = useState<TenantDetail | null>(null);
  const [modalLoading, setModalLoading] = useState(false);
  const [savingModule, setSavingModule] = useState<string | null>(null);
  const [savingStatus, setSavingStatus] = useState<string | null>(null);

  const [welcomeTitle, setWelcomeTitle] = useState('Bienvenido a SIGM Cloud');
  const [welcomeMessage, setWelcomeMessage] = useState(
    'Plataforma integral de gestión médica para clínicas, consultorios y pacientes.',
  );
  const [welcomeLoading, setWelcomeLoading] = useState(false);
  const [welcomeSaving, setWelcomeSaving] = useState(false);
  const [welcomeNotice, setWelcomeNotice] = useState<string | null>(null);
  const [showSecurity, setShowSecurity] = useState(false);

  const loadCore = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [statsRes, tenantsRes, modulesRes] = await Promise.all([api.stats(), api.tenants(), api.modules()]);
      setStats(statsRes);
      setTenants(tenantsRes);
      setCatalog(modulesRes);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No fue posible cargar el panel.');
      if (!getSession()) setSession(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (session) void loadCore();
  }, [session, loadCore]);

  useEffect(() => {
    if (!session) return;
    if (section === 'bienvenida') {
      setWelcomeLoading(true);
      api
        .welcome()
        .then((content) => {
          if (content?.title) setWelcomeTitle(content.title);
          if (content?.message) setWelcomeMessage(content.message);
        })
        .catch(() => {
          /* El contenido por defecto se mantiene si el endpoint aún no está desplegado. */
        })
        .finally(() => setWelcomeLoading(false));
    }
    if (section === 'auditoria' && audit.length === 0) {
      api.audit().then(setAudit).catch(() => setAudit([]));
    }
  }, [section, session, audit.length]);

  const openModules = async (tenant: Tenant) => {
    setModalLoading(true);
    setError(null);
    try {
      const detail = await api.tenant(tenant.id);
      setModuleModal(detail);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No fue posible cargar los módulos del cliente.');
    } finally {
      setModalLoading(false);
    }
  };

  const toggleModule = async (detail: TenantDetail, module: ModuleInfo) => {
    const nextEnabled = !(module.enabled ?? true);
    setSavingModule(module.code);
    try {
      await api.setFeature(detail.tenant.id, module.code, nextEnabled);
      setModuleModal((prev) =>
        prev
          ? {
              ...prev,
              modules: prev.modules.map((m) => (m.code === module.code ? { ...m, enabled: nextEnabled } : m)),
            }
          : prev,
      );
      setTenants((prev) =>
        prev.map((t) =>
          t.id === detail.tenant.id
            ? { ...t, disabledModules: Math.max(0, t.disabledModules + (nextEnabled ? -1 : 1)) }
            : t,
        ),
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No fue posible actualizar el módulo.');
    } finally {
      setSavingModule(null);
    }
  };

  const changeStatus = async (tenant: Tenant, status: string) => {
    setSavingStatus(tenant.id);
    setError(null);
    try {
      await api.setStatus(tenant.id, status);
      setTenants((prev) => prev.map((t) => (t.id === tenant.id ? { ...t, status } : t)));
      setStats((prev) =>
        prev
          ? {
              ...prev,
              activeTenants: prev.activeTenants + (status === 'ACTIVE' ? 1 : 0) - (tenant.status === 'ACTIVE' ? 1 : 0),
              suspendedTenants:
                prev.suspendedTenants + (status === 'SUSPENDED' ? 1 : 0) - (tenant.status === 'SUSPENDED' ? 1 : 0),
            }
          : prev,
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No fue posible cambiar el estado del cliente.');
    } finally {
      setSavingStatus(null);
    }
  };

  const saveWelcome = async () => {
    setWelcomeSaving(true);
    setWelcomeNotice(null);
    setError(null);
    try {
      await api.saveWelcome(welcomeTitle.trim(), welcomeMessage.trim());
      setWelcomeNotice('Contenido publicado. La página pública ya muestra estos cambios.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No fue posible guardar la página de bienvenida.');
    } finally {
      setWelcomeSaving(false);
    }
  };

  const logout = () => {
    clearSession();
    setSession(null);
    setStats(null);
    setTenants([]);
    setAudit([]);
  };

  if (!session) {
    return <LoginView onLogin={setSession} />;
  }

  const current = SECTIONS.find((s) => s.id === section) ?? SECTIONS[0];
  const totalModules = catalog.length;
  const enabledByTenant = (tenant: Tenant) => Math.max(0, totalModules - tenant.disabledModules);

  return (
    <div className="app">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark">
            <svg width="21" height="21" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
              <path d="M12 3l7 3v5c0 4.4-3 8.3-7 10-4-1.7-7-5.6-7-10V6l7-3z" strokeLinejoin="round" />
              <path d="M9 12l2 2 4-4" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </div>
          <div>
            <div className="brand-name">SIGM Cloud</div>
            <div className="brand-sub">Plataforma</div>
          </div>
        </div>
        <nav className="nav">
          <div className="nav-label">Administración</div>
          {SECTIONS.map((item) => (
            <button
              key={item.id}
              type="button"
              className={`nav-item ${section === item.id ? 'active' : ''}`}
              onClick={() => setSection(item.id)}
            >
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9">
                {item.id === 'resumen' && <path d="M4 13h6V4H4v9zm10 7h6V11h-6v9zM4 20h6v-4H4v4zm10-13h6V4h-6v3z" strokeLinejoin="round" />}
                {item.id === 'clientes' && <path d="M4 21v-2a4 4 0 014-4h4a4 4 0 014 4v2M10 3a4 4 0 110 8 4 4 0 010-8zm10 18v-2a4 4 0 00-3-3.9M16 3.1a4 4 0 010 7.8" strokeLinecap="round" strokeLinejoin="round" />}
                {item.id === 'modulos' && <path d="M4 5h7v7H4zM13 5h7v7h-7zM4 14h7v7H4zM13 14h7v7h-7z" strokeLinejoin="round" />}
                {item.id === 'bienvenida' && <path d="M4 5h16v11H8l-4 4V5z" strokeLinejoin="round" />}
                {item.id === 'auditoria' && <path d="M12 8v4l3 2M21 12a9 9 0 11-18 0 9 9 0 0118 0z" strokeLinecap="round" strokeLinejoin="round" />}
                {item.id === 'base_datos' && <path d="M4 6c0-1.7 3.6-3 8-3s8 1.3 8 3-3.6 3-8 3-8-1.3-8-3zm0 0v12c0 1.7 3.6 3 8 3s8-1.3 8-3V6" strokeLinejoin="round" />}
              </svg>
              {item.label}
            </button>
          ))}
        </nav>
        <div className="sidebar-foot">SIGM Cloud · Consola de plataforma</div>
      </aside>

      <div className="main">
        <header className="topbar">
          <div className="crumb">
            Plataforma / Superadmin
            <b>{current.label}</b>
          </div>
          <div className="topbar-right">
            <span className="badge violet">Superadmin</span>
            <div className="user">
              <div className="avatar">{initials(session.fullName || 'SIGM')}</div>
              <div>
                <div className="user-name">{session.fullName}</div>
                <div className="user-role">Soporte de plataforma</div>
              </div>
            </div>
            <button className="btn ghost sm" type="button" onClick={() => setShowSecurity(true)}>
              Seguridad
            </button>
            <button className="btn ghost sm" type="button" onClick={logout}>
              Salir
            </button>
          </div>
        </header>

        <main className="content">
          {error && <div className="alert error">{error}</div>}

          {section === 'resumen' && (
            <>
              <div className="stats">
                <div className="stat">
                  <div className="stat-ico blue">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9">
                      <path d="M4 21v-2a4 4 0 014-4h4a4 4 0 014 4v2M10 3a4 4 0 110 8 4 4 0 010-8zM20 21v-2a4 4 0 00-3-3.9" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  </div>
                  <div>
                    <div className="stat-val">{stats?.tenants ?? '—'}</div>
                    <div className="stat-lbl">Clientes totales</div>
                  </div>
                </div>
                <div className="stat">
                  <div className="stat-ico green">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9">
                      <path d="M5 13l4 4L19 7" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  </div>
                  <div>
                    <div className="stat-val">{stats?.activeTenants ?? '—'}</div>
                    <div className="stat-lbl">Clientes activos</div>
                  </div>
                </div>
                <div className="stat">
                  <div className="stat-ico amber">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9">
                      <path d="M12 9v4m0 4h.01M10.3 3.9L2.4 18a2 2 0 001.7 3h15.8a2 2 0 001.7-3L13.7 3.9a2 2 0 00-3.4 0z" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  </div>
                  <div>
                    <div className="stat-val">{stats?.suspendedTenants ?? '—'}</div>
                    <div className="stat-lbl">Suspendidos</div>
                  </div>
                </div>
                <div className="stat">
                  <div className="stat-ico violet">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9">
                      <path d="M16 21v-2a4 4 0 00-4-4H6a4 4 0 00-4 4v2M9 3a4 4 0 110 8 4 4 0 010-8zM22 21v-2a4 4 0 00-3-3.9M16 3.1a4 4 0 010 7.8" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  </div>
                  <div>
                    <div className="stat-val">{stats?.users ?? '—'}</div>
                    <div className="stat-lbl">Usuarios registrados</div>
                  </div>
                </div>
              </div>

              <section className="panel">
                <div className="panel-h">
                  <div>
                    <div className="panel-t">Uso de la plataforma</div>
                    <div className="panel-d">Volumen acumulado de pacientes y citas de todos los clientes.</div>
                  </div>
                </div>
                <div className="panel-b">
                  <div className="grid2">
                    <div className="stat">
                      <div className="stat-ico blue">
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9">
                          <path d="M16 21v-2a4 4 0 00-4-4H6a4 4 0 00-4 4v2M9 3a4 4 0 110 8 4 4 0 010-8z" strokeLinecap="round" strokeLinejoin="round" />
                        </svg>
                      </div>
                      <div>
                        <div className="stat-val">{stats?.patients ?? '—'}</div>
                        <div className="stat-lbl">Pacientes</div>
                      </div>
                    </div>
                    <div className="stat">
                      <div className="stat-ico green">
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9">
                          <path d="M8 2v4M16 2v4M3 10h18M5 4h14a2 2 0 012 2v14a2 2 0 01-2 2H5a2 2 0 01-2-2V6a2 2 0 012-2z" strokeLinecap="round" strokeLinejoin="round" />
                        </svg>
                      </div>
                      <div>
                        <div className="stat-val">{stats?.appointments ?? '—'}</div>
                        <div className="stat-lbl">Citas agendadas</div>
                      </div>
                    </div>
                  </div>
                </div>
              </section>

              <section className="panel">
                <div className="panel-h">
                  <div>
                    <div className="panel-t">Catálogo de módulos</div>
                    <div className="panel-d">{totalModules} módulos disponibles para habilitar por cliente.</div>
                  </div>
                  <button className="btn subtle sm" type="button" onClick={() => setSection('modulos')}>
                    Gestionar
                  </button>
                </div>
                <div className="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>Módulo</th>
                        <th>Código</th>
                        <th>Descripción</th>
                      </tr>
                    </thead>
                    <tbody>
                      {catalog.map((module) => (
                        <tr key={module.code}>
                          <td className="strong">{module.name}</td>
                          <td><span className="module-code">{module.code}</span></td>
                          <td>{module.description}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </section>
            </>
          )}

          {section === 'clientes' && (
            <section className="panel">
              <div className="panel-h">
                <div>
                  <div className="panel-t">Clientes (tenants)</div>
                  <div className="panel-d">Gestiona el estado de cada cuenta y sus módulos activos.</div>
                </div>
                <button className="btn ghost sm" type="button" onClick={() => void loadCore()}>
                  Actualizar
                </button>
              </div>
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Cliente</th>
                      <th>Producto</th>
                      <th>Estado</th>
                      <th className="num">Usuarios</th>
                      <th className="num">Pacientes</th>
                      <th className="num">Citas</th>
                      <th>Módulos</th>
                      <th>Acciones</th>
                    </tr>
                  </thead>
                  <tbody>
                    {tenants.length === 0 && (
                      <tr>
                        <td colSpan={8}>
                          <div className="empty">{loading ? 'Cargando clientes…' : 'Aún no hay clientes registrados.'}</div>
                        </td>
                      </tr>
                    )}
                    {tenants.map((tenant) => (
                      <tr key={tenant.id}>
                        <td>
                          <div className="strong">{tenant.commercialName}</div>
                          <small>{tenant.tenantCode} · {tenant.planName ?? 'Sin plan'}</small>
                        </td>
                        <td>{tenant.productType}</td>
                        <td><StatusBadge status={tenant.status} /></td>
                        <td className="num">{tenant.users}</td>
                        <td className="num">{tenant.patients}</td>
                        <td className="num">{tenant.appointments}</td>
                        <td>
                          <span className="badge slate">
                            {enabledByTenant(tenant)}/{totalModules || '—'} activos
                          </span>
                        </td>
                        <td>
                          <div className="row">
                            <button
                              className="btn subtle sm"
                              type="button"
                              disabled={modalLoading}
                              onClick={() => void openModules(tenant)}
                            >
                              Módulos
                            </button>
                            {tenant.status === 'SUSPENDED' || tenant.status === 'CANCELLED' ? (
                              <button
                                className="btn ghost sm"
                                type="button"
                                disabled={savingStatus === tenant.id}
                                onClick={() => void changeStatus(tenant, 'ACTIVE')}
                              >
                                Activar
                              </button>
                            ) : (
                              <button
                                className="btn danger sm"
                                type="button"
                                disabled={savingStatus === tenant.id}
                                onClick={() => void changeStatus(tenant, 'SUSPENDED')}
                              >
                                Suspender
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          )}

          {section === 'modulos' && (
            <>
              <section className="panel">
                <div className="panel-h">
                  <div>
                    <div className="panel-t">Catálogo de módulos</div>
                    <div className="panel-d">Selecciona un cliente para activar o desactivar cualquiera de estos módulos.</div>
                  </div>
                </div>
                <div className="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>Módulo</th>
                        <th>Código</th>
                        <th>Descripción</th>
                      </tr>
                    </thead>
                    <tbody>
                      {catalog.map((module) => (
                        <tr key={module.code}>
                          <td className="strong">{module.name}</td>
                          <td><span className="module-code">{module.code}</span></td>
                          <td>{module.description}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </section>

              <section className="panel">
                <div className="panel-h">
                  <div>
                    <div className="panel-t">Activar módulos por cliente</div>
                    <div className="panel-d">Abre un cliente para configurar su catálogo completo.</div>
                  </div>
                </div>
                <div className="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>Cliente</th>
                        <th>Estado</th>
                        <th>Módulos activos</th>
                        <th>Configurar</th>
                      </tr>
                    </thead>
                    <tbody>
                      {tenants.map((tenant) => (
                        <tr key={tenant.id}>
                          <td className="strong">{tenant.commercialName}</td>
                          <td><StatusBadge status={tenant.status} /></td>
                          <td>{enabledByTenant(tenant)} de {totalModules || '—'}</td>
                          <td>
                            <button className="btn subtle sm" type="button" onClick={() => void openModules(tenant)}>
                              Configurar módulos
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </section>
            </>
          )}

          {section === 'bienvenida' && (
            <section className="panel">
              <div className="panel-h">
                <div>
                  <div className="panel-t">Página de bienvenida</div>
                  <div className="panel-d">Este contenido se muestra en el sitio público de SIGM Cloud.</div>
                </div>
                <span className="badge blue">Público</span>
              </div>
              <div className="panel-b">
                {welcomeNotice && <div className="alert ok">{welcomeNotice}</div>}
                {welcomeLoading ? (
                  <div className="empty">Cargando contenido…</div>
                ) : (
                  <div className="grid2">
                    <div>
                      <label className="field">
                        <span>Título</span>
                        <input
                          type="text"
                          value={welcomeTitle}
                          maxLength={120}
                          onChange={(e) => setWelcomeTitle(e.target.value)}
                          placeholder="Bienvenido a SIGM Cloud"
                        />
                      </label>
                      <div className="hint">Se muestra como encabezado principal del sitio público.</div>
                      <label className="field" style={{ marginTop: 16 }}>
                        <span>Mensaje</span>
                        <textarea
                          value={welcomeMessage}
                          maxLength={600}
                          onChange={(e) => setWelcomeMessage(e.target.value)}
                          placeholder="Escribe el mensaje de bienvenida…"
                        />
                      </label>
                      <div className="row" style={{ marginTop: 16 }}>
                        <button
                          className="btn"
                          type="button"
                          disabled={welcomeSaving || !welcomeTitle.trim()}
                          onClick={() => void saveWelcome()}
                        >
                          {welcomeSaving ? 'Guardando…' : 'Guardar y publicar'}
                        </button>
                        <span className="muted">{welcomeMessage.length}/600 caracteres</span>
                      </div>
                    </div>
                    <div>
                      <div className="panel-sec-t" style={{ paddingTop: 0, borderTop: 0 }}>Vista previa</div>
                      <div className="stat" style={{ flexDirection: 'column', alignItems: 'flex-start', gap: 8 }}>
                        <span className="badge green">SIGM Cloud</span>
                        <div style={{ fontSize: 20, fontWeight: 700, color: 'var(--ink)' }}>
                          {welcomeTitle.trim() || 'Título de bienvenida'}
                        </div>
                        <p style={{ fontSize: 13.5, color: 'var(--muted)', lineHeight: 1.6 }}>
                          {welcomeMessage.trim() || 'El mensaje de bienvenida aparecerá aquí.'}
                        </p>
                      </div>
                    </div>
                  </div>
                )}
              </div>
            </section>
          )}

          {section === 'base_datos' && <DatabaseView />}

          {section === 'auditoria' && (
            <section className="panel">
              <div className="panel-h">
                <div>
                  <div className="panel-t">Auditoría de autenticación</div>
                  <div className="panel-d">Últimos 100 eventos de acceso en toda la plataforma.</div>
                </div>
                <button className="btn ghost sm" type="button" onClick={() => api.audit().then(setAudit).catch(() => undefined)}>
                  Actualizar
                </button>
              </div>
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Fecha</th>
                      <th>Cliente</th>
                      <th>Usuario</th>
                      <th>Evento</th>
                      <th>Resultado</th>
                    </tr>
                  </thead>
                  <tbody>
                    {audit.length === 0 && (
                      <tr>
                        <td colSpan={5}>
                          <div className="empty">No hay eventos de auditoría disponibles.</div>
                        </td>
                      </tr>
                    )}
                    {audit.map((event) => (
                      <tr key={event.id}>
                        <td>{formatDate(event.createdAt)}</td>
                        <td>{event.commercialName ?? '—'}</td>
                        <td>{event.email ?? '—'}</td>
                        <td className="strong">{event.eventType}</td>
                        <td>
                          <span className={`badge ${event.success ? 'green' : 'red'}`}>
                            {event.success ? 'Correcto' : 'Fallido'}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          )}
        </main>
      </div>

      {moduleModal && (
        <div className="modal-backdrop" role="presentation" onClick={() => setModuleModal(null)}>
          <div className="modal" role="dialog" aria-modal="true" aria-label="Módulos del cliente" onClick={(e) => e.stopPropagation()}>
            <div className="modal-h">
              <div>
                <div className="modal-t">{moduleModal.tenant.commercialName}</div>
                <div className="modal-d">Activa o desactiva los módulos de este cliente.</div>
              </div>
              <button className="icon-btn" type="button" aria-label="Cerrar" onClick={() => setModuleModal(null)}>
                ×
              </button>
            </div>
            <div className="modal-b">
              {moduleModal.modules.length === 0 && <div className="empty">No hay módulos configurados.</div>}
              {moduleModal.modules.map((module) => {
                const enabled = module.enabled ?? true;
                return (
                  <div className="module-row" key={module.code}>
                    <div className="grow">
                      <div className="module-name">{module.name}</div>
                      <div className="module-desc">{module.description}</div>
                      <div className="module-code">{module.code}</div>
                    </div>
                    <label className="switch">
                      <input
                        type="checkbox"
                        checked={enabled}
                        disabled={savingModule === module.code}
                        onChange={() => void toggleModule(moduleModal, module)}
                      />
                      <span className="track" />
                      <span className="muted" style={{ width: 72 }}>
                        {savingModule === module.code ? 'Guardando…' : enabled ? 'Activo' : 'Inactivo'}
                      </span>
                    </label>
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      )}
      {showSecurity && <SecurityModal onClose={() => setShowSecurity(false)} />}
    </div>
  );
}
