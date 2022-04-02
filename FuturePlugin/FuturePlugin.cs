using System.Collections.Generic;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;

// 1. 仙山洞府补丁(MountainCavePlugin)
// - 修复仙山洞府无限升级和自动升级

// 2. 投票补丁(VotePlugin)
// - 修复有人秒杀boss开始投票界面时,其他人在打小怪,投票过后游戏直接卡住

namespace FuturePlugin
{
    [BepInPlugin("Cherrysaber.FuturePlugin", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class FuturePlugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        private static ConfigEntry<bool> mountainCave;
        private static ConfigEntry<bool> vote;
        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            Log = Logger;

            mountainCave = Config.Bind(PluginInfo.PLUGIN_NAME, "仙山洞府补丁是否启用", true);
            if (mountainCave.Value)
            {
                MountainCavePlugin.Enable();
            }
            vote = Config.Bind(PluginInfo.PLUGIN_NAME, "投票补丁是否启用", true);
            if (vote.Value)
            {
                VotePlugin.Enable();
            }
        }
    }


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

    public class VotePlugin
    {
        // 在所有人都战斗结束后才开始投票
        public const string Version = "0.1.0";
        private static Harmony harmony;

        public static bool[] PlayerDone;
        public static bool Vote;

        public static void Enable()
        {
            FuturePlugin.Log.LogInfo($"VotePlugin {VotePlugin.Version} is loaded!");
            PlayerDone = new bool[] { };
            Vote = false;
            harmony = Harmony.CreateAndPatchAll(typeof(VotePlugin));
        }

        public static void Disable()
        {
            harmony.UnpatchSelf();
        }

        // 注入ClientMaster.ActionManage
        // StartGame 开始游戏,重置PlayerDone和Vote
        [HarmonyPatch(typeof(ClientMaster), "ActionManage")]
        [HarmonyPrefix]
        public static void PatchActionManage(ClientMaster __instance, Action action)
        {
            switch (action.type)
            {
                case Action.Type.StartGame:
                    FuturePlugin.Log.LogInfo("VotePlugin Reset");
                    Vote = false;
                    PlayerDone = new bool[] { true, true, true, true };
                    break;
                case Action.Type.InitBattle:
                    if (__instance.me.no != 0)
                    {
                        return; // 非主机
                    }
                    PlayerDone[0] = false;
                    break;
                case Action.Type.EndTurn:
                    if (__instance.me.no != 0)
                    {
                        return; // 非主机
                    }
                    PlayerDone[0] = true;
                    break;
            }
        }

        // 在RecvControll之后更新PlayerStates
        // Done 和 Unlink 为 true
        [HarmonyPatch(typeof(ServerPlayer), nameof(ServerPlayer.RecvControll))]
        [HarmonyPostfix]
        public static void PatchRecvControll(ServerPlayer __instance)
        {
            foreach (var player in ServerMaster.GetInstance().gamedata.players)
            {   // 检查所有player状态
                if (player.playerState == PlayerState.Done || player.playerState == PlayerState.Unlink)
                {
                    PlayerDone[player.no] = true;
                }
            }
            FuturePlugin.Log.LogInfo("PatchRecvControll");
            for (int i = 0; i < PlayerDone.Length; i++)
            {
                if (!PlayerDone[i])
                {
                    // 有人在战斗或者其他状态
                    return;
                }
            }
            if (Vote)
            {
                ServerMaster.GetInstance().StartVote();
            }

        }

        [HarmonyPatch(typeof(ServerMaster), nameof(ServerMaster.StartVote))]
        [HarmonyPrefix]
        public static bool PatchStartVote()
        {
            if (!Vote)
            {
                Vote = true;
            }
            for (int i = 0; i < PlayerDone.Length; i++)
            {
                if (!PlayerDone[i])
                {
                    // 有人在战斗或者其他状态
                    FuturePlugin.Log.LogInfo($"players {i} unready to vote");
                    return false;
                }
            }
            FuturePlugin.Log.LogInfo("all players ready to vote");
            Vote = false;
            return true;
        }

        [HarmonyPatch(typeof(ServerBattle), nameof(ServerBattle.EndBattle))]
        [HarmonyPrefix]
        public static void PatchEndBattle(ServerBattle __instance)
        {
            if (!__instance.isBoss)
            {
                return;
            }
            FuturePlugin.Log.LogInfo("PatchEndBattle");
            foreach (var player in __instance.players)
            {
                PlayerDone[player.no] = true;
            }
            foreach (var visitor in __instance.visitors)
            {
                PlayerDone[visitor.no] = true;
            }
        }

        // 进行攻击证明还在战斗状态, PlayerDone -> false
        [HarmonyPatch(typeof(ServerPlayer), nameof(ServerPlayer.Attack))]
        [HarmonyPostfix]
        public static void PatchAttack(ServerPlayer __instance)
        {
            FuturePlugin.Log.LogInfo($"player {__instance.no} attack ");
            PlayerDone[__instance.no] = false;
        }
    }
}
