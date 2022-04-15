using System;
using System.Collections.Generic;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;

// GC 补丁, 每天执行一次GC, 防止后期卡顿

namespace GCPlugin
{
    [BepInPlugin("Cherrysaber.GCPlugin", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class GCPlugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
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
