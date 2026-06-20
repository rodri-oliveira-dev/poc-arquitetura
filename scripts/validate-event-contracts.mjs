#!/usr/bin/env node
import { spawnSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = dirname(fileURLToPath(import.meta.url));
const result = spawnSync(process.execPath, [join(scriptDir, 'contracts/events/validate.mjs'), ...process.argv.slice(2)], { stdio: 'inherit' });
process.exit(result.status ?? 1);
