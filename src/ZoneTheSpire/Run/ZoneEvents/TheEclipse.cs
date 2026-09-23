using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.ZoneEvents;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Effects;
using ZoneTheSpire.Run.Prismatic;
using ZoneTheSpire.Run.Shadow;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Prismatic Storm and Shadow Corruption event: an eclipse, light against dark. Each player chooses on their own:
/// <list type="bullet">
/// <item>Split Your Shadow: a random card that can be Shadow Corrupted (preferring one that can still be upgraded) gets an
/// exact copy in the deck, upgraded if it can be; the original becomes Shadow Corrupted.</item>
/// <item>Step into the Light: transform 2 chosen cards into upgraded cards of other characters.</item>
/// <item>Step into the Dark: make 2 random cards Shadow Corrupted.</item>
/// <item>Look Straight at It: 50%: a random relic and a full heal; otherwise lose 10 Max HP.</item>
/// </list>
/// Random picks and the roll use the event's own RNG over the deck order, so every peer gets the same result. Every change
/// goes through the game's own commands, so it is saved and replicated normally.
/// </summary>
public sealed class TheEclipse : ZoneEventModel
{
    private const string PortraitFile = "the_eclipse.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/the_eclipse.png";

    private static readonly IReadOnlyList<string> Zones = new[]
    {
        new PrismaticStormBiome().Id,
        new ShadowCorruptionBiome().Id,
    };

    internal static string LightPromptKey => ModelDb.GetId<TheEclipse>().Entry + ".lightPrompt";

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        bool anyCorruptible = Owner!.Deck.Cards.Any(ShadowCorruption.CanCorrupt);
        IEnumerable<IHoverTip> corruptedTips = CorruptedTips();
        EventOption split = anyCorruptible
            ? Option(SplitYourShadow, corruptedTips, "INITIAL")
            : LockedOption("SPLIT_YOUR_SHADOW_LOCKED");
        EventOption light = Owner.Deck.Cards.Any(IsTransformable)
            ? Option(StepIntoTheLight)
            : LockedOption("STEP_INTO_THE_LIGHT_LOCKED");
        EventOption dark = anyCorruptible
            ? Option(StepIntoTheDark, corruptedTips, "INITIAL")
            : LockedOption("STEP_INTO_THE_DARK_LOCKED");
        EventOption look = EclipseRules.CanLook(Owner.Creature.MaxHp)
            ? Option(LookStraightAtIt)
            : LockedOption("LOOK_STRAIGHT_AT_IT_LOCKED");

        return new[] { split, light, dark, look };
    }

    private static IEnumerable<IHoverTip> CorruptedTips()
    {
        var tips = new List<IHoverTip>();
        CardModifier.Get<ShadowCorruptedModifier>().AddTips(tips);
        return tips;
    }

    private static bool IsTransformable(CardModel card) => card.Type != CardType.Quest && card.IsTransformable;

    private async Task SplitYourShadow()
    {
        List<CardModel> eligible = Owner!.Deck.Cards.Where(ShadowCorruption.CanCorrupt).ToList();
        List<CardModel> upgradable = eligible.Where(card => card.IsUpgradable).ToList();
        List<CardModel> pool = EclipseRules.SplitPrefersUpgradable(upgradable.Count) ? upgradable : eligible;
        if (pool.Count > 0)
        {
            CardModel original = pool[Rng.NextInt(pool.Count)];
            // Copy first, so the copy doesn't carry the corruption; then upgrade the copy and corrupt the original.
            CardModel copy = await MirrorDuplication.AddCopyToDeck(original, Owner);
            if (copy.IsUpgradable)
            {
                CardCmd.Upgrade(copy);
            }

            ShadowCorruptedModifier.TryAdd(original);
        }

        SetEventFinished(PageDescription("SPLIT"));
    }

    private async Task StepIntoTheLight()
    {
        int count = EclipseRules.LightCountFor(Owner!.Deck.Cards.Count(IsTransformable));
        List<CardModel> options = OtherCharacterCards.Unlocked(Owner).ToList();
        if (count > 0 && options.Count > 0)
        {
            var prefs = new CardSelectorPrefs(new LocString("events", LightPromptKey), count, count)
            {
                Cancelable = false,
                RequireManualConfirmation = true,
            };
            List<CardModel> chosen = (await CardSelectCmd.FromDeckForTransformation(Owner, prefs)).ToList();
            var transformations = new List<CardTransformation>();
            foreach (CardModel card in chosen)
            {
                CardModel replacement = CardFactory.CreateRandomCardForTransform(card, options, isInCombat: false, Rng);
                if (replacement.IsUpgradable)
                {
                    CardCmd.Upgrade(replacement, CardPreviewStyle.None);
                }

                transformations.Add(new CardTransformation(card, replacement));
            }

            await CardCmd.Transform(transformations, null);
        }

        SetEventFinished(PageDescription("LIGHT"));
    }

    private Task StepIntoTheDark()
    {
        List<CardModel> eligible = Owner!.Deck.Cards.Where(ShadowCorruption.CanCorrupt).ToList();
        int count = EclipseRules.DarkCountFor(eligible.Count);
        for (int i = 0; i < count; i++)
        {
            CardModel card = eligible[Rng.NextInt(eligible.Count)];
            eligible.Remove(card);
            ShadowCorruptedModifier.TryAdd(card);
        }

        SetEventFinished(PageDescription("DARK"));
        return Task.CompletedTask;
    }

    private async Task LookStraightAtIt()
    {
        bool blessed = EclipseRules.LookSucceeds(Rng.NextInt(100));
        if (blessed)
        {
            RelicModel relic = RelicFactory.PullNextRelicFromFront(Owner!).ToMutable();
            await RelicCmd.Obtain(relic, Owner!);
            int missing = Math.Max(0, Owner!.Creature.MaxHp - Owner.Creature.CurrentHp);
            if (missing > 0)
            {
                await CreatureCmd.Heal(Owner.Creature, missing);
            }
        }
        else
        {
            await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner!.Creature, EclipseRules.LookMaxHpLoss, isFromCard: false);
        }

        SetEventFinished(PageDescription(blessed ? "LOOKED_BLESSED" : "LOOKED_BURNED"));
    }
}
