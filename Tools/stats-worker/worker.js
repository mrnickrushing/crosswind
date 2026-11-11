// Crosswind play statistics: the game POSTs a JSON array of events to /v1/events (Game/App/Telemetry.cs); they're
// checked and stored in D1 (schema.sql). Nothing can be read back over HTTP: queries run with
// `wrangler d1 execute crosswind-stats --remote` (see README.md). No IPs or other request details are stored. A daily
// cron removes events older than a year.

const MAX_BODY = 64 * 1024, MAX_EVENTS = 50, MAX_KEYS = 40, MAX_STR = 160;
const NAME = /^[A-Za-z][A-Za-z0-9._-]{0,63}$/;
const HEX64 = /^[0-9a-f]{64}$/;

export default {
  async fetch(req, env) {
    const url = new URL(req.url);
    if (url.pathname === "/v1/health") return new Response("ok");
    if (url.pathname !== "/v1/events") return new Response("not found", { status: 404 });
    if (req.method !== "POST") return new Response("POST only", { status: 405 });

    const text = await req.text();
    if (text.length > MAX_BODY) return new Response("too large", { status: 413 });
    let events;
    try { events = JSON.parse(text); } catch { return new Response("bad json", { status: 400 }); }
    if (!Array.isArray(events) || events.length === 0 || events.length > MAX_EVENTS) return new Response("expected 1-50 events", { status: 422 });

    const now = new Date().toISOString();
    const rows = [];
    for (const e of events) {
      const r = check(e);
      if (typeof r === "string") return new Response(r, { status: 422 });   // all or nothing, like the game expects
      rows.push(r);
    }
    const stmt = env.DB.prepare(
      "INSERT INTO events (received, sent, user, session, type, test, value, step, step_index, level, location, day, minutes, version, platform, model, payload) " +
      "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)");
    await env.DB.batch(rows.map(r => stmt.bind(now, r.sent, r.user, r.session, r.type, r.test, r.value,
      r.p["Game.goal"] ?? null, int(r.p["Game.goalIndex"]), int(r.p["Game.stars"]), r.p["Game.map"] ?? null,
      int(r.p["Game.airports"]), int(r.p["Game.minutesPlayed"]), r.p["App.version"] ?? null,
      r.p["Device.platform"] ?? null, r.p["Device.model"] ?? null, JSON.stringify(r.p))));
    return new Response(String(rows.length));
  },

  async scheduled(_event, env) {
    await env.DB.prepare("DELETE FROM events WHERE received < ?").bind(new Date(Date.now() - 365 * 86400e3).toISOString()).run();
  },
};

function check(e) {
  if (!e || typeof e !== "object" || Array.isArray(e)) return "event is not an object";
  if (typeof e.type !== "string" || !NAME.test(e.type)) return "bad type";
  if (typeof e.clientUser !== "string" || !HEX64.test(e.clientUser)) return "bad clientUser";
  const p = e.payload ?? {};
  if (typeof p !== "object" || Array.isArray(p)) return "bad payload";
  const keys = Object.keys(p);
  if (keys.length > MAX_KEYS) return "too many payload keys";
  const clean = {};
  for (const k of keys) {
    const v = p[k];
    if (!NAME.test(k)) return "bad payload key";
    if (typeof v !== "string" && typeof v !== "number" && typeof v !== "boolean") return "payload values are strings, numbers or booleans";
    clean[k] = typeof v === "string" ? v.slice(0, MAX_STR) : v;
  }
  return {
    type: e.type, user: e.clientUser, p: clean,
    session: typeof e.sessionID === "string" ? e.sessionID.slice(0, 64) : null,
    sent: typeof e.receivedAt === "string" ? e.receivedAt.slice(0, 32) : null,
    test: e.isTestMode === true || e.isTestMode === "true" ? 1 : 0,
    value: typeof e.floatValue === "number" && isFinite(e.floatValue) ? e.floatValue : null,
  };
}

function int(v) { const n = parseInt(v, 10); return Number.isFinite(n) ? n : null; }
