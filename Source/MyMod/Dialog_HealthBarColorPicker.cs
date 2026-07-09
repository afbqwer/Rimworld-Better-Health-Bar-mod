using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ASQHPBar;

public class Dialog_HealthBarColorPicker : Dialog_ColorPickerBase
{
    private readonly Action<Color> onSave;

    private static readonly List<Color> pickableColors = new List<Color>
    {
        new (0.4f, 0.7f, 1f, 0.5f),
        new (0.7f, 0f, 0f, 0.5f),
        new (1f, 1f, 0f, 0.5f),
        new (0f, 1f, 0f, 0.5f),
        Color.white,
        Color.red,
        Color.green,
        Color.blue,
        Color.yellow,
        Color.cyan,
        Color.magenta,
        new (0.7f, 0f, 0f),
        new (0f, 0.5f, 0f),
        new (0f, 0f, 0.7f),
        new (0.5f, 0.5f, 0.5f),
        new (0.3f, 0.3f, 0.3f),
        new (1f, 0.5f, 0f),
        new (0.5f, 0f, 0.5f),
    };

    protected override Color DefaultColor => oldColor;
    protected override bool ShowDarklight => false;
    protected override List<Color> PickableColors => pickableColors;
    protected override float ForcedColorValue => -1f;
    protected override bool ShowColorTemperatureBar => false;
    public override Vector2 InitialSize => new Vector2(600f, 520f);

    public Dialog_HealthBarColorPicker(Color initialColor, Action<Color> onSave)
        : base(
            Widgets.ColorComponents.All,
            Widgets.ColorComponents.Red | Widgets.ColorComponents.Green | Widgets.ColorComponents.Blue)
    {
        color = initialColor;
        oldColor = initialColor;
        this.onSave = onSave;
    }

    protected override void SaveColor(Color color)
    {
        // 保持原始 alpha 不变（颜色选择器不支持编辑 alpha）
        Color savedColor = new Color(color.r, color.g, color.b, oldColor.a);
        onSave(savedColor);
    }
}
