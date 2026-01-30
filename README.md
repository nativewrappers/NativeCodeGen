# NativeCodeGen

**This project was mainly generated with the assistance of AI (Claude).**

This means things here could be wrong, broken, or not function in ways that may
seem logic.

With this project being mainly AI generated this project is under the `Unlicense`
you can use, modify, distribute this code however you want.

This project is primarily for use with https://github.com/nativewrappers/rdr3-natives
it is what is used to check prs and on push, updates these static urls:

JSON:
`https://static.avarian.dev/natives.json`

Proto:
`https://static.avarian.dev/natives.bin`
`https://static.avarian.dev/natives.proto`

You can use these url's however you please, these are hosted on a static pages
website through Cloudflare.

This project is mainly full of stuff that I have no desire of spending an extreme
amount of time doing, it parses mdx, provides useful outputs that can be used on
the frontend.

It can produce new native gen so you can have one resource that has the `lua-classes` or `lua-raw`
and you can include them as needed across other resource.

Native gen will, when defined, generate structs and let you use them in native calls.


---

Code generation tool for RDR3 (Red Dead Redemption 3) native functions. Parses MDX native definition files and generates typed wrappers, serialized databases, and documentation.

## Requirements

- .NET 10.0 SDK or later

## Installation

### From Source

```bash
cd NativeCodeGen
dotnet pack src/NativeCodeGen.Cli -c Release
dotnet tool install --global --add-source ./src/NativeCodeGen.Cli/nupkg NativeCodeGen
```

### Update

```bash
dotnet tool update --global --add-source ./src/NativeCodeGen.Cli/nupkg NativeCodeGen
```

### Uninstall

```bash
dotnet tool uninstall --global NativeCodeGen
```

## Usage

### Generate Command

```bash
nativegen generate -i <input> -o <output> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `-i, --input` | Input directory containing MDX files | Required |
| `-o, --output` | Output directory or file | Required |
| `-f, --format` | Output format: `typescript`, `lua`, `json`, `proto` | `typescript` |
| `-n, --namespaces` | Filter specific namespaces (comma-separated) | All |
| `--raw` | Generate raw functions without wrapper classes | `false` |
| `--single-file` | Combine all natives into single file (requires `--raw`) | `false` |
| `--exports` | Use ES module exports for tree-shaking (requires `--raw --single-file`) | `false` |
| `--package` | Generate complete npm package with esbuild plugin | `false` |
| `--package-name` | npm package name (required with `--package`) | - |
| `--package-version` | npm package version | `0.0.1` |
| `--strict` | Treat warnings as errors | `false` |

### Validate Command

```bash
nativegen validate -i <input> [--strict]
```

## Examples

```bash
# TypeScript with wrapper classes
nativegen generate -i ./rdr3-natives -o ./output -f typescript

# Raw TypeScript (one file per namespace)
nativegen generate -i ./rdr3-natives -o ./output -f typescript --raw

# Single file with ES exports (tree-shakeable)
nativegen generate -i ./rdr3-natives -o ./output -f typescript --raw --single-file --exports

# Complete npm package with esbuild plugin
nativegen generate -i ./rdr3-natives -o ./output -f typescript --package --package-name "@nativewrappers/rdr3"

# Class-based npm package (no esbuild plugin)
nativegen generate -i ./rdr3-natives -o ./output -f typescript --package --package-name "@nativewrappers/rdr3-classes"

# JSON database
nativegen generate -i ./rdr3-natives -o ./output/natives.json -f json

# Protocol Buffer binary
nativegen generate -i ./rdr3-natives -o ./output/natives.bin -f proto

# Lua with wrapper classes
nativegen generate -i ./rdr3-natives -o ./output -f lua
```

## Output Formats

### TypeScript

**Class mode** (default): OOP-style wrapper classes

```
output/
├── index.ts
├── types/
├── enums/
├── structs/
├── classes/          # Entity, Ped, Vehicle, etc.
└── namespaces/       # Static utility classes
```

**Raw mode** (`--raw`): Standalone functions per namespace

```
output/
├── index.ts
├── types/
├── enums/
├── structs/
└── natives/          # One file per namespace
```

**Single file mode** (`--raw --single-file`): All natives in one file

```
output/
├── index.ts
├── types/
├── enums/
├── structs/
└── natives.ts
```

**Package mode** (`--package`): Complete npm package

```
output/
├── package.json
├── tsconfig.json
├── README.md
├── plugin/           # esbuild plugin (raw mode only)
│   └── native-treeshake.js
└── src/
    ├── index.ts
    └── ...
```

### JSON

Single JSON file with complete database:

```json
{
  "namespaces": [...],
  "natives": [...],
  "enums": {...},
  "structs": {...},
  "sharedExamples": {...},
  "types": {...}
}
```

### Protocol Buffer

Binary file serialized per `natives.proto` schema.

You can fetch these from `https://static.avarian.dev/natives.bin` and `https://static.avarian.dev/natives.proto`

## Description Formatting

Native descriptions support inline references and callouts using bracket syntax.

### References

| Pattern | Description |
|---------|-------------|
| `[enum: Name]` | Link to enum definition |
| `[struct: Name]` | Link to struct definition |
| `[native: Name]` | Link to native (same game) |
| `[native: Name \| game]` | Link to native in another game |
| `[example: Name]` | Embed shared code example |

### Callouts

| Pattern | Description |
|---------|-------------|
| `[note: Description]` | Note callout |
| `[note: Title \| Description]` | Note with title |
| `[warning: ...]` | Warning callout |
| `[info: ...]` | Info callout |
| `[danger: ...]` | Danger callout |

Callouts are extracted and stored in the `callouts` array on each native.

## Tree-Shaking with esbuild

When generating with `--package --raw`, an esbuild plugin is included for automatic tree-shaking:

```javascript
import { nativeTreeshake } from '@nativewrappers/rdr3/plugin';
import esbuild from 'esbuild';

esbuild.build({
  entryPoints: ['src/index.ts'],
  bundle: true,
  plugins: [nativeTreeshake({
    natives: './node_modules/@nativewrappers/rdr3/dist/natives.js'
  })]
});
```

The plugin automatically detects which natives you use and imports only those.

## Project Structure

```
NativeCodeGen/
├── src/
│   ├── NativeCodeGen.Core/          # Parsing and models
│   │   ├── Models/                  # Data models
│   │   ├── Parsing/                 # MDX, enum, struct parsers
│   │   ├── Registry/                # Type registries
│   │   └── Export/                  # JSON, Protobuf exporters
│   ├── NativeCodeGen.TypeScript/    # TypeScript generation
│   ├── NativeCodeGen.Lua/           # Lua generation
│   └── NativeCodeGen.Cli/           # CLI application
└── tests/
    └── NativeCodeGen.Tests/         # Unit tests
```

## Building

```bash
dotnet build              # Debug
dotnet build -c Release   # Release
dotnet test               # Run tests
```
