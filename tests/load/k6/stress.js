import http from 'k6/http';
import { sleep } from 'k6';
import { authParams } from './lib/auth.js';
import { commonJsonChecks } from './lib/checks.js';
import { customerList } from './lib/endpoints.js';

export const options = {
  stages: [
    { duration: '1m', target: 20 },
    { duration: '2m', target: 50 },
    { duration: '2m', target: 100 },
    { duration: '1m', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.03'],
    http_req_duration: ['p(95)<2000'],
  },
};

export default function () {
  const res = http.get(customerList(1, 25), authParams({ tags: { endpoint: 'CustomerListStress' } }));
  commonJsonChecks(res, 'CustomerListStress', [200]);
  sleep(0.2);
}
