# {{PACKAGE_NAME}}

Auto-generated native function bindings with tree-shaking support.

## Installation

```bash
npm install {{PACKAGE_NAME}}
```

### With esbuild plugin (auto-import)

```javascript
import { nativeTreeshake } from '{{PACKAGE_NAME}}/plugin';
import esbuild from 'esbuild';

esbuild.build({
  entryPoints: ['src/index.ts'],
  bundle: true,
  plugins: [nativeTreeshake({
    natives: './node_modules/{{PACKAGE_NAME}}/dist/natives.js'
  })]
});
```

Then use natives directly without imports:

```typescript
// No import needed - plugin handles it
const coords = GetEntityCoords(entity, true);
const ped = CreatePed(model, x, y, z, heading, false, false);
```

## Tree-shaking

Both approaches support tree-shaking - only the natives you actually use will be included in your bundle.
