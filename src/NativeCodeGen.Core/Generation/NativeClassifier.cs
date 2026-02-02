using System.Collections.Frozen;
using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Parsing;

namespace NativeCodeGen.Core.Generation;

/// <summary>
/// Classifies natives into handle classes vs namespace utilities.
/// This logic is shared across all language generators.
/// </summary>
public class NativeClassifier
{
    public static readonly FrozenDictionary<string, string?> HandleClassHierarchy =
        new Dictionary<string, string?>
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
            ["WeaponModel"] = "BaseModel",
            ["Weapon"] = null
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static readonly FrozenDictionary<string, string> TypeToNamespace =
        new Dictionary<string, string>
        {
            ["Entity"] = "ENTITY",
            ["Ped"] = "PED",
            ["Vehicle"] = "VEHICLE",
            ["Object"] = "OBJECT",
            ["Pickup"] = "OBJECT",
            ["Player"] = "PLAYER",
            ["Cam"] = "CAM",
            ["Blip"] = "HUD",
            ["Interior"] = "INTERIOR",
            ["AnimScene"] = "ANIMSCENE",
            ["ItemSet"] = "ITEMSET",
            ["PersChar"] = "PERSCHAR",
            ["PropSet"] = "PROPSET",
            ["Volume"] = "VOLUME"
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static readonly FrozenSet<string> TaskClasses =
        new HashSet<string> { "BaseTask", "PedTask", "VehicleTask" }.ToFrozenSet(StringComparer.Ordinal);

    public static readonly FrozenSet<string> ModelClasses =
        new HashSet<string> { "BaseModel", "PedModel", "VehicleModel", "WeaponModel" }.ToFrozenSet(StringComparer.Ordinal);

    public static readonly FrozenSet<string> EntitySubclasses =
        new HashSet<string> { "Ped", "Vehicle", "Object", "Prop" }.ToFrozenSet(StringComparer.Ordinal);

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
                    if (!result.HandleClasses.TryGetValue(targetClass, out var list))
                    {
                        list = new List<NativeDefinition>();
                        result.HandleClasses[targetClass] = list;
                    }
                    list.Add(native);
                }
                else
                {
                    if (!result.NamespaceClasses.TryGetValue(ns.Name, out var list))
                    {
                        list = new List<NativeDefinition>();
                        result.NamespaceClasses[ns.Name] = list;
                    }
                    list.Add(native);
                }
            }
        }

        return result;
    }

    private static string? DetermineTargetClass(NativeDefinition native)
    {
        if (native.Parameters.Count == 0)
            return null;

        var firstParam = native.Parameters[0];

        if (firstParam.IsThis && firstParam.Type.Category == TypeCategory.Handle)
        {
            return TypeInfo.NormalizeHandleName(firstParam.Type.Name);
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

            if (native.Namespace.Equals("WEAPON", StringComparison.OrdinalIgnoreCase) && handleType == "Ped")
            {
                return "Weapon";
            }

            if (TypeToNamespace.TryGetValue(handleType, out var expectedNs))
            {
                if (native.Namespace.Equals(expectedNs, StringComparison.OrdinalIgnoreCase))
                {
                    return TypeInfo.NormalizeHandleName(handleType);
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
    public static bool IsWeaponClass(string className) => className == "Weapon";

    public static string GetTaskEntityType(string className) => className switch
    {
        "PedTask" => "Ped",
        "VehicleTask" => "Vehicle",
        _ => "Entity"
    };

    public static string NormalizeHandleType(string typeName) => TypeInfo.NormalizeHandleName(typeName);
}

public class ClassifiedNatives
{
    public Dictionary<string, List<NativeDefinition>> HandleClasses { get; } = new();
    public Dictionary<string, List<NativeDefinition>> NamespaceClasses { get; } = new();
}
