#region

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Harmony;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;
using Object = StardewValley.Object;

#endregion

namespace PlacementPlus.Patches
{
    [HarmonyPatch(typeof(Utility), nameof(Utility.playerCanPlaceItemHere))]
    internal class UtilityPatches_PlayerCanPlaceItemHere
    {
        private static string _ = ""; // Throwaway variable for Building.doesTileHaveProperty reference argument.
        private static IMonitor Monitor => PlacementPlus.Instance.Monitor;

        [SuppressMessage("ReSharper", "ConvertToLocalFunction")]
        [SuppressMessage("ReSharper", "ImplicitlyCapturedClosure")]
        private static void Postfix(GameLocation location, Item item, int x, int y, Farmer f, ref bool __result)
        {
            try
            {
                Func<Vector2, bool> preliminaryChecks = t => {
                    // * Begin preliminary checks * //
                    var itemIsFlooring          = item.category.Value == Object.furnitureCategory;
                    var isCursorInValidPosition = Utility.tileWithinRadiusOfPlayer((int) t.X, (int) t.Y, 1, f);

                    return itemIsFlooring && isCursorInValidPosition;
                };

                Func<Vector2, bool> objectAndTileChecks = t => {
                    var tileIsFlooring = location.terrainFeatures.ContainsKey(t) &&
                                         location.terrainFeatures[t] is Flooring;

                    // We assume that any tile that has an object is a valid tile for flooring to be placed.
                    var tileHasObject  = location.getObjectAtTile((int) t.X, (int) t.Y) != null;

                    return tileIsFlooring || tileHasObject;
                };
                
                Func<Vector2, Farm, bool> farmChecks = (t, l) => {
                    // * Begin Farm checks * //
                    var tileIsMailbox       = new[] { new Point((int) t.X, (int) t.Y), 
                                                      new Point((int) t.X, (int) t.Y + 1) }
                                              .Contains(l.GetMainMailboxPosition());

                    // Also ensure that the player is not on a walkable tile in the house location.
                    var tileIntersectsHouse =  l.GetHouseRect().Contains((int) t.X, (int) t.Y) &&
                                              !l.GetHouseRect().Contains((int) f.getTileLocation().X,
                                                                         (int) f.getTileLocation().Y);

                    return tileIsMailbox || tileIntersectsHouse;
                };
                
                Func<Vector2, BuildableGameLocation, bool> buildableGameLocationChecks = (t, l) => {
                    // * Begin BuildableGameLocation checks * //
                    var tileIntersectsBuilding = l.buildings.Any(b => b.occupiesTile(t) || b.doesTileHaveProperty(
                                                                          (int) t.X, (int) t.Y, "Mailbox", 
                                                                          "Buildings", ref _));

                    if (tileIntersectsBuilding) 
                        return true;
                    return location is Farm farm && farmChecks(t, farm);
                };
                
                // * Begin Postfix * //
                var targetTile = new Vector2((float) x / 64, (float) y / 64);

                if (!preliminaryChecks(targetTile)) return; // Run original logic.

                if (!(location is BuildableGameLocation gl        && 
                      buildableGameLocationChecks(targetTile, gl) ||
                      objectAndTileChecks(targetTile))
                ) return; // Run original logic.

                __result = true; // Original method will now return true.
            } catch (Exception e) {
                Monitor.Log($"Failed in {nameof(UtilityPatches_PlayerCanPlaceItemHere)}:\n{e}", LogLevel.Error);
            }
        }
    }
}