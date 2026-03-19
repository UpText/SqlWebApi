#!/usr/bin/env node

import fs from 'node:fs/promises';

function parseArgs(argv) {
  const args = {
    input: null,
    output: new URL('./k6/lib/endpoints.js', import.meta.url),
  };

  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (arg === '--input') {
      args.input = argv[i + 1];
      i += 1;
    } else if (arg === '--output') {
      args.output = new URL(argv[i + 1], `file://${process.cwd()}/`);
      i += 1;
    }
  }

  if (!args.input) {
    throw new Error('Missing required --input <swagger.json path or URL>');
  }

  return args;
}

async function readOpenApiDocument(input) {
  if (/^https?:\/\//i.test(input)) {
    const response = await fetch(input);
    if (!response.ok) {
      throw new Error(`Failed to fetch ${input}: ${response.status} ${response.statusText}`);
    }
    return response.json();
  }

  const raw = await fs.readFile(input, 'utf8');
  return JSON.parse(raw);
}

function pascalCase(value) {
  return value
    .split(/[^a-zA-Z0-9]+/)
    .filter(Boolean)
    .map(part => part[0].toUpperCase() + part.slice(1))
    .join('');
}

function camelCase(value) {
  const pascal = pascalCase(value);
  return pascal ? pascal[0].toLowerCase() + pascal.slice(1) : '';
}

function singularize(value) {
  if (value.endsWith('ies')) {
    return `${value.slice(0, -3)}y`;
  }
  if (value.endsWith('s') && !value.endsWith('ss')) {
    return value.slice(0, -1);
  }
  return value;
}

function normalizePath(pathname) {
  return pathname.replace(/^\/+/, '');
}

function toQueryObject(parameters) {
  const parts = parameters.map(param => `${JSON.stringify(param.name)}: params[${JSON.stringify(param.name)}]`);
  return `{ ${parts.join(', ')} }`;
}

function buildMethodFunctionName(method, resourceName) {
  const singular = singularize(resourceName);
  if (method === 'post') return `create${pascalCase(singular)}`;
  if (method === 'put') return `update${pascalCase(singular)}`;
  if (method === 'delete') return `delete${pascalCase(singular)}`;
  return `${method}${pascalCase(resourceName)}`;
}

function renderFunction(name, lines) {
  return [`export function ${name}${lines.signature} {`, ...lines.body.map(line => `  ${line}`), `}`].join('\n');
}

function generateSource(document) {
  const pathEntries = Object.entries(document.paths || {});
  const functions = [];
  const compatibilityAliases = [];

  for (const [pathName, methods] of pathEntries) {
    const resourcePath = normalizePath(pathName);
    const resourceName = camelCase(resourcePath);
    if (!resourceName) {
      continue;
    }

    const getOperation = methods.get;
    if (getOperation) {
      const queryParameters = (getOperation.parameters || []).filter(param => param.in === 'query');
      const hasPaging = queryParameters.some(param => param.name === 'first_row')
        && queryParameters.some(param => param.name === 'last_row');
      const hasFilter = queryParameters.some(param => param.name === 'filter');

      if (hasPaging) {
        const filterLine = hasFilter
          ? "filter: search,"
          : '';
        functions.push(
          renderFunction(resourceName, {
            signature: "(page = 1, pageSize = 25, search = '')",
            body: [
              'const firstRow = Math.max(0, (page - 1) * pageSize);',
              'const lastRow = firstRow + pageSize;',
              `return withQuery(\`${'${root()}'}/${resourcePath}\`, {`,
              '  first_row: firstRow,',
              '  last_row: lastRow,',
              filterLine,
              '});',
            ].filter(Boolean),
          }),
        );
      } else {
        functions.push(
          renderFunction(resourceName, {
            signature: '(params = {})',
            body: [`return withQuery(\`${'${root()}'}/${resourcePath}\`, ${toQueryObject(queryParameters)});`],
          }),
        );
      }

      const idParameter = queryParameters.find(param => param.name.toLowerCase() === 'id');
      if (idParameter) {
        const singular = camelCase(singularize(resourcePath));
        if (singular && singular !== resourceName) {
          functions.push(
            renderFunction(singular, {
              signature: `(id = config.default${pascalCase(singular)}Id)`,
              body: [`return withQuery(\`${'${root()}'}/${resourcePath}\`, { ${JSON.stringify(idParameter.name)}: id });`],
            }),
          );
        }
      }
    }

    for (const method of ['post', 'put', 'delete']) {
      const operation = methods[method];
      if (!operation) {
        continue;
      }

      const queryParameters = (operation.parameters || []).filter(param => param.in === 'query');
      const functionName = buildMethodFunctionName(method, resourcePath);

      if (method === 'delete') {
        const idParameter = queryParameters.find(param => param.name.toLowerCase() === 'id');
        if (idParameter) {
          functions.push(
            renderFunction(functionName, {
              signature: '(id)',
              body: [`return withQuery(\`${'${root()}'}/${resourcePath}\`, { ${JSON.stringify(idParameter.name)}: id });`],
            }),
          );
          continue;
        }
      }

      functions.push(
        renderFunction(functionName, {
          signature: '(params = {})',
          body: [`return withQuery(\`${'${root()}'}/${resourcePath}\`, ${toQueryObject(queryParameters)});`],
        }),
      );
    }
  }

  if (functions.some(fn => fn.includes('export function contacts('))) {
    compatibilityAliases.push("export const customerList = contacts;");
  }

  if (functions.some(fn => fn.includes('export function contact('))) {
    compatibilityAliases.push("export const customerGet = contact;");
  }

  if (functions.some(fn => fn.includes('export function createOrder('))) {
    compatibilityAliases.push("export const orderCreate = createOrder;");
  } else {
    compatibilityAliases.push('export function orderCreate() {');
    compatibilityAliases.push("  return `${root()}/OrderCreate`;");
    compatibilityAliases.push('}');
  }

  const file = [
    "import { config } from './config.js';",
    '',
    '// Generated by tests/load/generate-k6-endpoints.mjs from an OpenAPI document.',
    'function root() {',
    '  return [config.baseUrl, config.apiPrefix, config.procSchema]',
    '    .filter(Boolean)',
    "    .join('/');",
    '}',
    '',
    'function withQuery(path, params) {',
    '  const query = Object.entries(params)',
    "    .filter(([, value]) => value !== '' && value !== undefined && value !== null)",
    "    .map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(String(value))}`)",
    "    .join('&');",
    '',
    '  return query ? `${path}?${query}` : path;',
    '}',
    '',
    ...functions,
    '',
    ...compatibilityAliases,
    '',
    'export function healthPing() {',
    "  return `${config.baseUrl}/../../ping`;",
    '}',
    '',
  ];

  return `${file.join('\n')}`;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const document = await readOpenApiDocument(args.input);
  const source = generateSource(document);
  await fs.writeFile(args.output, source, 'utf8');
}

main().catch(error => {
  console.error(error.message);
  process.exitCode = 1;
});
