using System.Collections.Concurrent;
using System.CommandLine;
using NativeCodeGen.Core.Export;
using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Parsing;
using NativeCodeGen.Core.Registry;
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
            description: "Output format: typescript, lua, json, proto",
            getDefaultValue: () => "typescript");

        var namespacesOption = new Option<string[]>(
            aliases: new[] { "-n", "--namespaces" },
            description: "Specific namespaces to generate (comma-separated or multiple -n flags)")
        { AllowMultipleArgumentsPerToken = true };

        var rawOption = new Option<bool>(
            aliases: new[] { "--raw" },
            description: "Generate raw native declarations without wrapper classes (typescript only)",
            getDefaultValue: () => false);

        var strictOption = new Option<bool>(
            aliases: new[] { "--strict" },
            description: "Treat warnings as errors",
            getDefaultValue: () => false);

        generateCommand.AddOption(inputOption);
        generateCommand.AddOption(outputOption);
        generateCommand.AddOption(formatOption);
        generateCommand.AddOption(namespacesOption);
        generateCommand.AddOption(rawOption);
        generateCommand.AddOption(strictOption);

        generateCommand.SetHandler(async (input, output, format, namespaces, raw, strict) =>
        {
            await Generate(input, output, format, namespaces, raw, strict);
        }, inputOption, outputOption, formatOption, namespacesOption, rawOption, strictOption);

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

        validateCommand.SetHandler(async (input, strict) =>
        {
            await Validate(input, strict);
        }, validateInputOption, validateStrictOption);

        rootCommand.AddCommand(generateCommand);
        rootCommand.AddCommand(validateCommand);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task Generate(string input, string output, string format, string[]? namespaces, bool raw, bool strict)
    {
        Console.WriteLine($"Generating {format} output...");
        Console.WriteLine($"Input: {input}");
        Console.WriteLine($"Output: {output}");

        var (db, errors, warnings) = await ParseAllFiles(input);

        // Report issues
        ReportIssues(errors, warnings, strict);

        if (errors.Count > 0 || (strict && warnings.Count > 0))
        {
            Console.WriteLine($"\nGeneration aborted due to {errors.Count} errors" +
                (strict && warnings.Count > 0 ? $" and {warnings.Count} warnings (--strict)" : ""));
            Environment.ExitCode = 1;
            return;
        }

        // Create exporter based on format
        IExporter? exporter = format.ToLowerInvariant() switch
        {
            "typescript" or "ts" => new TypeScriptExporter(),
            "lua" => new LuaExporter(),
            "json" => new JsonExporter(),
            "proto" or "protobuf" => new ProtobufExporter(),
            _ => null
        };

        if (exporter == null)
        {
            Console.Error.WriteLine($"Error: Unknown format '{format}'. Supported formats: typescript, lua, json, proto");
            Environment.ExitCode = 1;
            return;
        }

        var options = new ExportOptions
        {
            Raw = raw,
            Strict = strict,
            Namespaces = namespaces?.Length > 0
                ? new HashSet<string>(namespaces.SelectMany(n => n.Split(',')), StringComparer.OrdinalIgnoreCase)
                : null
        };

        exporter.Export(db, output, options);

        Console.WriteLine($"\nGenerated {db.Namespaces.Sum(n => n.Natives.Count)} natives in {db.Namespaces.Count} namespaces");
        Console.WriteLine($"Included {db.Enums.Count} enums and {db.Structs.Count} structs");
        Console.WriteLine($"Output written to: {output}");
    }

    static async Task Validate(string input, bool strict)
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

        if (errorCount > 0)
        {
            Environment.ExitCode = 1;
        }
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
        if (Directory.Exists(sharedExamplesDir))
        {
            sharedExampleRegistry.LoadExamples(sharedExamplesDir);
            Console.WriteLine($"Loaded {sharedExampleRegistry.Count} shared examples from {sharedExamplesDir}");
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
                    allNatives.Add(result.Value);

                    foreach (var enumName in result.Value.UsedEnums)
                    {
                        if (!enumRegistry.Contains(enumName))
                        {
                            allWarnings.Add(new ParseWarning
                            {
                                FilePath = file,
                                Line = 1,
                                Column = 1,
                                Message = $"Referenced enum '{enumName}' not found in registry"
                            });
                        }
                    }
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

        // Group natives by namespace
        var namespaceDict = allNatives
            .GroupBy(n => n.Namespace, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Track enum usage (single-threaded, after parallel processing)
        foreach (var native in allNatives)
        {
            foreach (var enumName in native.UsedEnums)
            {
                if (enumRegistry.Contains(enumName))
                {
                    enumRegistry.TrackUsage(enumName, native.Hash);
                }
            }
        }

        // Track struct usage - check parameter types for struct references
        var structDict = structRegistry.GetAllStructs();
        foreach (var native in allNatives)
        {
            foreach (var param in native.Parameters)
            {
                // Check if parameter type matches a known struct
                var typeName = param.Type.Name;
                if (structDict.TryGetValue(typeName, out var structDef))
                {
                    // Avoid duplicates
                    if (!structDef.UsedByNatives.Any(u => u.Hash == native.Hash))
                    {
                        structDef.UsedByNatives.Add((native.Name, native.Hash));
                    }
                }
            }
        }

        // Convert to namespace list
        db.Namespaces = namespaceDict
            .Select(kvp => new NativeCodeGen.Core.Parsing.NativeNamespace { Name = kvp.Key, Natives = kvp.Value })
            .OrderBy(n => n.Name)
            .ToList();

        // Update enums with usage tracking
        db.Enums = enumRegistry.GetAllEnums();

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
