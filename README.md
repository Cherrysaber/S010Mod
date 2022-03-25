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
![image](https://github.com/Cherrysaber/S010Mod/blob/main/image/house.png)
![image](https://github.com/Cherrysaber/S010Mod/blob/main/image/root.png)

### v0.3.0
- bugfix: 修复多人游戏仙山洞府可以无限升级和自动升级

### v0.3.5 
- bugfix: 多人游戏非主机玩家无法正常显示 #1
- 需要主机也安装mod,非主机才能正常显示,如果主机没有安装mod,则显示原始提示
