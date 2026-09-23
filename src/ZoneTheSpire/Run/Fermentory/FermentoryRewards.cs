using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using ZoneTheSpire.Core.Fermentory;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Fermentory;

/// <summary>
/// Shared look of the Fermentory's own fight rewards: a custom relic-style icon (a vanilla potion if the texture is missing),
/// placed right after the potion reward. Custom RewardType values are turned back into these rewards when a save is loaded (FermentoryRewardLoadPatch).
/// </summary>
public abstract class FermentoryReward : Reward
{
    protected FermentoryReward(Player player)
        : base(player)
    {
        RestSiteLoc.EnsureInjected();
    }

    public override int RewardsSetIndex => 2;

    public override bool IsPopulated => true;

    /// <summary>The reward's icon texture in Assets/Textures.</summary>
    protected abstract string IconFile { get; }

    /// <summary>Fallback icon if the texture is missing.</summary>
    protected abstract PotionModel IconPotion { get; }

    public override void Populate()
    {
    }

    public override Control? CreateIcon()
    {
        if (TestMode.IsOn)
        {
            return null;
        }

        var icon = new TextureRect
        {
            Texture = ModTextures.Get(IconFile, IconPotion.ImagePath) ?? IconPotion.Image,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return icon;
    }

    public override void MarkContentAsSeen()
    {
    }
}

/// <summary>Fermentory fight reward: +1 potion slot.</summary>
public sealed class PotionSlotReward : FermentoryReward
{
    public const RewardType Type = (RewardType)7310;

    public PotionSlotReward(Player player)
        : base(player)
    {
    }

    protected override RewardType RewardType => Type;

    public override LocString Description => RestSiteLoc.PotionSlotRewardDescription;

    protected override string IconFile => "potion_slot_reward.png";

    protected override PotionModel IconPotion => ModelDb.Potion<PotionOfCapacity>();

    protected override async Task<bool> OnSelect()
    {
        await PlayerCmd.GainMaxPotionCount(1, Player);
        return true;
    }
}

/// <summary>Fermentory fight reward: the leftmost normal potion slot becomes special.</summary>
public sealed class SpecialSlotReward : FermentoryReward
{
    public const RewardType Type = (RewardType)7311;

    public SpecialSlotReward(Player player)
        : base(player)
    {
    }

    protected override RewardType RewardType => Type;

    public override LocString Description => RestSiteLoc.SpecialSlotRewardDescription;

    protected override string IconFile => "special_slot_reward.png";

    protected override PotionModel IconPotion => ModelDb.Potion<DistilledChaos>();

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { new HoverTip(ZoneTheSpireModifier.ModTextLoc("fermentory.special_slot"), FermentoryText.SpecialSlotDescription) };

    protected override Task<bool> OnSelect() => Task.FromResult(SpecialSlots.MakeNextSpecial(Player, "a fight reward") != null);
}

/// <summary>Loads the Fermentory's saved fight rewards (Reward.FromSerializable throws for types it doesn't know).</summary>
[HarmonyPatch(typeof(Reward), nameof(Reward.FromSerializable))]
internal static class FermentoryRewardLoadPatch
{
    private static bool Prefix(SerializableReward save, Player player, ref Reward __result)
    {
        try
        {
            if (save.RewardType == PotionSlotReward.Type)
            {
                __result = new PotionSlotReward(player);
                return false;
            }

            if (save.RewardType == SpecialSlotReward.Type)
            {
                __result = new SpecialSlotReward(player);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to load a saved Fermentory reward: {ex}");
            return true;
        }
    }
}
