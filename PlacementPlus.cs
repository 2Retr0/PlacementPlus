﻿using HarmonyLib;
using StardewModdingAPI;
using static PlacementPlus.ModState;

namespace PlacementPlus
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlacementPlus : Mod
    {
        // Static instance of entry class to allow for static mod classes (i.e. patch classes) to interact with entry class data.
        internal static PlacementPlus Instance;

        /*********
        ** Public methods
        *********/
        public override void Entry(IModHelper helper)
        {
            Instance = this; // Initialize static instance first as Harmony patches rely on it.
            Initialize(ref helper); // Initialize ModState to begin tracking values.
            
            var harmony = new Harmony(ModManifest.UniqueID); harmony.PatchAll();
        }
    }
}