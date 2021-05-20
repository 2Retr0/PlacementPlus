#region

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Harmony;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.TerrainFeatures;
using Object = StardewValley.Object;

#endregion

namespace PlacementPlus.Patches
{
    [HarmonyPatch]
    internal class ReusablePatches_SkipMethod
    {
        private static PlacementPlus.ModState modState => PlacementPlus.Instance.modState;
        private static IMonitor Monitor                => PlacementPlus.Instance.Monitor;

        [SuppressMessage("ReSharper", "ConvertToLocalFunction")]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            // Lambda to get MethodInfo for each overwritten CheckForAction() in subclasses StardewValley.Object
            Func<IEnumerable<MethodBase>> checkForActionIEnumerable = () => {
                // * Begin checkForActionIEnumerable * //
                return Assembly.Load("Stardew Valley").GetTypes()
                    .Where(t => t.IsSubclassOf(typeof(Object)))
                    .SelectMany(t => t.GetMethods())
                    .Where(m => m.Name.Equals("checkForAction"));
            };
            
            // * Begin TargetMethods * //
            // Concatenating other methods that we want to skip.
            return checkForActionIEnumerable().Concat(new [] {
                AccessTools.Method(typeof(GameLocation), nameof(GameLocation.performAction)),
                AccessTools.Method(typeof(ShippingBin),  nameof(ShippingBin.leftClicked))
            });
        }

        private static bool Prefix(ref bool __result)
        {
            try 
            {
                var tileAtCursorIsFlooring = modState.currentTerrainFeatures.ContainsKey(modState.tileAtPlayerCursor) && 
                                             modState.currentTerrainFeatures[modState.tileAtPlayerCursor] is Flooring;
                var isHoldingFlooring      = modState.currentlyHeldItem?.category.Value == Object.furnitureCategory;
                
                // Run original logic if the player is not holding flooring or the player-held item is the flooring they are looking at.
                if (!isHoldingFlooring     ||
                    tileAtCursorIsFlooring && 
                    PlacementPlus.Instance.FlooringAtTileIsPlayerItem(
                         modState.currentlyHeldItem, 
                         modState.tileAtPlayerCursor)
                ) return true;

                __result = false; // Original method will now return false.
                return false;     // Skip original logic.

            } catch (Exception e) {
                Monitor.Log($"Failed in {nameof(ReusablePatches_SkipMethod)}:\n{e}", LogLevel.Error);
                return true; // Run original logic.
            }
        }
    }
}