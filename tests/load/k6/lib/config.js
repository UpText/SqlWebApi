export function requiredEnv(name) {
  const value = __ENV[name];
  if (!value) {
    throw new Error(`Missing required environment variable: ${name}`);
  }
  return value;
}

export const config = {
  baseUrl: requiredEnv('BASE_URL').replace(/\/$/, ''),
  jwtToken: requiredEnv('JWT_TOKEN'),
  apiPrefix: (__ENV.API_PREFIX ?? '').replace(/^\//, '').replace(/\/$/, ''),
  procSchema: __ENV.PROC_SCHEMA || 'crmapi',
  tenantHeaderName: __ENV.TENANT_HEADER_NAME || '',
  tenantId: __ENV.TENANT_ID || '',
  defaultContactId: __ENV.CONTACT_ID || __ENV.CUSTOMER_ID || '1001',
};

export function buildHeaders(contentType = 'application/json') {
  const headers = {
    Authorization: `Bearer ${config.jwtToken}`,
    'Content-Type': contentType,
    Accept: 'application/json',
  };

  if (config.tenantHeaderName && config.tenantId) {
    headers[config.tenantHeaderName] = config.tenantId;
  }

  return headers;
}
