# SqlWebApi Load Testing Starter

This starter gives you:

- **k6** scripts for smoke, mixed API, and stress tests
- **JMeter** starter plan for CLI and Azure Load Testing
- **GitHub Actions** workflow for PR smoke tests and nightly load tests

## Expected environment variables

- `BASE_URL` - base URL to your SqlWebApi deployment, example `https://swa-test.azurewebsites.net`
- `JWT_TOKEN` - bearer token for authenticated endpoints
- `API_PREFIX` - optional, defaults to `api`
- `PROC_SCHEMA` - optional, defaults to `crmapi`
- `TENANT_HEADER_NAME` - optional, example `X-Tenant-Id`
- `TENANT_ID` - optional, example `demo`

## Endpoint assumptions

The starter assumes endpoints like:

- `/{API_PREFIX}/{PROC_SCHEMA}/CustomerList?page=1&pageSize=25`
- `/{API_PREFIX}/{PROC_SCHEMA}/CustomerGet?id=1001`
- `/{API_PREFIX}/{PROC_SCHEMA}/OrderCreate`

Adjust the endpoint names in `k6/lib/endpoints.js` and in the JMeter HTTP samplers.

## Run k6 locally

```bash
cd tests/load
BASE_URL=https://your-host \
JWT_TOKEN=your-token \
k6 run k6/smoke.js
```

Regenerate `k6/lib/endpoints.js` from an OpenAPI document:

```bash
cd tests/load
node generate-k6-endpoints.mjs --input http://localhost:7071/swa/crmapi/swagger.json
```

Mixed test:

```bash
cd tests/load
BASE_URL=https://your-host \
JWT_TOKEN=your-token \
k6 run k6/api-mixed.js
```

Stress test:

```bash
cd tests/load
BASE_URL=https://your-host \
JWT_TOKEN=your-token \
k6 run k6/stress.js
```

## Run JMeter locally

```bash
jmeter -n \
  -t tests/load/jmeter/sqlwebapi-api.jmx \
  -JbaseUrl=https://your-host \
  -JjwtToken=your-token \
  -JtenantHeaderName=X-Tenant-Id \
  -JtenantId=demo \
  -l tests/load/jmeter/results.jtl \
  -e -o tests/load/jmeter/report
```

## CI strategy

- PR: `k6-smoke`
- Main / nightly: `k6-mixed`, `jmeter-nightly`
- Optional manual dispatch for stress

## Notes for SqlWebApi

- Keep read tests and write tests separate in dashboards
- Seed deterministic test data for list and lookup scenarios
- Use dedicated test identities and tenant IDs
- For write tests, create unique payload values to avoid collisions
- Review Azure SQL metrics next to latency results
