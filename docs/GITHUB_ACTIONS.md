# GitHub Actions 编译说明

这个仓库已经配置了 Windows GitHub Actions 编译工作流。

## 重要前提
由于 AutoCAD .NET 插件依赖 Autodesk 提供的本机程序集，Actions 无法自己下载这些 DLL。

你需要把以下 3 个文件从你的 Windows AutoCAD 安装目录复制到仓库：

```text
lib/Autodesk/AcMgd.dll
lib/Autodesk/AcCoreMgd.dll
lib/Autodesk/AcDbMgd.dll
```

默认可从类似路径拷贝：

```text
C:\Program Files\Autodesk\AutoCAD 2018\
```

## 一旦这 3 个 DLL 入库后
GitHub Actions 就会：
1. 在 Windows runner 上启动
2. 执行 MSBuild
3. 编译 `XiaoLiPV.dll`
4. 将 DLL 作为 artifact 上传

## 触发方式
- push 到 `main` / `master`
- PR
- 手动 `Run workflow`

## 下载产物
进入 GitHub Actions 对应运行页面，下载 artifact：
- `XiaoLiPV-dll`
