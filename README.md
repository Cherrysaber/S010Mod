# 山河伏妖录Mod
基于 BepInEx 框架开发 https://github.com/BepInEx/BepInEx

1. 下载BepInEx模版
```bash
dotnet new -i BepInEx.Templates --nuget-source https://nuget.bepinex.dev/v3/index.json
```
2. 创建项目
```bash
dotnet new bepinex5plugin -n NewPlugin -T net46 -U 2018.4.23
```

---

## ShopItemShow (商店物品展示)

### v0.2.0
![image](https://raw.githubusercontent.com/Cherrysaber/S010Mod/main/image/house.png)
![image](https://raw.githubusercontent.com/Cherrysaber/S010Mod/main/image/root.png)

### v0.3.0
- bugfix: 修复多人游戏仙山洞府可以无限升级和自动升级

### v0.3.5 
- bugfix: 多人游戏非主机玩家无法正常显示 [#1](https://github.com/Cherrysaber/S010Mod/issues/1)
- 需要主机也安装mod,非主机才能正常显示,如果主机没有安装mod,则显示原始提示

### v0.3.9
- 仙山洞府补丁移动到FuturePlugin
- ItemDict 数据更新


## FuturePlugin (未来补丁)
游戏bug修复补丁

### v0.1.0
- 修复仙山洞府可以无限升级和自动升级的bug
- 修复多人游戏在开始投票界面后,有人还在其他战斗中,投票结束后游戏卡住的bug

### v0.2.0
- 结构调整

## BlackMarket (黑市Plus)

### v0.2.0
- 支持悬赏物品，公屏打字黑市寻找XX，悬赏物品购买刷新后出现，价格为30威望
- 支持模糊查找，多个结果随机选择一个

### v0.2.1
- 使用RNGCryptoServiceProvider生成随机数
- 更新黑市刷新物品池为游戏全物品池 (20%高级,30%中级,50%低级)
