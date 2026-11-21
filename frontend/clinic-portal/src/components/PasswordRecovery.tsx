import { useState } from 'react';
import { api } from '../api';

type Props = { onBack: () => void };

/** Recuperación de contraseña con token de un solo uso (30 minutos). */
export default function PasswordRecovery({ onBack }: Props) {
  const [mode, setMode] = useState<'forgot' | 'reset'>('forgot');
  const [email, setEmail] = useState('');
  const [token, setToken] = useState('');
  const [password, setPassword] = useState('');
  const [repeat, setRepeat] = useState('');
  const [error, setError] = useState('');
  const [info, setInfo] = useState('');
  const [busy, setBusy] = useState(false);

  async function request(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError('');
    setInfo('');
    try {
      const result = await api.forgotPassword(email.trim());
      setInfo(result.message);
      if (result.devToken) {
        setToken(result.devToken);
        setMode('reset');
        setInfo('Entorno de desarrollo: token generado automáticamente. Defina la nueva contraseña.');
      }
    } catch (err: any) {
      setError(err?.message || 'No fue posible solicitar el enlace.');
    } finally {
      setBusy(false);
    }
  }

  async function reset(e: React.FormEvent) {
    e.preventDefault();
    setError('');
    setInfo('');
    if (password !== repeat) {
      setError('Las contraseñas no coinciden.');
      return;
    }
    setBusy(true);
    try {
      const result = await api.resetPassword(token.trim(), password);
      setInfo(result.message);
      setPassword('');
      setRepeat('');
      setToken('');
      setMode('forgot');
    } catch (err: any) {
      setError(err?.message || 'El enlace no es válido o expiró.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      {mode === 'forgot' ? (
        <>
          <h2>Recuperar acceso</h2>
          <p className="sub">Le enviaremos un enlace de un solo uso, válido 30 minutos.</p>
          {error && <div className="alert error">{error}</div>}
          {info && <div className="alert" style={{ fontWeight: 500 }}>{info}</div>}
          <form onSubmit={request}>
            <label className="field"><span>Correo de la cuenta</span><input type="email" value={email} onChange={e => setEmail(e.target.value)} required autoComplete="username" /></label>
            <button className="btn" disabled={busy}>{busy ? 'Enviando…' : 'Enviar enlace'}</button>
          </form>
        </>
      ) : (
        <>
          <h2>Nueva contraseña</h2>
          <p className="sub">Use el token recibido por correo para definir su contraseña.</p>
          {error && <div className="alert error">{error}</div>}
          {info && <div className="alert" style={{ fontWeight: 500 }}>{info}</div>}
          <form onSubmit={reset}>
            <label className="field"><span>Token del enlace</span><input value={token} onChange={e => setToken(e.target.value)} required /></label>
            <label className="field"><span>Nueva contraseña</span><input type="password" minLength={8} value={password} onChange={e => setPassword(e.target.value)} required autoComplete="new-password" /></label>
            <label className="field"><span>Repetir contraseña</span><input type="password" minLength={8} value={repeat} onChange={e => setRepeat(e.target.value)} required autoComplete="new-password" /></label>
            <button className="btn" disabled={busy}>{busy ? 'Guardando…' : 'Restablecer contraseña'}</button>
          </form>
        </>
      )}
      <p className="auth-alt"><a href="#" onClick={e => { e.preventDefault(); onBack(); }}>← Volver a iniciar sesión</a></p>
    </>
  );
}
