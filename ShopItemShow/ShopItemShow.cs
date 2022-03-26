using System.Collections.Generic;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

// 20220323 edit by Cherrysaber
// bugfix: 多人游戏非主机无法正常显示 (https://github.com/Cherrysaber/S010Mod/issues/1)
// 游戏数据初始化仅在主机进行,非主机获取 gamedata.shop 会直接出错
// solution: ShopMaster 管理 shop 数据
// 主机直接获取 ShopMaster.shop = gamedata.shop
// 非主机通过 ShopMaster 管理 shop
// 注入 ClientMaster.ActionManage 
// 获取数据设置ShopMaster.shop,并监控其他玩家数据变化,更新商店时钟
// 注入 ServerPlayer.RecvControll,发送商店数据

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
            // bugfix: 初始化ShopMaster,避免null错误
            ShopMaster.Reset();

            // 注入 SelectCard 修改提示
            // 提示内容位于 this.information.text
            Harmony.CreateAndPatchAll(typeof(ShopItemShow));
        }

        // 游戏中每个 ClickCard 都在地图中对应有一个 (X,Y) 坐标
        // 注入 MapSprite.OnMouseOver 获取 (X,Y) 并保存
        // PatchSelectCard 中获取对应的 ClickCard 修改提示信息
        [HarmonyPatch(typeof(PromptController), nameof(PromptController.SelectCard))]
        [HarmonyPrefix]
        public static bool PatchSelectCard(PromptController __instance, Card.Type type, ref string name)
        {
            // 不是 Card.Type.Shop 返回执行原函数
            if (type != Card.Type.Shop)
            {
                return true;
            }

            // bugfix: 灵根灵脉灵气满时提示不全
            // 内部存储灵气 > 4 时,name 变成 name_FULL 导致无法正常获取信息
            // 将名字改回来
            if (name == "ROOT_FULL" || name == "MANASPRING_FULL"){
                name = name.Trim("_FULL".ToCharArray());
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

            // MapCard.name = X_Y
            // X,Y 为 Card 在地图的坐标
            string[] array = MapCard.name.Split(new char[] { '_' });
            int x = int.Parse(array[0]);
            int y = int.Parse(array[1]);
            // 调用 MapController.GetTableCard 获取 card
            var card = ClientMaster.GetInstance().mapController.GetTableCard(new Vector2Int(x, y));
            if (card == null)
            {
                // 没有找到card,就算返回执行原函数,他也找不到
                // 设置下 __instance.size 显示原始信息并记录错误
                Log.LogError($"not found card in {x},{y}");
                __instance.size = new Vector2(296f, 200f);
                return false;
            }
            // 获取gamedata.shop
            Dictionary<int, ServerBuilding> shop = ShopMaster.GetShop();

            if (!shop.ContainsKey(card.no))
            {
                // 找不到对应商店,返回执行原函数
                // 原函数会重新设置 __instance.size,不用设置
                // 如果是主机就记录错误
                if (ClientMaster.GetInstance().me.no == 0)
                {
                    Log.LogError($"{card.no} not found in gamedata.shop");
                }
                return true;
            }

            var building = shop[card.no];
            if (ShopUtility.IsParty(building.name))
            {
                // 门派,显示前4项
                for (int i = 0; i < 4; i++)
                {
                    var goods = building.goods[i];
                    string text = $"{ItemDict.GetItemName(goods.item)}  威望 {goods.prestige}";
                    info.text += text;
                    info.text += "\n";
                }
                __instance.size = new Vector2(296f, 270f);
                return false;
            }

            var index = ShopUtility.GetIndex(building.name);
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
                var goods = building.goods[i];
                if (goods.count <= 0)
                {
                    // 数量不足,不显示
                    continue;
                }
                // n 用来记录是否换行
                // 如果一个物品需要威望和灵气购买
                int n = 0;
                string text = $"{ItemDict.GetItemName(goods.item)}  ";
                if (goods.prestige > 0)
                {
                    text += "威望 " + goods.prestige.ToString();
                    n++;
                }
                if (goods.mana > 0)
                {
                    if (n == 1)
                    {
                        text += "\n";
                    }
                    text += "灵气 " + goods.mana.ToString();
                    n++;
                }
                if (goods.count > 0)
                {
                    if (n == 0)
                    {   // 灵根或者灵脉
                        text += "数量 " + goods.count.ToString();
                    }
                }
                text += "\n";
                info.text += text;
                showNum++;
            }

            float h = 210f;
            h += 15 * showNum;
            __instance.size = new Vector2(296f, h);
            return false;
        }

        // MapCard 存储 OnMouseOver 时的实例
        // MapCard.name = X_Y
        // X,Y 为 Card 在地图的坐标
        public static MapSprite MapCard;

        [HarmonyPatch(typeof(MapSprite), nameof(MapSprite.OnMouseOver))]
        [HarmonyPrefix]
        public static void PatchOnMouseOver(MapSprite __instance)
        {
            MapCard = __instance;
        }


        // 注入ClientMaster.ActionManage
        // StartGame 开始游戏,重置ShopMaster
        // ChangeCreature 有人数据变动,可能购买了商品,重置商店时钟
        // TableCardData card数据变动,重置商店时钟
        // Response  商店数据
        // ResetCard 重置时钟
        // ShopMaster.RecvGoods 为我们接收到的商店数据
        // ShopMaster.SetShop 设置商店物品
        [HarmonyPatch(typeof(ClientMaster), "ActionManage")]
        [HarmonyPrefix]
        public static bool PatchActionManage(ClientMaster __instance, Action action)
        {
            // ShopItemShow.Log.LogInfo($"action: {action.type}");
            switch (action.type)
            {
                case Action.Type.StartGame:
                    // new game
                    ShopItemShow.Log.LogInfo("ShopMaster Reset");
                    ShopMaster.Reset();
                    break;
                case Action.Type.ChangeCreature:
                    // 重置TaskClock中所有记录的商店
                    foreach (var name in ShopMaster.TaskClock)
                    {
                        ShopMaster.ResetClock(name);
                    }
                    ShopMaster.TaskClock.Clear();
                    break;
                case Action.Type.TableCardData:
                    // card数据改变,更新对应商店时钟
                    string[] cardMsg = action.parameter.Split(new char[] { ',' });
                    // ShopItemShow.Log.LogInfo(action.parameter);
                    // [ card.no, shop.name, card.x, card.y, ... ]
                    for (int i = 0; i < cardMsg.Length / 6; i++)
                    {
                        string pos = cardMsg[6 * i + 2] + "_" + cardMsg[6 * i + 3];
                        ShopMaster.ResetClock(pos);
                    }
                    break;
                case ShopMaster.Response:
                    string[] shopMsg = action.parameter.Split(new char[] { ',' });
                    ShopMaster.SetShop(shopMsg);
                    return false;
                case ShopMaster.ResetCard:
                    // 需要重置的Card加入队列
                    ShopMaster.TaskClock.Add(action.parameter);
                    return false;
            }
            return true;
        }


        // 注入 ServerPlayer.RecvControll
        // ShopMaster.GetGoods 我们发送的请求数据
        [HarmonyPatch(typeof(ServerPlayer), nameof(ServerPlayer.RecvControll))]
        [HarmonyPrefix]
        public static bool PathchRecvControll(ServerPlayer __instance, Controll controll)
        {

            switch (controll.type)
            {
                case ShopMaster.Request:
                    // 发送商店货物
                    ShopMaster.SendGoods(__instance, controll);
                    return false;
                case Controll.Type.SelectGoods:
                    if (__instance.joinShop == null)
                    {
                        break;
                    }
                    string text = __instance.joinShop.card.posx.ToString() + "_" + __instance.joinShop.card.posy.ToString();
                    foreach (var player in ServerMaster.GetInstance().gamedata.players)
                    {
                        player.SendMessage(ShopMaster.ResetCard, new string[] { text });
                    }
                    break;
            }
            return true;
        }




        // bugfix: 修复多人游戏洞府仙山可以无限升级和自动升级的bug
        // 玩家升级仙山或者洞府后,设置 this.upgrade
        // 但是在升完级后却没有重置 this.upgrade
        // 导致地图上建造建筑物或者升级时
        // 游戏都会遍历全部建筑调用 Upgrade

        // 仙山,升级后重置 this.upgrade
        [HarmonyPatch(typeof(ServerBuildingMountain), nameof(ServerBuildingMountain.Upgrade))]
        [HarmonyPostfix]
        public static void PatchMountainUpgrade(ServerBuildingMountain __instance)
        {
            if (__instance.upgrade != "")
            {
                Log.LogInfo("PatchMountainUpgrade: reset this.upgrade");
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
                Log.LogInfo("PatchCaveUpgrade: reset this.upgrade");
                __instance.upgrade = "";
                if (__instance.level == 2)
                {
                    __instance.goods[3].count = 0;
                }
            }
        }
    }


    public class ShopMaster
    {
        // Shop Master 用来管理 shop 数据保证主机和非主机都能正常显示商店物品
        // Reset  重置ShopMaster
        // GetShop 获取shop
        // GetTime 获取当前回合数
        // SetShop 设置商店数据
        // ResetClock 重置商店时钟,用来判断数据是否需要重新获取
        // ResetPlayerState 重置玩家状态,在获取数据后把 shop状态 转化为 selectPos状态

        // 非主机玩家使用的 shop, 保存了从主机获取的商店信息
        private static Dictionary<int, ServerBuilding> shop;

        // 每个建筑对应一个时钟,时钟记录shop数据是否需要更新
        // 新回合开始或者有玩家进行购物都会导致时钟数据更新
        private static Dictionary<string, int> clock;

        // 记录需要重置的商店,在触发 Action.Type.ChangeCreature 重置商店时钟
        public static List<string> TaskClock;

        // 我们请求head 从200开始设置避免冲突
        // 就是游戏更新多了几个 Action 或者 Controll Type , 也不会冲突

        public const Action.Type Response = (Action.Type)200;
        public const Action.Type ResetCard = (Action.Type)201;
        public const Controll.Type Request = (Controll.Type)200;


        public static void Reset()
        {   // new game reset all
            shop = new Dictionary<int, ServerBuilding>();
            clock = new Dictionary<string, int>();
            TaskClock = new List<string>();
        }
        public static Dictionary<int, ServerBuilding> GetShop()
        {
            if (ClientMaster.GetInstance().me.no == 0)
            {   // 主机,使用gamedata.shop
                return ServerMaster.GetInstance().gamedata.shop;
            }
            if (clock.ContainsKey(ShopItemShow.MapCard.name) && GetTime() == clock[ShopItemShow.MapCard.name])
            {
                // 数据没有变化
                return shop;
            }
            if (!clock.ContainsKey(ShopItemShow.MapCard.name))
            {
                // 设置商店时钟
                clock.Add(ShopItemShow.MapCard.name, 0);
            }
            // ShopItemShow.Log.LogInfo("send get data message");
            // 发送请求数据
            clock[ShopItemShow.MapCard.name] = GetTime();
            ClientMaster.GetInstance().SendMessage(Request, new string[] { ShopItemShow.MapCard.name });
            return shop;
        }

        public static int GetTime()
        {
            // 获取回合数, ClockController.time 为 private
            // 改为获取 (GetDay() - 1) * 12 + TimeConve()
            var day = ClockController.GetInstance().GetDay();
            var conve = ClockController.GetInstance().TimeConve();
            return (day - 1) * 12 + conve;
        }

        public static void SetShop(string[] str)
        {
            // str[1] = X_Y
            // X,Y 为 Card 在地图的坐标
            string[] array = str[1].Split(new char[] { '_' });
            int x = int.Parse(array[0]);
            int y = int.Parse(array[1]);
            var card = ClientMaster.GetInstance().mapController.GetTableCard(new Vector2Int(x, y));
            if (card == null)
            {
                // card 不存在
                ShopItemShow.Log.LogError($"ShopMaster: not found card in {x},{y}");
                return;
            }

            ServerBuilding building;
            if (!shop.ContainsKey(card.no))
            {
                // 新建ServerBuilding
                building = new ServerBuilding();
                for (int i = 0; i < 8; i++)
                {
                    building.goods.Add(new Goods("NONE", 0, 0, 0));
                }
                shop.Add(card.no, building);
            }

            building = shop[card.no];
            // shopId = str[0]
            // X_Y = str[1]
            // item.id = str[3]
            // count = str[4]
            // prestige = str[5]
            // mana = str[6]
            // population = str[7] // 升级所需人口
            building.name = str[0];
            var index = 0;
            for (int i = 3; i < str.Length; i += 5)
            {
                building.goods[index].item = ItemDict.GetItem(str[i]);
                building.goods[index].count = int.Parse(str[i + 1]);
                building.goods[index].prestige = int.Parse(str[i + 2]);
                building.goods[index].mana = int.Parse(str[i + 3]);
                building.goods[index].population = int.Parse(str[i + 4]);
                index++;
            }
        }

        public static void ResetClock(string name)
        {
            // 重置对应商店的clock
            if (!clock.ContainsKey(name))
            {
                return;
            }
            // ShopItemShow.Log.LogInfo($"reset {name} clock");
            clock[name] = -1;
        }

        public static void ResetAllClock()
        {
            var keys = new List<string>();
            foreach (var name in clock.Keys)
            {
                keys.Add(name);
            }
            foreach (var name in keys)
            {
                ResetClock(name);
            }
        }

        public static void SendGoods(ServerPlayer player, Controll controll)
        {

            // 发送商店货物信息
            string[] array = controll.parameter.Split(new char[] { '_' });
            int x = int.Parse(array[0]);
            int y = int.Parse(array[1]);
            var card = ClientMaster.GetInstance().mapController.GetTableCard(new Vector2Int(x, y));
            if (card == null)
            {
                return;
            }
            var shop = ServerMaster.GetInstance().gamedata.shop;
            if (!shop.ContainsKey(card.no))
            {
                ShopItemShow.Log.LogError($"{x},{y} not found in gamedata.shop");
                return;
            }
            var building = shop[card.no];
            string text = "";
            text += building.name;
            text += ",";
            // 这一项原本为四周格子人口,我们更改为坐标,这样和之前版本兼容,又多出新特性
            text += building.card.posx.ToString() + "_" + building.card.posy.ToString();
            text += ",";
            text += building.goods.Count;
            foreach (Goods goods in building.goods)
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
            player.SendMessage(Response, new string[] { text });
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
        public static ItemData[] Items;
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
            Items = ItemManager.GetInstance().items;
            dict = new Dictionary<string, ItemData>();

            // 修复游戏数据错误
            // S0012 为 爆炎拳
            // S0014 为 定身咒
            // 游戏原始items.name 都为 蚀心掌
            // 游戏里刀字不知道用什么字体,或者就不是这个刀
            // 全部改成刀,方便制作查找功能
            foreach (var itemData in Items)
            {
                switch (itemData.id)
                {
                    case "S0012":
                        itemData.name = "爆炎拳";
                        break;
                    case "S0014":
                        itemData.name = "定身咒";
                        break;
                    case "L0100":
                        itemData.name = "岳王刀";
                        break;
                    case "L0101":
                        itemData.name = "关帝刀";
                        break;
                    case "L0102":
                        itemData.name = "令公刀";
                        break;
                    case "L0103":
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
