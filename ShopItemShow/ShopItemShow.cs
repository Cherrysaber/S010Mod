﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;


namespace ShopItemShow
{
    [BepInPlugin("Cherrysaber.ShopItemShow", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class ShopItemShow : BaseUnityPlugin
    {
        public static ManualLogSource Log;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            Log = Logger;

            // 实例化 ShopUtility,ItemDict
            ShopUtility.GetInstance();
            ItemDict.GetInstance();

            // 注入 SelectCard 修改提示
            // 提示内容位于 this.information.text
            Harmony.CreateAndPatchAll(typeof(ShopItemShow));
        }

        // 游戏中每个 ClickCard 都在地图中对应有一个 (X,Y) 坐标
        // 注入 MapSprite.OnMouseOver 获取 (X,Y) 并保存
        // PatchSelectCard 中获取对应的 ClickCard 修改提示信息
        [HarmonyPatch(typeof(PromptController), nameof(PromptController.SelectCard))]
        [HarmonyPrefix]
        public static bool PatchSelectCard(PromptController __instance, ref Card.Type type, ref string name)
        {
            // 不是 Card.Type.Shop 返回执行原函数
            if (type != Card.Type.Shop)
            {
                return true;
            }

            // copy from PromptController.SelectCard
            if (__instance.life <= 0)
            {
                __instance.unfoldTimer = 10;
            }
            // __instance.size 先不设置根据显示内容大小动态设置
            // __instance.size = new Vector2(296f, 240f);
            // 220f 可以显示 2 行
            // 240f 可以显示 3 行
            // 260f 可以显示 4 行
            __instance.skillData.localScale = new Vector3(0f, 1f, 1f);

            // 在页面下方添加商品内容
            // 村屋,集市等显示  物品 威望 x
            // 灵根,灵脉显示    物品 数量 x
            // 仙山洞府显示     物品 灵气 x
            var info = __instance.information;
            info.text = "";
            info.text = Localization.Get("name_shop_" + name) + "\n" + Localization.Get("info_shop_" + name) + "\n";
            // 调用 MapController.GetTableCard 获取 card
            var card = ClientMaster.GetInstance().mapController.GetTableCard(new Vector2Int(ShopItemShow.CardX, ShopItemShow.CardY));
            if (card == null)
            {
                // 没有找到card,就算返回执行原函数,他也找不到
                // 设置下 __instance.size 显示原始信息并记录错误
                Log.LogError($"not found card in {ShopItemShow.CardX},{ShopItemShow.CardY}");
                __instance.size = new Vector2(296f, 200f);
                return false;
            }
            // 获取gamedata.shop
            Dictionary<int, ServerBuilding> shop = ServerMaster.GetInstance().gamedata.shop;
            if (!shop.ContainsKey(card.no))
            {
                // 找不到对应商店,返回执行原函数
                // 原函数会重新设置 __instance.size,不用设置
                Log.LogError($"{card.no} not found in gamedata.shop");
                return true;
            }

            if (ShopUtility.IsParty(shop[card.no].name))
            {
                // 门派,显示前4项
                for (int i = 0; i < 4; i++)
                {
                    var item = shop[card.no].goods[i].item;
                    string text = $"{ItemDict.GetItemName(item)}  威望 {shop[card.no].goods[i].prestige}";
                    info.text += text;
                    info.text += "\n";
                }
                __instance.size = new Vector2(296f, 260f);
                return false;
            }

            var index = ShopUtility.GetIndex(shop[card.no].name);
            if (index.Length == 0)
            {
                // 没有商品 index ,返回执行原函数
                return true;
            }

            // 显示行数,用来动态设置 __instance.size
            int showNum = 0;
            foreach (int i in index)
            {
                // 商品
                var good = shop[card.no].goods[i];
                if (good.count <= 0)
                {
                    // 数量不足,不显示
                    continue;
                }
                // n 用来记录是否换行
                // 如果一个物品需要威望和灵气购买
                int n = 0;
                string text = $"{ItemDict.GetItemName(good.item)}  ";
                if (good.prestige > 0)
                {
                    text += "威望 " + good.prestige.ToString();
                    n++;
                }
                if (shop[card.no].goods[i].mana > 0)
                {
                    if (n==1){
                        text += "\n";
                    }
                    text += "灵气 " + good.mana.ToString();
                    n++;
                }
                if (shop[card.no].goods[i].count > 0)
                {
                    if (n == 0)
                    {   // 灵根或者灵脉
                        text += "数量 " + good.count.ToString();
                    }
                }
                text += "\n";
                info.text += text;
                showNum++;
            }

            float h = 200f;
            h += 15 * showNum;
            __instance.size = new Vector2(296f, h);
            return false;
        }

        // X,Y 存储 MouseOver 时卡牌的 X,Y
        public static int CardX;
        private static int CardY;
        [HarmonyPatch(typeof(MapSprite), nameof(MapSprite.OnMouseOver))]
        [HarmonyPrefix]
        public static void PatchOnMouseOver(MapSprite __instance)
        {
            // 鼠标移动到 card 上存储坐标
            // MapSprite.name = X_Y
            // X,Y 就是 card 的坐标
            // 利用坐标可以从 gamedata.shop 获取对应的建筑
            string[] array = __instance.name.Split(new char[] { '_' });
            ShopItemShow.CardX = int.Parse(array[0]);
            ShopItemShow.CardY = int.Parse(array[1]);
        }
    }

    public class ShopUtility
    {
        // Shop Utility
        // IsParty  判断建筑是否为门派
        // GetIndex 提供商品索引      
        // GetName  提供建筑名称
        private static ShopUtility instance;// instance

        private static Dictionary<string, string> nameDict;
        private static Dictionary<string, int[]> indexDict;
        private static int[] emptyIndex;
        private static int[] partyIndex;

        public static ShopUtility GetInstance()
        {
            if (instance == null)
            {
                init();
            }
            return instance;
        }

        private static void init()
        {
            // 初始化
            nameDict = new Dictionary<string, string>
            {
                {"ACADEMY","学院"},{"TEMPLE","道观"},
                {"HOUSE","村屋"},{"MARKET","集市"},{"HOSPITAL","医馆"},{"FORGE","铁匠铺"},
                {"ROOT","灵根"},{"MANASPRING","灵脉"},{"MOUNTAIN","仙山"},{"CAVE","洞府"},
                {"GUARD","岗哨"},{"GUILD","盟会"},
                {"BAR","茶摊"},{"INN","客栈"},
                {"EMPTY","空地"} , {"RUIN","废墟"} , {"BLACK","黑市"} ,
                {"CHEST","宝库"} , {"CASIN","赌场"} , {"DIVINATION","挂摊"} ,
                {"School","???"},{"SHRINE","???"},
            };

            emptyIndex = new int[] { };// empty int[]
            partyIndex = new int[] { 0, 1, 2, 3 };// party index
            var houseIndex = new int[] { 2, 3 };
            var marketIndex = new int[] { 0, 1, 2, 3 };
            var hospitalIndex = new int[] { 1, 2, 3 };
            var manaIndex = new int[] { 1, 2 };
            var caveIndex = new int[] { 4, 5, 6 };
            indexDict = new Dictionary<string, int[]>{
                {"HOUSE",houseIndex},{"MARKET",marketIndex},{"HOSPITAL",hospitalIndex},
                {"ROOT",manaIndex},{"MANASPRING",manaIndex},
                {"MOUNTAIN",caveIndex},{"CAVE",caveIndex},
                {"BAR",houseIndex},{"INN",houseIndex},
            };

            // create instance
            instance = new ShopUtility();
        }

        public static bool IsParty(string name)
        {
            // nameDict 没有记录的为门派
            return !nameDict.ContainsKey(name);
        }

        public static string GetName(string name)
        {
            // 返回建筑对应的名称
            // nameDict 中没找到则返回 self
            if (nameDict.ContainsKey(name))
            {
                return nameDict[name];
            }
            ShopItemShow.Log.LogError($"{name} not found in ShopUtility.nameDict");
            return name;
        }

        public static int[] GetIndex(string name)
        {
            // 返回对应索引
            if (indexDict.ContainsKey(name))
            {
                return indexDict[name];
            }
            return emptyIndex;
        }
    }

    // 游戏中 ItemManager.FindItemInData
    // 	foreach (ItemData itemData in this.items)
    //	{
    //		if (itemData.id.Equals(itemId))
    //		{
    //			return itemData;
    //		}
    //	}
    //	return null;
    //
    //  O(n) 改为字典获取接近 O(1)
    public class ItemDict
    {
        // GetItem 通过 id 查找对应 ItemData
        // GetItemName 获取物品名称
        private static ItemDict instance; // instance

        private static Dictionary<string, ItemData> dict;
        public static ItemDict GetInstance()
        {
            if (instance == null)
            {
                init();
            }
            return instance;
        }

        private static void init()
        {
            // 初始化数据
            var items = ItemManager.GetInstance().items;
            dict = new Dictionary<string, ItemData>();

            // 修复游戏数据错误
            // S0012 为 爆炎拳
            // S0014 为 定身咒
            // 游戏原始items.name 都为 蚀心掌
            // 游戏里刀字不知道用什么字体,或者就不是这个刀
            // 全部改成刀,方便制作查找功能
            foreach (var itemData in items)
            {
                switch (itemData.id)
                {
                    case "S0012":
                        itemData.name = "爆炎拳";
                        break;
                    case "S0014":
                        itemData.name = "定身咒";
                        break;
                    case "L0101":
                        itemData.name = "岳王刀";
                        break;
                    case "L0102":
                        itemData.name = "关帝刀";
                        break;
                    case "L0103":
                        itemData.name = "令公刀";
                        break;
                    case "L0104":
                        itemData.name = "霸王刀";
                        break;
                }
                // 将 ItemData[] 转化为 Dictionary
                dict.Add(itemData.id, itemData);
            }
            // create instance
            instance = new ItemDict();
        }

        public static ItemData GetItem(string id)
        {
            if (dict.ContainsKey(id))
            {
                return dict[id];
            }
            ShopItemShow.Log.LogError($"{id} not found in ItemDict");
            return null;
        }

        public static string GetItemName(ItemData item)
        {
            // 通过我们修改过的dict查找
            // 找不到使用 item.name
            if (dict.ContainsKey(item.id))
            {
                return dict[item.id].name;
            }
            ShopItemShow.Log.LogError($"id='{item.id}' name='{item.name}' not found in ItemDict");
            return item.name;
        }
    }
}