using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ZoneTheSpire.Core.Shadow;

namespace ZoneTheSpire.Run.Shadow;

/// <summary>Removing Doom (Light the Way, Lantern Light): lowers the vanilla Doom power, or removes it when none would be left.</summary>
internal static class ShadowDoom
{
    public static async Task Remove(PlayerChoiceContext choiceContext, Creature creature, int amount, CardModel? source)
    {
        if (creature.GetPower<DoomPower>() is not { } doom)
        {
            return;
        }

        int left = ShadowRules.DoomAfterRemoving(doom.Amount, amount);
        if (left <= 0)
        {
            await PowerCmd.Remove(doom);
        }
        else
        {
            await PowerCmd.ModifyAmount(choiceContext, doom, left - doom.Amount, creature, source);
        }
    }
}
