# DeerFlow.WPF v1.4 发布说明

**发布日期**: 2026-11-26
**版本**: v1.4.0
**代号**: 交付收尾 - 让已建成的能力真正可用

---

## 本次主线

v1.3 完成了自我迭代系统的服务层与控件层，但多个控件没有接入页面，用户无法触达。v1.4 的目标是把这些能力交付到用户手里，并修掉一个启动即崩溃的问题。

---

## 修复

### P0 启动崩溃
`HealthMonitorControl` 引用了 4 个主题中不存在的资源键（`PrimaryColor`、`ProgressBarStyle`、`BooleanToVisibility`、`InverseBooleanToVisibility`），导致首页加载时抛 `XamlParseException`。已在 `DarkTheme.xaml` 与 `App.xaml` 中补齐资源与转换器。

### 空命令
首页「统计」「应用模式」「查看详情」三个命令此前只写日志。现在分别输出模式统计、应用确认与模式详情，结果在首页新增的「模式统计」卡片中展示。

---

## 新增与接线

### 回复反馈（接线）
- `FeedbackControl` 接入 `ChatPage`，生成完成后自动出现点赞/点踩
- 提交结果显示在控件内，清空对话时重置
- 修复原控件按钮无命令绑定、仅靠 code-behind 手动调用的问题

### 模式知识图谱（数据源替换）
- 移除硬编码示例数据，改读 `PatternMiner` 真实模式
- 布局改为按模式数量自适应的环形辐射
- 空库显示引导文案
- 节点点击同步 `HomeViewModel.SelectedPattern`，驱动「应用/详情」按钮可用状态

### 沙箱管理（新增页面）
- 新增「沙箱管理」菜单项与 `OpenSandboxPage`
- 复用既有 `OpenSandboxControl` 与 `OpenSandboxViewModel`，此前该功能完全不可达

### 告警中心（新增页面 + 持久化）
- 新增 `AlertStore`（SQLite + WAL + busy_timeout），告警重启不丢失
- `AlertService` 支持注入存储，触发/确认/删除/清理均同步落库；无存储时退回纯内存
- 新增「告警中心」页面：列表、详情、确认、删除、清除已确认、统计条

---

## 测试

- 新增 `AlertStoreTests` 8 项：落库读取、确认状态更新、删除、批量清理已确认、跨实例重载、服务与存储联动、无存储内存模式
- SystemToolkit 459 项测试全通过
- DeerFlow 目标框架为 `net10.0-windows`，CI 在 `windows-latest` 上运行完整测试

---

## 升级注意

- 菜单由 7 项增至 9 项（新增「沙箱管理」「告警中心」）
- 数据库新增 `alerts.db`，位于 `%LocalAppData%/DeerFlow.WPF/`
- 旧的 `experiences.db` 无需迁移

---

**维护者**: DeerFlow.WPF Team
**上次版本**: v1.3.0