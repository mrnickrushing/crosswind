// Crosswind friends: sign in with an opaque account id, get a short friend code, add friends by code and see their
// airports (Game/Platform/FriendsClient.cs; rules shared with Core/Friends.cs). Stored in D1 (schema.sql). The account
// id and the bearer token are kept only as SHA-256 hashes; no IPs, emails or other request details are stored or
// logged. The only free text is the display name, cleaned and screened by cleanName; players can block and report
// one another (blocks, reports). See README.md.

const MAX_BODY = 4 * 1024, MAX_FRIENDS = 50, NAME_MAX = 20, MAP_MAX = 24;
const MAX_STARS = 99, MAX_FLIGHTS = 2_000_000_000;
const ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // no 0/O/1/I; 32 symbols, so a byte maps onto it evenly
const CODE_LEN = 6;
const DEFAULT_NAME = "Airport Manager";
const OK_CHAR = /^[\p{L}\p{Nd} '\-._]$/u;

// Names others can see are screened (App Review guideline 1.2). Keep in step with FriendRules (Core/Friends.cs):
// a name is folded (lower case, common digit/symbol swaps undone, everything but letters dropped) and refused when
// it holds one of ROOTS anywhere or one of WORDS as a whole word; a refused name becomes "Airport Manager".
const ROOTS = ["fuck", "fuk", "shit", "cunt", "nigg", "niga", "faggot", "bitch", "whore", "slut", "rapist", "nazi",
  "hitler", "porn", "penis", "vagina", "retard", "wank", "twat", "kike", "chink", "dildo", "pussy", "bastard", "asshole",
  "jizz", "boner", "blowjob", "handjob", "kkk"];
const WORDS = ["ass", "arse", "dicks", "cock", "cocks", "fag", "fags", "spic", "tit", "tits", "cum", "sex", "rape",
  "raped", "nude", "nudes", "anal", "piss", "coon", "gook", "homo", "dyke", "tranny"];
const SWAPS = { "0": "o", "1": "i", "3": "e", "4": "a", "5": "s", "7": "t", "8": "b", "@": "a", "$": "s", "!": "i" };

// Per-IP limits, per isolate, in memory only: the key is a hash of the address salted with a value that dies with
// the isolate, so nothing about an address is ever written anywhere.
const LIMITS = { register: [10, 600], add: [30, 600] };   // [requests, seconds]
let salt = null;   // drawn at the first request (no random values at global scope)
const hits = new Map();

export default {
  async fetch(req, env) {
    try {
      return await route(req, env);
    } catch {
      return fail(500, "Something went wrong");   // the error is not logged: it could carry request details
    }
  },
};

async function route(req, env) {
  const path = new URL(req.url).pathname;
  if (path === "/v1/health") return new Response("ok");

  if (path === "/v1/friends") {
    if (req.method !== "GET") return fail(405, "GET only");
    const me = await auth(req, env);
    if (!me) return fail(401, "Sign in again");
    return listFriends(env, me);
  }

  const handler = { "/v1/register": register, "/v1/profile": profile, "/v1/friends/add": add,
    "/v1/friends/remove": remove, "/v1/friends/block": block, "/v1/friends/report": report, "/v1/delete": del }[path];
  if (!handler) return fail(404, "Not found");
  if (req.method !== "POST") return fail(405, "POST only");

  const text = await req.text();
  if (text.length > MAX_BODY) return fail(413, "Too large");
  let body = {};
  if (text.trim() !== "") {
    try { body = JSON.parse(text); } catch { return fail(400, "Bad JSON"); }
    if (!body || typeof body !== "object" || Array.isArray(body)) return fail(400, "Bad JSON");
  }

  if (handler === register) return register(req, env, body);
  const me = await auth(req, env);
  if (!me) return fail(401, "Sign in again");
  return handler(req, env, body, me);
}

async function register(req, env, body) {
  if (await limited(req, "register")) return fail(429, "Too many tries, wait a few minutes");
  const account = body.account;
  if (typeof account !== "string" || account.length < 8 || account.length > 256) return fail(422, "Bad account");
  const accountHash = await sha256("account:" + account);
  const token = randomHex(32), tokenHash = await sha256(token);
  const p = cleanProfile(body), t = now();

  for (let tries = 0; tries < 8; tries++) {
    try {
      // An account that exists keeps its code and gets a new token (the old one stops working).
      const row = await env.DB.prepare(
        "INSERT INTO players (account, token, code, name, map, stars, flights, created, last_seen) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?) " +
        "ON CONFLICT(account) DO UPDATE SET token = excluded.token, name = excluded.name, map = excluded.map, " +
        "stars = excluded.stars, flights = excluded.flights, last_seen = excluded.last_seen RETURNING code")
        .bind(accountHash, tokenHash, newCode(), p.name, p.map, p.stars, p.flights, t, t).first();
      return json({ code: row.code, token });
    } catch (e) {
      if (!String(e && e.message).includes("UNIQUE")) throw e;   // the new code was taken: draw another
    }
  }
  return fail(503, "Try again");
}

async function profile(_req, env, body, me) {
  const p = cleanProfile(body);
  await env.DB.prepare("UPDATE players SET name = ?, map = ?, stars = ?, flights = ?, last_seen = ? WHERE id = ?")
    .bind(p.name, p.map, p.stars, p.flights, now(), me.id).run();
  return json({ ok: true });
}

async function add(req, env, body, me) {
  if (await limited(req, "add")) return fail(429, "Too many tries, wait a few minutes");
  const code = validCode(body.code);
  if (!code) return fail(404, "No airport has that code");
  if (code === me.code) return fail(409, "That's your own code");
  const other = await env.DB.prepare("SELECT id, code, name, map, stars, flights, last_seen FROM players WHERE code = ?").bind(code).first();
  if (!other) return fail(404, "No airport has that code");
  // Either side having blocked the other reads as an unknown code, so a block can't be probed or worked round.
  const blocked = await env.DB.prepare("SELECT 1 FROM blocks WHERE (player = ?1 AND blocked = ?2) OR (player = ?2 AND blocked = ?1)")
    .bind(me.id, other.id).first();
  if (blocked) return fail(404, "No airport has that code");
  const counts = await env.DB.prepare(
    "SELECT (SELECT COUNT(*) FROM friends WHERE player = ?1 AND friend = ?2) AS already, " +
    "(SELECT COUNT(*) FROM friends WHERE player = ?1) AS mine, (SELECT COUNT(*) FROM friends WHERE player = ?2) AS theirs")
    .bind(me.id, other.id).first();
  if (counts.already > 0) return fail(409, "Already friends");
  if (counts.mine >= MAX_FRIENDS) return fail(409, "Friend list is full");
  if (counts.theirs >= MAX_FRIENDS) return fail(409, "Their friend list is full");
  const t = now();
  const ins = env.DB.prepare("INSERT OR IGNORE INTO friends (player, friend, added) VALUES (?, ?, ?)");
  await env.DB.batch([ins.bind(me.id, other.id, t), ins.bind(other.id, me.id, t)]);
  return json({ ok: true, friend: publicRow(other) });
}

async function remove(_req, env, body, me) {
  const code = validCode(body.code);
  const other = code ? await env.DB.prepare("SELECT id FROM players WHERE code = ?").bind(code).first() : null;
  if (other) {
    const del = env.DB.prepare("DELETE FROM friends WHERE player = ? AND friend = ?");
    await env.DB.batch([del.bind(me.id, other.id), del.bind(other.id, me.id)]);
  }
  return json({ ok: true });   // also when there was nothing to remove: the list ends up the same
}

// Ends the friendship both ways and stops that player adding this one again (and this one them).
async function block(_req, env, body, me) {
  const other = await byCode(env, body.code);
  if (!other || other.id === me.id) return fail(404, "No airport has that code");
  const del = env.DB.prepare("DELETE FROM friends WHERE player = ? AND friend = ?");
  await env.DB.batch([del.bind(me.id, other.id), del.bind(other.id, me.id),
    env.DB.prepare("INSERT OR IGNORE INTO blocks (player, blocked, added) VALUES (?, ?, ?)").bind(me.id, other.id, now())]);
  return json({ ok: true });
}

// A block, plus a note for the developer to look at: the name as it was shown, so it can be cleared or the
// filter extended. One open report per pair.
async function report(req, env, body, me) {
  const other = await byCode(env, body.code);
  if (!other || other.id === me.id) return fail(404, "No airport has that code");
  await env.DB.prepare("INSERT OR IGNORE INTO reports (reporter, reported, name, added) VALUES (?, ?, ?, ?)")
    .bind(me.id, other.id, other.name, now()).run();
  return block(req, env, body, me);
}

async function byCode(env, raw) {
  const code = validCode(raw);
  return code ? env.DB.prepare("SELECT id, name FROM players WHERE code = ?").bind(code).first() : null;
}

async function listFriends(env, me) {
  const [rows] = await env.DB.batch([
    env.DB.prepare("SELECT p.code, p.name, p.map, p.stars, p.flights, p.last_seen FROM friends f JOIN players p ON p.id = f.friend " +
      "WHERE f.player = ? ORDER BY p.stars DESC, p.flights DESC, p.name").bind(me.id),
    env.DB.prepare("UPDATE players SET last_seen = ? WHERE id = ?").bind(now(), me.id),
  ]);
  return json({ friends: rows.results.map(publicRow) });
}

async function del(_req, env, _body, me) {
  await env.DB.batch([
    env.DB.prepare("DELETE FROM friends WHERE player = ?1 OR friend = ?1").bind(me.id),
    env.DB.prepare("DELETE FROM blocks WHERE player = ?1 OR blocked = ?1").bind(me.id),
    env.DB.prepare("DELETE FROM reports WHERE reporter = ?1 OR reported = ?1").bind(me.id),
    env.DB.prepare("DELETE FROM players WHERE id = ?").bind(me.id),
  ]);
  return json({ ok: true });
}

// ---- helpers ----

async function auth(req, env) {
  const m = /^Bearer ([0-9a-f]{64})$/.exec(req.headers.get("Authorization") || "");
  if (!m) return null;
  return env.DB.prepare("SELECT id, code FROM players WHERE token = ?").bind(await sha256(m[1])).first();
}

function publicRow(r) {
  return { code: r.code, name: r.name, map: r.map, stars: r.stars, flights: r.flights, lastSeen: r.last_seen };
}

function cleanProfile(b) {
  return { name: cleanName(b.name), map: cleanText(b.map, MAP_MAX), stars: clamp(b.stars, MAX_STARS), flights: clamp(b.flights, MAX_FLIGHTS) };
}

// Keep in step with FriendRules.CleanName (Core/Friends.cs): letters, digits, space and '-._ only (judged one UTF-16
// unit at a time, as C# does), runs of spaces become one, 20 characters at most, "Airport Manager" when nothing is left.
function cleanName(v) {
  if (typeof v === "string" && rude(v)) return DEFAULT_NAME;
  const n = cleanText(v, NAME_MAX) || DEFAULT_NAME;
  return rude(n) ? DEFAULT_NAME : n;
}

function fold(s) {
  let out = "";
  for (const ch of s.toLowerCase()) {
    const c = SWAPS[ch] || ch;
    out += c >= "a" && c <= "z" ? c : " ";
  }
  return out;
}

function rude(s) {
  const words = fold(s.slice(0, 400)).split(" ").filter(w => w !== "");
  const joined = words.join("");
  if (ROOTS.some(r => joined.includes(r))) return true;
  return words.some(w => WORDS.includes(w));
}

function cleanText(v, max) {
  if (typeof v !== "string") return "";
  let out = "";
  const s = v.slice(0, 400);
  for (let i = 0; i < s.length; i++) {
    const c = s[i];
    if (!OK_CHAR.test(c)) continue;
    if (c === " " && (out === "" || out.endsWith(" "))) continue;
    out += c;
  }
  return out.slice(0, max).trim();
}

function clamp(v, max) {
  const n = Math.floor(Number(v));
  return Number.isFinite(n) ? Math.min(Math.max(n, 0), max) : 0;
}

function validCode(v) {
  if (typeof v !== "string") return null;
  const c = v.trim().toUpperCase();
  if (c.length !== CODE_LEN) return null;
  for (const ch of c) if (!ALPHABET.includes(ch)) return null;
  return c;
}

function newCode() {
  const b = crypto.getRandomValues(new Uint8Array(CODE_LEN));
  let s = "";
  for (const x of b) s += ALPHABET[x & 31];
  return s;
}

function randomHex(bytes) {
  return [...crypto.getRandomValues(new Uint8Array(bytes))].map(x => x.toString(16).padStart(2, "0")).join("");
}

async function sha256(s) {
  const d = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(s));
  return [...new Uint8Array(d)].map(x => x.toString(16).padStart(2, "0")).join("");
}

async function limited(req, what) {
  const [max, seconds] = LIMITS[what];
  const ip = req.headers.get("CF-Connecting-IP");
  if (!ip) return false;
  if (!salt) salt = crypto.randomUUID();
  const t = now(), key = what + ":" + await sha256(salt + ip);
  if (hits.size > 5000) for (const [k, h] of hits) if (h.until <= t) hits.delete(k);
  let h = hits.get(key);
  if (!h || h.until <= t) { h = { n: 0, until: t + seconds }; hits.set(key, h); }
  return ++h.n > max;
}

function now() { return Math.floor(Date.now() / 1000); }

function json(o, status = 200) {
  return new Response(JSON.stringify(o), { status, headers: { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store" } });
}

function fail(status, error) { return json({ ok: false, error }, status); }
