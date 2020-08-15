﻿using System.Collections.Generic;

namespace PKHeX.Core
{
    /// <inheritdoc />
    /// <summary>
    /// <see cref="GameVersion.XY"/> encounter area
    /// </summary>
    public sealed class EncounterArea6XY : EncounterArea32
    {
        public override IEnumerable<EncounterSlot> GetMatchingSlots(PKM pkm, IReadOnlyList<EvoCriteria> chain)
        {
            foreach (var slot in Slots)
            {
                foreach (var evo in chain)
                {
                    if (slot.Species != evo.Species)
                        continue;

                    if (!slot.IsLevelWithinRange(evo.MinLevel, evo.Level))
                        break;

                    if (slot.Form != evo.Form)
                    {
                        if (!Legal.WildForms.Contains(slot.Species))
                            break;

                        var maxLevel = slot.LevelMax;
                        if (!ExistsPressureSlot(evo, ref maxLevel))
                            break;

                        if (maxLevel != pkm.Met_Level)
                            break;

                        var clone = (EncounterSlot6XY)slot.Clone();
                        clone.Form = evo.Form;
                        clone.Pressure = true;
                        yield return clone;
                        break;
                    }

                    yield return slot;
                    break;
                }
            }
        }

        private bool ExistsPressureSlot(DexLevel evo, ref int level)
        {
            bool existsForm = false;
            foreach (var z in Slots)
            {
                if (z.Species != evo.Species)
                    continue;
                if (z.Form == evo.Form)
                    continue;
                if (z.LevelMax < level)
                    continue;
                level = z.LevelMax;
                existsForm = true;
            }
            return existsForm;
        }
    }
}
