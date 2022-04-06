using HarmonyLib;

// 仙山洞府补丁

namespace FuturePlugin{
    public class MountainCavePlugin
    {

        // 玩家升级仙山或者洞府后,设置 this.upgrade
        // 但是在升完级后却没有重置 this.upgrade
        // 导致地图上建造建筑物或者升级时
        // 游戏都会遍历全部建筑调用 Upgrade
        public const string Version = "0.1.0";
        private static Harmony harmony;

        public static void Enable()
        {
            FuturePlugin.Log.LogInfo($"MountainCavePlugin {MountainCavePlugin.Version} is loaded!");
            harmony = Harmony.CreateAndPatchAll(typeof(MountainCavePlugin));
        }

        public static void Disable()
        {
            harmony.UnpatchSelf();
        }

        // 仙山,升级后重置 this.upgrade
        [HarmonyPatch(typeof(ServerBuildingMountain), nameof(ServerBuildingMountain.Upgrade))]
        [HarmonyPostfix]
        public static void PatchMountainUpgrade(ServerBuildingMountain __instance)
        {
            if (__instance.upgrade != "")
            {
                FuturePlugin.Log.LogInfo("PatchMountainUpgrade: reset this.upgrade");
                __instance.upgrade = "";
                if (__instance.level == 2)
                {
                    __instance.goods[3].count = 0;
                }
            }

        }

        // 洞府,升级后重置 this.upgrade
        [HarmonyPatch(typeof(ServerBuildingCave), nameof(ServerBuildingCave.Upgrade))]
        [HarmonyPostfix]
        public static void PatchCaveUpgrade(ServerBuildingCave __instance)
        {
            if (__instance.upgrade != "")
            {
                FuturePlugin.Log.LogInfo("PatchCaveUpgrade: reset this.upgrade");
                __instance.upgrade = "";
                if (__instance.level == 2)
                {
                    __instance.goods[3].count = 0;
                }
            }
        }

    }
}