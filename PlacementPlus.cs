#region

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Harmony;
using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Network;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Object = StardewValley.Object;

#endregion

namespace PlacementPlus
{
    // Lost code: https://pastebin.com/AyCbCMZv
    /// <summary>The mod entry point.</summary>
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    public class PlacementPlus : Mod
    {
        // Flooring.cs for tile id's, Object.cs for item id's
        private static readonly Dictionary<int, int> TILE_ID_TO_ITEM = new Dictionary<int, int>
        {
            {0, 328},  // Wood Floor
            {1, 329},  // Stone Floor
            {2, 331},  // Weathered Floor
            {3, 333},  // Crystal Floor
            {4, 401},  // Straw Floor
            {5, 407},  // Gravel Path
            {6, 405},  // Rustic Plank Floor
            {7, 409},  // Crystal Path
            {8, 411},  // Cobblestone Path
            {9, 415},  // Stepping Stone Path
            {10, 293}, // Brick Floor
            {11, 840}, // Wood Path
            {12, 841}  // Stone Walkway Floor
        };

        private static readonly Dictionary<int, int> ITEM_TO_TILE_ID = 
            TILE_ID_TO_ITEM.ToDictionary(tile => tile.Value, tile => tile.Key);

        internal ModState modState;

        // Static instance of entry class to allow for static mod classes (i.e. patch classes) to interact with entry class data.
        internal static PlacementPlus Instance { get; private set; }

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            var harmony = HarmonyInstance.Create(ModManifest.UniqueID); harmony.PatchAll();
            Instance = this;

            // We keep track of the time since the player last placed any flooring to counteract input spam.
            // TODO: THIS IS REALLY HACKY BUT I DUNNO WHAT CAN BE DONE
            helper.Events.GameLoop.UpdateTicked += (o, e) => modState.timeSinceLastPlacement++;

            helper.Events.GameLoop.DayStarted   += (o, e) => modState.currentPlayer = Game1.player;

            helper.Events.Input.ButtonsChanged  += (o, e) => {
                if (!Context.IsWorldReady) return;
                
                // Update ModState fields
                modState.currentTerrainFeatures = modState.currentPlayer.currentLocation.terrainFeatures;
                modState.tileAtPlayerCursor     = e.Cursor.Tile;
                modState.currentlyHeldItem      = modState.currentPlayer.CurrentItem;
                
                SwapTile(o, e);
            };
        }


        /*********
        ** Internal methods
        *********/
        internal bool FlooringAtTileIsPlayerItem(Item flooringItem, Vector2 flooringTile)
        {
            return flooringItem.ParentSheetIndex.Equals(
                TILE_ID_TO_ITEM[((Flooring) modState.currentTerrainFeatures[flooringTile]).whichFloor]);
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Raised after the player presses a button on the keyboard, controller, or mouse.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        [SuppressMessage("ReSharper", "ConvertToLocalFunction")]
        private void SwapTile(object sender, ButtonsChangedEventArgs e)
        {
            Action<Item, Vector2> SwapFlooring    = (fi, ft) => {
                if (FlooringAtTileIsPlayerItem(fi, ft)) return;

                modState.currentTerrainFeatures[ft].performToolAction(new Axe(), 1, ft, modState.currentPlayer.currentLocation);
                modState.currentTerrainFeatures.Remove(ft);
                modState.currentTerrainFeatures.Add(ft, new Flooring(ITEM_TO_TILE_ID[fi.ParentSheetIndex]));

                modState.currentPlayer.reduceActiveItemByOne();
            };
            
            Func<Vector2, bool> preliminaryChecks = t => {
                // * Begin preliminary checks * //
                var isHoldingActionUseButton = e.Held.Any(button => button.IsActionButton() || button.IsUseToolButton());
                var isCursorInValidPosition  = Utility.tileWithinRadiusOfPlayer((int) t.X, (int) t.Y, 
                                                                                1, modState.currentPlayer);

                return modState.timeSinceLastPlacement > 10 &&
                       isHoldingActionUseButton             &&
                       isCursorInValidPosition;
            };

            // TODO: ALLOW REPLACEMENT FOR OLD FENCES WITH NEW FENCES AND CHESTS WITH ANY CHEST
            if (!preliminaryChecks(e.Cursor.Tile)) return; // Preliminary checks

            var tileAtCursorIsFlooring = modState.currentTerrainFeatures.ContainsKey(e.Cursor.Tile) && 
                                         modState.currentTerrainFeatures[e.Cursor.Tile] is Flooring;
            var isHoldingFlooring      = modState.currentlyHeldItem?.category.Value == Object.furnitureCategory;

            if (!(tileAtCursorIsFlooring && isHoldingFlooring)) return;
            
            SwapFlooring(modState.currentlyHeldItem, modState.tileAtPlayerCursor);
            modState.timeSinceLastPlacement = 0;
        }

        // Struct to allow for other mod classes to interact with entry class data
        internal struct ModState
        {
            internal int     timeSinceLastPlacement;
            internal Farmer  currentPlayer;
            internal Vector2 tileAtPlayerCursor;
            internal Item    currentlyHeldItem;
            internal NetVector2Dictionary<TerrainFeature,NetRef<TerrainFeature>> currentTerrainFeatures;
        }
    }
}