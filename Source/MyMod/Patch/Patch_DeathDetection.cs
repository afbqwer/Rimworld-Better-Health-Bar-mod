using HarmonyLib;
using Verse;

namespace ASQHPBar;

/// <summary>
/// Pawn 死亡时登记死亡残留血条动画。
/// Prefix 阶段 Pawn 仍 Spawned（Pawn.Kill 内部随后才 DeSpawn），DrawPos 有效。
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
public static class Patch_Pawn_Kill
{
    public static void Prefix(Pawn __instance)
    {
        HealthBarEaseHelper.RegisterPawnDeath(__instance);
    }
}

/// <summary>
/// 炮台等 ThingWithComps 死亡时登记（Building_Turret 无 Kill 覆写，走此方法）。
/// Pawn 已由 Patch_Pawn_Kill 处理，RegisterThingDeath 内部会过滤。
/// </summary>
[HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.Kill))]
public static class Patch_ThingWithComps_Kill
{
    public static void Prefix(ThingWithComps __instance)
    {
        HealthBarEaseHelper.RegisterThingDeath(__instance);
    }
}