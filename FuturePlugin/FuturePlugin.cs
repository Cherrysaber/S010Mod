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
}
