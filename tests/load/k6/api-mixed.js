import http from 'k6/http';
import { sleep } from 'k6';
import { authParams } from './lib/auth.js';
import { commonJsonChecks } from './lib/checks.js';
import { customerGet, customerList, orderCreate } from './lib/endpoints.js';

export const options = {
  scenarios: {
    listCustomers: {
      executor: 'ramping-vus',
      exec: 'listCustomers',
      startVUs: 1,
      stages: [
        { duration: '30s', target: 5 },
        { duration: '60s', target: 20 },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '10s',
      tags: { endpoint: 'CustomerList' },
    },
    getCustomer: {
      executor: 'constant-vus',
      exec: 'getCustomer',
      vus: 10,
      duration: '2m',
      tags: { endpoint: 'CustomerGet' },
    },
    createOrder: {
      executor: 'constant-arrival-rate',
      exec: 'createOrder',
      rate: 5,
      timeUnit: '1s',
      duration: '2m',
      preAllocatedVUs: 20,
      maxVUs: 50,
      tags: { endpoint: 'OrderCreate' },
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<800', 'p(99)<1500'],
    'http_req_duration{endpoint:CustomerList}': ['p(95)<700'],
    'http_req_duration{endpoint:CustomerGet}': ['p(95)<400'],
    'http_req_duration{endpoint:OrderCreate}': ['p(95)<1200'],
  },
};

export function listCustomers() {
  const res = http.get(customerList(1, 25, 'acme'), authParams({ tags: { endpoint: 'CustomerList' } }));
  commonJsonChecks(res, 'CustomerList', [200]);
  sleep(1);
}

export function getCustomer() {
  const res = http.get(customerGet(), authParams({ tags: { endpoint: 'CustomerGet' } }));
  commonJsonChecks(res, 'CustomerGet', [200]);
  sleep(0.5);
}

export function createOrder() {
  const suffix = `${__VU}-${__ITER}-${Date.now()}`;
  const body = JSON.stringify({
    customerId: 1001,
    orderDate: new Date().toISOString().slice(0, 10),
    externalReference: `k6-${suffix}`,
    totalAmount: 1499.0,
  });

  const res = http.post(orderCreate(), body, authParams({ tags: { endpoint: 'OrderCreate' } }));
  commonJsonChecks(res, 'OrderCreate', [200, 201]);
}
