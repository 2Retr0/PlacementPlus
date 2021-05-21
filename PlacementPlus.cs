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
using StardewValley.Locations;
using StardewValley.Network;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using xTile.Dimensions;
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
            
            // Ensure that modState.tileAtPlayerCursor is initialized for patches
            modState.tileAtPlayerCursor = new Vector2();

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
        [SuppressMessage("ReSharper", "UnusedParameter.Local")]
        private void SwapTile(object sender, ButtonsChangedEventArgs e)
        {
            Action<Item, Vector2> swapFlooring      = (i, t) => {
                if (FlooringAtTileIsPlayerItem(i, t)) return;

                modState.currentTerrainFeatures[t].performToolAction(null, 1, t, modState.currentPlayer.currentLocation);
                modState.currentTerrainFeatures.Remove(t);
                modState.currentTerrainFeatures.Add(t, new Flooring(ITEM_TO_TILE_ID[i.ParentSheetIndex]));

                modState.currentPlayer.reduceActiveItemByOne();
            };
            
            Action<Item, Chest, Vector2> swapChest = (i, o, t) => {
                var currentLocation  = modState.currentPlayer.currentLocation;

                // If the current location is not a valid location to replace chests.
                if (currentLocation is MineShaft || currentLocation is VolcanoDungeon) return;

                var chestToPlace = new Chest(true, t, i.ParentSheetIndex) { shakeTimer = 100 };
                // Use reflection to set private chest coloring and inventory.
                Helper.Reflection.GetField<NetColor>(chestToPlace, "playerChoiceColor").SetValue(o.playerChoiceColor);
                Helper.Reflection.GetField<NetObjectList<Item>>(chestToPlace, "items").SetValue(new NetObjectList<Item>(o.items));

                // Clearing out the object chest's inventory before 'dropping' it and playing its destruction sound.
                o.items.Clear(); o.clearNulls();
                currentLocation.debris.Add(new Debris(-o.ParentSheetIndex, 
                                                          t * 64f + new Vector2(32f, 32f), 
                                                          modState.currentPlayer.Position));
                currentLocation.playSound( o.ParentSheetIndex == 130 ? "axe" : "hammer");
                
                // Spawning broken particles to simulate chest breaking.
                Game1.createRadialDebris(currentLocation, o.ParentSheetIndex == 130 ? 12 : 14, 
                    (int) t.X, (int) t.Y, 4, false);
                
                currentLocation.Objects.Remove(t);
                currentLocation.objects.Add(t, chestToPlace);
                
                modState.currentPlayer.reduceActiveItemByOne();
            };
            
            Func<Vector2, bool> preliminaryChecks   = t => {
                // * Begin preliminary checks * //
                var currentlyHeldItemNotNull = modState.currentlyHeldItem != null;
                var isHoldingActionUseButton = e.Held.Any(button => button.IsActionButton() || button.IsUseToolButton());
                var isCursorInValidPosition  = Utility.tileWithinRadiusOfPlayer((int) t.X, (int) t.Y, 
                                                                                1, modState.currentPlayer);

                return modState.timeSinceLastPlacement > 10 &&
                       currentlyHeldItemNotNull             &&
                       isHoldingActionUseButton             &&
                       isCursorInValidPosition;
            };

            // TODO: ALLOW REPLACEMENT FOR OLD FENCES WITH NEW FENCES
            if (!preliminaryChecks(e.Cursor.Tile)) return; // Preliminary checks

            // * Flooring checks * //
            var tileAtCursorIsFlooring = modState.currentTerrainFeatures.ContainsKey(e.Cursor.Tile) && 
                                         modState.currentTerrainFeatures[e.Cursor.Tile] is Flooring;
            var isHoldingFlooring      = modState.currentlyHeldItem.category.Value == Object.furnitureCategory;

            // * Chest checks * //
            // ParentSheetIndex = 130 -> Wooden Chest, ParentSheetIndex = 232 -> Stone Chest
            var objectAtTile           = modState.currentPlayer.currentLocation.getObjectAtTile((int) e.Cursor.Tile.X, 
                                                                                                (int) e.Cursor.Tile.Y);
            var objectAtTileIsChest    = objectAtTile != null &&
                                         objectAtTile.ParentSheetIndex != modState.currentlyHeldItem.ParentSheetIndex && 
                                         new [] { 130, 232 }.Contains(objectAtTile.ParentSheetIndex);
            var isHoldingChest         = new [] { 130, 232 }.Contains(modState.currentlyHeldItem.ParentSheetIndex);

            if (tileAtCursorIsFlooring && isHoldingFlooring)
                swapFlooring(modState.currentlyHeldItem, modState.tileAtPlayerCursor);
            else if (objectAtTileIsChest && isHoldingChest)
                swapChest(modState.currentlyHeldItem, (Chest) objectAtTile, e.Cursor.Tile);
            
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