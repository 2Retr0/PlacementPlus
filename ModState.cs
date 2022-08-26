﻿using System.Linq;
using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Network;
using StardewValley.TerrainFeatures;
using Object = StardewValley.Object;

namespace PlacementPlus
{
    /// <summary> Static class holding and maintaining data used primarily in Harmony patches. </summary>
    public static class ModState
    {
        internal static Vector2 cursorTile;
        internal static Item    heldItem;
        internal static bool    holdingToolButton;
        internal static Object  tileObject;
        internal static NetVector2Dictionary<TerrainFeature,NetRef<TerrainFeature>> terrainFeatures;

        private static bool initialized;
        
        public static void Initialize(ref IModHelper helper)
        {
            if (initialized) return;
            
            helper.Events.Input.ButtonsChanged  += (_, e) => {
                if (!Context.IsWorldReady) return;
                
                // Updating mod state fields
                terrainFeatures = Game1.player.currentLocation.terrainFeatures;
                cursorTile = e.Cursor.Tile;
                heldItem = Game1.player.CurrentItem;

                holdingToolButton = e.Held.Any(button => button.IsUseToolButton()); // (i.e. left click)
                
                tileObject = Game1.player.currentLocation.getObjectAtTile((int) e.Cursor.Tile.X, (int) e.Cursor.Tile.Y);
            };
            initialized = true;
        }
    }
}