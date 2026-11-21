# SIGM Cloud — Requisitos (resumen maestro)

Fuente: `sigm medical.docx` + decisiones de proyecto consolidadas.

## Producto
- Una plataforma (SIGM Cloud), dos líneas comerciales: **SIGM Clínicas** y **SIGM Consultorios** (marcas/identidades configurables), más **Portal de Pacientes**.
- SaaS cloud, mercado inicial Colombia, multi-tenant con aislamiento estricto.
- Desarrollo en paralelo de ambos modelos sobre núcleo común.

## Alcance funcional (núcleo)
- **Organizaciones/tenants:** tipo clínica/consultorio, sedes, configuración regional, suscripción/plan, módulos y límites.
- **Especialidades:** catálogo central; cada tenant habilita las suyas; profesionales con una o varias especialidades y habilitación por sede/registro/estado.
- **Identidad y acceso:** usuarios, roles/permisos (RBAC), membresías por tenant, sesiones/eventos, MFA (objetivo para personal), principio de mínimo privilegio.
- **Pacientes:** registro, identificación/contactos, responsables, antecedentes/alertas, consentimientos, documentos, historial, solicitudes de datos (Ley 1581).
- **Historia clínica:** expediente, encuentros, notas configurables por especialidad, antecedentes, alergias, signos vitales, diagnósticos (CIE-10), órdenes, prescripciones, resultados/adjuntos, firma y adendas inmutables, trazabilidad completa.
- **Agendamiento:** calendario por profesional/especialidad/sede/consultorio/recurso, duración y reglas de disponibilidad, reservas/confirmaciones/cancelaciones/reprogramaciones/lista de espera/recordatorios/asistencia/no-show, sin doble reserva.
- **Documentos:** versiones, permisos, firmas, publicación controlada al paciente.
- **Facturación y EPS:** servicios/tarifarios/contratos/aseguradoras, autorizaciones, facturas/notas/pagos/cartera, integración facturación electrónica DIAN vía proveedor, RIPS y adaptadores por entidad.
- **Interoperabilidad:** preparación HL7 FHIR, IHCE Colombia, RDA (Res. 1888/2025), mapeos y logs de transmisión.
- **Auditoría:** registro de accesos y acciones relevantes (usuario, tenant, fecha, acción, entidad, resultado, motivo/origen).

## Requisitos no funcionales
- Seguridad by design, cifrado en tránsito/reposo, gestión de secretos, RLS/aislamiento, auditoría, backups y recuperación, monitoreo.
- Disponibilidad objetivo 99.9%, RPO 15 min, RTO 4 h (a validar/contratar), rendimiento <2s consultas habituales, pruebas de carga/seguridad.
- Ambientes separados, sin datos reales en desarrollo/pruebas.

## Normativa (Colombia)
Res. 1995/1999 y 839/2017 (historia clínica), Ley 1581/2012 (datos personales), Ley 2015/2020, Res. 866/2021 (interoperabilidad/IHCE), Res. 1888/2025 (RDA). Cumplimiento verificado antes de producción.

## Criterios de aceptación (producción)
Aislamiento entre tenants sin acceso cruzado, RBAC efectivo, auditoría completa, integridad de notas firmadas/versionadas, agenda concurrente sin solapes, restauración probada, seguridad sin críticos, rendimiento validado, flujos clínicos y revisión jurídica/normativa/interoperabilidad aplicables.
