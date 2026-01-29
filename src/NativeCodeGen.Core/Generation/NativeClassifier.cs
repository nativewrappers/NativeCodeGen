using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Parsing;

namespace NativeCodeGen.Core.Generation;

/// <summary>
/// Classifies natives into handle classes vs namespace utilities.
/// This logic is shared across all language generators.
/// </summary>
public class NativeClassifier
{
    public static readonly Dictionary<string, string?> HandleClassHierarchy = new()
    {
        ["Entity"] = null,
        ["Ped"] = "Entity",
        ["Vehicle"] = "Entity",
        ["Prop"] = "Entity",
        ["Pickup"] = null,
        ["Player"] = null,
        ["Cam"] = null,
        ["Blip"] = null,
        ["Interior"] = null,
        ["FireId"] = null,
        ["AnimScene"] = null,
        ["ItemSet"] = null,
        ["PersChar"] = null,
        ["PopZone"] = null,
        ["PropSet"] = null,
        ["Volume"] = null,
        ["PedGroup"] = null,
        ["BaseTask"] = null,
        ["PedTask"] = "BaseTask",
        ["VehicleTask"] = "BaseTask",
        ["BaseModel"] = null,
        ["PedModel"] = "BaseModel",
        ["VehicleModel"] = "BaseModel",
        ["WeaponModel"] = "BaseModel"
    };

    public static readonly Dictionary<string, string> TypeToNamespace = new()
    {
        ["Entity"] = "ENTITY",
        ["Ped"] = "PED",
        ["Vehicle"] = "VEHICLE",
        ["Object"] = "OBJECT",
        ["Pickup"] = "OBJECT",
        ["Player"] = "PLAYER",
        ["Cam"] = "CAM",
        ["Blip"] = "HUD",
        ["Interior"] = "INTERIOR"
    };

    public static readonly HashSet<string> TaskClasses = new() { "BaseTask", "PedTask", "VehicleTask" };
    public static readonly HashSet<string> ModelClasses = new() { "BaseModel", "PedModel", "VehicleModel", "WeaponModel" };
    public static readonly HashSet<string> EntitySubclasses = new() { "Ped", "Vehicle", "Object", "Prop" };

    public ClassifiedNatives Classify(NativeDatabase db)
    {
        var result = new ClassifiedNatives();

        foreach (var ns in db.Namespaces)
        {
            foreach (var native in ns.Natives)
            {
                var targetClass = DetermineTargetClass(native);
                if (targetClass != null)
                {
                    if (!result.HandleClasses.ContainsKey(targetClass))
                    {
                        result.HandleClasses[targetClass] = new List<NativeDefinition>();
                    }
                    result.HandleClasses[targetClass].Add(native);
                }
                else
                {
                    if (!result.NamespaceClasses.ContainsKey(ns.Name))
                    {
                        result.NamespaceClasses[ns.Name] = new List<NativeDefinition>();
                    }
                    result.NamespaceClasses[ns.Name].Add(native);
                }
            }
        }

        return result;
    }

    private string? DetermineTargetClass(NativeDefinition native)
    {
        if (native.Parameters.Count == 0)
            return null;

        var firstParam = native.Parameters[0];

        if (firstParam.IsThis && firstParam.Type.Category == TypeCategory.Handle)
        {
            var typeName = firstParam.Type.Name;
            return typeName == "Object" ? "Prop" : typeName;
        }

        if (firstParam.Type.Category == TypeCategory.Handle)
        {
            var handleType = firstParam.Type.Name;

            if (native.Namespace.Equals("TASK", StringComparison.OrdinalIgnoreCase))
            {
                return handleType switch
                {
                    "Ped" => "PedTask",
                    "Vehicle" => "VehicleTask",
                    "Entity" => "BaseTask",
                    _ => null
                };
            }

            if (TypeToNamespace.TryGetValue(handleType, out var expectedNs))
            {
                if (native.Namespace.Equals(expectedNs, StringComparison.OrdinalIgnoreCase))
                {
                    return handleType == "Object" ? "Prop" : handleType;
                }
            }

            if (handleType == "Entity" && native.Namespace == "ENTITY")
            {
                return "Entity";
            }
        }

        if (native.Namespace.Equals("STREAMING", StringComparison.OrdinalIgnoreCase) &&
            firstParam.Type.Category == TypeCategory.Hash)
        {
            var upperName = native.Name.ToUpperInvariant();
            if (upperName.Contains("PED") || upperName.Contains("HUMAN"))
                return "PedModel";
            if (upperName.Contains("VEHICLE"))
                return "VehicleModel";
            if (upperName.Contains("WEAPON"))
                return "WeaponModel";
            return "BaseModel";
        }

        return null;
    }

    public static bool IsTaskClass(string className) => TaskClasses.Contains(className);
    public static bool IsModelClass(string className) => ModelClasses.Contains(className);

    public static string GetTaskEntityType(string className) => className switch
    {
        "PedTask" => "Ped",
        "VehicleTask" => "Vehicle",
        _ => "Entity"
    };

    public static string NormalizeHandleType(string typeName) => typeName == "Object" ? "Prop" : typeName;
}

public class ClassifiedNatives
{
    public Dictionary<string, List<NativeDefinition>> HandleClasses { get; } = new();
    public Dictionary<string, List<NativeDefinition>> NamespaceClasses { get; } = new();
}
