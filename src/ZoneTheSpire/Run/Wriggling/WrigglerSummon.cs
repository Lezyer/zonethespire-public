using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ZoneTheSpire.Run.Wriggling;

/// <summary>Summons a player's Wriggler (or grows the living one), the way Osty's Summon works.</summary>
internal static class WrigglerSummon
{
    private const float PetGap = 10f;
    private const int MaxLayoutRetries = 3;

    public static async Task Summon(PlayerChoiceContext choiceContext, Player player, int amount)
    {
        if (amount <= 0 || player.Creature.IsDead || player.Creature.CombatState == null || player.PlayerCombatState == null)
        {
            return;
        }

        Creature? existing = player.PlayerCombatState.GetPet<WrigglerPet>();
        if (existing != null && existing.IsAlive)
        {
            await CreatureCmd.GainMaxHp(existing, amount);
            return;
        }

        Creature pet = await PlayerCmd.AddPet<WrigglerPet>(player);
        await CreatureCmd.SetMaxHp(pet, amount);
        await CreatureCmd.Heal(pet, amount);
        await PowerCmd.Apply<WrigglerGuardPower>(choiceContext, pet, 1m, null, null);
        Place(pet, player, 0);
    }

    /// <summary>
    /// Visual only: stands the Wriggler just behind (left of) its owner, facing the enemies, so it never takes Osty's spot in
    /// front of the player. Waits for the node when the game adds it to the scene late.
    /// </summary>
    private static void Place(Creature pet, Player player, int attempt)
    {
        try
        {
            NCombatRoom? room = NCombatRoom.Instance;
            NCreature? node = room?.GetCreatureNode(pet);
            NCreature? ownerNode = room?.GetCreatureNode(player.Creature);
            if (node == null || ownerNode == null)
            {
                return;
            }

            if (!node.IsInsideTree())
            {
                if (attempt < MaxLayoutRetries)
                {
                    Callable.From(() => Place(pet, player, attempt + 1)).CallDeferred();
                }

                return;
            }

            float x = ownerNode.Position.X - ownerNode.Visuals.Bounds.Size.X * 0.5f - node.Visuals.Bounds.Size.X * 0.5f - PetGap;
            node.Position = new Vector2(x, ownerNode.Position.Y + 10f);
            if (node.Body is Node2D body)
            {
                body.Scale = new Vector2(-Math.Abs(body.Scale.X), body.Scale.Y);
            }

            ShowInterface(pet);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to position a Wriggler pet: {ex}");
        }
    }

    /// <summary>
    /// Visual only: gives the local player's living Wriggler its health bar, name and hover tips, like the local Osty. The
    /// game makes every other pet non-interactable when a pet is added (other players' pets stay hidden, as vanilla does).
    /// </summary>
    internal static void ShowInterface(Creature pet)
    {
        try
        {
            if (pet.IsAlive && pet.PetOwner != null && LocalContext.IsMe(pet.PetOwner)
                && NCombatRoom.Instance?.GetCreatureNode(pet) is { } node)
            {
                node.ToggleIsInteractable(true);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to show a Wriggler's health bar: {ex}");
        }
    }
}
