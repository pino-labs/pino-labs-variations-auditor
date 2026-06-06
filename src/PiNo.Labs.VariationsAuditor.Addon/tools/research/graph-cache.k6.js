// graph-cache.k6.js — LIVE counterpart to the in-process cache model.
// Measures real cache hit-rate AND p50/p99 latency for item vs items, and SOME vs ALL,
// against an Optimizely Graph gateway. The in-process model (CacheExperiment.cs) reproduces
// the hit-rate cliff deterministically; this script is what you run when you also want
// wall-clock latency from a real gateway.
//
//   run: k6 run -e KEY=<single-key> -e IDS=key1,key2,... graph-cache.k6.js
//
import http from 'k6/http';
import { Trend, Rate } from 'k6/metrics';

const URL = `https://cg.optimizely.com/content/v2?auth=${__ENV.KEY}`;

// Fixed 100-id working set so runs are comparable. Pass a comma-separated list of _metadata.key
// values via -e IDS=..., or replace with your own seeded set.
const IDS = (__ENV.IDS || '').split(',').filter(Boolean);

const hit = {};      // Rate per scenario
const lat = {};      // Trend per scenario (ms)
for (const s of ['item', 'item_some', 'items', 'items_some', 'items_all']) {
  hit[s] = new Rate(`hit_${s}`);
  lat[s] = new Trend(`lat_${s}`, true);
}

const Q = {
  item:       (id) => `{ _Content(where:{_metadata:{key:{eq:"${id}"}}}, limit:1){ items{ _metadata{ key displayName } } } }`,
  item_some:  (id) => `{ _Content(where:{_metadata:{key:{eq:"${id}"}}}, limit:1, variation:{ include: SOME, value:["WinterCampaign"], includeOriginal:true }){ items{ _metadata{ key } } } }`,
  items:      ()   => `{ _Content(limit:100){ items{ _metadata{ key displayName } } } }`,
  items_some: ()   => `{ _Content(limit:100, variation:{ include: SOME, value:["WinterCampaign"], includeOriginal:true }){ items{ _metadata{ key } } } }`,
  items_all:  ()   => `{ _Content(limit:100, variation:{ include: ALL, includeOriginal:true }){ items{ _metadata{ key } } } }`,
};

export const options = {
  scenarios: {
    soak: { executor: 'constant-vus', vus: 50, duration: '60s', gracefulStop: '5s' },
  },
};

export default function () {
  const id = IDS.length ? IDS[Math.floor(Math.random() * IDS.length)] : 'sample-key';
  for (const s of Object.keys(Q)) {
    const body = JSON.stringify({ query: s.startsWith('item') ? Q[s](id) : Q[s]() });
    const res = http.post(URL, body, { headers: { 'Content-Type': 'application/json' } });
    const cacheHdr = (res.headers['X-Cache'] || res.headers['Cf-Cache-Status'] || '').toLowerCase();
    hit[s].add(cacheHdr.includes('hit'));
    lat[s].add(res.timings.duration);
  }
}

