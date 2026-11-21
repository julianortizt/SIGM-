import { useState } from 'react';
import type { FormEvent } from 'react';
import { api, ApiError, setSession, type Session } from '../api';

export default function LoginView({ onLogin }: { onLogin: (session: Session) => void }) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [mfaCode, setMfaCode] = useState('');
  const [mfaRequired, setMfaRequired] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [mode, setMode] = useState<'login' | 'forgot' | 'reset'>('login');
  const [resetToken, setResetToken] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [repeatPassword, setRepeatPassword] = useState('');
  const [info, setInfo] = useState<string | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const session = await api.login(email.trim(), password, mfaRequired ? mfaCode.trim() : undefined);
      if (!session?.accessToken) {
        setError('Credenciales inválidas.');
        return;
      }
      if (!session.permissions?.includes('platform.tenants.manage')) {
        setError('Esta cuenta no tiene privilegios de superadministrador de plataforma.');
        return;
      }
      setSession(session);
      onLogin(session);
    } catch (err) {
      if (err instanceof ApiError && err.status === 401 && err.data?.mfaRequired) {
        setMfaRequired(true);
        setMfaCode('');
        setError('Verificación en dos pasos: escriba el código de su aplicación autenticadora.');
      } else {
        setError(err instanceof Error ? err.message : 'No fue posible iniciar sesión.');
      }
    } finally {
      setLoading(false);
    }
  };

  async function requestReset(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setInfo(null);
    setLoading(true);
    try {
      const result = await api.forgotPassword(email.trim());
      setInfo(result.message);
      if (result.devToken) {
        setResetToken(result.devToken);
        setMode('reset');
        setInfo('Entorno de desarrollo: enlace generado automáticamente. Defina la nueva contraseña.');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No fue posible solicitar el enlace.');
    } finally {
      setLoading(false);
    }
  }

  async function applyReset(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setInfo(null);
    if (newPassword !== repeatPassword) {
      setError('Las contraseñas no coinciden.');
      return;
    }
    setLoading(true);
    try {
      const result = await api.resetPassword(resetToken.trim(), newPassword);
      setInfo(result.message);
      setMode('login');
      setPassword('');
      setNewPassword('');
      setRepeatPassword('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'El enlace no es válido o expiró.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="auth">
      <aside className="auth-brand">
        <div className="auth-brand-head">
          <div className="auth-brand-mark">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7">
              <path d="M12 3l7 3v5c0 4.4-3 8.3-7 10-4-1.7-7-5.6-7-10V6l7-3z" strokeLinejoin="round" />
              <path d="M9 12l2 2 4-4" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </div>
          <div>
            <div className="auth-brand-title">SIGM Cloud</div>
            <div className="auth-brand-sub">Consola de plataforma</div>
          </div>
        </div>
        <div className="auth-hero">
          <h1>Administración de<br />clientes y módulos</h1>
          <p>
            Panel de soporte SaaS para aprovisionar tenants, habilitar módulos por cliente y
            supervisar la actividad de autenticación de toda la plataforma.
          </p>
          <div className="auth-feats">
            <div className="auth-feat">
              <div className="auth-feat-ico">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M4 4h16v6H4zM4 14h10v6H4z" />
                </svg>
              </div>
              <div>
                <b>Módulos por cliente</b>
                <span>Activa o desactiva cualquier módulo del catálogo.</span>
              </div>
            </div>
            <div className="auth-feat">
              <div className="auth-feat-ico">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M3 12h4l3 8 4-16 3 8h4" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </div>
              <div>
                <b>Estado de las cuentas</b>
                <span>Suspende o reactiva tenants en segundos.</span>
              </div>
            </div>
            <div className="auth-feat">
              <div className="auth-feat-ico">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M12 8v4l3 2M21 12a9 9 0 11-18 0 9 9 0 0118 0z" strokeLinecap="round" />
                </svg>
              </div>
              <div>
                <b>Auditoría global</b>
                <span>Historial de inicios de sesión e intentos fallidos.</span>
              </div>
            </div>
          </div>
        </div>
        <div className="auth-foot">© {new Date().getFullYear()} SIGM Cloud · Acceso restringido a soporte</div>
      </aside>

      <main className="auth-panel">
        <div className="auth-card">
          {mode === 'login' && <>
          <h2>Iniciar sesión</h2>
          <p className="sub">Ingresa con una cuenta de superadministrador de plataforma.</p>
          {error && <div className="alert error">{error}</div>}
          {info && <div className="alert" style={{ fontWeight: 500 }}>{info}</div>}
          <form onSubmit={submit}>
            <label className="field">
              <span>Correo electrónico</span>
              <input
                type="email"
                autoComplete="username"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="soporte@sigm.cloud"
              />
            </label>
            <label className="field">
              <span>Contraseña</span>
              <input
                type="password"
                autoComplete="current-password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
              />
            </label>
            {mfaRequired && (
              <label className="field">
                <span>Código de verificación o de recuperación</span>
                <input
                  autoComplete="one-time-code"
                  required
                  autoFocus
                  value={mfaCode}
                  onChange={(e) => setMfaCode(e.target.value)}
                  placeholder="123456"
                />
              </label>
            )}
            <button className="btn" type="submit" disabled={loading}>
              {loading ? 'Verificando…' : mfaRequired ? 'Verificar e ingresar' : 'Entrar al panel'}
            </button>
          </form>
          {mfaRequired && (
            <p className="auth-alt">
              <a href="#" onClick={(e) => { e.preventDefault(); setMfaRequired(false); setMfaCode(''); setError(null); }}>
                ← Usar otra cuenta
              </a>
            </p>
          )}
          <p className="auth-alt">
            <a href="#" onClick={(e) => { e.preventDefault(); setMode('forgot'); setError(null); setInfo(null); setMfaRequired(false); }}>
              ¿Olvidó su contraseña?
            </a>
          </p>
          <p className="auth-alt">
            <a href="/">← Volver al inicio</a>
          </p>
          </>}

          {mode === 'forgot' && <>
            <h2>Recuperar acceso</h2>
            <p className="sub">Le enviaremos un enlace de un solo uso, válido 30 minutos.</p>
            {error && <div className="alert error">{error}</div>}
            {info && <div className="alert" style={{ fontWeight: 500 }}>{info}</div>}
            <form onSubmit={requestReset}>
              <label className="field">
                <span>Correo de la cuenta</span>
                <input type="email" required value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="username" />
              </label>
              <button className="btn" type="submit" disabled={loading}>{loading ? 'Enviando…' : 'Enviar enlace'}</button>
            </form>
            <p className="auth-alt">
              <a href="#" onClick={(e) => { e.preventDefault(); setMode('login'); setError(null); setInfo(null); }}>← Volver a iniciar sesión</a>
            </p>
          </>}

          {mode === 'reset' && <>
            <h2>Nueva contraseña</h2>
            <p className="sub">Use el token recibido por correo para definir su contraseña.</p>
            {error && <div className="alert error">{error}</div>}
            {info && <div className="alert" style={{ fontWeight: 500 }}>{info}</div>}
            <form onSubmit={applyReset}>
              <label className="field">
                <span>Token del enlace</span>
                <input required value={resetToken} onChange={(e) => setResetToken(e.target.value)} />
              </label>
              <label className="field">
                <span>Nueva contraseña</span>
                <input type="password" required minLength={8} value={newPassword} onChange={(e) => setNewPassword(e.target.value)} autoComplete="new-password" />
              </label>
              <label className="field">
                <span>Repetir contraseña</span>
                <input type="password" required minLength={8} value={repeatPassword} onChange={(e) => setRepeatPassword(e.target.value)} autoComplete="new-password" />
              </label>
              <button className="btn" type="submit" disabled={loading}>{loading ? 'Guardando…' : 'Restablecer contraseña'}</button>
            </form>
            <p className="auth-alt">
              <a href="#" onClick={(e) => { e.preventDefault(); setMode('login'); setError(null); setInfo(null); }}>← Volver a iniciar sesión</a>
            </p>
          </>}
        </div>
      </main>
    </div>
  );
}
