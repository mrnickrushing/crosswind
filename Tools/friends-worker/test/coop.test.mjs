// The co-op week endpoint against a real SQLite database (node:sqlite, Node 22.5+) behind a small stand-in for D1's
// prepare/bind/first/run/all/batch. Nothing here talks to Cloudflare. Run: node --test   (in Tools/friends-worker)
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { DatabaseSync } from "node:sqlite";
import worker from "../worker.js";

const here = new URL("..", import.meta.url);

function fakeD1() {
  const db = new DatabaseSync(":memory:");
  db.exec(readFileSync(new URL("schema.sql", here), "utf8"));
  db.exec(readFileSync(new URL("migrations/0001_coop.sql", here), "utf8"));   // runs twice over harmlessly
  const stmt = (sql, args = []) => ({
    sql, args,
    bind: (...a) => stmt(sql, a),
    first: async () => db.prepare(sql).get(...args) ?? null,
    run: async () => { db.prepare(sql).run(...args); return { success: true }; },
    all: async () => ({ results: db.prepare(sql).all(...args) }),
  });
  return {
    db,
    prepare: sql => stmt(sql),
    batch: async list => {
      db.exec("BEGIN");
      try {
        const out = list.map(s => ({ results: db.prepare(s.sql).all(...s.args) }));
        db.exec("COMMIT");
        return out;
      } catch (e) { db.exec("ROLLBACK"); throw e; }
    },
  };
}

const env = { DB: fakeD1() };
const week = Math.floor((Math.floor(Date.now() / 1000 / 86400) + 3) / 7);

async function call(path, body, token, ip) {
  const headers = { "Content-Type": "application/json" };
  if (token) headers.Authorization = "Bearer " + token;
  if (ip) headers["CF-Connecting-IP"] = ip;
  const res = await worker.fetch(new Request("https://test" + path, { method: "POST", headers, body: typeof body === "string" ? body : JSON.stringify(body) }), env);
  return { status: res.status, body: await res.json() };
}

async function player(name) {
  const r = await call("/v1/register", { account: "coop-test-" + name, name, map: "meadow", stars: 3, flights: 10 });
  assert.equal(r.status, 200);
  return { ...r.body, id: env.DB.db.prepare("SELECT id FROM players WHERE code = ?").get(r.body.code).id };
}

const report = (p, passengers, w = week, ip) => call("/v1/coop/report", { week: w, passengers }, p.token, ip);

test("the group total is you plus your mutual friends this week, and the goal grows with who flew", async () => {
  const a = await player("Alpha"), b = await player("Bravo"), c = await player("Charlie"), d = await player("Delta");

  let r = await report(a, 1000);
  assert.equal(r.status, 200);
  assert.deepEqual(r.body, { ok: true, week, mine: 1000, total: 1000, members: 1, tiers: [2500, 10000, 30000, 80000] });

  assert.equal((await call("/v1/friends/add", { code: b.code }, a.token)).status, 200);
  assert.equal((await call("/v1/friends/add", { code: c.code }, a.token)).status, 200);
  r = await report(a, 1000);
  assert.equal(r.body.total, 1000, "friends who haven't reported add nothing");
  assert.equal(r.body.members, 1, "nor count towards the goal");

  await report(b, 3000);
  await report(c, 0);
  r = await report(a, 1200);
  assert.equal(r.body.mine, 1200);
  assert.equal(r.body.total, 4200);
  assert.equal(r.body.members, 2, "a friend at zero doesn't raise the goal");
  assert.deepEqual(r.body.tiers, [4500, 18000, 54000, 144000], "the same numbers as Sim.CoopTargets(2)");

  r = await report(a, 800);
  assert.equal(r.body.mine, 1200, "a lower count for the same week doesn't lower it");

  // Only mutual friends: half a friendship (as left by a failed write) counts nothing.
  await report(d, 50000);
  env.DB.db.prepare("INSERT INTO friends (player, friend, added) VALUES (?, ?, 0)").run(a.id, d.id);
  r = await report(a, 1200);
  assert.equal(r.body.total, 4200);

  // Only this week: last week's count of a friend isn't in it.
  env.DB.db.prepare("INSERT INTO coop (player, week, passengers, updated) VALUES (?, ?, 77777, 0)").run(b.id, week - 1);
  r = await report(a, 1200);
  assert.equal(r.body.total, 4200);
  r = await report(b, 1, week - 1);
  assert.equal(r.status, 200, "a device a day behind is still in last week");
  assert.equal(r.body.total, 77777, "and is answered for that week");

  // A friend removed is out of the total at once.
  await call("/v1/friends/remove", { code: b.code }, a.token);
  r = await report(a, 1200);
  assert.equal(r.body.total, 1200);
  assert.equal(r.body.members, 1);

  // Old weeks are cleared when the player reports; deleting the account clears the rest.
  env.DB.db.prepare("INSERT INTO coop (player, week, passengers, updated) VALUES (?, ?, 5, 0)").run(a.id, week - 5);
  await report(a, 1200);
  assert.equal(env.DB.db.prepare("SELECT COUNT(*) AS n FROM coop WHERE player = ? AND week < ?").get(a.id, week - 2).n, 0);
  assert.equal((await call("/v1/delete", "", a.token)).status, 200);
  assert.equal(env.DB.db.prepare("SELECT COUNT(*) AS n FROM coop WHERE player = ?").get(a.id).n, 0);
});

test("a report is checked like every other call", async () => {
  const e = await player("Echo");
  assert.equal((await call("/v1/coop/report", { week, passengers: 5 })).status, 401, "no token");
  assert.equal((await call("/v1/coop/report", { week, passengers: 5 }, "0".repeat(64))).status, 401, "a token nobody holds");
  assert.equal((await report(e, 5, week + 2)).status, 422, "a clock far out");
  assert.equal((await report(e, 5, week - 2)).status, 422);
  assert.equal((await report(e, 5, "12")).status, 422, "the week is a whole number");
  assert.equal((await report(e, 5, 2.5)).status, 422);
  assert.equal((await call("/v1/coop/report", "{week:", e.token)).status, 400);
  assert.equal((await call("/v1/coop/report", JSON.stringify({ week, passengers: 1, pad: "x".repeat(5000) }), e.token)).status, 413);

  let r = await report(e, -40);
  assert.equal(r.body.mine, 0, "never below zero");
  r = await report(e, 1e15);
  assert.equal(r.body.mine, 2_000_000_000, "nor above the cap");
  r = await report(e, "lots");
  assert.equal(r.body.mine, 2_000_000_000, "junk counts as nothing, and the best stays");

  const got = [];
  for (let i = 0; i < 31; i++) got.push((await report(e, 1, week, "203.0.113.9")).status);
  assert.deepEqual(got.slice(0, 30), Array(30).fill(200));
  assert.equal(got[30], 429, "30 reports in ten minutes from one address, then a pause");
});
