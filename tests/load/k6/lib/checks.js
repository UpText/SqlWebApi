import { check } from 'k6';

export function commonJsonChecks(res, label, expectedStatuses = [200]) {
  return check(res, {
    [`${label}: expected status`]: r => expectedStatuses.includes(r.status),
    [`${label}: response time under 5s`]: r => r.timings.duration < 5000,
    [`${label}: content type json or text`]: r => {
      const ct = r.headers['Content-Type'] || r.headers['content-type'] || '';
      return ct.includes('application/json') || ct.includes('text/plain') || ct === '';
    },
  });
}

export function bodyHasText(res, label, text) {
  return check(res, {
    [`${label}: body contains expected text`]: r => typeof r.body === 'string' && r.body.includes(text),
  });
}
