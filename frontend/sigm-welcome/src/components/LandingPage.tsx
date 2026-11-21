import { useEffect, useState } from "react";

const API = import.meta.env.VITE_API_URL || "http://localhost:5067";

const DEFAULT_TITLE = "¡Bienvenido a SIGM Cloud!";
const DEFAULT_MESSAGE =
  "Plataforma Integral de Gestión Médica en la Nube con tres modelos de negocio adaptados a su necesidad.";

type ModuleIcon = "hospital" | "practice" | "patients" | "platform";

type PortalModule = {
  key: string;
  title: string;
  subtitle: string;
  description: string;
  items: string[];
  /** Ruta del portal dentro del mismo dominio (sin puerto). */
  href: string;
  cta: string;
  icon: ModuleIcon;
  tone: "teal" | "violet" | "rose" | "slate";
};

const MODULES: PortalModule[] = [
  {
    key: "clinicas",
    title: "SIGM Clínicas",
    subtitle: "Gestión médica institucional y multi-especialidad",
    icon: "hospital",
    tone: "teal",
    href: "/app/",
    cta: "Acceder a Clínicas",
    description:
      "Administre hospitales, centros médicos y clínicas con múltiples especialidades, profesionales y sedes.",
    items: [
      "Gestión multisectorial",
      "Múltiples especialidades",
      "Administración completa",
      "Facturación y reportes",
      "Portal de pacientes avanzado",
    ],
  },
  {
    key: "consultorios",
    title: "SIGM Consultorios",
    subtitle: "Gestión clínica individual desde la nube",
    icon: "practice",
    tone: "violet",
    href: "/consultorios/",
    cta: "Acceder a Consultorios",
    description:
      "Ideal para médicos independientes y consultorios particulares que necesiten trabajar desde cualquier lugar vía Internet.",
    items: [
      "Historia clínica electrónica",
      "Agenda médica individual",
      "Gestión de pacientes",
      "Fórmulas y certificados",
      "Acceso remoto seguro",
    ],
  },
  {
    key: "pacientes",
    title: "SIGM Pacientes",
    subtitle: "Módulo de pacientes y gestión de clientes",
    icon: "patients",
    tone: "rose",
    href: "/pacientes/",
    cta: "Acceder a Pacientes",
    description:
      "Expediente clínico completo, seguimiento personalizado y portal para citas y resultados.",
    items: [
      "Expediente unificado",
      "Seguimiento clínico",
      "Portal de citas",
      "Historial de resultados",
    ],
  },
  {
    key: "plataforma",
    title: "SIGM Plataforma",
    subtitle: "Administración y operación del servicio",
    icon: "platform",
    tone: "slate",
    href: "/admin/",
    cta: "Acceder a Plataforma",
    description:
      "Consola de operación para gestionar organizaciones, planes, usuarios y la configuración global del sistema.",
    items: [
      "Organizaciones y tenants",
      "Planes y suscripciones",
      "Usuarios y roles",
      "Auditoría y seguridad",
    ],
  },
];

function ModuleIcon({ kind }: { kind: ModuleIcon }) {
  return (
    <svg
      width="26"
      height="26"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.7}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {kind === "hospital" && (
        <>
          <path d="M19 21V5a2 2 0 0 0-2-2H7a2 2 0 0 0-2 2v16" />
          <path d="M21 21h-2M5 21H3m2 0h5m9 0h-5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1v5m-4 0h4" />
        </>
      )}
      {kind === "practice" && (
        <>
          <path d="M8 3h8a2 2 0 0 1 2 2v16H6V5a2 2 0 0 1 2-2z" />
          <path d="M9 8h6M9 12h6M9 16h3" />
        </>
      )}
      {kind === "patients" && (
        <>
          <path d="M15 19.13v-.01c0-1.11-.28-2.16-.79-3.07M15 19.23v.1A12.32 12.32 0 0 1 8.62 21c-2.33 0-4.51-.65-6.37-1.77l-.01-.11a6.37 6.37 0 0 1 11.96-3.07" />
          <circle cx="8.625" cy="6.375" r="3.375" />
          <circle cx="17.25" cy="8.625" r="2.625" />
        </>
      )}
      {kind === "platform" && (
        <>
          <rect x="3" y="4" width="18" height="7" rx="2" />
          <rect x="3" y="14" width="18" height="6" rx="2" />
          <path d="M7 7.5h.01M7 17h.01" />
        </>
      )}
    </svg>
  );
}

function ArrowIcon() {
  return (
    <svg
      width="15"
      height="15"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2.2}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M5 12h13M12 5l7 7-7 7" />
    </svg>
  );
}

export default function LandingPage() {
  const [welcome, setWelcome] = useState({
    title: DEFAULT_TITLE,
    message: DEFAULT_MESSAGE,
  });

  useEffect(() => {
    fetch(`${API}/api/v1/public/welcome`)
      .then((res) => (res.ok ? res.json() : null))
      .then((data: { title?: string; message?: string } | null) => {
        if (data?.title && data?.message) {
          setWelcome({ title: data.title, message: data.message });
        }
      })
      .catch(() => {
        /* Sin API disponible se conserva el contenido por defecto. */
      });
  }, []);

  return (
    <div className="page">
      <header className="site-header">
        <div className="site-header-inner">
          <div className="brand">
            <div className="brand-mark">
              <svg
                width="22"
                height="22"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth={2}
                strokeLinecap="round"
                strokeLinejoin="round"
                aria-hidden="true"
              >
                <path d="M3 15a4 4 0 0 0 4 4h9a5 5 0 1 0-.1-9.999 5.002 5.002 0 1 0-9.78 2.096A4.001 4.001 0 0 0 3 15z" />
              </svg>
            </div>
            <div>
              <div className="brand-title">
                <span className="brand-a">SIGM</span>
                <span className="brand-b">CLOUD</span>
              </div>
              <p className="brand-sub">Sistema Integral de Gestión Médica · Colombia</p>
            </div>
          </div>

          <span className="header-chip">
            <span className="dot" aria-hidden="true" />
            Plataforma activa
          </span>
        </div>
      </header>

      <main className="hero">
        <div className="hero-bg" aria-hidden="true">
          <svg className="hero-ecg" viewBox="0 0 1440 160" preserveAspectRatio="none">
            <path d="M0 104 H286 l14 0 10-30 16 62 16-92 14 60 12 0 H700 l14 0 10-30 16 62 16-92 14 60 12 0 H1440" />
          </svg>
          <svg className="hero-cross" viewBox="0 0 24 24">
            <path d="M9 2h6v7h7v6h-7v7H9v-7H2V9h7z" />
          </svg>
        </div>

        <div className="hero-inner">
          <div className="hero-head">
            <span className="eyebrow">Salud digital · Nube segura</span>
            <h1>{welcome.title}</h1>
            <p className="lead">{welcome.message}</p>
          </div>

          <section className="mods" aria-label="Módulos del sistema">
            {MODULES.map((module) => (
              <article className={`mod mod-${module.tone}`} key={module.key}>
                <div className="mod-ico">
                  <ModuleIcon kind={module.icon} />
                </div>

                <h2>{module.title}</h2>
                <p className="mod-sub">{module.subtitle}</p>
                <p className="mod-desc">{module.description}</p>

                <ul className="mod-list">
                  {module.items.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>

                <a className="mod-btn" href={module.href}>
                  {module.cta}
                  <ArrowIcon />
                </a>
              </article>
            ))}
          </section>
        </div>
      </main>

      <footer className="site-footer">
        <div className="site-footer-inner">
          <div className="footer-tagline">
            <p>
              Una sola plataforma.
              <br />
              Cuatro experiencias conectadas.
            </p>
          </div>

          <div className="footer-badges">
            <span className="footer-badge">
              <span className="badge-ico teal">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
                  <rect x="4" y="10" width="16" height="11" rx="2" />
                  <path d="M8 10V7a4 4 0 0 1 8 0v3" />
                </svg>
              </span>
              Seguridad
            </span>
            <span className="footer-badge">
              <span className="badge-ico cyan">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
                  <path d="M6 19h11a4 4 0 0 0 0-8 6 6 0 0 0-11.6 2A3.5 3.5 0 0 0 6 19z" />
                </svg>
              </span>
              Cloud
            </span>
            <span className="footer-badge">
              <span className="badge-ico orange">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
                  <path d="M12 3v18M3 12h18" />
                </svg>
              </span>
              HL7 FHIR
            </span>
            <span className="footer-badge">
              <span className="badge-ico teal">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
                  <rect x="2" y="6" width="20" height="14" rx="2" />
                  <path d="M6 18l8-8m0 8l8-8" />
                </svg>
              </span>
              Interoperabilidad
            </span>
          </div>
        </div>

        <div className="footer-copy">
          SIGM Cloud · Soluciones Médicas Integrales © {new Date().getFullYear()} · Colombia
        </div>
      </footer>
    </div>
  );
}
