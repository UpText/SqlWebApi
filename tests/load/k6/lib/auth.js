import { buildHeaders } from './config.js';

export function authParams(overrides = {}) {
  return {
    headers: {
      ...buildHeaders(),
      ...(overrides.headers || {}),
    },
    tags: overrides.tags || {},
  };
}
