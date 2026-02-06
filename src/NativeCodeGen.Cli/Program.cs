using System.Collections.Concurrent;
using System.CommandLine;
using NativeCodeGen.Core.Export;
using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Parsing;
using NativeCodeGen.Core.Registry;
using NativeCodeGen.Core.Utilities;
using NativeCodeGen.Core.Validation;
using NativeCodeGen.CSharp;
using NativeCodeGen.Lua;
using NativeCodeGen.TypeScript;

namespace NativeCodeGen.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("RDR3 Native Code Generator");

        // Generate command
        var generateCommand = new Command("generate", "Generate code from MDX native definitions");

        var inputOption = new Option<string>(
            aliases: new[] { "-i", "--input" },
            description: "Input directory containing MDX files")
        { IsRequired = true };

        var outputOption = new Option<string>(
            aliases: new[] { "-o", "--output" },
            description: "Output directory or file")
        { IsRequired = true };

        var formatOption = new Option<string>(
            aliases: new[] { "-f", "--format" },
            description: "Output format: typescript, lua, csharp, json, proto",
            getDefaultValue: () => "typescript");

        var namespacesOption = new Option<string[]>(
            aliases: new[] { "-n", "--namespaces" },
            description: "Specific namespaces to generate (comma-separated or multiple -n flags)")
        { AllowMultipleArgumentsPerToken = true };

        var rawOption = new Option<bool>(
            aliases: new[] { "--raw" },
            description: "Generate raw native declarations without wrapper classes",
            getDefaultValue: () => false);

        var singleFileOption = new Option<bool>(
            aliases: new[] { "--single-file" },
            description: "Generate all natives in a single file (requires --raw)",
            getDefaultValue: () => false);

        var strictOption = new Option<bool>(
            aliases: new[] { "--strict" },
            description: "Treat warnings as errors",
            getDefaultValue: () => false);

        var exportsOption = new Option<bool>(
            aliases: new[] { "--exports" },
            description: "Use ES module exports for tree-shaking (requires --raw --single-file)",
            getDefaultValue: () => false);

        var packageOption = new Option<bool>(
            aliases: new[] { "--package" },
            description: "Generate a complete npm package with esbuild plugin",
            getDefaultValue: () => false);

        var packageNameOption = new Option<string?>(
            aliases: new[] { "--package-name" },
            description: "npm package name (e.g., @nativewrappers/natives-rdr3)");

        var packageVersionOption = new Option<string?>(
            aliases: new[] { "--package-version" },
            description: "npm package version (e.g., 1.0.0)",
            getDefaultValue: () => "0.0.1");

        generateCommand.AddOption(inputOption);
        generateCommand.AddOption(outputOption);
        generateCommand.AddOption(formatOption);
        generateCommand.AddOption(namespacesOption);
        generateCommand.AddOption(rawOption);
        generateCommand.AddOption(singleFileOption);
        generateCommand.AddOption(strictOption);
        generateCommand.AddOption(exportsOption);
        generateCommand.AddOption(packageOption);
        generateCommand.AddOption(packageNameOption);
        generateCommand.AddOption(packageVersionOption);

        generateCommand.SetHandler(async (context) =>
        {
            var input = context.ParseResult.GetValueForOption(inputOption)!;
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var format = context.ParseResult.GetValueForOption(formatOption)!;
            var namespaces = context.ParseResult.GetValueForOption(namespacesOption);
            var raw = context.ParseResult.GetValueForOption(rawOption);
            var singleFile = context.ParseResult.GetValueForOption(singleFileOption);
            var strict = context.ParseResult.GetValueForOption(strictOption);
            var exports = context.ParseResult.GetValueForOption(exportsOption);
            var package_ = context.ParseResult.GetValueForOption(packageOption);
            var packageName = context.ParseResult.GetValueForOption(packageNameOption);
            var packageVersion = context.ParseResult.GetValueForOption(packageVersionOption);

            var exitCode = await Generate(input, output, format, namespaces, raw, singleFile, strict, exports, package_, packageName, packageVersion);
            context.ExitCode = exitCode;
        });

        // Validate command
        var validateCommand = new Command("validate", "Validate MDX files without generating output");

        var validateInputOption = new Option<string>(
            aliases: new[] { "-i", "--input" },
            description: "Input directory containing MDX files")
        { IsRequired = true };

        var validateStrictOption = new Option<bool>(
            aliases: new[] { "--strict" },
            description: "Treat warnings as errors",
            getDefaultValue: () => false);

        validateCommand.AddOption(validateInputOption);
        validateCommand.AddOption(validateStrictOption);

        validateCommand.SetHandler(async (context) =>
        {
            var input = context.ParseResult.GetValueForOption(validateInputOption)!;
            var strict = context.ParseResult.GetValueForOption(validateStrictOption);

            var exitCode = await Validate(input, strict);
            context.ExitCode = exitCode;
        });

        rootCommand.AddCommand(generateCommand);
        rootCommand.AddCommand(validateCommand);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task<int> Generate(string input, string output, string format, string[]? namespaces, bool raw, bool singleFile, bool strict, bool exports, bool package_, string? packageName, string? packageVersion)
    {
        Console.WriteLine($"Generating {format} output...");
        Console.WriteLine($"Input: {input}");
        Console.WriteLine($"Output: {output}");

        if (singleFile && !raw)
        {
            Console.Error.WriteLine("Error: --single-file requires --raw");
            return 1;
        }

        if (exports && (!raw || !singleFile))
        {
            Console.Error.WriteLine("Error: --exports requires --raw --single-file");
            return 1;
        }

        if (package_)
        {
            if (string.IsNullOrEmpty(packageName))
            {
                Console.Error.WriteLine("Error: --package requires --package-name");
                return 1;
            }

            // If raw mode is specified with package, also enable single-file and exports
            if (raw)
            {
                singleFile = true;
                exports = true;
            }
        }

        var (db, errors, warnings) = await ParseAllFiles(input);

        // Report issues
        ReportIssues(errors, warnings, strict);

        if (errors.Count > 0 || (strict && warnings.Count > 0))
        {
            Console.WriteLine($"\nGeneration aborted due to {errors.Count} errors" +
                (strict && warnings.Count > 0 ? $" and {warnings.Count} warnings (--strict)" : ""));
            return 1;
        }

        // Create exporter based on format
        IExporter? exporter = format.ToLowerInvariant() switch
        {
            "typescript" or "ts" => new TypeScriptExporter(),
            "lua" => new LuaExporter(),
            "csharp" or "cs" => new CSharpExporter(),
            "json" => new JsonExporter(),
            "proto" or "protobuf" => new ProtobufExporter(),
            _ => null
        };

        if (exporter == null)
        {
            Console.Error.WriteLine($"Error: Unknown format '{format}'. Supported formats: typescript, lua, csharp, json, proto");
            return 1;
        }

        var options = new ExportOptions
        {
            Raw = raw,
            SingleFile = singleFile,
            Strict = strict,
            UseExports = exports,
            Package = package_,
            PackageName = packageName,
            PackageVersion = packageVersion ?? "0.0.1",
            Namespaces = namespaces?.Length > 0
                ? new HashSet<string>(namespaces.SelectMany(n => n.Split(',')), StringComparer.OrdinalIgnoreCase)
                : null
        };

        exporter.Export(db, output, options);

        Console.WriteLine($"\nGenerated {db.Namespaces.Sum(n => n.Natives.Count)} natives in {db.Namespaces.Count} namespaces");
        Console.WriteLine($"Included {db.Enums.Count} enums and {db.Structs.Count} structs");
        Console.WriteLine($"Output written to: {output}");
        return 0;
    }

    static async Task<int> Validate(string input, bool strict)
    {
        Console.WriteLine($"Validating MDX files in: {input}");

        var (db, errors, warnings) = await ParseAllFiles(input);

        // Report issues
        ReportIssues(errors, warnings, strict);

        var totalFiles = db.Namespaces.Sum(n => n.Natives.Count);
        var errorCount = errors.Count + (strict ? warnings.Count : 0);

        Console.WriteLine();
        Console.WriteLine($"Validated {totalFiles} files in {db.Namespaces.Count} namespaces");
        Console.WriteLine($"Found {errors.Count} errors, {warnings.Count} warnings");

        return errorCount > 0 ? 1 : 0;
    }

    static async Task<(NativeCodeGen.Core.Parsing.NativeDatabase db, List<ParseError> errors, List<ParseWarning> warnings)> ParseAllFiles(string inputDir)
    {
        var db = new NativeCodeGen.Core.Parsing.NativeDatabase();

        // Load enums
        var enumRegistry = new EnumRegistry();
        var enumsDir = Path.Combine(inputDir, "code", "enums");
        if (Directory.Exists(enumsDir))
        {
            enumRegistry.LoadEnums(enumsDir);
            Console.WriteLine($"Loaded {enumRegistry.Count} enums from {enumsDir}");
        }
        db.Enums = enumRegistry.GetAllEnums();

        // Load structs
        var structRegistry = new StructRegistry();
        var structsDir = Path.Combine(inputDir, "code", "structs");
        if (Directory.Exists(structsDir))
        {
            structRegistry.LoadStructs(structsDir);
            Console.WriteLine($"Loaded {structRegistry.Count} structs from {structsDir}");
        }
        db.Structs = structRegistry.GetAllStructs();

        // Load shared examples
        var sharedExampleRegistry = new SharedExampleRegistry();
        var sharedExamplesDir = Path.Combine(inputDir, "code", "shared-examples");
        var sharedExampleErrors = new List<ParseError>();
        if (Directory.Exists(sharedExamplesDir))
        {
            sharedExampleRegistry.LoadExamples(sharedExamplesDir);
            Console.WriteLine($"Loaded {sharedExampleRegistry.Count} shared examples from {sharedExamplesDir}");

            // Convert shared example errors to ParseErrors
            foreach (var error in sharedExampleRegistry.Errors)
            {
                sharedExampleErrors.Add(new ParseError
                {
                    FilePath = error.Split(':')[0],
                    Line = 1,
                    Column = 1,
                    Message = error.Contains(':') ? error[(error.IndexOf(':') + 2)..] : error
                });
            }
        }
        db.SharedExamples = sharedExampleRegistry.GetAllExamples();

        // Find all MDX files (natives are in namespace directories)
        var mdxFiles = Directory.GetFiles(inputDir, "*.mdx", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.Combine("code", "enums")))           // Exclude enum files
            .Where(f => !f.Contains(Path.Combine("code", "shared-examples"))) // Exclude shared examples
            .ToArray();

        Console.WriteLine($"Found {mdxFiles.Length} MDX files to parse...");

        // Thread-safe collections for parallel processing
        var allErrors = new ConcurrentBag<ParseError>();
        var allWarnings = new ConcurrentBag<ParseWarning>();
        var allNatives = new ConcurrentBag<NativeDefinition>();
        var processedCount = 0;

        // Create type validator for type resolution and validation
        var typeValidator = new TypeValidator(enumRegistry, structRegistry);

        // Process files in parallel (CPU-bound parsing)
        Parallel.ForEach(mdxFiles, file =>
        {
            try
            {
                var content = File.ReadAllText(file);
                var parser = new MdxParser();
                var result = parser.Parse(content, file);

                foreach (var error in result.Errors)
                    allErrors.Add(error);
                foreach (var warning in result.Warnings)
                    allWarnings.Add(warning);

                if (result.Value != null)
                {
                    // Validate all types in the native definition
                    var validationErrors = typeValidator.ValidateNative(result.Value, file);
                    foreach (var error in validationErrors)
                        allErrors.Add(error);

                    allNatives.Add(result.Value);
                }

                Interlocked.Increment(ref processedCount);
            }
            catch (Exception ex)
            {
                allErrors.Add(new ParseError
                {
                    FilePath = file,
                    Line = 1,
                    Column = 1,
                    Message = $"Failed to parse file: {ex.Message}"
                });
            }
        });

        // Check for duplicate native names (globally, after normalization)
        var duplicateNames = allNatives
            .GroupBy(n => NameConverter.NormalizeNativeName(n.Name), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var dup in duplicateNames)
        {
            var natives = dup.ToList();
            var names = natives.Select(n => n.Name).Distinct().ToList();
            var nameDisplay = names.Count > 1 ? $"'{string.Join("', '", names)}'" : $"'{names[0]}'";
            allErrors.Add(new ParseError
            {
                FilePath = natives[0].SourceFile ?? "unknown",
                Line = 1,
                Column = 1,
                Message = $"Duplicate native name {nameDisplay} (normalized: '{dup.Key}'). Also defined in: {string.Join(", ", natives.Skip(1).Select(n => Path.GetFileName(n.SourceFile ?? "unknown")))}"
            });
        }

        // Check for duplicate hashes
        var duplicateHashes = allNatives
            .GroupBy(n => n.Hash, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var dup in duplicateHashes)
        {
            var natives = dup.ToList();
            allErrors.Add(new ParseError
            {
                FilePath = natives[0].SourceFile ?? "unknown",
                Line = 1,
                Column = 1,
                Message = $"Duplicate hash '{dup.Key}'. Also defined in: {string.Join(", ", natives.Skip(1).Select(n => Path.GetFileName(n.SourceFile ?? "unknown")))}"
            });
        }

        // Auto-link shared examples to natives by scanning example code for function calls
        if (db.SharedExamples.Count > 0)
        {
            var linked = sharedExampleRegistry.AutoLinkExamples(allNatives);
            if (linked > 0)
                Console.WriteLine($"Auto-linked {linked} example references to natives");
        }

        // Group natives by namespace
        var namespaceDict = allNatives
            .GroupBy(n => n.Namespace, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Track struct and enum usage
        var structDict = structRegistry.GetAllStructs();
        var enumDict = enumRegistry.GetAllEnums();

        foreach (var native in allNatives)
        {
            // Check parameter types
            foreach (var param in native.Parameters)
            {
                var typeName = param.Type.Name;

                // Check struct usage
                if (structDict.TryGetValue(typeName, out var structDef))
                {
                    if (!structDef.UsedByNatives.Contains(native.Hash))
                    {
                        structDef.UsedByNatives.Add(native.Hash);
                    }
                }

                // Check enum usage (when Category is Enum, Name holds the enum name)
                if (param.Type.Category == TypeCategory.Enum && enumDict.TryGetValue(param.Type.Name, out var enumDef))
                {
                    if (!enumDef.UsedByNatives.Contains(native.Hash))
                    {
                        enumDef.UsedByNatives.Add(native.Hash);
                    }
                }
            }

            // Check return type for enum usage
            if (native.ReturnType.Category == TypeCategory.Enum && enumDict.TryGetValue(native.ReturnType.Name, out var returnEnumDef))
            {
                if (!returnEnumDef.UsedByNatives.Contains(native.Hash))
                {
                    returnEnumDef.UsedByNatives.Add(native.Hash);
                }
            }
        }

        // Convert to namespace list
        db.Namespaces = namespaceDict
            .Select(kvp => new NativeCodeGen.Core.Parsing.NativeNamespace { Name = kvp.Key, Natives = kvp.Value })
            .OrderBy(n => n.Name)
            .ToList();

        db.Enums = enumRegistry.GetAllEnums();

        // Add shared example errors
        foreach (var error in sharedExampleErrors)
            allErrors.Add(error);

        return (db, allErrors.ToList(), allWarnings.ToList());
    }

    static void ReportIssues(List<ParseError> errors, List<ParseWarning> warnings, bool strict)
    {
        if (errors.Count > 0)
        {
            Console.WriteLine("\nERRORS:");
            foreach (var error in errors.Take(50)) // Limit output
            {
                Console.WriteLine($"  {error}");
            }
            if (errors.Count > 50)
            {
                Console.WriteLine($"  ... and {errors.Count - 50} more errors");
            }
        }

        if (warnings.Count > 0)
        {
            Console.WriteLine($"\nWARNINGS{(strict ? " (treated as errors with --strict)" : "")}:");
            foreach (var warning in warnings.Take(50)) // Limit output
            {
                Console.WriteLine($"  {warning}");
            }
            if (warnings.Count > 50)
            {
                Console.WriteLine($"  ... and {warnings.Count - 50} more warnings");
            }
        }
    }
}
