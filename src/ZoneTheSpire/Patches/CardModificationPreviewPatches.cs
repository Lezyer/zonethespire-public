using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using ZoneTheSpire.Core.Infestation;
using ZoneTheSpire.Run;
using ZoneTheSpire.Run.Ferrosand;
using ZoneTheSpire.Run.HallsOfMidas;
using ZoneTheSpire.Run.ForgottenEmpire;
using ZoneTheSpire.Run.Wriggling;
using ZoneTheSpire.Run.ZoneEvents;

namespace ZoneTheSpire.Patches;

/// <summary>Builds detached, UI-only after-cards for custom card modification choices.</summary>
internal static class CardModificationPreview
{
    public static Func<CardModel, CardModel>? ForPrompt(LocString prompt)
    {
        if (Matches(prompt, "events", MarbleWorm.SelectionPromptKey))
        {
            return MarbleWormAfter;
        }

        if (Matches(prompt, "events", GoldenThrone.SelectionPromptKey))
        {
            return GoldenThroneAfter;
        }

        if (Matches(prompt, "events", WrithingPit.NestPromptKey))
        {
            return WrithingPitAfter;
        }

        if (Matches(prompt, "events", SingingLodestone.AttunePromptKey))
        {
            return CloneWithMagnetic;
        }

        if (Matches(prompt, "events", SingingLodestone.ChargePromptKey))
        {
            return CloneUpgraded;
        }

        if (Matches(prompt, "events", Frostfallen.DitchPromptKey))
        {
            return CloneFrostbitten;
        }

        if (Matches(prompt, "events", Frostfallen.StrugglePromptKey))
        {
            return CloneFrostnipped;
        }

        if (Matches(prompt, "events", TheStillMonk.SitPromptKey))
        {
            return CloneSatWith;
        }

        if (Matches(prompt, RestSiteLoc.FrostbindPrompt))
        {
            return CloneFrostbound;
        }

        if (Matches(prompt, RestSiteLoc.MeditatePrompt))
        {
            return CloneMeditated;
        }

        if (Matches(prompt, RestSiteLoc.SculptPrompt))
        {
            return CloneWithMarbled;
        }

        return null;
    }

    /// <summary>
    /// The full result of a Smith replacement's upgrade screen, found by its prompt (null for vanilla Smith and anything else).
    /// Add a line here for every campfire action that upgrades through the upgrade screen and changes the card further.
    /// </summary>
    public static Func<CardModel, CardModel>? ForUpgradePrompt(LocString prompt)
    {
        if (Matches(prompt, RestSiteLoc.FesteringSmithPrompt))
        {
            return FesteringSmithAfter;
        }

        if (Matches(prompt, RestSiteLoc.BlasphemePrompt))
        {
            return BlasphemeAfter;
        }

        return null;
    }

    /// <summary>Blaspheme: upgraded, and an attack also gains Hallowing and Blasphemous.</summary>
    public static CardModel BlasphemeAfter(CardModel original)
    {
        CardModel after = Clone(original);
        after.UpgradeInternal();
        after.UpgradePreviewType = CardUpgradePreviewType.Deck;
        if (Run.Hallowed.HallowingModifier.CanHallow(after))
        {
            Run.Hallowed.HallowingModifier.TryAdd(after);
            Run.Hallowed.BlasphemousModifier.TryAdd(after);
        }

        return after;
    }

    public static CardModel FesteringSmithAfter(CardModel original)
    {
        CardModel after = Clone(original);
        after.UpgradeInternal();
        after.UpgradePreviewType = CardUpgradePreviewType.Deck;
        int amount = WrigglingRules.SmithAmount(after.EnergyCost.GetResolved(), after.EnergyCost.CostsX);
        WrigglingModifier.AddOrIncrease(after, amount);
        return after;
    }

    public static NPreviewCardHolder? AddCard(Control container, CardModel card, PileType pileType, bool upgradePreview)
    {
        NCard? node = NCard.Create(card);
        if (node == null)
        {
            return null;
        }

        NPreviewCardHolder? holder = NPreviewCardHolder.Create(node, showHoverTips: true, scaleOnHover: false);
        if (holder == null)
        {
            node.QueueFreeSafely();
            return null;
        }

        container.AddChildSafely(holder);
        try
        {
            if (upgradePreview)
            {
                node.ShowUpgradePreview();
            }
            else
            {
                node.UpdateVisuals(pileType, CardPreviewMode.Normal);
            }
        }
        catch (Exception ex)
        {
            // Other mods patch NCard.UpdateVisuals and may not expect a detached preview card (seen with TheKin). The card was
            // already drawn when created, so it stays in the preview, just without that refresh.
            Log.WarnOnce("Card preview refresh (possibly another mod's card visuals)", ex);
        }

        return holder;
    }

    public static void Clear(Control container)
    {
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFreeSafely();
        }
    }

    private static CardModel MarbleWormAfter(CardModel original)
    {
        CardModel after = CloneWithMarbled(original);
        int amount = WrigglingRules.RewardAmount(after.EnergyCost.GetResolved(), after.EnergyCost.CostsX);
        WrigglingModifier.AddOrIncrease(after, amount);
        return after;
    }

    private static CardModel GoldenThroneAfter(CardModel original)
    {
        CardModel after = CloneUpgraded(original);
        GildedModifier.TryAdd(after);
        return after;
    }

    private static CardModel WrithingPitAfter(CardModel original)
    {
        CardModel after = Clone(original);
        WrigglingModifier.AddOrIncrease(after, WrithingPit.NestAmount(original));
        return after;
    }

    private static CardModel CloneUpgraded(CardModel original)
    {
        CardModel after = Clone(original);
        if (after.IsUpgradable)
        {
            after.UpgradeInternal();
            after.UpgradePreviewType = CardUpgradePreviewType.Deck;
        }

        return after;
    }

    private static CardModel CloneWithMarbled(CardModel original)
    {
        CardModel after = Clone(original);
        MarbledModifier.TryAdd(after);
        return after;
    }

    private static CardModel CloneFrostbitten(CardModel original) => CloneWithCold(original, ZoneTheSpire.Core.Hoarfrost.FrostfallenRules.DitchColdPerCost);

    private static CardModel CloneFrostnipped(CardModel original) => CloneWithCold(original, ZoneTheSpire.Core.Hoarfrost.FrostfallenRules.StruggleColdPerCost);

    private static CardModel CloneSatWith(CardModel original)
    {
        CardModel after = Clone(original);
        TheStillMonk.ApplySit(after);
        return after;
    }

    private static CardModel CloneWithCold(CardModel original, int perCost)
    {
        CardModel after = Clone(original);
        Run.Hoarfrost.BitingColdModifier.AddOrIncrease(after, ZoneTheSpire.Core.Hoarfrost.FrostRules.ColdFor(perCost, after.EnergyCost.GetResolved(), after.EnergyCost.CostsX));
        return after;
    }

    private static CardModel CloneFrostbound(CardModel original)
    {
        CardModel after = Clone(original);
        Run.Hoarfrost.BitingColdModifier.AddOrIncrease(after, ZoneTheSpire.Core.Hoarfrost.FrostRules.FrostbindCold(after.EnergyCost.GetResolved(), after.EnergyCost.CostsX));
        return after;
    }

    private static CardModel CloneMeditated(CardModel original)
    {
        CardModel after = Clone(original);
        Run.Devas.ChakraModifier.Meditate(after);
        return after;
    }

    private static CardModel CloneWithMagnetic(CardModel original)
    {
        CardModel after = Clone(original);
        MagneticModifier.TryAdd(after);
        return after;
    }

    private static CardModel Clone(CardModel original) => (CardModel)original.ClonePreservingMutability();

    private static bool Matches(LocString actual, LocString expected) =>
        Matches(actual, expected.LocTable, expected.LocEntryKey);

    private static bool Matches(LocString actual, string table, string key) =>
        actual.LocTable == table && actual.LocEntryKey == key;
}

/// <summary>Associates a generic deck picker with the appropriate custom after-card builder.</summary>
[HarmonyPatch(typeof(NDeckCardSelectScreen), nameof(NDeckCardSelectScreen.Create))]
internal static class ModifiedCardSelectScreenPatch
{
    private sealed record PreviewConfig(Func<CardModel, CardModel> BuildAfter);

    private static readonly ConditionalWeakTable<NDeckCardSelectScreen, PreviewConfig> Screens = new();

    private static void Postfix(CardSelectorPrefs prefs, NDeckCardSelectScreen __result)
    {
        try
        {
            if (CardModificationPreview.ForPrompt(prefs.Prompt) is { } buildAfter)
            {
                Screens.Add(__result, new PreviewConfig(buildAfter));
            }
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Card preview (deck selection screen)", ex);
        }
    }

    internal static Func<CardModel, CardModel>? GetBuilder(NDeckCardSelectScreen screen) =>
        Screens.TryGetValue(screen, out PreviewConfig? config) ? config.BuildAfter : null;
}

/// <summary>
/// The generic picker already puts each selected original in its confirmation view. Insert the corresponding detached
/// after-card beside every original and resize the group using the vanilla preview's own thresholds.
/// </summary>
[HarmonyPatch(typeof(NDeckCardSelectScreen), "PreviewSelection", new Type[] { })]
internal static class ModifiedCardPreviewPatch
{
    private static readonly AccessTools.FieldRef<NDeckCardSelectScreen, HashSet<CardModel>> SelectedCards =
        SafeRef.Field<NDeckCardSelectScreen, HashSet<CardModel>>("_selectedCards");

    private static readonly AccessTools.FieldRef<NDeckCardSelectScreen, Control> PreviewCards =
        SafeRef.Field<NDeckCardSelectScreen, Control>("_previewCards");

    private static void Postfix(NDeckCardSelectScreen __instance)
    {
        try
        {
            if (ModifiedCardSelectScreenPatch.GetBuilder(__instance) is not { } buildAfter)
            {
                return;
            }

            Control container = PreviewCards(__instance);
            List<CardModel> selected = SelectedCards(__instance).ToList();
            for (int i = 0; i < selected.Count; i++)
            {
                CardModel original = selected[i];
                NPreviewCardHolder? after = CardModificationPreview.AddCard(
                    container,
                    buildAfter(original),
                    original.Pile?.Type ?? PileType.Deck,
                    upgradePreview: false);
                if (after != null)
                {
                    container.MoveChild(after, i * 2 + 1);
                }
            }

            int displayedCards = selected.Count * 2;
            Callable.From(() =>
            {
                container.PivotOffset = container.Size / 2f;
                float scale = displayedCards > 6 ? 0.55f : displayedCards > 3 ? 0.8f : 1f;
                container.Scale = Vector2.One * scale;
            }).CallDeferred();
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Card preview (selected cards)", ex);
        }
    }
}

/// <summary>
/// Marks upgrade screens opened by a zone's Smith replacement (Festering Smith, Blaspheme) with the builder of their full result,
/// found by the screen's prompt; vanilla Smith and other upgrade screens are left alone.
/// </summary>
[HarmonyPatch(typeof(NDeckUpgradeSelectScreen), nameof(NDeckUpgradeSelectScreen.ShowScreen))]
internal static class ModifiedUpgradeScreenPatch
{
    private static readonly ConditionalWeakTable<NDeckUpgradeSelectScreen, Func<CardModel, CardModel>> Builders = new();

    private static void Postfix(CardSelectorPrefs prefs, NDeckUpgradeSelectScreen __result)
    {
        try
        {
            if (CardModificationPreview.ForUpgradePrompt(prefs.Prompt) is { } builder)
            {
                Builders.AddOrUpdate(__result, builder);
            }
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Card preview (upgrade screen)", ex);
        }
    }

    internal static Func<CardModel, CardModel>? GetBuilder(NDeckUpgradeSelectScreen screen) =>
        Builders.TryGetValue(screen, out Func<CardModel, CardModel>? builder) ? builder : null;

    /// <summary>The builder of the upgrade screen this node sits in, if that screen belongs to a Smith replacement.</summary>
    internal static Func<CardModel, CardModel>? GetBuilderAround(Node node)
    {
        for (Node? current = node; current != null; current = current.GetParent())
        {
            if (current is NDeckUpgradeSelectScreen screen)
            {
                return GetBuilder(screen);
            }
        }

        return null;
    }
}

/// <summary>Replaces the single-card upgraded preview with the complete result (upgrade plus the zone's change).</summary>
[HarmonyPatch(typeof(NUpgradePreview), "Reload", new Type[] { })]
internal static class ModifiedUpgradeSinglePreviewPatch
{
    private static readonly AccessTools.FieldRef<NUpgradePreview, Control> AfterContainer =
        SafeRef.Field<NUpgradePreview, Control>("_after");

    private static void Postfix(NUpgradePreview __instance)
    {
        try
        {
            CardModel? original = __instance.Card;
            if (original == null || ModifiedUpgradeScreenPatch.GetBuilderAround(__instance) is not { } builder)
            {
                return;
            }

            Control afterContainer = AfterContainer(__instance);
            CardModificationPreview.Clear(afterContainer);
            CardModificationPreview.AddCard(afterContainer, builder(original), original.Pile?.Type ?? PileType.Deck, upgradePreview: true);
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Card preview (single upgrade)", ex);
        }
    }
}

/// <summary>Rebuilds the multi-smith result grid so every upgraded card also shows the zone's change.</summary>
[HarmonyPatch(typeof(NDeckUpgradeSelectScreen), "OnCardClicked")]
internal static class ModifiedUpgradeMultiPreviewPatch
{
    private static readonly AccessTools.FieldRef<NDeckUpgradeSelectScreen, HashSet<CardModel>> SelectedCards =
        SafeRef.Field<NDeckUpgradeSelectScreen, HashSet<CardModel>>("_selectedCards");

    private static readonly AccessTools.FieldRef<NDeckUpgradeSelectScreen, Control> PreviewContainer =
        SafeRef.Field<NDeckUpgradeSelectScreen, Control>("_upgradeMultiPreviewContainer");

    private static readonly AccessTools.FieldRef<NDeckUpgradeSelectScreen, Control> PreviewCards =
        SafeRef.Field<NDeckUpgradeSelectScreen, Control>("_multiPreview");

    private static void Postfix(NDeckUpgradeSelectScreen __instance)
    {
        try
        {
            if (ModifiedUpgradeScreenPatch.GetBuilder(__instance) is not { } builder || !PreviewContainer(__instance).Visible)
            {
                return;
            }

            Control container = PreviewCards(__instance);
            CardModificationPreview.Clear(container);
            foreach (CardModel original in SelectedCards(__instance))
            {
                CardModificationPreview.AddCard(container, builder(original), original.Pile?.Type ?? PileType.Deck, upgradePreview: true);
            }
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Card preview (multiple upgrades)", ex);
        }
    }
}
