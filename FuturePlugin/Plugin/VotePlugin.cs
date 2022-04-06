using HarmonyLib;

// 投票补丁

namespace FuturePlugin{
    public class VotePlugin
    {
        // 在所有人都战斗结束后才开始投票
        public const string Version = "0.1.3";
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
                    PlayerDone[0] = false;
                    break;
                case Action.Type.EndBattle:
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

        // 结束战斗
        [HarmonyPatch(typeof(ServerBattle), nameof(ServerBattle.EndBattle))]
        [HarmonyPrefix]
        public static void PatchEndBattle(ServerBattle __instance)
        {
            // FuturePlugin.Log.LogInfo("PatchEndBattle");
            foreach (var player in __instance.players)
            {
                PlayerDone[player.no] = true;
            }
            foreach (var visitor in __instance.visitors)
            {
                PlayerDone[visitor.no] = true;
            }
        }

        // 开始战斗, PlayerDone -> false
        [HarmonyPatch(typeof(ServerPlayer), nameof(ServerPlayer.StartBattle))]
        [HarmonyPostfix]
        public static void PatchStartBattle(ServerPlayer __instance)
        {
            // FuturePlugin.Log.LogInfo($"player {__instance.no} StartBattle");
            PlayerDone[__instance.no] = false;
        }
    }
}