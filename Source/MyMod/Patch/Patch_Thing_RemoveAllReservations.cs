using HarmonyLib;
using Verse;

namespace ASQHPBar;

/// <summary>
/// 在 Thing.RemoveAllReservationsAndDesignationsOnThis 执行后清理 easeDataTable 中的条目，
/// 防止普通字典强引用导致 ThingWithComps 无法被 GC。
/// </summary>
[HarmonyPatch(typeof(Thing), "RemoveAllReservationsAndDesignationsOnThis")]
public static class Patch_Thing_RemoveAllReservations
{
    public static void Postfix(Thing __instance)
    {
        if (__instance is ThingWithComps twc)
            HealthBarEaseHelper.RemoveEaseData(twc);
    }
}
