# XiaoLiPV .NET DLL 侧边栏版

这是按 AutoCAD **NETLOAD + DLL + PaletteSet 侧边栏** 路线实现的源码工程。

## 当前目标
优先验证：
- DLL 能编译
- `NETLOAD` 能加载
- `XLPV` 能显示可停靠侧边栏

## 仓库结构

```text
.
├── .github/workflows/build.yml
├── docs/
│   ├── BUILD_AND_TEST.md
│   └── GITHUB_ACTIONS.md
├── lib/Autodesk/        # 需要你自己补 3 个 Autodesk DLL
├── src/
├── XiaoLiPV.csproj
└── README.md
```

## 编译方式

### 方式 1：GitHub Actions
先把以下文件提交到仓库：

- `lib/Autodesk/AcMgd.dll`
- `lib/Autodesk/AcCoreMgd.dll`
- `lib/Autodesk/AcDbMgd.dll`

然后 push，即可自动编译。

### 方式 2：本地 Visual Studio
- Visual Studio 2019/2022
- .NET Framework 4.8
- 修正引用路径或使用 `lib/Autodesk` 相对路径

## AutoCAD 中测试
1. 编译出 `XiaoLiPV.dll`
2. 打开 AutoCAD
3. 输入 `NETLOAD`
4. 加载 `XiaoLiPV.dll`
5. 输入 `XLPV`

## 命令
- `XLPV`
- `XL_PANEL`
- `XL_SHADOW`
- `XL_LAYOUT`
- `XL_CABLE`
- `XL_NAME`
- `XL_BRIDGE`
- `XL_PLINE`
- `XL_TEXT`

## 说明
当前这版首先解决 UI 形态问题：
- 从 AutoLISP/DCL 改为 DLL 侧边栏
- 业务算法后续再继续迁移
