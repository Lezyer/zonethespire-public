using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using ZoneTheSpire.Core.Ferrosand;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Ferrosand;

/// <summary>
/// Ferrosand fight reward: magnetizes 1 random magnetizable card in the deck, with no choice. The pick uses the run seed, the
/// map location and the player's id, so every peer magnetizes the same card. The reward runs on every peer. Uses a custom
/// RewardType value, which a patch on Reward.FromSerializable turns back into this reward when a save is loaded.
/// </summary>
public sealed class MagnetizeReward : Reward
{
    /// <summary>Outside the vanilla RewardType values; saved as its number.</summary>
    public const RewardType Type = (RewardType)7301;

    private static readonly string VanillaIconPath = ImageHelper.GetImagePath("ui/reward_screen/reward_icon_card_removal.png");

    public MagnetizeReward(Player player)
        : base(player)
    {
        RestSiteLoc.EnsureInjected();
    }

    protected override RewardType RewardType => Type;

    /// <summary>Right after the vanilla card removal reward.</summary>
    public override int RewardsSetIndex => 8;

    public override bool IsPopulated => true;

    public override LocString Description => RestSiteLoc.MagnetizeRewardDescription;

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var tips = new List<IHoverTip>();
            CardModifier.Get<MagneticModifier>().AddTips(tips);
            return tips;
        }
    }

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
            Texture = ModTextures.Get("magnetize.png", VanillaIconPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return icon;
    }

    protected override Task<bool> OnSelect()
    {
        List<CardModel> eligible = Player.Deck.Cards.Where(MagneticModifier.CanMagnetize).ToList();
        if (eligible.Count == 0)
        {
            return Task.FromResult(false);
        }

        IRunState runState = Player.RunState;
        MapCoord? coord = runState.CurrentMapCoord;
        string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, runState.CurrentRoom?.Id ?? 0);
        int index = FerrosandRules.PickMagnetizeIndices(runState.Rng.Seed, location, Player.NetId, eligible.Count, count: 1).FirstOrDefault();
        if (index < 0 || index >= eligible.Count || !MagneticModifier.TryAdd(eligible[index]))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public override void MarkContentAsSeen()
    {
    }
}
