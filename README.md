# NativeCodeGen

**This project was mainly generated with the assistance of AI (Claude).**

---

NativeCodeGen is a code generation tool for RDR3 (Red Dead Redemption 3) native functions. It parses MDX native definition files and generates typed wrappers, serialized databases, and documentation for use in game modification projects.

## Features

- Parses MDX native definition files with YAML frontmatter and C-style function signatures
- Parses C-style enum definitions with hexadecimal values
- Parses C-style struct definitions with field attributes
- Generates TypeScript wrapper classes with proper type mappings
- Generates Lua wrapper classes with proper type annotations
- Generates JSON database for web-based documentation
- Generates Protocol Buffer binary files for efficient data transfer
- Validates native definitions with error accumulation and reporting

## Requirements

- .NET 10.0 SDK or later

## Installation

### From Source (recommended)

Clone the repository and install as a global dotnet tool:

```bash
cd NativeCodeGen
dotnet pack src/NativeCodeGen.Cli -c Release
dotnet tool install --global --add-source ./src/NativeCodeGen.Cli/nupkg NativeCodeGen
```

The `nativegen` command will now be available globally.

### Update

To update to a newer version:

```bash
dotnet tool update --global --add-source ./src/NativeCodeGen.Cli/nupkg NativeCodeGen
```

### Uninstall

```bash
dotnet tool uninstall --global NativeCodeGen
```

## Building (for development)

```bash
cd NativeCodeGen
dotnet build
```

For a release build:

```bash
dotnet build -c Release
```

## Usage

The CLI provides two main commands: `generate` and `validate`.

### Generate Command

Generates code or data files from MDX native definitions.

```bash
dotnet run --project src/NativeCodeGen.Cli -- generate -i <input> -o <output> [options]
```

#### Required Arguments

| Argument | Description |
|----------|-------------|
| `-i, --input` | Input directory containing MDX files |
| `-o, --output` | Output directory or file |

#### Options

| Option | Description | Default |
|--------|-------------|---------|
| `-f, --format` | Output format: `typescript`, `lua`, `json`, `proto` | `typescript` |
| `-n, --namespaces` | Filter specific namespaces (comma-separated) | All namespaces |
| `--raw` | Generate raw native declarations without wrapper classes (TypeScript/Lua) | `false` |
| `--single-file` | Combine all natives into a single file (requires `--raw`) | `false` |
| `--strict` | Treat warnings as errors | `false` |

#### Examples

Generate TypeScript wrapper classes:

```bash
nativegen generate -i /path/to/rdr3-natives -o ./output -f typescript
```

Generate raw TypeScript declarations (no classes, one file per namespace):

```bash
nativegen generate -i /path/to/rdr3-natives -o ./output -f typescript --raw
```

Generate raw TypeScript declarations (single file with all natives):

```bash
nativegen generate -i /path/to/rdr3-natives -o ./output -f typescript --raw --single-file
```

Generate Lua wrapper classes:

```bash
nativegen generate -i /path/to/rdr3-natives -o ./output -f lua
```

Generate raw Lua declarations (no classes, one file per namespace):

```bash
nativegen generate -i /path/to/rdr3-natives -o ./output -f lua --raw
```

Generate raw Lua declarations (single file with all natives):

```bash
nativegen generate -i /path/to/rdr3-natives -o ./output -f lua --raw --single-file
```

Generate JSON database:

```bash
nativegen generate -i /path/to/rdr3-natives -o ./output/natives.json -f json
```

Generate Protocol Buffer binary:

```bash
nativegen generate -i /path/to/rdr3-natives -o ./output/natives.bin -f proto
```

Generate only specific namespaces:

```bash
nativegen generate -i /path/to/rdr3-natives -o ./output -f typescript -n ENTITY,PED,VEHICLE
```

### Validate Command

Validates MDX files without generating output. Useful for CI pipelines.

```bash
dotnet run --project src/NativeCodeGen.Cli -- validate -i <input> [options]
```

#### Options

| Option | Description | Default |
|--------|-------------|---------|
| `--strict` | Treat warnings as errors | `false` |

#### Example

```bash
nativegen validate -i /path/to/rdr3-natives --strict
```

## Generation Modes

The `--raw` and `--single-file` flags control how native functions are generated:

### Default Mode (Classes)

Without flags, generates OOP-style wrapper classes where natives are organized as methods on entity classes:

```typescript
// classes/Entity.ts
export class Entity implements IHandle {
  constructor(public handle: number) {}

  getCoords(alive: boolean): Vector3 {
    return Vector3.fromArray(inv<number[]>('0x...', this.handle, alive, rav()));
  }
}

// Usage
const ped = new Ped(pedHandle);
const coords = ped.getCoords(true);
```

### Raw Mode (`--raw`)

Generates standalone exported functions with handles as plain numbers. One file per namespace:

```typescript
// natives/ENTITY.ts
export function GetEntityCoords(entity: number, alive: boolean): Vector3 {
  return Vector3.fromArray(inv('0x...', entity, alive, rav()));
}

// Usage
const coords = GetEntityCoords(pedHandle, true);
```

### Single File Mode (`--raw --single-file`)

Generates all natives in a single file as global functions. Useful for smaller bundles:

```typescript
// natives.ts
globalThis.GetEntityCoords = function(entity: number, alive: boolean): Vector3 {
  return Vector3.fromArray(inv('0x...', entity, alive, rav()));
};

// Usage (functions are on globalThis)
const coords = GetEntityCoords(pedHandle, true);
```

### Lua Modes

Lua follows the same pattern:

- **Raw mode**: Module tables per namespace (`Water.GetWaterHeight(...)`)
- **Single file mode**: Global functions (`GetWaterHeight(...)`)

## Output Formats

### TypeScript (default)

Generates typed wrapper classes organized by entity type:

```
output/
├── index.ts              # Re-exports all modules
├── types/
│   ├── IHandle.ts        # Handle interface
│   ├── Vector3.ts        # Vector3 class
│   └── BufferedClass.ts  # Base class for structs
├── enums/
│   └── *.ts              # Enum definitions
├── structs/
│   └── *.ts              # Struct classes (BufferedClass)
├── classes/
│   ├── Entity.ts         # Base entity class
│   ├── Ped.ts            # Ped class (extends Entity)
│   ├── Vehicle.ts        # Vehicle class (extends Entity)
│   └── *.ts              # Other handle classes
└── namespaces/
    └── *.ts              # Static utility classes
```

With `--raw` flag, generates standalone functions without class wrappers (one file per namespace):

```
output/
├── index.ts
├── types/
├── enums/
├── structs/
└── natives/
    └── *.ts              # Raw native function exports per namespace
```

With `--raw --single-file` flags, generates all natives in a single file:

```
output/
├── index.ts
├── types/
├── enums/
├── structs/
└── natives.ts            # All native functions in one file
```

### Lua

Generates typed wrapper classes with LuaLS annotations:

```
output/
├── init.lua              # Re-exports all modules
├── types/
│   ├── IHandle.lua       # Handle interface
│   ├── Vector3.lua       # Vector3 class
│   └── BufferedClass.lua # Base class for structs
├── enums/
│   └── *.lua             # Enum definitions
├── structs/
│   └── *.lua             # Struct classes (BufferedClass)
├── classes/
│   ├── Entity.lua        # Base entity class
│   ├── Ped.lua           # Ped class (extends Entity)
│   ├── Vehicle.lua       # Vehicle class (extends Entity)
│   └── *.lua             # Other handle classes
└── namespaces/
    └── *.lua             # Static utility classes
```

With `--raw` flag, generates standalone functions (one file per namespace):

```
output/
├── fxmanifest.lua
├── enums/
├── structs/
└── natives/
    └── *.lua             # Raw native function exports per namespace (module tables)
```

With `--raw --single-file` flags, generates all natives in a single file:

```
output/
├── fxmanifest.lua
├── enums/
├── structs/
└── natives.lua           # All native functions in one file (global functions)
```

### JSON

Generates a single JSON file containing the complete native database:

```json
{
  "namespaces": [...],
  "enums": {...},
  "structs": {...}
}
```

### Protocol Buffer

Generates a binary file (`natives.bin`) serialized according to the schema defined in `src/NativeCodeGen.Core/Export/natives.proto`. This format is optimized for efficient loading in web applications.

## Input Format

### MDX Native Files

Native definitions use MDX format with YAML frontmatter:

```markdown
---
ns: ENTITY
aliases: ["0xA86D5F069399F44D"]
apiset: client
---
## GET_ENTITY_COORDS

```cpp
// 0xA86D5F069399F44D
Vector3 GET_ENTITY_COORDS(Entity entity, bool alive, bool realCoords);
```

Gets the current coordinates of an entity.

## Parameters
* **entity**: The entity to get coordinates from
* **alive**: Unknown
* **realCoords**: Unknown

## Return value
The entity's world coordinates as a Vector3.
```

### Enum Files

Enum definitions use C-style syntax in `code/enums/`:

```c
enum eWeaponHash : Hash {
    WEAPON_UNARMED = 0xA2719263,
    WEAPON_KNIFE = 0x99B507EA,
};
```

### Struct Files

Struct definitions use C-style syntax in `code/structs/`:

```c
struct ExampleStruct {
    /// The unique identifier
    @out u32 hash;
    /// Input parameter
    @in bool enabled;
    /// Nested array of data
    OtherStruct data[3];
    /// Reserved space
    @padding u8 reserved[16];
};
```

**Field Attributes:**

| Attribute | Description |
|-----------|-------------|
| `@in` | Generate setter only |
| `@out` | Generate getter only |
| `@padding` | No accessors, reserves space in buffer |
| `@alignas(N)` | Override field alignment to N bytes |

**Alignment Control:**

By default, struct fields are aligned to 8 bytes (native pointer width). Use `@alignas(N)` to customize alignment at the struct level (applies to all fields) or per-field:

```c
// Struct-level alignment: all fields default to 4 bytes
@alignas(4)
struct CompactStruct {
    u32 hash;           // Uses 4 bytes (struct default)
    u16 flags;          // Uses 4 bytes (struct default)
    @alignas(2) u16 id; // Uses 2 bytes (field override)
    bool enabled;       // Uses 4 bytes (struct default)
};

// No struct-level alignment: fields use global default (8 bytes)
struct NativeStruct {
    u32 hash;           // Uses 8 bytes (global default)
    @alignas(4) u32 id; // Uses 4 bytes (field override)
};
```

Alignment precedence: field `@alignas` > struct `@alignas` > global default (8)

## Project Structure

```
NativeCodeGen/
├── NativeCodeGen.sln
└── src/
    ├── NativeCodeGen.Core/          # Core parsing and models
    │   ├── Models/                  # Data models (IR)
    │   ├── Parsing/                 # MDX, enum, struct parsers
    │   ├── Registry/                # Enum and struct registries
    │   ├── TypeSystem/              # Type mapping and categories
    │   └── Export/                  # Exporters (JSON, Protobuf)
    ├── NativeCodeGen.TypeScript/    # TypeScript generation
    │   ├── Generation/              # Code generators
    │   └── Utilities/               # Name conversion, etc.
    ├── NativeCodeGen.Lua/           # Lua generation
    │   ├── Generation/              # Code generators
    │   └── Utilities/               # Name conversion, etc.
    └── NativeCodeGen.Cli/           # Command-line interface
```
