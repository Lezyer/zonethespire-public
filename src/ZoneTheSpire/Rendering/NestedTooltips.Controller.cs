using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ZoneTheSpire.Core.Tooltips;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Controller side of nested tooltips. A nested tip shows the Peek button's icon (left stick press by default, unused on the
/// map; the icon follows the controller type and rebinding like vanilla's). Holding it fills a ring around the icon; when full
/// the tip locks and takes focus, with a gold border: the movement buttons cycle its keywords, the selected keyword's tip opens
/// beside it and can be focused the same way, and Cancel (B) steps back one tip, closing the last one and returning focus to
/// where it was. The actions are read by polling the input state each frame; while a tip is focused, a focus catcher control
/// holds focus so nothing underneath navigates, and the game's hotkeys are blocked.
/// </summary>
internal static partial class NestedTooltips
{
    private const float IconSize = 26f;
    private const float HoldRingSize = 52f;
    private const float RepeatDelay = 0.4f;
    private const float RepeatInterval = 0.12f;

    private static readonly Action SwallowPeek = () => { };

    /// <summary>The game's own actions (what NInputManager turns controller buttons into).</summary>
    private static readonly HashSet<StringName> GameActions = MegaInput.AllInputs.Select(name => new StringName(name)).ToHashSet();
    private static bool _peekSwallowed;
    private static bool _holdNeedsRelease;
    private static Control? _focusCatcher;
    private static Control? _previousFocus;
    private static StyleBoxFlat? _borderStyle;
    private static int _heldDirection;
    private static float _repeatTimer;

    private static bool ControllerActive => NControllerManager.Instance?.InputType == InputType.Controller;

    private static Level? FocusedLevel => Chain.LastOrDefault(level => level.Focused);

    private static string? SelectedKeyword(Level level) =>
        level.Selected >= 0 && level.Selected < level.Keywords.Count ? level.Keywords[level.Selected] : null;

    private static void TickController(float delta)
    {
        SwallowPeekWhileShown(true);
        bool held = Input.IsActionPressed(MegaInput.peek);
        if (!held)
        {
            _holdNeedsRelease = false;
        }

        if (FocusedLevel is { } current)
        {
            PollFocusedInput(current, delta);
        }

        // Only the focused tip's selected keyword may have a tip open beyond it.
        Level? focused = FocusedLevel;
        int focusedIndex = focused == null ? -1 : Chain.IndexOf(focused);
        if (focused != null)
        {
            string? selected = SelectedKeyword(focused);
            if (Chain.Count > focusedIndex + 1 && Chain[focusedIndex + 1].Keyword != selected)
            {
                CloseFrom(focusedIndex + 1);
            }

            if (Chain.Count == focusedIndex + 1 && selected != null)
            {
                OpenChild(focused, selected);
            }
        }

        // The tip a hold would focus: the first one, or the selected keyword's.
        if (focusedIndex + 1 >= Chain.Count)
        {
            return;
        }

        Level target = Chain[focusedIndex + 1];
        if (!held || _holdNeedsRelease)
        {
            if (!target.Locked)
            {
                SetProgress(target, 0f);
            }

            return;
        }

        if (target.Locked || Advance(target, delta))
        {
            Focus(target);
        }
    }

    /// <summary>Locks focus onto a tip: border, first keyword selected, controller input caught.</summary>
    private static void Focus(Level level)
    {
        _holdNeedsRelease = true;
        if (!CatchFocus())
        {
            return;
        }

        foreach (Level other in Chain.Where(other => other.Focused))
        {
            other.Focused = false;
            Render(other);
        }

        level.Focused = true;
        level.Selected = level.Keywords.Count > 0 ? Math.Max(0, level.Selected) : -1;
        Render(level);
    }

    private static bool CatchFocus()
    {
        if (_focusCatcher != null && GodotObject.IsInstanceValid(_focusCatcher))
        {
            return true;
        }

        if (NGame.Instance?.HoverTipsContainer is not { } host)
        {
            return false;
        }

        _previousFocus = NGame.Instance.GetViewport()?.GuiGetFocusOwner();
        var catcher = new Control
        {
            Name = "ZoneTheSpireNestedFocus",
            FocusMode = Control.FocusModeEnum.All,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = new Vector2(1f, 1f),
        };
        host.AddChild(catcher);
        NodePath self = new(".");
        catcher.FocusNeighborTop = self;
        catcher.FocusNeighborBottom = self;
        catcher.FocusNeighborLeft = self;
        catcher.FocusNeighborRight = self;
        catcher.FocusNext = self;
        catcher.FocusPrevious = self;
        catcher.GuiInput += inputEvent => OnFocusedInput(catcher, inputEvent);
        catcher.FocusExited += () =>
        {
            // Something else took focus (a screen change): let the chain go.
            if (_focusCatcher == catcher)
            {
                ReleaseCatcher();
                CloseFrom(0);
            }
        };
        _focusCatcher = catcher;
        catcher.GrabFocus();
        NHotkeyManager.Instance?.AddBlockingScreen(catcher);

        // A direction already held when focus arrives shouldn't move the selection straight away.
        _heldDirection = Input.IsActionPressed(MegaInput.up) || Input.IsActionPressed(MegaInput.left) ? -1
            : Input.IsActionPressed(MegaInput.down) || Input.IsActionPressed(MegaInput.right) ? 1
            : 0;
        _repeatTimer = RepeatDelay;
        return true;
    }

    /// <summary>
    /// Stops the game's own actions (Cancel, the directions, ...) at the catcher so nothing underneath reacts; the actions
    /// themselves are read in PollFocusedInput. Raw controller button events must pass: NInputManager._UnhandledInput is what
    /// turns them into those actions, so accepting them here would leave every action unpressed.
    /// </summary>
    private static void OnFocusedInput(Control catcher, InputEvent inputEvent)
    {
        if (_focusCatcher == catcher && inputEvent is InputEventAction action && GameActions.Contains(action.Action))
        {
            catcher.AcceptEvent();
        }
    }

    /// <summary>Cancel steps back; a direction steps the selection once, then repeats while held.</summary>
    private static void PollFocusedInput(Level level, float delta)
    {
        if (Input.IsActionJustPressed(MegaInput.cancel))
        {
            Back(level);
            return;
        }

        int direction = Input.IsActionPressed(MegaInput.up) || Input.IsActionPressed(MegaInput.left) ? -1
            : Input.IsActionPressed(MegaInput.down) || Input.IsActionPressed(MegaInput.right) ? 1
            : 0;
        if (direction == 0)
        {
            _heldDirection = 0;
            return;
        }

        if (direction != _heldDirection)
        {
            _heldDirection = direction;
            _repeatTimer = RepeatDelay;
            Select(level, direction);
            return;
        }

        _repeatTimer -= delta;
        if (_repeatTimer <= 0f)
        {
            _repeatTimer = RepeatInterval;
            Select(level, direction);
        }
    }

    private static void Select(Level level, int step)
    {
        if (level.Keywords.Count == 0)
        {
            return;
        }

        level.Selected = NestedTooltipText.Cycle(level.Selected, step, level.Keywords.Count);
        Render(level);
    }

    /// <summary>Cancel: focus goes back to the parent tip, or out of the tips altogether from the first one.</summary>
    private static void Back(Level level)
    {
        int index = Chain.IndexOf(level);
        if (index > 0)
        {
            // Focus moves first, so closing never finds the chain without a focused tip. The parent's selection stays on the
            // closed keyword, whose tip reopens as an unfocused preview.
            Level parent = Chain[index - 1];
            level.Focused = false;
            parent.Focused = true;
            CloseFrom(index);
            Render(parent);
            return;
        }

        Control? previous = _previousFocus;
        ReleaseCatcher();
        CloseFrom(0);
        if (previous != null && GodotObject.IsInstanceValid(previous) && previous.IsInsideTree() && previous.IsVisibleInTree())
        {
            previous.GrabFocus();
        }
    }

    private static void ReleaseCatcher()
    {
        Control? catcher = _focusCatcher;
        _focusCatcher = null;
        _previousFocus = null;
        foreach (Level level in Chain)
        {
            level.Focused = false;
        }

        if (catcher != null && GodotObject.IsInstanceValid(catcher))
        {
            NHotkeyManager.Instance?.RemoveBlockingScreen(catcher);
            catcher.ReleaseFocus();
            catcher.QueueFree();
        }
    }

    /// <summary>Called whenever levels close: with no tips left (or none focused), controller mode lets go of its input.</summary>
    private static void AfterChainChanged()
    {
        if (_focusCatcher != null && FocusedLevel == null)
        {
            ReleaseCatcher();
        }

        if (Chain.Count == 0)
        {
            SwallowPeekWhileShown(false);
        }
    }

    /// <summary>Back to the mouse: drop controller focus (the mouse rules then keep or close the tips).</summary>
    private static void LeaveControllerMode()
    {
        if (_focusCatcher != null)
        {
            ReleaseCatcher();
            foreach (Level level in Chain)
            {
                Render(level);
            }
        }

        SwallowPeekWhileShown(false);
    }

    /// <summary>While a nested tip is shown, Peek presses are ours (the hotkey manager runs the last binding pushed).</summary>
    private static void SwallowPeekWhileShown(bool swallow)
    {
        if (swallow == _peekSwallowed || NHotkeyManager.Instance is not { } hotkeys)
        {
            return;
        }

        _peekSwallowed = swallow;
        if (swallow)
        {
            hotkeys.PushHotkeyPressedBinding(MegaInput.peek, SwallowPeek);
            hotkeys.PushHotkeyReleasedBinding(MegaInput.peek, SwallowPeek);
        }
        else
        {
            hotkeys.RemoveHotkeyPressedBinding(MegaInput.peek, SwallowPeek);
            hotkeys.RemoveHotkeyReleasedBinding(MegaInput.peek, SwallowPeek);
        }
    }

    /// <summary>Shows the focused tip's selected keyword highlighted (in the first text that links it), others plain.</summary>
    private static void Render(Level level)
    {
        string? keyword = level.Focused ? SelectedKeyword(level) : null;
        bool highlighted = false;
        foreach ((RichTextLabel label, string text) in level.LinkedTexts.Where(pair => GodotObject.IsInstanceValid(pair.Key)))
        {
            string shown = !highlighted && keyword != null && NestedTooltipText.LinkedKeywords(text).Contains(keyword)
                ? NestedTooltipText.Highlight(text, keyword)
                : text;
            highlighted |= shown != text;
            if (label.Text != shown)
            {
                label.Text = shown;
            }
        }
    }

    private static void CreateControllerVisuals(Level level, Control set)
    {
        level.Icon = new TextureRect
        {
            Name = "ZoneTheSpireNestedButton",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Size = new Vector2(IconSize, IconSize),
            Visible = false,
        };
        set.AddChild(level.Icon);

        _borderStyle ??= new StyleBoxFlat
        {
            DrawCenter = false,
            BorderColor = new Color(1f, 0.84f, 0.36f),
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ShadowColor = new Color(1f, 0.84f, 0.36f, 0.35f),
            ShadowSize = 8,
        };
        level.Border = new Panel { Name = "ZoneTheSpireNestedBorder", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        level.Border.AddThemeStyleboxOverride("panel", _borderStyle);
        set.AddChild(level.Border);
    }

    /// <summary>
    /// Controller look of a level: the tip a hold would focus shows the Peek icon inside a filling ring; the focused tip shows
    /// the Cancel icon and the gold border; other tips show nothing extra.
    /// </summary>
    private static void PlaceControllerVisuals(Level level, ColorRect ring, Rect2 tipRect)
    {
        Level? focused = FocusedLevel;
        int index = Chain.IndexOf(level);
        bool isTarget = index == (focused == null ? 0 : Chain.IndexOf(focused) + 1);
        Vector2 iconPosition = new(tipRect.End.X - IconSize - RingInset - 8f, tipRect.Position.Y + RingInset + 8f);

        ring.Visible = isTarget;
        ring.Size = new Vector2(HoldRingSize, HoldRingSize);
        ring.GlobalPosition = iconPosition + new Vector2(IconSize, IconSize) / 2f - ring.Size / 2f;
        (ring.Material as ShaderMaterial)?.SetShaderParameter("backdrop", 0.6f);

        if (level.Icon is { } icon && GodotObject.IsInstanceValid(icon))
        {
            string? action = isTarget ? MegaInput.peek : level.Focused ? MegaInput.cancel : null;
            icon.Visible = action != null;
            if (action != null)
            {
                Texture2D? texture = NInputManager.Instance?.GetHotkeyIcon(action);
                if (icon.Texture != texture)
                {
                    icon.Texture = texture;
                }

                icon.Size = new Vector2(IconSize, IconSize);
                icon.GlobalPosition = iconPosition;
            }
        }

        if (level.Border is { } border && GodotObject.IsInstanceValid(border))
        {
            border.Visible = level.Focused;
            if (level.Focused && RectOf(level) is { } whole)
            {
                Rect2 framed = whole.Grow(4f);
                border.GlobalPosition = framed.Position;
                border.Size = framed.Size;
            }
        }
    }

    private static void HideControllerVisuals(Level level)
    {
        if (level.Icon is { } icon && GodotObject.IsInstanceValid(icon))
        {
            icon.Visible = false;
        }

        if (level.Border is { } border && GodotObject.IsInstanceValid(border))
        {
            border.Visible = false;
        }
    }
}
