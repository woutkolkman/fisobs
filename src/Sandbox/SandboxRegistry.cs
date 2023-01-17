﻿using ArenaBehaviors;
using Fisobs.Core;
using Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fisobs.Sandbox;

/// <summary>
/// A registry that stores <see cref="ISandboxHandler"/> instances and the hooks relevant to them.
/// </summary>
public sealed partial class SandboxRegistry : Registry
{
    /// <summary>
    /// The singleton instance of this class.
    /// </summary>
    public static SandboxRegistry Instance { get; } = new SandboxRegistry();

    readonly Dictionary<PhysobType, ISandboxHandler> sboxes = new();

    /// <inheritdoc/>
    protected override void Process(IContent content)
    {
        if (content is ISandboxHandler handler) {
            sboxes[handler.Type] = handler;
        }
    }

    private void Update(List<MultiplayerUnlocks.SandboxUnlockID> list, IList<SandboxUnlock> unlocks, bool remove)
    {
        foreach (var unlock in unlocks) {
            if (remove) {
                list.Remove(unlock.Type);
            } else if (!list.Contains(unlock.Type)) {
                list.Add(unlock.Type);
            }
        }
    }

    /// <inheritdoc/>
    protected override void Initialize()
    {
        // Sandbox UI
        On.RainWorld.OnModsInit += RainWorld_OnModsInit;
        On.RainWorld.OnModsDisabled += RainWorld_OnModsDisabled;
        On.Menu.SandboxSettingsInterface.ctor += AddPages;

        // Creatures
        On.Menu.SandboxSettingsInterface.DefaultKillScores += DefaultKillScores;

        // Common.cs: Items + Creatures
        On.SandboxGameSession.SpawnEntity += SpawnEntity;
        On.MultiplayerUnlocks.SymbolDataForSandboxUnlock += FromUnlock;
        On.MultiplayerUnlocks.SandboxUnlockForSymbolData += FromSymbolData;
        On.MultiplayerUnlocks.ParentSandboxID += GetParent;
        On.MultiplayerUnlocks.TiedSandboxIDs += TiedSandboxIDs;
        On.PlayerProgression.MiscProgressionData.GetTokenCollected_SandboxUnlockID += GetCollected; // force-assume slugcat token is collected
        On.ArenaBehaviors.SandboxEditor.GetPerformanceEstimate += SandboxEditor_GetPerformanceEstimate;
    }

    private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        foreach (var sbox in sboxes.Values) {
            if (sbox.Type.IsCrit) {
                Update(MultiplayerUnlocks.CreatureUnlockList, sbox.SandboxUnlocks, remove: false);
            } else {
                Update(MultiplayerUnlocks.ItemUnlockList, sbox.SandboxUnlocks, remove: false);
            }
        }
    }

    private void RainWorld_OnModsDisabled(On.RainWorld.orig_OnModsDisabled orig, RainWorld self, ModManager.Mod[] newlyDisabledMods)
    {
        orig(self, newlyDisabledMods);

        foreach (var sbox in sboxes.Values) {
            if (sbox.Type.IsCrit) {
                Update(MultiplayerUnlocks.CreatureUnlockList, sbox.SandboxUnlocks, remove: true);
            } else {
                Update(MultiplayerUnlocks.ItemUnlockList, sbox.SandboxUnlocks, remove: true);
            }
        }
    }

    private void AddPages(On.Menu.SandboxSettingsInterface.orig_ctor orig, SandboxSettingsInterface self, Menu.Menu menu, MenuObject owner)
    {
        orig(self, menu, owner);

        self.subObjects.Add(new Paginator(self, Vector2.zero));
    }

    private void DefaultKillScores(On.Menu.SandboxSettingsInterface.orig_DefaultKillScores orig, ref int[] killScores)
    {
        orig(ref killScores);

        foreach (var unlock in sboxes.Values.Where(c => c.Type.IsCrit).SelectMany(c => c.SandboxUnlocks)) {
            int unlockTy = (int)unlock.Type;

            if (unlockTy >= 0 && unlockTy < killScores.Length) {
                killScores[unlockTy] = unlock.KillScore;
            } else {
                Debug.LogError($"The sandbox unlock type \"{unlock.Type}\" ({(int)unlock.Type}) is not in the range [0, {killScores.Length}).");
            }
        }
    }
}
