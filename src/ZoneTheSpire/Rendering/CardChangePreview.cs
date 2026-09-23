using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using ZoneTheSpire.Patches;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Before/after display for campfire actions that change cards without a choice (random picks: Haunt, Magnetize, Repent). Each
/// changed card is shown as its before copy, an arrow and its after copy; a removed card is shown greyed with "Removed" instead.
/// It stays up until the player clicks, or for a few seconds. Local UI only (the chooser's DoLocalPostSelectVfx); never
/// affects gameplay. Take the before copies with <see cref="Snapshot"/> in OnSelect, before changing the cards.
/// </summary>
internal static class CardChangePreview
{
    private const string NodeName = "ZoneTheSpireCardChangePreview";
    private const float CardWidth = 300f;
    private const float CardHeight = 422f;
    private const float ArrowWidth = 90f;
    private const float PairGap = 70f;
    private const double TimeoutSeconds = 8.0;

    /// <summary>One changed card: its copy before the change, and after it (null when the card was removed).</summary>
    internal readonly record struct Change(CardModel Before, CardModel? After);

    /// <summary>A detached display copy of the card as it is right now.</summary>
    public static CardModel Snapshot(CardModel card) => (CardModel)card.ClonePreservingMutability();

    /// <summary>Shows the changes and completes when the player clicks, the timeout passes, or <paramref name="ct"/> is cancelled.</summary>
    public static async Task Show(string title, IReadOnlyList<Change> changes, CancellationToken ct = default)
    {
        if (changes.Count == 0 || NRun.Instance?.GlobalUi is not Control host || !GodotObject.IsInstanceValid(host))
        {
            return;
        }

        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Control? root = null;
        try
        {
            host.GetNodeOrNull(NodeName)?.QueueFree();
            root = Build(title, changes, () => done.TrySetResult(true));
            host.AddChild(root);
            root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            root.Modulate = new Color(1f, 1f, 1f, 0f);
            root.CreateTween().TweenProperty(root, "modulate:a", 1f, 0.2);
            host.GetTree().CreateTimer(TimeoutSeconds).Timeout += () => done.TrySetResult(true);
            using (ct.Register(() => done.TrySetResult(true)))
            {
                await done.Task;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to show the card change preview: {ex}");
        }
        finally
        {
            if (root != null && GodotObject.IsInstanceValid(root))
            {
                Control fading = root;
                Tween fade = fading.CreateTween();
                fade.TweenProperty(fading, "modulate:a", 0f, 0.2);
                fade.TweenCallback(Callable.From(fading.QueueFree));
            }
        }
    }

    private static Control Build(string title, IReadOnlyList<Change> changes, Action dismiss)
    {
        var root = new Control { Name = NodeName, MouseFilter = Control.MouseFilterEnum.Stop };
        root.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true })
            {
                dismiss();
            }
        };

        var backdrop = new ColorRect { Color = new Color(0f, 0f, 0f, 0.62f), MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 24);
        center.AddChild(column);
        column.AddChild(Text(title, 40, Colors.White));

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", (int)PairGap);
        foreach (Change change in changes)
        {
            row.AddChild(Pair(change));
        }

        // Scale the row of pairs down to fit the screen width.
        float width = changes.Count * (CardWidth * 2f + ArrowWidth) + (changes.Count - 1) * PairGap;
        float scale = Math.Min(1f, 1650f / Math.Max(1f, width));
        var rowHolder = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(width * scale, CardHeight * scale),
        };
        rowHolder.AddChild(row);
        row.Scale = Vector2.One * scale;
        column.AddChild(rowHolder);

        column.AddChild(Text(ZoneTheSpire.Core.Localization.ModText.Get("ui.card_preview.continue"), 22, new Color(1f, 1f, 1f, 0.7f)));
        return root;
    }

    private static Control Pair(Change change)
    {
        var pair = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        pair.AddThemeConstantOverride("separation", 0);
        pair.AddChild(Slot(change.Before, dimmed: change.After == null));
        var arrow = Text(change.After == null ? "✕" : "→", 64, change.After == null ? new Color("E05A4F") : Colors.White);
        arrow.CustomMinimumSize = new Vector2(ArrowWidth, CardHeight);
        pair.AddChild(arrow);
        if (change.After != null)
        {
            pair.AddChild(Slot(change.After, dimmed: false));
        }
        else
        {
            Label removed = Text(ZoneTheSpire.Core.Localization.ModText.Get("ui.card_preview.removed"), 34, new Color("E05A4F"));
            removed.CustomMinimumSize = new Vector2(CardWidth, CardHeight);
            pair.AddChild(removed);
        }

        return pair;
    }

    /// <summary>A card-sized slot with the card centred in it (card nodes are drawn around their origin).</summary>
    private static Control Slot(CardModel card, bool dimmed)
    {
        var slot = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(CardWidth, CardHeight) };
        if (CardModificationPreview.AddCard(slot, card, PileType.Deck, upgradePreview: false) is { } holder)
        {
            holder.Position = new Vector2(CardWidth, CardHeight) / 2f;
            if (dimmed)
            {
                holder.Modulate = new Color(0.55f, 0.55f, 0.55f, 0.85f);
            }
        }

        return slot;
    }

    private static Label Text(string text, int size, Color color)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 8);
        return label;
    }
}
