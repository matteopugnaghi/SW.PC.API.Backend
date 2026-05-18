// Inyecta traducciones de PrintPreviewModal + QrPreviewModal.
const fs = require('fs');
const path = require('path');

const F = path.join(__dirname, '..', 'Projects', 'A72.TOUTWP', 'translations', 'translations.json');
const j = JSON.parse(fs.readFileSync(F, 'utf8'));

const N = {
  // PrintPreviewModal
  'printPreview.defaultTitle':   { SPA: 'Vista previa', ENG: 'Preview', FRA: 'Aperçu', ITA: 'Anteprima' },
  'printPreview.button.printNow':{ SPA: 'Imprimir ahora', ENG: 'Print now', FRA: 'Imprimer maintenant', ITA: 'Stampa ora' },
  'printPreview.button.close':   { SPA: 'Cerrar', ENG: 'Close', FRA: 'Fermer', ITA: 'Chiudi' },

  // QrPreviewModal
  'qrPreview.title':           { SPA: 'Escanea con tu móvil', ENG: 'Scan with your phone', FRA: 'Scannez avec votre mobile', ITA: 'Scansiona col tuo cellulare' },
  'qrPreview.description':     { SPA: 'Tu móvil abrirá la <b>app Mail</b> con el informe pre-rellenado.', ENG: 'Your phone will open the <b>Mail app</b> with the report pre-filled.', FRA: 'Votre mobile ouvrira l\'<b>application Mail</b> avec le rapport pré-rempli.', ITA: 'Il tuo cellulare aprirà l\'<b>app Mail</b> con il report precompilato.' },
  'qrPreview.recipient':       { SPA: 'Destinatario', ENG: 'Recipient', FRA: 'Destinataire', ITA: 'Destinatario' },
  'qrPreview.tooLarge':        { SPA: 'Informe demasiado grande para QR', ENG: 'Report too large for QR', FRA: 'Rapport trop volumineux pour QR', ITA: 'Report troppo grande per il QR' },
  'qrPreview.tooLargeDetail':  { SPA: '{{len}} caracteres, máx {{max}}', ENG: '{{len}} characters, max {{max}}', FRA: '{{len}} caractères, max {{max}}', ITA: '{{len}} caratteri, max {{max}}' },
  'qrPreview.usePrint':        { SPA: 'Use Imprimir / PDF para la versión completa.', ENG: 'Use Print / PDF for the full version.', FRA: 'Utilisez Imprimer / PDF pour la version complète.', ITA: 'Usa Stampa / PDF per la versione completa.' },
  'qrPreview.chars':           { SPA: '{{count}} chars', ENG: '{{count}} chars', FRA: '{{count}} car.', ITA: '{{count}} car.' },
  'qrPreview.close':           { SPA: 'Cerrar', ENG: 'Close', FRA: 'Fermer', ITA: 'Chiudi' },
};

j.translations = j.translations || {};
let added = 0, skipped = 0;
for (const [k, v] of Object.entries(N)) {
  if (j.translations[k]) { skipped++; continue; }
  j.translations[k] = v;
  added++;
}
j.pages = j.pages || {};
j.pages.STATISTICS = j.pages.STATISTICS || { labels: [] };
const set = new Set(j.pages.STATISTICS.labels);
for (const k of Object.keys(N)) set.add(k);
j.pages.STATISTICS.labels = Array.from(set);
fs.writeFileSync(F, JSON.stringify(j, null, 2), 'utf8');
console.log('Added:', added, 'Skipped:', skipped, 'Total STATISTICS:', j.pages.STATISTICS.labels.length);
