/**
 * esbuild plugin for tree-shaking RDR3/FiveM natives
 *
 * Usage:
 *   import { nativeTreeshake } from './native-treeshake.js';
 *
 *   esbuild.build({
 *     entryPoints: ['src/index.ts'],
 *     bundle: true,
 *     plugins: [nativeTreeshake({
 *       natives: './natives.ts',  // Path to generated natives file
 *       globals: true             // Optional: expose as globalThis (default: false)
 *     })],
 *   });
 *
 * In your code, just use natives directly:
 *   const coords = GetEntityCoords(entity);
 *   const ped = CreatePed(model, x, y, z, heading, false, false);
 *
 * The plugin will automatically:
 *   1. Detect which natives you're using
 *   2. Import only those from the natives file
 *   3. Tree-shake the rest
 */

import { readFileSync } from 'fs';
import { resolve, dirname } from 'path';

/**
 * Extract all exported function names from the natives file
 */
function extractNativeNames(nativesPath) {
  const content = readFileSync(nativesPath, 'utf-8');
  const exportRegex = /export\s+function\s+(\w+)\s*\(/g;
  const names = new Set();
  let match;
  while ((match = exportRegex.exec(content)) !== null) {
    names.add(match[1]);
  }
  return names;
}

/**
 * Find all potential native calls in source code
 */
function findUsedNatives(code, allNatives) {
  const used = new Set();
  // Match function calls: FunctionName(
  const callRegex = /\b([A-Z][A-Za-z0-9_]*)\s*\(/g;
  let match;
  while ((match = callRegex.exec(code)) !== null) {
    const name = match[1];
    if (allNatives.has(name)) {
      used.add(name);
    }
  }
  return used;
}

export function nativeTreeshake(options = {}) {
  const {
    natives: nativesPath = './natives.ts',
    globals = false
  } = options;

  let allNatives = null;
  let resolvedNativesPath = null;

  return {
    name: 'native-treeshake',

    setup(build) {
      const workingDir = build.initialOptions.absWorkingDir || process.cwd();
      resolvedNativesPath = resolve(workingDir, nativesPath);

      // Lazily load native names
      const getNatives = () => {
        if (!allNatives) {
          allNatives = extractNativeNames(resolvedNativesPath);
        }
        return allNatives;
      };

      // Virtual module that re-exports only used natives
      build.onResolve({ filter: /^@natives$/ }, args => {
        return {
          path: args.path,
          namespace: 'native-inject',
          pluginData: { importer: args.importer }
        };
      });

      build.onLoad({ filter: /.*/, namespace: 'native-inject' }, async args => {
        // This is called when @natives is imported
        // We need to figure out what natives the importing file uses
        // For now, export everything and let esbuild tree-shake
        const natives = getNatives();
        const exports = [...natives].map(name => `  ${name}`).join(',\n');

        return {
          contents: `export {\n${exports}\n} from '${resolvedNativesPath.replace(/\\/g, '/')}';`,
          loader: 'ts',
          resolveDir: dirname(resolvedNativesPath)
        };
      });

      // Transform user code to add imports and optionally globalize
      build.onLoad({ filter: /\.(ts|js|tsx|jsx)$/ }, async args => {
        // Skip the natives file itself
        if (args.path === resolvedNativesPath) {
          return null;
        }

        const source = readFileSync(args.path, 'utf-8');
        const natives = getNatives();
        const usedNatives = findUsedNatives(source, natives);

        if (usedNatives.size === 0) {
          return null; // No natives used, don't transform
        }

        // Generate import statement
        const importList = [...usedNatives].join(', ');
        const importStatement = `import { ${importList} } from '${resolvedNativesPath.replace(/\\/g, '/')}';\n`;

        // Optionally add global assignments
        let globalAssignments = '';
        if (globals) {
          globalAssignments = [...usedNatives]
            .map(name => `globalThis.${name} = ${name};`)
            .join('\n') + '\n';
        }

        const loader = args.path.endsWith('.tsx') ? 'tsx'
          : args.path.endsWith('.jsx') ? 'jsx'
          : args.path.endsWith('.ts') ? 'ts'
          : 'js';

        return {
          contents: importStatement + globalAssignments + source,
          loader
        };
      });
    }
  };
}

export default nativeTreeshake;
