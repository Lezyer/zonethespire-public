using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Core.ZoneRelics;

namespace ZoneTheSpire.Run.Phantasmal;

/// <summary>
/// Combat-only modifier applied by Fluttering Phantasm. It heals the card's owner whenever the card is played, adds that
/// effect to the card text, and marks the card for the Phantasm-Haunted visual treatment. Combat cards and their modifiers
/// are discarded at combat end, so it can never leak into the permanent deck.
/// </summary>
public sealed class FlutteringPhantasmModifier : CardModifier, ILocalizationProvider
{
    public string? LocTable => "card_modifiers";

    /// <summary>English text: Localization/eng/*.json (loaded by the game, see ModLocalization).</summary>
    public List<(string, string)>? Localization => null;

    public override void ModifyDescription(Creature? target, ref string description)
    {
        string name = global::ZoneTheSpire.Run.Localization.ModLocalization.GameText("card_modifiers", Id.Entry + ".title");
        string line = $"[gold]{name}[/gold].";
        description = string.IsNullOrEmpty(description) ? line : line + "\n" + description;
    }

    public override void AddTips(List<IHoverTip> tips)
    {
        tips.Add(new HoverTip(GetLoc("title"), GetLoc("description")));
    }

    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            Creature? owner = cardPlay.Player?.Creature;
            if (Owner == null || owner == null || owner.IsDead)
            {
                return;
            }

            await CreatureCmd.Heal(owner, ZoneRelicEffects.FlutteringPhantasmHeal);
        }
        catch (Exception ex)
        {
            Log.Warn($"Fluttering Phantasm card failed to heal: {ex}");
        }
    }
}
