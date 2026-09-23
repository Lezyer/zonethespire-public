using System;
using System.Globalization;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Fermentory;
using ZoneTheSpire.Run.Fermentory;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// The Fermentory: an enemy's brewed potion floats above its intent, bobbing gently, with an intent-style icon and number beside
/// it (the vanilla bomb intent (Gas Bomb's explosion) and damage for Fire, the defend intent for Block, the heal intent for Blood, the power's own
/// icon for the rest). Fire's number is the damage you would take, like a vanilla attack intent, and updates as it changes.
/// Hovering shows what the enemy will do, just above the potion. Local rendering only.
/// </summary>
internal static class EnemyBrewVisuals
{
    private const string NodeName = "ZoneTheSpireBrew";
    private const float PotionSize = 56f;
    private const float EffectSize = 44f;
    private const float Gap = 4f;

    private static readonly AccessTools.FieldRef<NHoverTipSet, Control>? TextContainer =
        AccessTools.Field(typeof(NHoverTipSet), "_textHoverTipContainer") != null
            ? SafeRef.Field<NHoverTipSet, Control>("_textHoverTipContainer")
            : null;

    private static Font? _font;

    public static void Show(Creature enemy, Brew brew, bool multiplayer)
    {
        try
        {
            Clear(enemy);
            if (CreatureNodeLookup.Find(enemy) is not NCreature node || !node.IsInsideTree())
            {
                return;
            }

            PotionModel potion = EnemyBrewing.VanillaPotion(brew.Potion);
            var root = new Control
            {
                Name = NodeName,
                Size = new Vector2(PotionSize + Gap + EffectSize, PotionSize),
                MouseFilter = Control.MouseFilterEnum.Stop,
                ZIndex = 5,
            };
            root.AddChild(new TextureRect
            {
                Texture = potion.Image,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Size = new Vector2(PotionSize, PotionSize),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });

            var effect = new TextureRect
            {
                Texture = EffectIcon(brew),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Size = new Vector2(EffectSize, EffectSize),
                Position = new Vector2(PotionSize + Gap, (PotionSize - EffectSize) / 2f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            root.AddChild(effect);

            var value = new Label
            {
                Text = ValueText(enemy, brew),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Right,
                Size = new Vector2(EffectSize + 14f, 30f),
                Position = new Vector2(PotionSize + Gap - 4f, PotionSize - 26f),
            };
            if (Font() is { } font)
            {
                value.AddThemeFontOverride("font", font);
            }

            value.AddThemeFontSizeOverride("font_size", 26);
            value.AddThemeColorOverride("font_color", new Color(1f, 0.97f, 0.9f));
            value.AddThemeColorOverride("font_outline_color", new Color(0.1f, 0.06f, 0.04f));
            value.AddThemeConstantOverride("outline_size", 10);
            root.AddChild(value);

            // Fire's number follows the damage you'd take (Vulnerable, Intangible, ...), like a vanilla attack intent.
            if (brew.Potion == EnemyPotion.Fire)
            {
                var timer = new Timer { WaitTime = 0.25, Autostart = true };
                timer.Timeout += () =>
                {
                    if (GodotObject.IsInstanceValid(value))
                    {
                        value.Text = ValueText(enemy, brew);
                    }
                };
                root.AddChild(timer);
            }

            float scale = Math.Max(0.0001f, node.GetGlobalTransform().Scale.Y);
            Control intents = node.IntentContainer;
            Vector2 intentTop = (intents.GlobalPosition - node.GlobalPosition) / scale;
            float centreX = intentTop.X + intents.Size.X / 2f;
            root.Position = new Vector2(centreX - root.Size.X / 2f, intentTop.Y - PotionSize - 10f);

            var tip = new HoverTip(potion.Title, FermentoryText.EnemyPotionLine(brew.Potion, brew.Amount, multiplayer));
            root.MouseEntered += () => ShowTip(root, tip);
            root.MouseExited += () => NHoverTipSet.Remove(root);
            node.AddChild(root);

            Tween bob = root.CreateTween().SetLoops();
            bob.TweenProperty(root, "position:y", root.Position.Y - 6f, 1.1).SetTrans(Tween.TransitionType.Sine);
            bob.TweenProperty(root, "position:y", root.Position.Y, 1.1).SetTrans(Tween.TransitionType.Sine);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to show an enemy brew: {ex}");
        }
    }

    public static void Clear(Creature enemy)
    {
        try
        {
            if (CreatureNodeLookup.Find(enemy)?.GetNodeOrNull<Control>(NodeName) is { } root)
            {
                NHoverTipSet.Remove(root);
                root.Name = NodeName + "Old";
                root.QueueFree();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to clear an enemy brew: {ex}");
        }
    }

    /// <summary>The number beside the effect icon: Fire's damage to you, Blood's heal in HP, otherwise the amount.</summary>
    private static string ValueText(Creature enemy, Brew brew)
    {
        int value = brew.Potion switch
        {
            EnemyPotion.Fire => LocalPlayer(enemy) is { } me ? EnemyBrewing.FireDamageTo(enemy, me.Creature, brew) : brew.Amount,
            EnemyPotion.Blood => FermentoryRules.BloodHeal(enemy.MaxHp),
            _ => brew.Amount,
        };
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static Player? LocalPlayer(Creature enemy) =>
        enemy.CombatState is { } combat ? LocalContext.GetMe(combat) : null;

    private static Texture2D? EffectIcon(Brew brew) => brew.Potion switch
    {
        EnemyPotion.Fire => Intent("intent_death_blow"),
        EnemyPotion.Block => Intent("intent_defend"),
        EnemyPotion.Blood => Intent("intent_heal"),
        EnemyPotion.Strength => ModelDb.Power<StrengthPower>().Icon,
        EnemyPotion.Regen => ModelDb.Power<RegenPower>().Icon,
        EnemyPotion.LiquidBronze => ModelDb.Power<ThornsPower>().Icon,
        EnemyPotion.Weak => ModelDb.Power<WeakPower>().Icon,
        EnemyPotion.Vulnerable => ModelDb.Power<VulnerablePower>().Icon,
        _ => ModelDb.Power<PoisonPower>().Icon,
    };

    private static Texture2D? Intent(string name) =>
        PreloadManager.Cache.GetTexture2D(ImageHelper.GetImagePath("atlases/intent_atlas.sprites/" + name + ".tres"));

    private static Font? Font() => _font ??= ResourceLoader.Exists("res://fonts/kreon_bold.ttf") ? GD.Load<Font>("res://fonts/kreon_bold.ttf") : null;

    /// <summary>Shows the tip centred just above the potion, kept on screen (placed once the tip has its size).</summary>
    private static void ShowTip(Control root, IHoverTip tip)
    {
        try
        {
            NHoverTipSet.Remove(root);
            NHoverTipSet? set = NHoverTipSet.CreateAndShow(root, tip);
            if (set == null || TextContainer == null)
            {
                return;
            }

            void Place()
            {
                if (!GodotObject.IsInstanceValid(set) || !GodotObject.IsInstanceValid(root))
                {
                    return;
                }

                Control box = TextContainer(set);
                Rect2 screen = root.GetViewportRect();
                Vector2 size = box.Size;
                Vector2 anchor = root.GlobalPosition + new Vector2(root.Size.X * root.GetGlobalTransform().Scale.X / 2f, 0f);
                float x = Math.Clamp(anchor.X - size.X / 2f, 8f, Math.Max(8f, screen.Size.X - size.X - 8f));
                float y = Math.Max(8f, anchor.Y - size.Y - 12f);
                box.GlobalPosition = new Vector2(x, y);
            }

            Place();
            Callable.From(Place).CallDeferred();
            root.GetTree().CreateTimer(0.05).Timeout += Place;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to show an enemy brew tip: {ex}");
        }
    }
}
