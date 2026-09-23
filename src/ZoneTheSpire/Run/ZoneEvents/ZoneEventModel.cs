using System;
using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Models;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Base class for events that can only be selected by the Zone the Spire zone-event system. BaseLib normally adds a
/// custom event with no acts to every act's vanilla event bag; passing <c>autoAdd: false</c> keeps zone events out of that
/// bag while ModelDb still discovers the model type normally.
/// Extra loc keys (e.g. card selection prompts) must be built from <c>ModelDb.GetId&lt;T&gt;().Entry</c>: BaseLib registers an
/// event's loc under its model id, which carries the mod prefix (ZONETHESPIRE-MARBLE_WORM), not the bare event name.
/// </summary>
public abstract class ZoneEventModel : CustomEventModel
{
    protected ZoneEventModel() : base(autoAdd: false)
    {
    }

    /// <summary>The biome ids that may roll this event. Multiple events may share a biome and vice versa.</summary>
    public abstract IReadOnlyList<string> ZoneIds { get; }

    /// <summary>Zone events never enter an act's normal event pool.</summary>
    public sealed override ActModel[] Acts => Array.Empty<ActModel>();
}
