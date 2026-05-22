# 编译与测试建议

## 你当前最该先验证的目标
先不要追求完整业务逻辑，先验证这 3 件事：

1. `NETLOAD` 能成功加载 `XiaoLiPV.dll`
2. 输入 `XLPV` 后，能弹出一个可停靠的侧边栏
3. 点击按钮时，命令入口能触发

## 建议测试顺序

### 1. 编译
在 Visual Studio 中打开项目，修正 AutoCAD DLL 引用路径后编译。

### 2. NETLOAD
在 AutoCAD 里加载 `bin\\Release\\XiaoLiPV.dll`

### 3. 命令测试
依次输入：
- `XLPV`
- `XL_PANEL`
- `XL_SHADOW`
- `XL_LAYOUT`

### 4. 观察结果
如果 `XLPV` 能显示侧边栏，说明这条 DLL 路线是对的。

## 下一阶段建议
等侧边栏稳定显示后，再继续做：
- 阴影分析参数页
- 组件排布参数页
- 调 AutoCAD .NET API 画图
- 将旧 AutoLISP 算法逐步迁移到 C#
