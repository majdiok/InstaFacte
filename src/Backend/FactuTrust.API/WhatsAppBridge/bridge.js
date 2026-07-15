'use strict';

/*
 * Pont WhatsApp minimal piloté par l'API .NET (FactuTrust).
 *
 * Rôle : encapsuler whatsapp-web.js (unique dépendance npm) et dialoguer avec le processus parent
 * .NET par stdin/stdout, une ligne JSON par message. Le pont reste « bête » : il transmet les
 * messages entrants bruts (le filtrage et toute la logique métier vivent côté .NET).
 *
 * stdout (événements) : {"type":"state","state":...} | {"type":"qr","qr":...}
 *                       {"type":"message",...} | {"type":"sent","id":...,"ok":...,"error":...}
 * stdin  (commandes)  : {"type":"send","id":...,"chatId":...,"text":...} | {"type":"logout"} | {"type":"shutdown"}
 *
 * Cycle de vie : auto-arrêt quand stdin se ferme (mort du parent détectée) ou sur "shutdown".
 * La session est persistée par LocalAuth dans WA_SESSION_DIR (fourni par le parent).
 */

const readline = require('node:readline');
const { Client, LocalAuth } = require('whatsapp-web.js');

const sessionDir = process.env.WA_SESSION_DIR || '.whatsapp-session';

/** Écrit un événement JSON sur stdout (une ligne complète et atomique). */
function emit(event) {
  process.stdout.write(JSON.stringify(event) + '\n');
}

function emitState(state) {
  emit({ type: 'state', state });
}

let client = null;
let shuttingDown = false;

function buildClient() {
  const c = new Client({
    authStrategy: new LocalAuth({ dataPath: sessionDir }),
    puppeteer: {
      headless: true,
      args: ['--no-sandbox', '--disable-setuid-sandbox']
    }
  });

  c.on('qr', qr => emit({ type: 'qr', qr }));
  c.on('authenticated', () => emitState('authenticated'));
  c.on('auth_failure', () => emitState('auth_failure'));
  c.on('ready', () => emitState('ready'));
  c.on('disconnected', () => emitState('disconnected'));

  c.on('message', async msg => {
    try {
      // Transmission brute : le .NET filtre (discussions directes, non-soi, non-statut, texte).
      emit({
        type: 'message',
        externalMessageId: msg.id && msg.id._serialized ? msg.id._serialized : '',
        externalUserId: msg.from || '',
        externalChatId: msg.from || '',
        text: typeof msg.body === 'string' ? msg.body : '',
        sentAtUnixSeconds: typeof msg.timestamp === 'number' ? msg.timestamp : 0,
        fromMe: msg.fromMe === true,
        isStatus: msg.isStatus === true,
        messageType: msg.type || ''
      });
    } catch (err) {
      process.stderr.write('message handler error: ' + String(err) + '\n');
    }
  });

  return c;
}

async function start() {
  emitState('initializing');
  client = buildClient();
  try {
    await client.initialize();
  } catch (err) {
    process.stderr.write('initialize error: ' + String(err) + '\n');
    emitState('auth_failure');
  }
}

async function handleSend(cmd) {
  const id = cmd.id || '';
  if (!client) {
    emit({ type: 'sent', id, ok: false, error: 'client not ready' });
    return;
  }
  try {
    await client.sendMessage(cmd.chatId, cmd.text);
    emit({ type: 'sent', id, ok: true, error: null });
  } catch (err) {
    emit({ type: 'sent', id, ok: false, error: String(err) });
  }
}

async function handleLogout() {
  if (!client) return;
  try {
    await client.logout();
  } catch (err) {
    process.stderr.write('logout error: ' + String(err) + '\n');
  }
}

async function shutdown() {
  if (shuttingDown) return;
  shuttingDown = true;
  try {
    if (client) await client.destroy();
  } catch (err) {
    process.stderr.write('destroy error: ' + String(err) + '\n');
  } finally {
    process.exit(0);
  }
}

// Lecture des commandes ligne par ligne sur stdin.
const rl = readline.createInterface({ input: process.stdin });
rl.on('line', line => {
  let cmd;
  try {
    cmd = JSON.parse(line);
  } catch {
    return; // ligne non-JSON ignorée
  }
  if (!cmd || typeof cmd.type !== 'string') return;
  switch (cmd.type) {
    case 'send':
      void handleSend(cmd);
      break;
    case 'logout':
      void handleLogout();
      break;
    case 'shutdown':
      void shutdown();
      break;
    default:
      break;
  }
});

// Mort du parent : quand stdin se ferme, on s'arrête proprement (évite les orphelins Chromium).
rl.on('close', () => void shutdown());
process.stdin.on('end', () => void shutdown());
process.on('SIGTERM', () => void shutdown());
process.on('SIGINT', () => void shutdown());

void start();
