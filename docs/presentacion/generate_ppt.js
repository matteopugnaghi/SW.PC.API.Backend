/**
 * AQUAFRISCH SUPERVISOR CORE - Presentación Comercial
 * Con capturas de pantalla REALES del software
 * 
 * Ejecutar: node generate_ppt.js
 */

const pptxgen = require("pptxgenjs");
const fs = require("fs");
const path = require("path");

const COLORS = {
  darkBg: "0A0E27", darkBg2: "111633",
  accentBlue: "00B4D8", accentCyan: "00F5FF", accentGold: "FFB800",
  accentGreen: "00E676", accentRed: "FF3D71",
  white: "FFFFFF", lightGray: "B8C4D0", medGray: "6B7A8D",
  cardBg: "1A1F3D", cardBorder: "2A3060",
};
const F = { title: "Segoe UI Light", sub: "Segoe UI", body: "Segoe UI", accent: "Segoe UI Semibold", mono: "Cascadia Code" };
const IMG = __dirname;
const VENDOR = path.resolve(__dirname, "../../wwwroot/vendor-logos");

function img(f) { const p = path.join(IMG, f); return fs.existsSync(p) ? p : null; }
function vimg(f) { const p = path.join(VENDOR, f); return fs.existsSync(p) ? p : null; }
function bg(s) { s.background = { fill: COLORS.darkBg }; }
function glow(s) { s.addShape("rect", { x: 0, y: 0, w: "100%", h: 0.06, fill: { type: "solid", color: COLORS.accentCyan }, shadow: { type: "outer", blur: 20, offset: 5, color: COLORS.accentCyan, opacity: 0.6 } }); }
function foot(s, n, t) {
  s.addShape("rect", { x: 0, y: 7.1, w: "100%", h: 0.4, fill: { type: "solid", color: "050815" } });
    s.addText("AQUAFRISCH SUPERVISOR CORE  •  Confidencial  •  2026", { x: 0.5, y: 7.15, w: 7, h: 0.3, fontSize: 8, color: COLORS.medGray, fontFace: F.body });
  s.addText(`${n}/${t}`, { x: 8.5, y: 7.15, w: 1.2, h: 0.3, fontSize: 8, color: COLORS.medGray, fontFace: F.mono, align: "right" });
}
function card(s, x, y, w, h, o = {}) {
  s.addShape("roundRect", { x, y, w, h, fill: { type: "solid", color: o.fill || COLORS.cardBg }, line: { color: o.border || COLORS.cardBorder, width: 0.75 }, rectRadius: 0.1, shadow: o.glow ? { type: "outer", blur: 12, offset: 0, color: o.glow, opacity: 0.3 } : undefined });
}
function shot(s, x, y, w, h, file, bc) {
  if (file) {
    s.addImage({ path: file, x, y, w, h });
    s.addShape("roundRect", { x, y, w, h, fill: { type: "none" }, line: { color: bc || COLORS.accentCyan, width: 1.2 }, rectRadius: 0.05, shadow: { type: "outer", blur: 8, offset: 0, color: bc || COLORS.accentCyan, opacity: 0.3 } });
  }
}
function section(pptx, num, title, sub, n, t) {
  const s = pptx.addSlide(); bg(s); glow(s);
  s.addText(num, { x: 0.3, y: 1.5, w: 3, h: 3, fontSize: 150, color: COLORS.cardBg, fontFace: F.title });
  s.addShape("rect", { x: 0.8, y: 3.2, w: 1.5, h: 0.04, fill: { type: "solid", color: COLORS.accentCyan } });
  s.addText(title, { x: 0.8, y: 3.4, w: 8, h: 1, fontSize: 36, color: COLORS.white, fontFace: F.title });
  if (sub) s.addText(sub, { x: 0.8, y: 4.4, w: 8, h: 0.5, fontSize: 14, color: COLORS.accentBlue, fontFace: F.sub, italic: true });
  foot(s, n, t);
  return s;
}

const TOTAL = 22;

async function gen() {
  const pptx = new pptxgen();
  pptx.author = "Aquafrisch"; pptx.company = "Aquafrisch";
  pptx.title = "Aquafrisch Supervisor Core"; pptx.subject = "Presentación Comercial";
  pptx.defineLayout({ name: "W", width: 10, height: 7.5 }); pptx.layout = "W";
  let n = 0;

  // === 1: PORTADA ===
  n++; {
    const s = pptx.addSlide(); bg(s);
    s.addShape("rect", { x: 0, y: 0, w: "100%", h: 0.08, fill: { type: "solid", color: COLORS.accentCyan }, shadow: { type: "outer", blur: 30, offset: 8, color: COLORS.accentCyan, opacity: 0.7 } });
    s.addShape("rect", { x: 7.5, y: 0.5, w: 0.01, h: 3, fill: { type: "solid", color: COLORS.cardBorder }, rotate: 25 });
    s.addShape("rect", { x: 8.2, y: 0.3, w: 0.01, h: 4, fill: { type: "solid", color: COLORS.cardBorder }, rotate: 20 });
    s.addShape("rect", { x: 8.8, y: 0.1, w: 0.01, h: 5, fill: { type: "solid", color: COLORS.cardBorder }, rotate: 15 });
    const logoPath = path.resolve(__dirname, '../../wwwroot/vendor-logos/../login-background.jpg');
    // Logo as text with special styling (SVG not supported in pptxgenjs)
    s.addText("A Q U A F R I S C H", { x: 0.8, y: 0.8, w: 5, h: 0.5, fontSize: 16, color: COLORS.accentCyan, fontFace: F.accent, charSpacing: 3 });
    s.addShape("rect", { x: 0.8, y: 1.25, w: 1.2, h: 0.03, fill: { type: "solid", color: COLORS.accentCyan } });
    s.addText("Supervisor Core", { x: 0.8, y: 1.5, w: 8, h: 1.2, fontSize: 52, color: COLORS.white, fontFace: F.title });
    s.addText("El futuro del control industrial inteligente", { x: 0.8, y: 2.7, w: 7, h: 0.6, fontSize: 22, color: COLORS.accentBlue, fontFace: F.title, italic: true });
    s.addShape("rect", { x: 0.8, y: 3.5, w: 2, h: 0.04, fill: { type: "solid", color: COLORS.accentCyan } });
    s.addText("Núcleo de Supervisión Industrial 3D\ncon Ciberseguridad Nativa e Inteligencia Artificial\nEU Cyber Resilience Act Ready  •  Industria 6.0", { x: 0.8, y: 3.7, w: 6, h: 0.9, fontSize: 13, color: COLORS.lightGray, fontFace: F.body, lineSpacingMultiple: 1.4 });
    card(s, 0.8, 5.0, 2.2, 0.5, { glow: COLORS.accentGreen, border: COLORS.accentGreen });
    s.addText("✓ EU CRA COMPLIANT", { x: 0.8, y: 5.0, w: 2.2, h: 0.5, fontSize: 10, color: COLORS.accentGreen, fontFace: F.accent, align: "center", valign: "middle" });
    card(s, 3.2, 5.0, 2.2, 0.5, { glow: COLORS.accentBlue, border: COLORS.accentBlue });
    s.addText("🛡️ SECURE BY DESIGN", { x: 3.2, y: 5.0, w: 2.2, h: 0.5, fontSize: 10, color: COLORS.accentBlue, fontFace: F.accent, align: "center", valign: "middle" });
    card(s, 5.6, 5.0, 2.2, 0.5, { glow: COLORS.accentGold, border: COLORS.accentGold });
    s.addText("🔮 INDUSTRY 6.0", { x: 5.6, y: 5.0, w: 2.2, h: 0.5, fontSize: 10, color: COLORS.accentGold, fontFace: F.accent, align: "center", valign: "middle" });
    s.addText("2026", { x: 0.8, y: 6.0, w: 2, h: 0.4, fontSize: 12, color: COLORS.medGray, fontFace: F.mono });
    s.addShape("rect", { x: 0, y: 7.0, w: "100%", h: 0.5, fill: { type: "solid", color: "050815" } });
    s.addText("CONFIDENCIAL  •  Aquafrisch Supervisor Core  •  Presentación Comercial", { x: 0, y: 7.05, w: "100%", h: 0.4, fontSize: 9, color: COLORS.medGray, fontFace: F.body, align: "center" });
  }

  // === 2: ¿POR QUÉ AHORA? ===
  n++; {
    const s = pptx.addSlide(); bg(s); glow(s);
    s.addText("¿Por qué ahora?", { x: 0.8, y: 0.4, w: 8, h: 0.8, fontSize: 36, color: COLORS.white, fontFace: F.title });
    s.addText("El sector ferroviario está en un punto de inflexión", { x: 0.8, y: 1.1, w: 8, h: 0.5, fontSize: 16, color: COLORS.accentBlue, fontFace: F.sub, italic: true });
    const pb = [
      { icon: "⚠️", t: "REGULACIÓN OBLIGATORIA", c: COLORS.accentRed, d: "El EU Cyber Resilience Act (2024/2847) será obligatorio en dic. 2027.\n\nProductos industriales sin ciberseguridad NO podrán venderse en la UE." },
      { icon: "🏭", t: "COMPETENCIA OBSOLETA", c: COLORS.accentGold, d: "Los competidores siguen centrados solo en hardware.\n\nQuien domine el software dominará el mercado de la próxima década." },
      { icon: "🤖", t: "LA IA ES INEVITABLE", c: COLORS.accentCyan, d: "Mantenimiento predictivo, optimización de consumos y diagnóstico asistido ya no son opcionales.\n\nLos clientes lo esperan." },
      { icon: "📊", t: "DATOS SIN EXPLOTAR", c: COLORS.accentGreen, d: "Las máquinas generan millones de datos que se pierden.\n\nConvertidos en inteligencia son un activo de valor incalculable." },
    ];
    pb.forEach((p, i) => {
      const col = i % 2, row = Math.floor(i / 2);
      const x = 0.5 + col * 4.8, y = 1.9 + row * 1.9;
      card(s, x, y, 4.2, 1.6, { glow: p.c });
      s.addText(`${p.icon}  ${p.t}`, { x: x+0.2, y: y+0.1, w: 3.8, h: 0.4, fontSize: 13, color: p.c, fontFace: F.accent });
      s.addText(p.d, { x: x+0.2, y: y+0.5, w: 3.8, h: 1.0, fontSize: 10, color: COLORS.lightGray, fontFace: F.body, lineSpacingMultiple: 1.2 });
    });
    s.addShape("rect", { x: 1.5, y: 5.9, w: 7, h: 0.06, fill: { type: "solid", color: COLORS.accentCyan } });
    s.addText("Aquafrisch no va a ser espectador. Vamos a liderar.", { x: 1.5, y: 6.1, w: 7, h: 0.5, fontSize: 18, color: COLORS.white, fontFace: F.accent, align: "center" });
    foot(s, n, TOTAL);
  }

  // === 3: NUESTRA RESPUESTA + LOGIN ===
  n++; {
    const s = pptx.addSlide(); bg(s); glow(s);
    s.addText("Nuestra respuesta", { x: 0.8, y: 0.4, w: 8, h: 0.7, fontSize: 34, color: COLORS.white, fontFace: F.title });
    s.addText("Aquafrisch\nSupervisor Core", { x: 0.8, y: 1.3, w: 5, h: 1.6, fontSize: 40, color: COLORS.accentCyan, fontFace: F.title, lineSpacingMultiple: 1.1, shadow: { type: "outer", blur: 20, offset: 0, color: COLORS.accentCyan, opacity: 0.4 } });
    s.addText("Núcleo de Supervisión Industrial de nueva generación\ncon visualización 3D, ciberseguridad nativa\ny preparada para inteligencia artificial.", { x: 0.8, y: 3.0, w: 5, h: 0.9, fontSize: 12, color: COLORS.lightGray, fontFace: F.body, lineSpacingMultiple: 1.4 });
    s.addShape("rect", { x: 0.8, y: 4.1, w: 1.5, h: 0.04, fill: { type: "solid", color: COLORS.accentGold } });
    s.addText("EVOLUCIÓN →", { x: 0.8, y: 4.3, w: 3, h: 0.3, fontSize: 10, color: COLORS.accentGold, fontFace: F.accent });
    s.addText("Aquafrisch Supervisor AI Core", { x: 0.8, y: 4.6, w: 5, h: 0.5, fontSize: 22, color: COLORS.accentGold, fontFace: F.title, italic: true });
    s.addText("Con AquarIA™ — Inteligencia Artificial nativa de Aquafrisch", { x: 0.8, y: 5.1, w: 5, h: 0.4, fontSize: 12, color: COLORS.lightGray, fontFace: F.body, italic: true });
    shot(s, 5.3, 1.0, 4.4, 3.3, img("01_login.png"), COLORS.accentCyan);
    card(s, 5.3, 4.5, 4.4, 2.1, { border: COLORS.accentBlue });
    s.addText("LO QUE NOS DIFERENCIA", { x: 5.5, y: 4.55, w: 4, h: 0.3, fontSize: 10, color: COLORS.accentCyan, fontFace: F.accent });
    ["✦  Visualización 3D Digital Twin","✦  Ciberseguridad CRA de serie","✦  IA nativa (no módulo externo)","✦  10 años de soporte garantizado","✦  Multi-máquina, una plataforma","✦  Configuración sin programar"].forEach((d,i) => {
      s.addText(d, { x: 5.5, y: 4.9+i*0.26, w: 4, h: 0.26, fontSize: 10, color: COLORS.lightGray, fontFace: F.body });
    });
    foot(s, n, TOTAL);
  }

  // === 4: SECCIÓN INNOVACIÓN VISUAL ===
  n++; section(pptx, "01", "INNOVACIÓN VISUAL\nDigital Twin 3D en Tiempo Real", null, n, TOTAL);

  // === 5: DIGITAL TWIN GRANDE ===
  n++; {
    const s = pptx.addSlide(); bg(s); glow(s);
    s.addText("Digital Twin 3D — Su máquina, en tiempo real", { x: 0.5, y: 0.3, w: 9, h: 0.6, fontSize: 26, color: COLORS.white, fontFace: F.title });
    s.addText("Visualización inmersiva que ningún competidor ofrece", { x: 0.5, y: 0.85, w: 9, h: 0.35, fontSize: 13, color: COLORS.accentBlue, fontFace: F.sub, italic: true });
    shot(s, 0.3, 1.4, 9.4, 4.4, img("02_vista_3d.png"), COLORS.accentCyan);
    [{ i: "🎮", t: "Múltiples cámaras" },{ i: "⚡", t: "Datos PLC real-time" },{ i: "🏗️", t: "Modelos 3D industriales" },{ i: "🖥️", t: "En navegador, sin instalar" }].forEach((f,i) => {
      s.addText(`${f.i}  ${f.t}`, { x: 0.3+i*2.35, y: 6.1, w: 2.3, h: 0.6, fontSize: 10, color: COLORS.lightGray, fontFace: F.body });
    });
    foot(s, n, TOTAL);
  }

  // === 6: INMERSIVA + DETALLES ===
  n++; {
    const s = pptx.addSlide(); bg(s); glow(s);
    s.addText("Experiencia Inmersiva — Control Total desde el 3D", { x: 0.5, y: 0.3, w: 9, h: 0.6, fontSize: 24, color: COLORS.white, fontFace: F.title });
    shot(s, 0.3, 1.1, 5.5, 3.3, img("02_01_vista_3d_inmersiva.png"), COLORS.accentCyan);
    shot(s, 6.0, 1.1, 3.7, 1.55, img("03_vista_3d_detalle.png"), COLORS.accentBlue);
    shot(s, 6.0, 2.85, 3.7, 1.55, img("03_vista_3d_detalle_01.png"), COLORS.accentBlue);
    card(s, 0.3, 4.7, 9.4, 1.0, { glow: COLORS.accentGold, border: COLORS.accentGold });
    s.addText("\"Nuestros clientes no ven un SCADA genérico. Ven su máquina real en 3D.\"", { x: 0.8, y: 4.7, w: 8.4, h: 0.6, fontSize: 16, color: COLORS.accentGold, fontFace: F.title, italic: true, align: "center", valign: "middle" });
    s.addText("— Ventaja competitiva directa: ningún competidor del sector tiene esto", { x: 0.8, y: 5.3, w: 8.4, h: 0.3, fontSize: 10, color: COLORS.medGray, fontFace: F.body, align: "center" });
    foot(s, n, TOTAL);
  }

  // === 7: GALERÍA 3D ===
  n++; {
    const s = pptx.addSlide(); bg(s); glow(s);
    s.addText("HMI 3D — Cada ángulo, cada detalle, cada dato", { x: 0.5, y: 0.3, w: 9, h: 0.6, fontSize: 24, color: COLORS.white, fontFace: F.title });
    const di = ["03_vista_3d_detalle_02.png","03_vista_3d_detalle_03.png","03_vista_3d_detalle_04.png","03_vista_3d_detalle_05.png"];
    const lb = ["Panel de control 3D con datos en vivo","Zoom a componentes y sensores","Monitorización de proceso en tiempo real","Vista completa de la instalación"];
    di.forEach((d, i) => {
      const col = i%2, row = Math.floor(i/2);
      const x = 0.3+col*4.85, y = 1.2+row*2.7;
      s.addText(lb[i].toUpperCase(), { x, y: y-0.05, w: 4.6, h: 0.3, fontSize: 9, color: COLORS.accentCyan, fontFace: F.accent });
      shot(s, x, y+0.25, 4.6, 2.2, img(d), COLORS.accentBlue);
    });
    foot(s, n, TOTAL);
  }

  // === 8: SECCIÓN CIBERSEGURIDAD ===
  n++; {
    const s = section(pptx, "02", "CIBERSEGURIDAD NATIVA\nEU Cyber Resilience Act", null, n, TOTAL);
    card(s, 0.8, 4.8, 3.5, 0.6, { glow: COLORS.accentGreen, border: COLORS.accentGreen });
    s.addText("✓  CUMPLIMIENTO ~95%  •  EU 2024/2847", { x: 0.8, y: 4.8, w: 3.5, h: 0.6, fontSize: 11, color: COLORS.accentGreen, fontFace: F.accent, align: "center", valign: "middle" });
  }

  // === 9: CRA TIMELINE ===
  n++; {
    const s = pptx.addSlide(); bg(s); glow(s);
    s.addText("Cyber Resilience Act — Calendario Obligatorio", { x: 0.5, y: 0.3, w: 9, h: 0.6, fontSize: 24, color: COLORS.white, fontFace: F.title });
    s.addText("Sin cumplimiento CRA, no se puede vender en la UE a partir de diciembre 2027", { x: 0.5, y: 0.85, w: 9, h: 0.35, fontSize: 12, color: COLORS.accentRed, fontFace: F.sub, italic: true });
    s.addShape("rect", { x: 0.8, y: 2.6, w: 8.4, h: 0.04, fill: { type: "solid", color: COLORS.accentCyan } });
    [{x:1.2,yr:"FEB 2026",lb:"HOY",sl:"Aquafrisch\n~95% compliant",c:COLORS.accentGreen},{x:3.3,yr:"JUN 2026",lb:"",sl:"Notificación\norganismos",c:COLORS.accentGold},{x:5.5,yr:"SEP 2026",lb:"",sl:"Obligación notif.\nvulnerabilidades",c:COLORS.accentGold},{x:7.9,yr:"DIC 2027",lb:"⚡ OBLIGATORIO",sl:"Cumplimiento total\nrequerido",c:COLORS.accentRed}].forEach(t => {
      s.addShape("ellipse", { x: t.x, y: 2.4, w: 0.4, h: 0.4, fill: { type: "solid", color: t.c }, shadow: { type: "outer", blur: 10, offset: 0, color: t.c, opacity: 0.6 } });
      if (t.lb) s.addText(t.lb, { x: t.x-0.6, y: 1.9, w: 1.8, h: 0.35, fontSize: 10, color: t.c, fontFace: F.accent, align: "center" });
      s.addText(t.yr, { x: t.x-0.6, y: 2.9, w: 1.8, h: 0.3, fontSize: 9, color: COLORS.medGray, fontFace: F.mono, align: "center" });
      s.addText(t.sl, { x: t.x-0.8, y: 3.2, w: 2.2, h: 0.5, fontSize: 9, color: t.c, fontFace: F.body, align: "center" });
    });
    card(s, 0.5, 4.1, 4.3, 2.5, { glow: COLORS.accentGreen, border: COLORS.accentGreen });
    s.addText("✓  AQUAFRISCH", { x: 0.7, y: 4.2, w: 3.9, h: 0.4, fontSize: 14, color: COLORS.accentGreen, fontFace: F.accent });
    ["Ciberseguridad integrada de serie","SBOM automático (CycloneDX)","Audit Log cifrado SHA-256","Verificación integridad cada 2 min","Soporte garantizado 10 años","Scanner vulnerabilidades integrado","Monitorización hardware real-time"].forEach((x,i) => {
      s.addText(`✓  ${x}`, { x: 0.7, y: 4.65+i*0.26, w: 3.9, h: 0.26, fontSize: 9.5, color: COLORS.lightGray, fontFace: F.body });
    });
    card(s, 5.2, 4.1, 4.3, 2.5, { border: COLORS.accentRed });
    s.addText("✗  COMPETENCIA", { x: 5.4, y: 4.2, w: 3.9, h: 0.4, fontSize: 14, color: COLORS.accentRed, fontFace: F.accent });
    ["Sin plan de ciberseguridad","Sin SBOM ni trazabilidad","Sin audit logs industriales","Sin verificación de integridad","Soporte limitado a contrato","Sin gestión de vulnerabilidades","Sin monitorización de IPC"].forEach((x,i) => {
      s.addText(`✗  ${x}`, { x: 5.4, y: 4.65+i*0.26, w: 3.9, h: 0.26, fontSize: 9.5, color: COLORS.medGray, fontFace: F.body });
    });
    foot(s, n, TOTAL);
  }

  // === 10: AUDIT LOG + INTEGRIDAD ===
  n++; {
    const s = pptx.addSlide(); bg(s); glow(s);
    s.addText("Ciberseguridad que se ve — Audit Log e Integridad", { x: 0.5, y: 0.3, w: 9, h: 0.6, fontSize: 24, color: COLORS.white, fontFace: F.title });
    s.addText("No es un PDF de cumplimiento. Es software funcionando.", { x: 0.5, y: 0.85, w: 9, h: 0.35, fontSize: 12, color: COLORS.accentCyan, fontFace: F.sub, italic: true });
    s.addText("AUDIT LOG — Cadena de Hash SHA-256", { x: 0.3, y: 1.4, w: 4.6, h: 0.3, fontSize: 10, color: COLORS.accentCyan, fontFace: F.accent });
    shot(s, 0.3, 1.8, 4.6, 2.6, img("05_audit_log.png"), COLORS.accentCyan);
    s.addText("VERIFICACIÓN DE INTEGRIDAD", { x: 5.1, y: 1.4, w: 4.6, h: 0.3, fontSize: 10, color: COLORS.accentGreen, fontFace: F.accent });
    shot(s, 5.1, 1.8, 2.2, 2.2, img("06_integridad.png"), COLORS.accentGreen);
    shot(s, 7.4, 1.8, 2.2, 2.2, img("06_integridad_01.png"), COLORS.accentGreen);
    card(s, 0.3, 4.7, 9.4, 1.9, { border: COLORS.cardBorder });
    s.addText("¿QUÉ SIGNIFICA PARA EL CLIENTE?", { x: 0.5, y: 4.8, w: 9, h: 0.3, fontSize: 11, color: COLORS.accentGold, fontFace: F.accent });
    ["🔒  Cada acción queda registrada con firma criptográfica — imposible de manipular","🛡️  El software se auto-verifica cada 2 minutos — detecta modificaciones no autorizadas","📋  Cumple trazabilidad CRA (Art. 13) e IEC 62443 (Seguridad en Sistemas de Automatización y Control Industrial)","⚡  Respuesta inmediata ante cualquier intento de intrusión o manipulación"].forEach((b,i) => {
      s.addText(b, { x: 0.5, y: 5.2+i*0.33, w: 9, h: 0.33, fontSize: 10, color: COLORS.lightGray, fontFace: F.body });
    });
    foot(s, n, TOTAL);
  }

  // === 11: SBOM + VULNERABILIDADES ===
  n++; {
    const s = pptx.addSlide(); bg(s); glow(s);
    s.addText("Transparencia Total — SBOM y Scanner de Vulnerabilidades", { x: 0.5, y: 0.3, w: 9, h: 0.6, fontSize: 24, color: COLORS.white, fontFace: F.title });
    s.addText("SBOM — Software Bill of Materials", { x: 0.3, y: 1.2, w: 4.6, h: 0.3, fontSize: 10, color: COLORS.accentCyan, fontFace: F.accent });
    shot(s, 0.3, 1.6, 4.6, 1.5, img("07_sbom.png"), COLORS.accentCyan);
    shot(s, 0.3, 3.3, 4.6, 1.7, img("07_sbom_01.png"), COLORS.accentCyan);
    s.addText("SCANNER DE VULNERABILIDADES", { x: 5.1, y: 1.2, w: 4.6, h: 0.3, fontSize: 10, color: COLORS.accentRed, fontFace: F.accent });
    shot(s, 5.1, 1.6, 4.6, 2.0, img("08_vulnerabilidades.png"), COLORS.accentRed);
    card(s, 5.1, 3.8, 4.6, 1.2, { border: COLORS.accentGold });
    s.addText("SLA DE RESPUESTA (Tiempo máximo de resolución garantizado)", { x: 5.3, y: 3.85, w: 4.2, h: 0.3, fontSize: 9, color: COLORS.accentGold, fontFace: F.accent });
    s.addText("Crítica: 48 horas  •  Alta: 7 días\nMedia: 30 días  •  Baja: próxima actualización", { x: 5.3, y: 4.2, w: 4.2, h: 0.55, fontSize: 10, color: COLORS.lightGray, fontFace: F.body, lineSpacingMultiple: 1.3 });
    card(s, 0.3, 5.3, 9.4, 1.3, { border: COLORS.cardBorder });
    s.addText("REQUISITO CRA (Anexo I, Parte II): Todo producto digital debe proporcionar SBOM y gestionar vulnerabilidades.", { x: 0.5, y: 5.4, w: 9, h: 0.35, fontSize: 10, color: COLORS.accentGold, fontFace: F.accent });
    s.addText("Aquafrisch genera el SBOM automáticamente, escanea bases de datos (OSV, NVD, GitHub) y ofrece SLA (Service Level Agreement — Acuerdo de nivel de respuesta) definidos.\nEl cliente tiene transparencia total sobre qué software ejecuta su máquina.", { x: 0.5, y: 5.8, w: 9, h: 0.6, fontSize: 10, color: COLORS.lightGray, fontFace: F.body, lineSpacingMultiple: 1.3 });
    foot(s, n, TOTAL);
  }

  // === 12: CRA DOCUMENTACIÓN — Próximos Pasos ===
  n++; {
    const s = pptx.addSlide(); bg(s); glow(s);
    s.addText("CRA — Documentación y Próximos Pasos", { x: 0.5, y: 0.3, w: 9, h: 0.6, fontSize: 24, color: COLORS.white, fontFace: F.title });
    s.addText("El software está listo. Ahora completamos la documentación oficial para la certificación.", { x: 0.5, y: 0.85, w: 9, h: 0.35, fontSize: 12, color: COLORS.accentCyan, fontFace: F.sub, italic: true });

    // Barras de progreso
    card(s, 0.3, 1.5, 4.4, 1.8, { border: COLORS.accentGreen });
    s.addText("SOFTWARE — Implementación Técnica", { x: 0.5, y: 1.55, w: 4, h: 0.3, fontSize: 10, color: COLORS.accentGreen, fontFace: F.accent });
    // Barra 95%
    s.addShape("roundRect", { x: 0.5, y: 1.95, w: 4.0, h: 0.3, fill: { type: "solid", color: COLORS.cardBorder }, rectRadius: 0.05 });
    s.addShape("roundRect", { x: 0.5, y: 1.95, w: 3.8, h: 0.3, fill: { type: "solid", color: COLORS.accentGreen }, rectRadius: 0.05 });
    s.addText("95%", { x: 3.5, y: 1.95, w: 0.8, h: 0.3, fontSize: 11, color: COLORS.darkBg, fontFace: F.accent, align: "center", valign: "middle" });
    ["✓ Audit Log cifrado SHA-256","✓ SBOM automático CycloneDX","✓ Scanner vulnerabilidades","✓ Verificación integridad"].forEach((x,i) => {
      s.addText(x, { x: 0.5, y: 2.35+i*0.22, w: 4, h: 0.22, fontSize: 9, color: COLORS.lightGray, fontFace: F.body });
    });

    card(s, 5.3, 1.5, 4.4, 1.8, { border: COLORS.accentGold });
    s.addText("DOCUMENTACIÓN — Certificación Oficial", { x: 5.5, y: 1.55, w: 4, h: 0.3, fontSize: 10, color: COLORS.accentGold, fontFace: F.accent });
    // Barra 30%
    s.addShape("roundRect", { x: 5.5, y: 1.95, w: 4.0, h: 0.3, fill: { type: "solid", color: COLORS.cardBorder }, rectRadius: 0.05 });
    s.addShape("roundRect", { x: 5.5, y: 1.95, w: 1.2, h: 0.3, fill: { type: "solid", color: COLORS.accentGold }, rectRadius: 0.05 });
    s.addText("30%", { x: 5.5, y: 1.95, w: 1.2, h: 0.3, fontSize: 11, color: COLORS.darkBg, fontFace: F.accent, align: "center", valign: "middle" });
    ["⏳ Evaluación riesgos ciberseguridad","⏳ Documentación técnica (Anexo VII)","⏳ Manual seguridad usuario","⏳ Declaración UE conformidad"].forEach((x,i) => {
      s.addText(x, { x: 5.5, y: 2.35+i*0.22, w: 4, h: 0.22, fontSize: 9, color: COLORS.lightGray, fontFace: F.body });
    });

    // Sistema de Gestión Documental
    s.addText("SISTEMA DE GESTIÓN DOCUMENTAL (DMS) — En implementación", { x: 0.3, y: 3.6, w: 9.4, h: 0.3, fontSize: 11, color: COLORS.accentCyan, fontFace: F.accent });
    card(s, 0.3, 3.95, 9.4, 2.6, { border: COLORS.accentCyan });

    // 3 columnas de docs
    // Col 1: Pública
    s.addText("📄 DOCUMENTACIÓN\nPÚBLICA", { x: 0.5, y: 4.0, w: 2.8, h: 0.5, fontSize: 10, color: COLORS.accentGreen, fontFace: F.accent });
    ["Política de seguridad","Períodos de soporte (10 años)","Manual de usuario software","Declaración UE Conformidad"].forEach((x,i) => {
      s.addText(`▸ ${x}`, { x: 0.5, y: 4.5+i*0.22, w: 2.8, h: 0.22, fontSize: 9, color: COLORS.lightGray, fontFace: F.body });
    });

    // Col 2: Portal Cliente
    s.addText("🔐 PORTAL CLIENTE\n(Acceso con login)", { x: 3.5, y: 4.0, w: 2.8, h: 0.5, fontSize: 10, color: COLORS.accentBlue, fontFace: F.accent });
    ["SBOM por instalación","Configuración específica","Manual personalizado","Historial de actualizaciones"].forEach((x,i) => {
      s.addText(`▸ ${x}`, { x: 3.5, y: 4.5+i*0.22, w: 2.8, h: 0.22, fontSize: 9, color: COLORS.lightGray, fontFace: F.body });
    });

    // Col 3: Interno
    s.addText("🏢 DOCUMENTACIÓN\nINTERNA (Auditorías)", { x: 6.5, y: 4.0, w: 3.0, h: 0.5, fontSize: 10, color: COLORS.accentGold, fontFace: F.accent });
    ["Doc. técnica Anexo VII","Evaluación de riesgos","Gestión de terceros","Documentación por proyecto"].forEach((x,i) => {
      s.addText(`▸ ${x}`, { x: 6.5, y: 4.5+i*0.22, w: 3.0, h: 0.22, fontSize: 9, color: COLORS.lightGray, fontFace: F.body });
    });

    // Enlace empresa
    s.addText("Conectado con el sistema documental de la empresa (Directiva Máquinas + CRA)", { x: 0.5, y: 5.4, w: 9, h: 0.25, fontSize: 9, color: COLORS.medGray, fontFace: F.body, italic: true });

    // Timeline
    card(s, 0.3, 5.8, 9.4, 0.85, { border: COLORS.cardBorder });
    s.addText("CALENDARIO", { x: 0.5, y: 5.85, w: 9, h: 0.25, fontSize: 9, color: COLORS.accentCyan, fontFace: F.accent, align: "center" });
    [{d:"MAR 2026",t:"Evaluación\nriesgos",c:COLORS.accentRed},{d:"JUN 2026",t:"Doc. técnica\n+ Manual",c:COLORS.accentGold},{d:"SEP 2026",t:"Declaración\nUE conformidad",c:COLORS.accentCyan},{d:"DIC 2027",t:"Cumplimiento\ntotal ✓",c:COLORS.accentGreen}].forEach((t,i) => {
      const x = 0.7+i*2.3;
      s.addText(t.d, { x, y: 6.1, w: 1.8, h: 0.2, fontSize: 9, color: t.c, fontFace: F.accent, align: "center" });
      s.addText(t.t, { x, y: 6.3, w: 1.8, h: 0.3, fontSize: 8, color: COLORS.lightGray, fontFace: F.body, align: "center" });
    });
    foot(s, n, TOTAL);
  }

  // === 13: SECCIÓN GESTIÓN ===
  n++; section(pptx, "03", "GESTIÓN INTEGRAL\nAlarmas, Usuarios, Hardware", null, n, TOTAL);

  // === 13: ALARMAS + USUARIOS ===
  n++; {
    const s = pptx.addSlide(); bg(s); glow(s);
    s.addText("Alarmas y Gestión de Usuarios", { x: 0.5, y: 0.3, w: 9, h: 0.6, fontSize: 26, color: COLORS.white, fontFace: F.title });
    s.addText("SISTEMA DE ALARMAS", { x: 0.3, y: 1.1, w: 4.6, h: 0.3, fontSize: 10, color: COLORS.accentRed, fontFace: F.accent });
    shot(s, 0.3, 1.5, 4.6, 1.7, img("04_alarmas.png"), COLORS.accentRed);
    shot(s, 0.3, 3.4, 4.6, 1.4, img("04_alarmas_01.png"), COLORS.accentRed);
    s.addText("USUARIOS — 5 NIVELES DE ACCESO", { x: 5.1, y: 1.1, w: 4.6, h: 0.3, fontSize: 10, color: COLORS.accentBlue, fontFace: F.accent });
    shot(s, 5.1, 1.5, 4.6, 1.7, img("09_usuarios.png"), COLORS.accentBlue);
    shot(s, 5.1, 3.4, 4.6, 1.4, img("09_usuarios_01.png"), COLORS.accentBlue);
    card(s, 0.3, 5.1, 9.4, 1.5, { border: COLORS.cardBorder });
    s.addText("JERARQUÍA DE SEGURIDAD", { x: 0.5, y: 5.15, w: 9, h: 0.3, fontSize: 10, color: COLORS.accentCyan, fontFace: F.accent });
    [{l:"L1",n:"Administrador",d:"Seguridad cliente",c:COLORS.accentGold},{l:"L2",n:"Operador",d:"Control diario",c:COLORS.accentCyan},{l:"L3",n:"Mantenimiento",d:"Calibración",c:COLORS.accentBlue},{l:"L4",n:"Visor",d:"Solo lectura",c:COLORS.accentGreen}].forEach((r,i) => {
      const x = 1.0+i*2.2;
      s.addText(r.l, { x, y: 5.5, w: 0.5, h: 0.35, fontSize: 14, color: r.c, fontFace: F.accent });
      s.addText(r.n, { x: x+0.5, y: 5.45, w: 1.3, h: 0.2, fontSize: 10, color: COLORS.white, fontFace: F.accent });
      s.addText(r.d, { x: x+0.5, y: 5.65, w: 1.3, h: 0.2, fontSize: 8, color: COLORS.medGray, fontFace: F.body });
    });
    foot(s, n, TOTAL);
  }

  // === 14: HARDWARE MONITOR ===
  n++; {
    const s = pptx.addSlide(); bg(s); glow(s);
    s.addText("Monitorización de Hardware Industrial (IPC)", { x: 0.5, y: 0.3, w: 9, h: 0.6, fontSize: 26, color: COLORS.white, fontFace: F.title });
    s.addText("Conocer el estado real del PC industrial que controla su máquina", { x: 0.5, y: 0.85, w: 9, h: 0.35, fontSize: 13, color: COLORS.accentBlue, fontFace: F.sub, italic: true });
    shot(s, 0.3, 1.4, 6.0, 3.8, img("10_harware monitor.png"), COLORS.accentCyan);
    shot(s, 6.5, 1.4, 3.2, 2.3, img("10_harware monitor_01.png"), COLORS.accentBlue);
    card(s, 6.5, 3.9, 3.2, 1.3, { border: COLORS.accentGold });
    s.addText("MÉTRICAS EN TIEMPO REAL", { x: 6.7, y: 4.0, w: 2.8, h: 0.3, fontSize: 9, color: COLORS.accentGold, fontFace: F.accent });
    ["▸ CPU, RAM, Disco","▸ Temperatura","▸ Estado del sistema","▸ Alertas automáticas"].forEach((f,i) => {
      s.addText(f, { x: 6.7, y: 4.35+i*0.22, w: 2.8, h: 0.22, fontSize: 9, color: COLORS.lightGray, fontFace: F.body });
    });
    card(s, 0.3, 5.5, 9.4, 1.1, { glow: COLORS.accentGold, border: COLORS.accentGold });
    s.addText("\"El cliente sabe el estado de salud de su IPC en todo momento.\nSi algo falla, lo sabe ANTES de que la máquina pare.\"", { x: 0.8, y: 5.5, w: 8.4, h: 1.1, fontSize: 14, color: COLORS.accentGold, fontFace: F.title, italic: true, align: "center", valign: "middle" });
    foot(s, n, TOTAL);
  }

  // === 15: MANTENIMIENTO, ESTADÍSTICAS y SERVICIO AL CLIENTE ===
  n++; {
    const s = pptx.addSlide(); bg(s); glow(s);
    s.addText("Mantenimiento Inteligente y Estadísticas de Operación", { x: 0.5, y: 0.3, w: 9, h: 0.6, fontSize: 24, color: COLORS.white, fontFace: F.title });
    s.addText("De la reparación reactiva al mantenimiento predictivo", { x: 0.5, y: 0.85, w: 9, h: 0.35, fontSize: 12, color: COLORS.accentCyan, fontFace: F.sub, italic: true });

    // Columna izq: HOY
    card(s, 0.3, 1.5, 4.5, 3.2, { glow: COLORS.accentGreen, border: COLORS.accentGreen });
    s.addText("✓  HOY — Supervisor Core", { x: 0.5, y: 1.55, w: 4.1, h: 0.35, fontSize: 12, color: COLORS.accentGreen, fontFace: F.accent });
    ["📊  Estadísticas de ciclos de lavado por máquina","⏱️  Tiempos de operación y paradas registradas","🚨  Historial completo de alarmas con timestamp","🛠️  Registro de intervenciones de mantenimiento","📈  Contadores de uso por componente","🔔  Alertas de mantenimiento programado","💾  Datos exportables para análisis externo"].forEach((x,i) => {
      s.addText(x, { x: 0.5, y: 2.0+i*0.35, w: 4.1, h: 0.35, fontSize: 10, color: COLORS.lightGray, fontFace: F.body });
    });

    // Columna der: MAÑANA con AquarIA
    card(s, 5.2, 1.5, 4.5, 3.2, { glow: COLORS.accentGold, border: COLORS.accentGold });
    s.addText("🔮  MAÑANA — Con AquarIA™", { x: 5.4, y: 1.55, w: 4.1, h: 0.35, fontSize: 12, color: COLORS.accentGold, fontFace: F.accent });
    ["🧠  Predicción de fallos antes de que ocurran","⚡  Optimización automática de consumo agua/energía","📉  Reducción de paradas no planificadas (-40%)","🔧  Planificación inteligente de recambios","🎯  Recetas de lavado adaptativas según estado","💬  Asistente IA para diagnóstico del operador","📊  Dashboard predictivo con tendencias"].forEach((x,i) => {
      s.addText(x, { x: 5.4, y: 2.0+i*0.35, w: 4.1, h: 0.35, fontSize: 10, color: COLORS.lightGray, fontFace: F.body });
    });

    // Beneficios para el cliente
    card(s, 0.3, 5.0, 9.4, 1.6, { border: COLORS.accentCyan });
    s.addText("¿QUÉ GANA EL CLIENTE?", { x: 0.5, y: 5.05, w: 9, h: 0.3, fontSize: 11, color: COLORS.accentCyan, fontFace: F.accent, align: "center" });
    [{i:"💰",t:"AHORRO",d:"Reducción consumo agua\nhasta 25%. Menos\nquímicos y energía.",c:COLORS.accentGreen},{i:"⏰",t:"DISPONIBILIDAD",d:"Máquina operativa\nmás tiempo. Menos\nparadas imprevistas.",c:COLORS.accentCyan},{i:"📅",t:"PLANIFICACIÓN",d:"Mantenimiento cuando\ntoca, no cuando falla.\nRecambios a tiempo.",c:COLORS.accentBlue},{i:"📊",t:"DATOS",d:"Informes de operación\npara justificar inversión\ny optimizar contratos.",c:COLORS.accentGold}].forEach((b,i) => {
      const x = 0.5+i*2.35;
      s.addText(b.i, { x, y: 5.35, w: 2.1, h: 0.35, fontSize: 18, align: "center" });
      s.addText(b.t, { x, y: 5.65, w: 2.1, h: 0.25, fontSize: 9, color: b.c, fontFace: F.accent, align: "center" });
      s.addText(b.d, { x, y: 5.9, w: 2.1, h: 0.55, fontSize: 8.5, color: COLORS.lightGray, fontFace: F.body, align: "center", lineSpacingMultiple: 1.2 });
    });
    foot(s, n, TOTAL);
  }

  // === 16: SECCIÓN MULTI-PROYECTO ===
  n++; section(pptx, "04", "UNA PLATAFORMA\nTodas sus máquinas", null, n, TOTAL);

  // === 16: MULTI-PROYECTO ===
  n++; {
    const s = pptx.addSlide(); bg(s); glow(s);
    s.addText("Una Plataforma — Todas las Instalaciones", { x: 0.5, y: 0.3, w: 9, h: 0.6, fontSize: 26, color: COLORS.white, fontFace: F.title });
    s.addText("Mismo software. Distintas máquinas. Configuración independiente.", { x: 0.5, y: 0.85, w: 9, h: 0.35, fontSize: 13, color: COLORS.accentBlue, fontFace: F.sub, italic: true });
    card(s, 3.0, 1.5, 4.0, 1.0, { glow: COLORS.accentCyan, border: COLORS.accentCyan });
    s.addText("AQUAFRISCH SUPERVISOR CORE", { x: 3.0, y: 1.5, w: 4.0, h: 1.0, fontSize: 14, color: COLORS.accentCyan, fontFace: F.accent, align: "center", valign: "middle" });
    [{n:"Túnel de Lavado\nMadrid",x:0.3,c:COLORS.accentBlue},{n:"Cabina Lavado\nBarcelona",x:2.55,c:COLORS.accentGold},{n:"Lavado Bogies\nSevilla",x:4.8,c:COLORS.accentGreen},{n:"Extracción WC\nParís",x:7.05,c:COLORS.accentCyan}].forEach(p => {
      card(s, p.x, 3.1, 2.1, 0.9, { border: p.c });
      s.addText(p.n, { x: p.x, y: 3.1, w: 2.1, h: 0.9, fontSize: 9.5, color: p.c, fontFace: F.body, align: "center", valign: "middle" });
      s.addShape("rect", { x: p.x+1, y: 2.5, w: 0.02, h: 0.6, fill: { type: "solid", color: COLORS.cardBorder } });
    });
    card(s, 0.3, 4.3, 9.4, 2.3, { border: COLORS.cardBorder });
    s.addText("CADA INSTALACIÓN INCLUYE:", { x: 0.5, y: 4.35, w: 9, h: 0.3, fontSize: 11, color: COLORS.accentCyan, fontFace: F.accent, align: "center" });
    [{i:"📋",t:"Config Excel\npropia",d:"Sin programar"},{i:"🏗️",t:"Modelos 3D\npropios",d:"GLB/GLTF"},{i:"💾",t:"Base datos\nindependiente",d:"Aislada"},{i:"🔄",t:"Backup\nautomático",d:"Restaurable"},{i:"🔐",t:"Seguridad\naislada",d:"Por proyecto"}].forEach((f,i) => {
      const x = 0.5+i*1.85;
      s.addText(f.i, { x, y: 4.75, w: 1.7, h: 0.4, fontSize: 22, align: "center" });
      s.addText(f.t, { x, y: 5.15, w: 1.7, h: 0.5, fontSize: 10, color: COLORS.white, fontFace: F.body, align: "center" });
      s.addText(f.d, { x, y: 5.6, w: 1.7, h: 0.3, fontSize: 8, color: COLORS.medGray, fontFace: F.body, align: "center" });
    });
    foot(s, n, TOTAL);
  }

  // === 17: SECCIÓN FUTURO ===
  n++; section(pptx, "05", "EL FUTURO\nAquarIA™ e Industria 6.0", null, n, TOTAL);

  // === 18: ROADMAP ===
  n++; {
    const s = pptx.addSlide(); bg(s); glow(s);
    s.addText("Roadmap — De Supervisor Core a AI Core", { x: 0.5, y: 0.3, w: 9, h: 0.6, fontSize: 24, color: COLORS.accentGold, fontFace: F.title, shadow: { type: "outer", blur: 15, offset: 0, color: COLORS.accentGold, opacity: 0.4 } });
    s.addText("AquarIA™ — IA nativa, parte estructural del sistema, no un módulo externo", { x: 0.5, y: 0.85, w: 9, h: 0.35, fontSize: 12, color: COLORS.lightGray, fontFace: F.sub, italic: true });
    [{y:"2026",t:"SUPERVISOR CORE",d:"Visualización 3D • SCADA real-time • Ciberseguridad CRA • Multi-proyecto",c:COLORS.accentGreen,b:"✓ DISPONIBLE"},{y:"2027",t:"SUPERVISOR AI CORE v1",d:"AquarIA™ Mantenimiento Predictivo • Detección anomalías • Optimización consumos",c:COLORS.accentCyan,b:"EN DESARROLLO"},{y:"2028",t:"SUPERVISOR AI CORE v2",d:"Diagnóstico auto-asistido • Asistente IA operador • Recetas inteligentes",c:COLORS.accentBlue,b:"PLANIFICADO"},{y:"2030+",t:"PLATAFORMA SaaS",d:"Modelo negocio recurrente • Flota en la nube • Analytics multi-site",c:COLORS.accentGold,b:"VISIÓN"}].forEach((r,i) => {
      const yy = 1.5+i*1.3;
      if (i<3) s.addShape("rect", { x: 1.55, y: yy+0.5, w: 0.03, h: 1.05, fill: { type: "solid", color: COLORS.cardBorder } });
      s.addShape("ellipse", { x: 1.3, y: yy+0.15, w: 0.5, h: 0.5, fill: { type: "solid", color: r.c }, shadow: { type: "outer", blur: 10, offset: 0, color: r.c, opacity: 0.5 } });
      s.addText(r.y, { x: 0.2, y: yy+0.1, w: 1, h: 0.5, fontSize: 14, color: r.c, fontFace: F.accent, align: "center" });
      card(s, 2.2, yy, 7.3, 1.05, { border: r.c });
      s.addText(r.t, { x: 2.4, y: yy+0.05, w: 5, h: 0.35, fontSize: 13, color: r.c, fontFace: F.accent });
      s.addText(r.d, { x: 2.4, y: yy+0.4, w: 5, h: 0.5, fontSize: 10, color: COLORS.lightGray, fontFace: F.body, lineSpacingMultiple: 1.3 });
      s.addText(r.b, { x: 7.5, y: yy+0.1, w: 1.8, h: 0.35, fontSize: 8, color: r.c, fontFace: F.accent, align: "center" });
    });
    s.addText("Base tecnológica propia para la próxima década  •  2025–2035", { x: 0.5, y: 6.5, w: 9, h: 0.3, fontSize: 11, color: COLORS.accentGold, fontFace: F.sub, align: "center", italic: true });
    foot(s, n, TOTAL);
  }

  // === 19: AquarIA™ ===
  n++; {
    const s = pptx.addSlide(); bg(s); glow(s);
    s.addText("AquarIA™ — ¿Qué hará por sus operadores?", { x: 0.5, y: 0.3, w: 9, h: 0.6, fontSize: 26, color: COLORS.accentGold, fontFace: F.title });
    [{i:"🔮",t:"PREDICTIVO",c:COLORS.accentCyan,its:["Anticipa fallos antes de que ocurran","Reduce paradas no planificadas 40%","Programa mantenimiento óptimo","Extiende vida útil componentes"]},{i:"⚡",t:"OPTIMIZACIÓN",c:COLORS.accentGold,its:["Reduce consumo agua hasta 25%","Optimiza ciclos por tipo de tren","Adapta programas al estado real","ROI (Retorno de Inversión) demostrable"]},{i:"🧠",t:"ASISTENCIA",c:COLORS.accentGreen,its:["Diagnóstico asistido por IA","Guía paso a paso al operador","Recetas inteligentes adaptativas","Aprendizaje de datos reales"]}].forEach((cp,i) => {
      const x = 0.3+i*3.2;
      card(s, x, 1.2, 3.0, 4.3, { glow: cp.c, border: cp.c });
      s.addText(cp.i, { x, y: 1.3, w: 3.0, h: 0.6, fontSize: 32, align: "center" });
      s.addText(cp.t, { x, y: 1.9, w: 3.0, h: 0.4, fontSize: 14, color: cp.c, fontFace: F.accent, align: "center" });
      s.addShape("rect", { x: x+0.5, y: 2.35, w: 2.0, h: 0.02, fill: { type: "solid", color: cp.c } });
      cp.its.forEach((it,j) => {
        s.addText(`▸  ${it}`, { x: x+0.2, y: 2.5+j*0.5, w: 2.6, h: 0.45, fontSize: 10.5, color: COLORS.lightGray, fontFace: F.body });
      });
    });
    // Note: ROI items use explanation
    card(s, 0.3, 5.8, 9.4, 0.8, { glow: COLORS.accentGold, border: COLORS.accentGold });
    s.addText("\"AquarIA™ no reemplaza al operador. Le da superpoderes.\"", { x: 0.8, y: 5.8, w: 8.4, h: 0.8, fontSize: 18, color: COLORS.accentGold, fontFace: F.title, italic: true, align: "center", valign: "middle" });
    foot(s, n, TOTAL);
  }

  // === 20: CIERRE ===
  n++; {
    const s = pptx.addSlide(); bg(s);
    s.addShape("rect", { x: 0, y: 0, w: "100%", h: 0.1, fill: { type: "solid", color: COLORS.accentCyan }, shadow: { type: "outer", blur: 40, offset: 10, color: COLORS.accentCyan, opacity: 0.8 } });
    s.addShape("rect", { x: 8.0, y: 0.5, w: 0.01, h: 4, fill: { type: "solid", color: COLORS.cardBorder }, rotate: 20 });
    s.addShape("rect", { x: 8.7, y: 0.3, w: 0.01, h: 5, fill: { type: "solid", color: COLORS.cardBorder }, rotate: 15 });
    s.addText("AQUAFRISCH", { x: 0.8, y: 1.2, w: 8, h: 0.5, fontSize: 18, color: COLORS.accentCyan, fontFace: F.accent, charSpacing: 8 });
    s.addText("El futuro del lavado\nferroviario es inteligente.", { x: 0.8, y: 1.8, w: 8, h: 1.4, fontSize: 40, color: COLORS.white, fontFace: F.title, lineSpacingMultiple: 1.1 });
    s.addText("Y ya está aquí.", { x: 0.8, y: 3.1, w: 8, h: 0.7, fontSize: 36, color: COLORS.accentGold, fontFace: F.title, italic: true, shadow: { type: "outer", blur: 15, offset: 0, color: COLORS.accentGold, opacity: 0.4 } });
    s.addShape("rect", { x: 0.8, y: 4.0, w: 2, h: 0.04, fill: { type: "solid", color: COLORS.accentCyan } });
    ["✦  Primer sistema de supervisión 3D con ciberseguridad CRA nativa del sector","✦  Tecnología propia con perspectiva a 10 años (2025–2035)","✦  AquarIA™ — IA nativa para mantenimiento predictivo y optimización","✦  De fabricante de hardware a líder en software industrial","✦  Modelo de negocio evolutivo: producto → software → servicio recurrente (SaaS)"].forEach((p,i) => {
      s.addText(p, { x: 0.8, y: 4.2+i*0.3, w: 8, h: 0.3, fontSize: 11.5, color: COLORS.lightGray, fontFace: F.body });
    });
    card(s, 0.8, 5.9, 3.5, 0.9, { glow: COLORS.accentCyan, border: COLORS.accentCyan });
    s.addText("¿Hablamos?", { x: 0.8, y: 5.9, w: 3.5, h: 0.4, fontSize: 18, color: COLORS.accentCyan, fontFace: F.accent, align: "center" });
    s.addText("www.aquafrisch.com", { x: 0.8, y: 6.3, w: 3.5, h: 0.35, fontSize: 12, color: COLORS.lightGray, fontFace: F.body, align: "center" });
    card(s, 4.8, 5.9, 1.8, 0.9, { border: COLORS.accentGreen });
    s.addText("EU CRA\nCOMPLIANT", { x: 4.8, y: 5.9, w: 1.8, h: 0.9, fontSize: 10, color: COLORS.accentGreen, fontFace: F.accent, align: "center", valign: "middle" });
    card(s, 6.8, 5.9, 1.8, 0.9, { border: COLORS.accentGold });
    s.addText("INDUSTRY\n6.0 READY", { x: 6.8, y: 5.9, w: 1.8, h: 0.9, fontSize: 10, color: COLORS.accentGold, fontFace: F.accent, align: "center", valign: "middle" });
    s.addShape("rect", { x: 0, y: 7.0, w: "100%", h: 0.5, fill: { type: "solid", color: "050815" } });
    s.addText("© 2026 Aquafrisch  •  Todos los derechos reservados  •  Aquafrisch Supervisor Core™", { x: 0, y: 7.05, w: "100%", h: 0.4, fontSize: 9, color: COLORS.medGray, fontFace: F.body, align: "center" });
  }

  const out = path.join(__dirname, "Aquafrisch_Supervisor_Core_2026.pptx");
  await pptx.writeFile({ fileName: out });
  console.log(`\n✅ Presentación generada: ${TOTAL} slides con capturas REALES!`);
  console.log(`📁 ${out}`);
}

gen().catch(console.error);
