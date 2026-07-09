
using UnityEngine;
using Verse;

namespace ASQHPBar;

public class SimpleHealthBar : Mod
{
    private static SimpleHealthBarSettings? settings;

    public SimpleHealthBar(ModContentPack pack) : base(pack)
    {
        settings = GetSettings<SimpleHealthBarSettings>();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        base.DoSettingsWindowContents(inRect);
        settings!.DoSettingsWindowContents(inRect);
    }
    public override string SettingsCategory()
    {
        return "ASQHPBar_ModName".Translate();
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        settings!.Write();
    }
}
