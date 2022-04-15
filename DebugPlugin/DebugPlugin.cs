using System;
using System.Collections.Generic;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;

// 1. ClientMaster ActionManage
// 2. UDPConnect  RecvMessage
// 3. SteamConnect SendMessage
// 4. ServerPlayer RecvControll

namespace DebugPlugin
{
    [BepInPlugin("Cherrysaber.DebugPlugin", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class DebugPlugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            Log = Logger;

            Harmony.CreateAndPatchAll(typeof(DebugPlugin));
        }

        // Patch for ClientMaster.ActionManage
        [HarmonyPatch(typeof(ClientMaster), "ActionManage")]
        [HarmonyPrefix]
        public static void PatchActionManage(Action action)
        {
            Log.LogInfo($"执行: {action.type}-{action.parameter}");
        }

        // Patch for UDPConnect.RecvMessage
        [HarmonyPatch(typeof(UDPConnect), nameof(UDPConnect.RecvMessage),new Type[] { typeof(string),typeof(string)})]
        [HarmonyPrefix]
        public static void PatchRecvMessage(string head, string message)
        {
            Log.LogInfo($"收到消息: '{head}' - '{message}'");
        }

        // Patch for SteamConnect.SendMessage
        [HarmonyPatch(typeof(SteamConnect), nameof(SteamConnect.SendMessage), new Type[] { typeof(int), typeof(string) })]
        [HarmonyPrefix]
        public static void PatchSendMessage(int no, string str)
        {
            Log.LogInfo($"发送消息: '{no}' - '{str}'");
            
        }

        // Patch for ServerPlayer.RecvControll
        [HarmonyPatch(typeof(ServerPlayer), nameof(ServerPlayer.RecvControll))]
        [HarmonyPrefix]
        public static void PatchRecvControll(ServerPlayer __instance,Controll controll)
        {
            Log.LogInfo($"玩家 {__instance.no} 操作: {controll.type}-{controll.parameter}");
        }
    }

    // GC 补丁, 定时执行 GC.Collect
    [BepInPlugin("Cherrysaber.DebugPlugin.GCPlugin", "DebugPlugin.GCPlugin", "1.0.0")]
    [BepInDependency("Cherrysaber.DebugPlugin")]
    public class GCPlugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo("Plugin DebugPlugin.GCPlugin is loaded!");
            Log = Logger;

            Harmony.CreateAndPatchAll(typeof(GCPlugin));
        }

        // Patch for ClientMaster.ActionManage
        [HarmonyPatch(typeof(ClientMaster), "ActionManage")]
        [HarmonyPrefix]
        public static void PatchActionManage(Action action)
        {
            // 每天执行一次 GC.Collect
            if (action.type == Action.Type.Time && ClockController.GetInstance().TimeConve() == 6){
                Log.LogInfo("执行 GC.Collect");
                GC.Collect();
            }
        }
    }
}
