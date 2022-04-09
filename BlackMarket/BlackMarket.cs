using System;
using System.Collections.Generic;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Security.Cryptography;

// 黑市,消费威望的地点

namespace BlackMarket
{
    [BepInPlugin("Cherrysaber.BlackMarket", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class BlackMarket : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        public static Goods EmptyGoods;
        public static Goods RefreshGoods;
        public static List<String> TextList; // 保存寻找技能的字符串
        public static RNGCryptoServiceProvider Random;
        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            Log = Logger;

            EmptyGoods = new Goods("NONE", 0, 0, 0);
            RefreshGoods = new Goods("cave_refresh", -1, 5, 0);
            TextList = new List<String>();
            Random = new RNGCryptoServiceProvider();
            // copy from ShopItemShow
            // 修复Items数据错误
            // Items部分技能数据错误
            // 使用 SkillManager.GetInstance().FindSkillInData 获取最新数据
            // 替换 itemData.name
            foreach (var itemData in ItemManager.GetInstance().items)
            {
                if (itemData.type == "Skill")
                {
                    var skill = SkillManager.GetInstance().FindSkillInData(itemData.parameters);
                    itemData.name = Localization.Get("name_skill_" + skill.id);
                }
            }

            Harmony.CreateAndPatchAll(typeof(BlackMarket));
        }

        // 如果地图中没有黑市,则将其中一个村屋替换为黑市
        [HarmonyPatch(typeof(GameData), nameof(GameData.MapEdit))]
        [HarmonyPostfix]
        public static void PatchMapEdit(GameData __instance)
        {
            foreach (var key in __instance.shop.Keys)
            {
                var shop = __instance.shop[key];
                if (shop.name == "BLACK")
                {
                    Log.LogInfo("BlackMarket is already in map");
                    return;
                }
            }
            foreach (var key in __instance.shop.Keys)
            {
                var shop = __instance.shop[key];
                if (shop.name == "HOUSE")
                {
                    shop.upgrade = "Black";
                    shop.Upgrade();
                    Log.LogInfo($"{shop.card.posx},{shop.card.posy} House -> BlackMarket");
                    break;
                }
            }
        }

        // 替换黑市
        [HarmonyPatch(typeof(ServerMaster), nameof(ServerMaster.SendStartData))]
        [HarmonyPrefix]
        public static void PatchSendStartData(ServerMaster __instance)
        {
            foreach (var key in __instance.gamedata.shop.Keys)
            {
                var shop = __instance.gamedata.shop[key];
                if (shop.name == "BLACK")
                {
                    Log.LogInfo("set BlackMarket");
                    __instance.gamedata.shop.Remove(shop.card.no);
                    new Building(shop.card, shop.goods);
                    break;
                }
            }
        }

        // 查找功能,注入 ServerPlayer.RecvControll
        // 拦截 Controll.Type.Speak
        // 黑市寻找xxx
        [HarmonyPatch(typeof(ServerPlayer), nameof(ServerPlayer.RecvControll))]
        [HarmonyPrefix]
        public static void PatchRecvControll(Controll controll)
        {
            if (controll.type != Controll.Type.Speak)
            {
                return;
            }
            if (!controll.parameter.Contains("黑市寻找"))
            {
                return;
            }
            TextList.Add(controll.parameter.TrimStart("黑市寻找".ToCharArray()));
        }

        public static int RandomNext(int minValue, int maxValue)
        {
            if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));
            if (minValue == maxValue) return minValue;
            var data = new byte[4];
            Random.GetBytes(data);
            int generatedValue = Math.Abs(BitConverter.ToInt32(data, startIndex: 0));
            int diff = maxValue - minValue;
            int mod = generatedValue % diff;
            return minValue + mod;

        }

        // 生成随机物品,物品池为全技能
        // 20 高级,30 中级,50 低级
        public static List<ItemData> GetRandomItem(int n)
        {
            List<ItemData> result = new List<ItemData>();
            var itemList = ServerMaster.GetInstance().gamedata.randomItem;
            for (int i = 0; i < n; i++)
            {
                int level = RandomNext(0, 100);
                if (level > 80)
                {
                    level = 2;
                }
                else if (level > 50)
                {
                    level = 1;
                }
                else
                {
                    level = 0;
                }
                int index = RandomNext(0, itemList[level].Count);
                result.Add(itemList[level][index]);
            }
            return result;
        }

    }

    // 黑市Plus建筑
    [Serializable]
    public class Building : ServerBuilding
    {
        public Building(Card card) : base(card)
        {
            this.name = "BLACK";
            this.unbreakable = true;
            for (int i = 0; i < 7; i++)
            {
                this.goods.Add(BlackMarket.EmptyGoods);
            }
            this.goods.Add(BlackMarket.RefreshGoods);
        }

        public Building(Card card, List<Goods> goods) : base(card)
        {
            this.name = "BLACK";
            this.unbreakable = true;
            for (int i = 0; i < 7; i++)
            {
                this.goods.Add(BlackMarket.EmptyGoods);
            }
            this.goods.Add(BlackMarket.RefreshGoods);

            for (int i = 0; i < 7 && i < goods.Count; i++)
            {
                this.goods[i] = goods[i];
            }
        }

        public override void Time(int time)
        {
            if (time == 3)
            {
                for (int i = 0; i < 7; i++)
                {
                    this.goods[i].count = 0;
                }
                // this.goods.Add(BlackMarket.RefreshGoods);
            }
            else if (time == 11)
            {
                this.refresh();
            }

        }
        public override void SendMe(ServerPlayer player)
        {
            string text = "";
            text += this.name;
            text += ",";
            text += ServerMaster.GetInstance().gamedata.GetPopulation(this.card.posx, this.card.posy);
            text += ",";
            text += this.goods.Count;
            foreach (Goods goods in this.goods)
            {
                text += ",";
                text += goods.item.id;
                text += ",";
                text += goods.GetCount(player.no);
                text += ",";
                text += goods.GetPrestige(player.no);
                text += ",";
                text += goods.GetMana(player.no);
                text += ",";
                text += goods.GetPopulation(player.no);
            }
            player.SendMessage(Action.Type.Shop, new string[] { text });
        }

        public override void Refresh()
        {
            this.refresh();
            base.SendMe();
        }

        private void refresh()
        {
            List<ItemData> randomItem = BlackMarket.GetRandomItem(7);
            for (int i = 0; i < randomItem.Count; i++)
            {
                Goods goods = new Goods(randomItem[i].id, 1, 5 * (randomItem[i].rarity + 1), 0);
                this.goods[i] = goods;
            }

            // 遍历TextList,有匹配的物品则加入黑市
            for (int i = 0; i < 7 && i < BlackMarket.TextList.Count; i++)
            {
                var find = new List<ItemData>();
                foreach (var item in ItemManager.GetInstance().items)
                {
                    if (item.name.Contains(BlackMarket.TextList[i]))
                    {
                        // BlackMarket.Log.LogInfo($"find {item.name}");
                        if (item.type == "Skill" || item.type == "Weapon")
                        {
                            find.Add(item);
                        }
                    }
                }

                // 模糊查找,随机选择一样加入黑市
                if (find.Count == 0)
                {
                    continue;
                }
                var index = BlackMarket.RandomNext(0, find.Count);
                this.goods[i].item = find[index];
                this.goods[i].prestige = 30;
            }
            BlackMarket.TextList.Clear();
        }
    }
}
