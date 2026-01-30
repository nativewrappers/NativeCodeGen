# NativeCodeGen - Claude Context

Always use /tmp/nativegen for putting your generated builds

## Project Overview

NativeCodeGen is a code generator for RDR3 (Red Dead Redemption 3) and FiveM/RedM native functions. It parses MDX files containing native function definitions and generates TypeScript, Lua, JSON, and Protobuf outputs.

## Project Structure

```
NativeCodeGen/
├── src/
│   ├── NativeCodeGen.Cli/           # CLI application
│   │   ├── Program.cs               # Entry point, command parsing
│   │   └── TrimmerRoots.xml         # Types to preserve for AOT/trimming
│   ├── NativeCodeGen.Core/          # Core library
│   │   ├── Export/                  # Exporters for different formats
│   │   │   ├── IExporter.cs         # Exporter interface + ExportOptions
│   │   │   ├── ICodeGenerator.cs    # Code generator interface + GeneratorOptions
│   │   │   ├── BaseExporter.cs      # Base class for code exporters
│   │   │   ├── JsonExporter.cs      # JSON output
│   │   │   ├── ProtobufExporter.cs  # Protobuf binary output
│   │   │   ├── DatabaseConverter.cs # Converts parsed models to export models
│   │   │   ├── ExportModels.cs      # Export DTOs with protobuf attributes
│   │   │   ├── JsonContext.cs       # JSON source generator context
│   │   │   └── natives.proto        # Protobuf schema definition
│   │   ├── Generation/              # Code generation utilities
│   │   │   ├── CodeBuilder.cs       # String builder with indentation
│   │   │   ├── RawNativeBuilder.cs  # Raw function generation (TS/Lua)
│   │   │   ├── ArgumentBuilder.cs   # Native invoke argument building
│   │   │   ├── NativeClassifier.cs  # Classifies natives into handle classes
│   │   │   ├── SharedClassGenerator.cs    # Generates handle/namespace classes
│   │   │   ├── SharedStructGenerator.cs   # Generates struct classes
│   │   │   └── SharedEnumGenerator.cs     # Generates enum definitions
│   │   ├── Models/                  # Data models
│   │   │   ├── NativeDefinition.cs  # Native function definition
│   │   │   ├── NativeParameter.cs   # Function parameter with flags
│   │   │   ├── TypeInfo.cs          # Type information + categorization
│   │   │   ├── EnumDefinition.cs    # Enum with members
│   │   │   ├── StructDefinition.cs  # Struct with fields
│   │   │   ├── SharedExample.cs     # Shared code examples
│   │   │   ├── Callout.cs           # Note/warning callouts
│   │   │   └── Flags.cs             # ParamFlags, FieldFlags enums
│   │   ├── Parsing/                 # MDX/C parsing
│   │   │   ├── MdxParser.cs         # Main MDX file parser
│   │   │   ├── MdxComponentParser.cs # Bracket syntax parser [enum: X]
│   │   │   ├── FrontmatterParser.cs # YAML frontmatter parser
│   │   │   ├── SignatureParser.cs   # C-style signature parser
│   │   │   ├── EnumParser.cs        # C-style enum parser
│   │   │   ├── StructParser.cs      # C-style struct parser
│   │   │   └── CLexer.cs            # C tokenizer
│   │   ├── Registry/                # Type registries
│   │   │   ├── EnumRegistry.cs      # Enum definitions lookup
│   │   │   ├── StructRegistry.cs    # Struct definitions lookup
│   │   │   └── SharedExampleRegistry.cs # Shared examples lookup
│   │   ├── TypeSystem/              # Type mapping
│   │   │   ├── ITypeMapper.cs       # Type mapper interface
│   │   │   ├── TypeMapperBase.cs    # Base implementation
│   │   │   └── LanguageConfig.cs    # Language-specific config
│   │   └── Utilities/               # Helpers
│   │       ├── NameConverter.cs     # Name transformations
│   │       └── NameDeduplicator.cs  # Deduplication logic
│   ├── NativeCodeGen.TypeScript/    # TypeScript generator
│   │   ├── TypeScriptExporter.cs    # Main exporter
│   │   ├── Generation/
│   │   │   ├── TypeScriptGenerator.cs # Generates TS files
│   │   │   ├── TypeScriptEmitter.cs   # Language-specific emission
│   │   │   └── TypeScriptTypeMapper.cs # TS type mapping
│   └── NativeCodeGen.Lua/           # Lua generator (similar structure)
├── tests/
│   └── NativeCodeGen.Tests/         # Unit tests
├── packages/                        # Generated npm packages (WIP)
└── esbuild-plugin/                  # esbuild tree-shaking plugin
```

## Key Concepts

### MDX File Format
Native definitions are in MDX files with:
- YAML frontmatter: `ns` (namespace), `aliases`, `apiset`
- Heading with native name: `## NATIVE_NAME`
- C-style signature in code block
- Description, parameters, return value sections

### Bracket Syntax
- `[enum: EnumName]` - Reference to enum
- `[struct: StructName]` - Reference to struct
- `[native: NativeName]` or `[native: NativeName | gta5]` - Reference to native (with optional game)
- `[example: ExampleName]` - Reference to shared example
- `[note: Description]` or `[note: Title | Description]` - Note callout
- `[warning: ...]`, `[info: ...]`, `[danger: ...]` - Other callout types

### Type Categories (TypeInfo.cs)
- `Void`, `Primitive`, `Handle`, `Hash`, `String`, `Vector3`, `Any`, `Struct`, `Enum`

### Parameter Flags (ParamFlags)
- `None`, `Output`, `This`, `NotNull`, `In`
- `Output` = pointer output (int*, float*, Vector3*)
- `In` = input+output pointer (@in attribute)
- `This` = instance method receiver (@this attribute)

### Binding Styles (RawNativeBuilder)
- `Global` - `globalThis.X = function()` (no tree-shaking)
- `Export` - `export function X()` (tree-shakeable)
- `Module` - `ModuleName.X = function()`

### Generation Modes
1. **Class-based** (default): Generates handle classes (Entity, Ped, Vehicle) and namespace utilities
2. **Raw**: Generates plain functions per namespace file
3. **Single-file**: All natives in one file (requires --raw)
4. **Package**: Complete npm package with esbuild plugin (WIP)

## CLI Commands

```bash
# Generate TypeScript (class-based)
nativegen generate -i <input> -o <output> -f typescript

# Generate raw functions
nativegen generate -i <input> -o <output> -f typescript --raw

# Generate single file with exports (tree-shakeable)
nativegen generate -i <input> -o <output> -f typescript --raw --single-file --exports

# Generate JSON database
nativegen generate -i <input> -o <output>/natives.json -f json

# Generate Protobuf binary
nativegen generate -i <input> -o <output>/natives.bin -f proto

# Validate only
nativegen validate -i <input>
```

## Important Implementation Details

### Trimmer/AOT Compatibility
- Uses `TrimmerRoots.xml` to preserve reflection-based types
- Must add any YamlDotNet-deserialized classes (Frontmatter, SharedExampleFrontmatter)
- Must add protobuf-net serialized classes (Export* classes)

### Enum Value Generation
- C-style: unassigned members get `previous + 1`
- Handles negative start values (e.g., eVehicleSeat starts at -2)
- Parses hex values for incrementing

### JSON Output Structure
- Flat `natives` array (each with `ns` field)
- `enums`, `structs`, `sharedExamples` as dictionaries keyed by name
- `types` for type definitions

### Protobuf Output
- Uses protobuf-net for serialization
- Schema in `natives.proto`
- Binary output for efficient parsing

## Current Work in Progress

1. **--package flag**: Generate complete npm package with:
   - package.json
   - esbuild plugin for tree-shaking
   - Build scripts
   - Generated natives with `export function` style

2. **esbuild plugin**: Auto-import used natives for tree-shaking without explicit imports

## Testing

```bash
dotnet test                           # Run all tests
dotnet test --filter "ClassName"      # Run specific test class
```

## Building

```bash
dotnet build                          # Debug build
dotnet publish src/NativeCodeGen.Cli -c Release -r linux-x64 --self-contained  # Release
```

## Related Repositories

- `rdr3-natives`: MDX native definitions for RDR3
- Input expected at: `code/enums/`, `code/structs/`, `code/shared-examples/`, and namespace directories
