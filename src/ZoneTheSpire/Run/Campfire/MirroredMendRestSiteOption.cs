using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using ZoneTheSpire.Run.Effects;

namespace ZoneTheSpire.Run.Campfire;

/// <summary>
/// Mirrorlands Mend: the vanilla Mend (synced target choice, heal, hooks, VFX), then the mender picks 1 of 3 random cards
/// from the mended player's deck (no skipping) and an exact copy goes into the mender's deck.
/// </summary>
public sealed class MirroredMendRestSiteOption : RestSiteOption
{
    private readonly HealVar _healVar = new(0m);

    /// <summary>
    /// Offered cards per possible target (by NetId), fixed when the rest site options are created: the target may change
    /// their deck at the same time as this option runs (see MirrorDuplication.SnapshotOffer).
    /// </summary>
    private readonly Dictionary<ulong, IReadOnlyList<CardModel>> _offersByTarget = new();

    private LocString? _description;

    public MirroredMendRestSiteOption(Player owner)
        : base(owner)
    {
        foreach (Player other in owner.RunState.Players)
        {
            if (other.NetId != owner.NetId)
            {
                _offersByTarget[other.NetId] = MirrorDuplication.SnapshotOffer(owner, other);
            }
        }

        RestSiteLoc.EnsureInjected();
    }

    public override string OptionId => RestSiteLoc.MirroredMendId;

    public override IEnumerable<string> AssetPaths => new MendRestSiteOption(Owner).AssetPaths;

    public override LocString Description
    {
        get
        {
            if (_description == null)
            {
                _description = base.Description;
                _description.Add("HasTarget", variable: false);
                _description.Add("Name", "");
                _description.Add(_healVar);
            }

            return _description;
        }
    }

    public override async Task<bool> OnSelect()
    {
        uint choiceId = RunManager.Instance.PlayerChoiceSynchronizer.ReserveChoiceId(Owner);
        Player? target = null;
        if (LocalContext.IsMe(Owner))
        {
            NRestSiteRoom.Instance!.AnimateDescriptionDown();
            NRestSiteButton button = NRestSiteRoom.Instance.GetButtonForOption(this)!;
            Vector2 startPosition = button.GlobalPosition + button.Size / 2f;
            bool usingController = NControllerManager.Instance!.IsUsingDirectionalNavigation;
            NTargetManager targetManager = NTargetManager.Instance;
            targetManager.StartTargeting(TargetType.AnyPlayer, startPosition, usingController ? TargetMode.Controller : TargetMode.ClickMouseToTarget, ShouldCancelTargeting, AllowHoveringNode);
            if (usingController)
            {
                List<NRestSiteCharacter> others = NRestSiteRoom.Instance.characterAnims.Where(c => c.Player != Owner).ToList();
                for (int i = 0; i < others.Count; i++)
                {
                    others[i].Hitbox.SetFocusMode(Control.FocusModeEnum.All);
                    others[i].Hitbox.FocusNeighborTop = others[i].Hitbox.GetPath();
                    others[i].Hitbox.FocusNeighborBottom = others[i].Hitbox.GetPath();
                    others[i].Hitbox.FocusNeighborLeft = i <= 0 ? others[others.Count - 1].Hitbox.GetPath() : others[i - 1].Hitbox.GetPath();
                    others[i].Hitbox.FocusNeighborRight = i < others.Count - 1 ? others[i + 1].Hitbox.GetPath() : others[0].Hitbox.GetPath();
                }

                others.FirstOrDefault()?.Hitbox.TryGrabFocus();
            }

            targetManager.Connect(NTargetManager.SignalName.NodeHovered, Callable.From<Node>(OnNodeHovered));
            targetManager.Connect(NTargetManager.SignalName.NodeUnhovered, Callable.From<Node>(OnNodeUnhovered));
            try
            {
                target = NodeToPlayer(await targetManager.SelectionFinished());
                RunManager.Instance.PlayerChoiceSynchronizer.SyncLocalChoice(Owner, choiceId, PlayerChoiceResult.FromPlayerId(target?.NetId));
            }
            finally
            {
                targetManager.Disconnect(NTargetManager.SignalName.NodeHovered, Callable.From<Node>(OnNodeHovered));
                targetManager.Disconnect(NTargetManager.SignalName.NodeUnhovered, Callable.From<Node>(OnNodeUnhovered));
                if (usingController)
                {
                    foreach (NRestSiteCharacter character in NRestSiteRoom.Instance.characterAnims)
                    {
                        character.Hitbox.SetFocusMode(Control.FocusModeEnum.None);
                    }
                }
            }
        }
        else
        {
            ulong? targetId = (await RunManager.Instance.PlayerChoiceSynchronizer.WaitForRemoteChoice(Owner, choiceId)).AsPlayerId();
            if (targetId.HasValue)
            {
                target = Owner.RunState.GetPlayer(targetId.Value);
            }
        }

        NRestSiteRoom.Instance?.AnimateDescriptionUp();
        Description.Add("HasTarget", variable: false);
        NRestSiteRoom.Instance?.GetButtonForOption(this)?.RefreshTextState();
        if (target == null)
        {
            return false;
        }

        await CreatureCmd.Heal(target.Creature, MendRestSiteOption.GetHealAmount(target));
        await Hook.AfterRestSiteHeal(target.RunState, target, isMimicked: false);
        if (TestMode.IsOff)
        {
            string scenePath = SceneHelper.GetScenePath("vfx/vfx_cross_heal");
            Node2D vfx = PreloadManager.Cache.GetScene(scenePath).Instantiate<Node2D>(PackedScene.GenEditState.Disabled);
            NRestSiteRoom.Instance?.GetCharacterForPlayer(target)?.AddChildSafely(vfx);
            vfx.Position = Vector2.Zero;
        }

        if (_offersByTarget.TryGetValue(target.NetId, out IReadOnlyList<CardModel>? offers))
        {
            await MirrorDuplication.OfferSnapshot(Owner, offers);
        }

        return true;
    }

    private void OnNodeHovered(Node node)
    {
        Player? player = NodeToPlayer(node);
        if (player == null)
        {
            return;
        }

        Description.Add("HasTarget", variable: true);
        Description.Add("Name", PlatformUtil.GetPlayerName(RunManager.Instance.NetService.Platform, player.NetId));
        _healVar.BaseValue = HealRestSiteOption.GetBaseHealAmount(player.Creature);
        _healVar.PreviewValue = MendRestSiteOption.GetHealAmount(player);
        NRestSiteRoom.Instance?.GetButtonForOption(this)?.RefreshTextState();
    }

    private void OnNodeUnhovered(Node _)
    {
        Description.Add("HasTarget", variable: false);
        NRestSiteRoom.Instance?.GetButtonForOption(this)?.RefreshTextState();
    }

    private static Player? NodeToPlayer(Node? node) => node switch
    {
        NMultiplayerPlayerState state => state.Player,
        NRestSiteCharacter character => character.Player,
        _ => null,
    };

    private static bool ShouldCancelTargeting() =>
        NOverlayStack.Instance!.ScreenCount > 0 || NCapstoneContainer.Instance!.InUse;

    private static bool AllowHoveringNode(Node node) => !LocalContext.IsMe(NodeToPlayer(node));
}
