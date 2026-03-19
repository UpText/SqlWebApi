import http from 'k6/http';
import { sleep } from 'k6';
import { authParams } from './lib/auth.js';
import { commonJsonChecks, bodyHasText } from './lib/checks.js';
import { contact, contacts, healthPing } from './lib/endpoints.js';

export const options = {
  vus: 1,
  iterations: 100,
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1500'],
  },
};

export default function () {
  const ping = http.get(healthPing());
  bodyHasText(ping, 'ping', '');

  const listRes = http.get(contacts(1, 10), authParams({ tags: { endpoint: 'contacts' } }));
  commonJsonChecks(listRes, 'Contacts', [200]);

  const getRes = http.get(contact(), authParams({ tags: { endpoint: 'contact' } }));
  commonJsonChecks(getRes, 'Contacts', [200]);

  sleep(1);
}
