import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

const API_BASE = __ENV.API_BASE || 'http://localhost:5000';
const ANALYTICS_BASE = __ENV.ANALYTICS_BASE || 'http://localhost:8000';
const MODE = (__ENV.MODE || 'polyglot').toLowerCase(); // polyglot | monolith
const PRODUCT_MIN_ID = Number(__ENV.PRODUCT_MIN_ID || 1);
const PRODUCT_MAX_ID = Number(__ENV.PRODUCT_MAX_ID || 100000);
const WAREHOUSE_MIN_ID = Number(__ENV.WAREHOUSE_MIN_ID || 1);
const WAREHOUSE_MAX_ID = Number(__ENV.WAREHOUSE_MAX_ID || 10);
const PRODUCT_SAMPLE = Number(__ENV.PRODUCT_SAMPLE || 5000);
const TOKEN = __ENV.TOKEN || '';

const errors = new Rate('errors');

export const options = {
  scenarios: {
    transactional_write: {
      executor: 'ramping-arrival-rate',
      startRate: 10,
      timeUnit: '1s',
      preAllocatedVUs: 50,
      maxVUs: 500,
      stages: [
        { target: 50, duration: '20s' },
        { target: 200, duration: '20s' },
        { target: 500, duration: '20s' },
      ],
      exec: 'createOrder',
    },
    transactional_read: {
      executor: 'ramping-arrival-rate',
      startRate: 20,
      timeUnit: '1s',
      preAllocatedVUs: 50,
      maxVUs: 400,
      stages: [
        { target: 100, duration: '20s' },
        { target: 300, duration: '20s' },
        { target: 400, duration: '20s' },
      ],
      exec: 'readData',
    },
    analytics: {
      executor: 'ramping-arrival-rate',
      startRate: 5,
      timeUnit: '1s',
      preAllocatedVUs: 30,
      maxVUs: 200,
      stages: [
        { target: 50, duration: '20s' },
        { target: 150, duration: '20s' },
        { target: 200, duration: '20s' },
      ],
      exec: 'analyticsWorkload',
    },
  },
  thresholds: {
    errors: ['rate<0.01'],
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1000', 'p(99)<2500'],
  },
};

function randId(min, max) {
  return Math.floor(Math.random() * (max - min + 1)) + min;
}

function randomOrderPayload() {
  const itemsCount = Math.max(1, Math.floor(Math.random() * 4));
  const items = [];
  for (let i = 0; i < itemsCount; i++) {
    items.push({
      productId: randId(PRODUCT_MIN_ID, PRODUCT_MAX_ID),
      warehouseId: randId(WAREHOUSE_MIN_ID, WAREHOUSE_MAX_ID),
      quantity: randId(1, 20),
      unitPrice: Math.floor(Math.random() * 500) + 10,
      discount: 0,
    });
  }
  return {
    customerName: `Client-${randId(1, 1000000)}`,
    customerEmail: `client${randId(1, 1000000)}@example.com`,
    customerAddress: 'Adresse auto-générée',
    taxRate: 0.2,
    items,
  };
}

export function createOrder() {
  const payload = JSON.stringify(randomOrderPayload());
  const res = http.post(`${API_BASE}/api/orders`, payload, {
    headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${TOKEN}` },
    tags: { endpoint: 'orders_post', mode: MODE },
  });
  const ok = check(res, {
    'order created': (r) => r.status === 200 || r.status === 201,
  });
  if (!ok) errors.add(1);
  sleep(0.1);
}

export function readData() {
  const resProducts = http.get(`${API_BASE}/api/products`, { headers: { 'Authorization': `Bearer ${TOKEN}` }, tags: { endpoint: 'products_get', mode: MODE } });
  const okProducts = check(resProducts, { 'products ok': (r) => r.status === 200 });
  if (!okProducts) errors.add(1);

  const resStocks = http.get(`${API_BASE}/api/stocks`, { headers: { 'Authorization': `Bearer ${TOKEN}` }, tags: { endpoint: 'stocks_get', mode: MODE } });
  const okStocks = check(resStocks, { 'stocks ok': (r) => r.status === 200 });
  if (!okStocks) errors.add(1);

  sleep(0.1);
}

export function analyticsWorkload() {
  const pid = randId(PRODUCT_MIN_ID, Math.max(PRODUCT_MIN_ID, Math.min(PRODUCT_MAX_ID, PRODUCT_MIN_ID + PRODUCT_SAMPLE)));

  if (MODE === 'monolith') {
    // Simulate analytics load hitting the transactional backend
    const res1 = http.get(`${API_BASE}/api/stocks/product/${pid}/warehouse/${randId(WAREHOUSE_MIN_ID, WAREHOUSE_MAX_ID)}`, {
      headers: { 'Authorization': `Bearer ${TOKEN}` },
      tags: { endpoint: 'stocks_by_product', mode: MODE },
    });
    const res2 = http.get(`${API_BASE}/api/orders`, { headers: { 'Authorization': `Bearer ${TOKEN}` }, tags: { endpoint: 'orders_get', mode: MODE } });
    const okMono = check(res1, { 'stocks monolith ok': (r) => r.status === 200 }) &&
      check(res2, { 'orders list ok': (r) => r.status === 200 });
    if (!okMono) errors.add(1);
  } else {
    const resPred = http.get(`${ANALYTICS_BASE}/predict/${pid}`, {
      headers: { 'Authorization': `Bearer ${TOKEN}` },
      tags: { endpoint: 'predict', mode: MODE },
    });
    const resOpt = http.get(`${ANALYTICS_BASE}/optimize/${pid}`, {
      headers: { 'Authorization': `Bearer ${TOKEN}` },
      tags: { endpoint: 'optimize', mode: MODE },
    });
    const okPred = check(resPred, { 'predict ok': (r) => r.status === 200 });
    const okOpt = check(resOpt, { 'optimize ok': (r) => r.status === 200 });
    if (!okPred || !okOpt) errors.add(1);
  }

  sleep(0.2);
}

