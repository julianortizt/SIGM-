// Las rutas ya empiezan por /api/v1; si VITE_API_URL termina en /api se quita
// para no generar /api/api/v1 (404 en Vercel).
export const API = (import.meta.env.VITE_API_URL || 'http://localhost:5000').replace(/\/api\/?$/, '');

export type LoginResponse = { accessToken: string; expiresAt: string; userId: string; tenantId?: string; fullName: string; permissions: string[]; requiresTenantSelection?: boolean; tenants?: string[] };
export function getToken() { return localStorage.getItem('sigm_token'); }
export function setSession(r: LoginResponse) { localStorage.setItem('sigm_token', r.accessToken); localStorage.setItem('sigm_tenant', r.tenantId || ''); localStorage.setItem('sigm_user', JSON.stringify(r)); }
export function clearSession() { localStorage.removeItem('sigm_token'); localStorage.removeItem('sigm_tenant'); localStorage.removeItem('sigm_user'); }
export function getSession(): LoginResponse | null {
  try {
    const raw = localStorage.getItem('sigm_user');
    if (!raw) return null;
    const s = JSON.parse(raw) as LoginResponse;
    if (s?.expiresAt && new Date(s.expiresAt).getTime() <= Date.now()) { clearSession(); return null; }
    return s;
  } catch { clearSession(); return null; }
}

async function req<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers: Record<string,string> = { 'Content-Type': 'application/json', ...(init.headers as any) };
  const t = getToken(); if (t) headers.Authorization = `Bearer ${t}`;
  const tenant = localStorage.getItem('sigm_tenant'); if (tenant) headers['X-Tenant-Id'] = tenant;
  const res = await fetch(`${API}${path}`, { ...init, headers });
  const text = await res.text();
  let data: any = null;
  if (text) { try { data = JSON.parse(text); } catch { data = text; } }
  if (!res.ok) {
    if (res.status === 401 && !path.includes('/auth/')) {
      clearSession();
      window.location.assign(import.meta.env.BASE_URL || '/');
    }
    const error: any = new Error(data?.message || data?.title || res.statusText || `Error ${res.status}`);
    error.status = res.status; error.data = data;
    throw error;
  }
  return data as T;
}

export const api = {
  login: (body: any) => req<any>('/api/v1/auth/login', { method: 'POST', body: JSON.stringify(body) }),
  registerOrg: (body: any) => req<any>('/api/v1/organizations', { method: 'POST', body: JSON.stringify(body) }),
  specialties: () => req<any[]>('/api/v1/specialties'),
  tenantSpecialties: () => req<any[]>('/api/v1/specialties/tenant'),
  saveTenantSpecialties: (specialtyIds: string[]) => req<void>('/api/v1/specialties/tenant', { method: 'PUT', body: JSON.stringify({ specialtyIds }) }),
  users: () => req<any[]>('/api/v1/users'),
  createUser: (body: any) => req<any>('/api/v1/users', { method: 'POST', body: JSON.stringify(body) }),
  sites: () => req<any[]>('/api/v1/sites'),
  createSite: (body: any) => req<any>('/api/v1/sites', { method: 'POST', body: JSON.stringify(body) }),
  offices: (siteId?: string) => req<any[]>(`/api/v1/medical-offices${siteId?`?siteId=${siteId}`:''}`),
  createOffice: (body: any) => req<any>('/api/v1/medical-offices', { method: 'POST', body: JSON.stringify(body) }),
  professionals: () => req<any[]>('/api/v1/professionals'),
  createProfessional: (body: any) => req<any>('/api/v1/professionals', { method: 'POST', body: JSON.stringify(body) }),
  patients: (q?: string) => req<any[]>(`/api/v1/patients${q?`?q=${encodeURIComponent(q)}`:''}`),
  createPatient: (body: any) => req<any>('/api/v1/patients', { method: 'POST', body: JSON.stringify(body) }),
  schedules: () => req<any[]>('/api/v1/schedules'),
  createSchedule: (body: any) => req<any>('/api/v1/schedules', { method: 'POST', body: JSON.stringify(body) }),
  rules: (scheduleId?: string) => req<any[]>(`/api/v1/schedules/availability-rules${scheduleId?`?scheduleId=${scheduleId}`:''}`),
  addRule: (body: any) => req<any>('/api/v1/schedules/availability-rules', { method: 'POST', body: JSON.stringify(body) }),
  exceptions: (professionalId?: string) => req<any[]>(`/api/v1/schedules/exceptions${professionalId?`?professionalId=${professionalId}`:''}`),
  addException: (body: any) => req<any>('/api/v1/schedules/exceptions', { method: 'POST', body: JSON.stringify(body) }),
  appointmentTypes: () => req<any[]>('/api/v1/appointment-types'),
  createAppointmentType: (body: any) => req<any>('/api/v1/appointment-types', { method: 'POST', body: JSON.stringify(body) }),
  appointments: () => req<any[]>('/api/v1/appointments'),
  createAppointment: (body: any) => req<any>('/api/v1/appointments', { method: 'POST', body: JSON.stringify(body) }),
  updateAppointmentStatus: (id: string, status: string, reason?: string) => req<any>(`/api/v1/appointments/${id}/status`, { method: 'POST', body: JSON.stringify({ status, reason }) }),
  encounters: (patientId?: string) => req<any[]>(`/api/v1/encounters${patientId?`?patientId=${patientId}`:''}`),
  createEncounter: (body: any) => req<any>('/api/v1/encounters', { method: 'POST', body: JSON.stringify(body) }),
  notes: (encounterId: string) => req<any[]>(`/api/v1/encounters/${encounterId}/notes`),
  addNote: (encounterId: string, body: any) => req<any>(`/api/v1/encounters/${encounterId}/notes`, { method: 'POST', body: JSON.stringify(body) }),
  signNote: (id: string) => req<any>(`/api/v1/notes/${id}/sign`, { method: 'POST' }),
  clinicalSummary: (patientId: string) => req<any>(`/api/v1/clinical/patients/${patientId}/summary`),
  addVitals: (body: any) => req<any>('/api/v1/clinical/vitals', { method: 'POST', body: JSON.stringify(body) }),
  addAllergy: (body: any) => req<any>('/api/v1/clinical/allergies', { method: 'POST', body: JSON.stringify(body) }),
  addHistory: (body: any) => req<any>('/api/v1/clinical/history', { method: 'POST', body: JSON.stringify(body) }),
  searchDx: (q: string) => req<any[]>(`/api/v1/clinical/diagnosis-catalog?q=${encodeURIComponent(q)}`),
  addDiagnosis: (body: any) => req<any>('/api/v1/clinical/diagnoses', { method: 'POST', body: JSON.stringify(body) }),
  clinicalRecord: (patientId: string) => req<any>(`/api/v1/patients/${patientId}/clinical-record`),
  /** Descarga el PDF oficial de historia clínica (el hash SHA-256 viaja en X-Document-Sha256). */
  clinicalRecordPdf: async (patientId: string): Promise<Blob> => {
    const headers: Record<string, string> = {};
    const token = getToken(); if (token) headers.Authorization = `Bearer ${token}`;
    const tenant = localStorage.getItem('sigm_tenant'); if (tenant) headers['X-Tenant-Id'] = tenant;
    const res = await fetch(`${API}/api/v1/patients/${patientId}/clinical-record/pdf`, { headers });
    if (!res.ok) {
      let message = `No fue posible generar el PDF (${res.status}).`;
      try { const data = await res.json(); message = data?.message || data?.title || message; } catch { /* cuerpo no JSON */ }
      throw new Error(message);
    }
    return res.blob();
  },
  billingServices: () => req<any[]>('/api/v1/billing/services'),
  createBillingService: (body: any) => req<any>('/api/v1/billing/services', { method: 'POST', body: JSON.stringify(body) }),
  invoices: () => req<any[]>('/api/v1/billing/invoices'),
  createInvoice: (body: any) => req<any>('/api/v1/billing/invoices', { method: 'POST', body: JSON.stringify(body) }),
  ar: () => req<any[]>('/api/v1/billing/accounts-receivable'),
  documents: (patientId?: string) => req<any[]>(`/api/v1/documents${patientId?`?patientId=${patientId}`:''}`),
  createDocument: (body: any) => req<any>('/api/v1/documents', { method: 'POST', body: JSON.stringify(body) }),
  waitingList: () => req<any[]>('/api/v1/waiting-list'),
  createWaiting: (body: any) => req<any>('/api/v1/waiting-list', { method: 'POST', body: JSON.stringify(body) }),
  reminders: () => req<any[]>('/api/v1/reminders'),
  createReminder: (body: any) => req<any>('/api/v1/reminders', { method: 'POST', body: JSON.stringify(body) }),
  labOrders: (patientId?: string) => req<any[]>(`/api/v1/laboratory/orders${patientId?`?patientId=${patientId}`:''}`),
  createLabOrder: (body: any) => req<any>('/api/v1/laboratory/orders', { method: 'POST', body: JSON.stringify(body) }),
  labTests: (q?: string) => req<any[]>(`/api/v1/laboratory/tests${q?`?q=${encodeURIComponent(q)}`:''}`),
  prescriptions: (patientId?: string) => req<any[]>(`/api/v1/prescriptions${patientId?`?patientId=${patientId}`:''}`),
  createPrescription: (body: any) => req<any>('/api/v1/prescriptions', { method: 'POST', body: JSON.stringify(body) }),
  insurers: () => req<any[]>('/api/v1/insurers'),
  contracts: () => req<any[]>('/api/v1/contracts'),
  authorizations: (patientId?: string) => req<any[]>(`/api/v1/authorizations${patientId?`?patientId=${patientId}`:''}`),
  payments: (invoiceId?: string) => req<any[]>(`/api/v1/payments${invoiceId?`?invoiceId=${invoiceId}`:''}`),
  createPayment: (body: any) => req<any>('/api/v1/payments', { method: 'POST', body: JSON.stringify(body) }),
  interopEvents: () => req<any[]>('/api/v1/interoperability/events'),
  transmissions: () => req<any[]>('/api/v1/interoperability/transmissions'),
  rda: (patientId?: string) => req<any[]>(`/api/v1/interoperability/rda${patientId?`?patientId=${patientId}`:''}`),
  auditLogs: () => req<any[]>('/api/v1/audit/logs'),
  accessLogs: () => req<any[]>('/api/v1/audit/access'),
  departments: () => req<any[]>('/api/v1/departments'),
  createDepartment: (body: any) => req<any>('/api/v1/departments', { method: 'POST', body: JSON.stringify(body) }),
  resources: () => req<any[]>('/api/v1/resources'),
  createResource: (body: any) => req<any>('/api/v1/resources', { method: 'POST', body: JSON.stringify(body) }),
  availability: (professionalId: string, date: string) => req<any>(`/api/v1/availability?professionalId=${encodeURIComponent(professionalId)}&date=${encodeURIComponent(date)}`),
};
