using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ASQHPBar;

[StaticConstructorOnStartup]
public static class HarmonyPatching
{
    static HarmonyPatching()
    {
        Harmony harmony = new Harmony("assssssqwww.ASQHPBar");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        AmmoReadoutCompat.TryInit();
    }
}
