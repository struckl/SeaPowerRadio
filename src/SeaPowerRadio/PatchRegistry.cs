using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace SeaPowerRadio;

/// <summary>
/// One mod feature and the game methods it hooks. Each patch class registers its hooks here; the registry
/// resolves them all first and only patches if every target exists.
/// </summary>
internal sealed class Feature
{
    internal sealed class Target
    {
        public string Type;
        public string Method;
        public Type[] Args;          // null = unique by name
        public MethodInfo Prefix;
        public MethodInfo Postfix;
    }

    public readonly string Name;
    public readonly List<Target> Targets = new List<Target>();
    public bool Active { get; internal set; }

    public Feature(string name) => Name = name;

    /// <summary>Declare a hook. <paramref name="type"/> is the game type's full name, e.g. "SeaPower.VoiceMessage".</summary>
    public Feature Hook(string type, string method, Type[] args, Type patchClass,
        string prefix = null, string postfix = null)
    {
        Targets.Add(new Target
        {
            Type = type,
            Method = method,
            Args = args,
            Prefix = prefix == null ? null : AccessTools.Method(patchClass, prefix),
            Postfix = postfix == null ? null : AccessTools.Method(patchClass, postfix),
        });
        return this;
    }
}

/// <summary>
/// Every game method we hook is declared through here, grouped by feature. Game updates rename or remove methods;
/// when a target is missing the whole feature is switched off and logged — the rest of the mod keeps working and
/// the game never sees an exception from us.
/// </summary>
internal static class PatchRegistry
{
    private static readonly List<Feature> Features = new List<Feature>();

    public static bool IsActive(string feature) => Features.Any(f => f.Name == feature && f.Active);

    public static string Summary() =>
        string.Join(", ", Features.Select(f => f.Name + (f.Active ? "" : " (OFF)")));

    public static void ApplyAll(Harmony harmony)
    {
        Features.Add(Patches.StockVoicePatches.Declare());
        Features.Add(Patches.MissionPatches.Declare());
        Features.Add(Patches.WhyNotPatches.Declare());
        Features.Add(Patches.AlertPatches.Declare());
        Features.Add(Patches.ContactAlertPatches.Declare());
        Features.Add(Patches.RtbPatches.Declare());
        Features.Add(Patches.SarPatches.Declare());

        foreach (Feature feature in Features)
            Apply(harmony, feature);
    }

    private static void Apply(Harmony harmony, Feature feature)
    {
        var resolved = new List<(Feature.Target spec, MethodBase method)>();
        foreach (Feature.Target spec in feature.Targets)
        {
            Type type = AccessTools.TypeByName(spec.Type);
            MethodBase method = type == null ? null
                : spec.Method == ".ctor" ? AccessTools.Constructor(type, spec.Args ?? Type.EmptyTypes)
                : spec.Args == null ? AccessTools.Method(type, spec.Method)
                : AccessTools.Method(type, spec.Method, spec.Args);
            if (method == null)
            {
                ModLog.Warn($"Feature '{feature.Name}' disabled: {spec.Type}.{spec.Method} not found (game update?).");
                return;
            }
            if (spec.Prefix == null && spec.Postfix == null)
            {
                ModLog.Error($"Feature '{feature.Name}': no patch method bound for {spec.Type}.{spec.Method}.");
                return;
            }
            resolved.Add((spec, method));
        }

        var applied = new List<(Feature.Target spec, MethodBase method)>();
        try
        {
            foreach (var (spec, method) in resolved)
            {
                harmony.Patch(method,
                    prefix: spec.Prefix == null ? null : new HarmonyMethod(spec.Prefix),
                    postfix: spec.Postfix == null ? null : new HarmonyMethod(spec.Postfix));
                applied.Add((spec, method));
            }
            feature.Active = true;
        }
        catch (Exception ex)
        {
            ModLog.Error($"Feature '{feature.Name}' disabled, patching failed: {ex.Message}");
            foreach (var (spec, method) in applied)
            {
                if (spec.Prefix != null) harmony.Unpatch(method, spec.Prefix);
                if (spec.Postfix != null) harmony.Unpatch(method, spec.Postfix);
            }
        }
    }
}
