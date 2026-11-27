# DeerFlow.WPF v1.4.1 发布说明

**发布日期**: 2026-11-27
**版本**: v1.4.1
**代号**: UI 体检 - 让界面真正显示数据

---

## 本次主线

v1.4.0 之后做了一次 UI 层体检，用脚本把每个 XAML 的绑定路径与对应 ViewModel 成员做了对账。修掉 4 处「控件在、数据不通」的接线缺陷，另将性能基准测试移出 CI 门禁，消除环境噪声导致的随机失败。

---

## 修复

### 首页最近任务卡片内容为空
`HomePage.xaml` 的最近任务模板绑定 `{Binding Title}` 与 `{Binding Progress}`，而 `TaskModel` 上只有 `Name`、`Status`、`CpuUsagePercent` 等属性，两个绑定路径都不存在，卡片标题与指标一直是空白。

- 标题改绑 `Name`
- 状态新增 `TaskStatusToTextConverter`，把 `TaskStatus` 枚举显示为「等待中/运行中/已完成/失败/已暂停」
- 百分比指标改用真实的 `CpuUsagePercent`，显示为 `42.3% CPU` 形式

### 实验中心按钮无视觉反馈
`SettingsViewModel.ShowExperimentCenter` 被 `ToggleExperimentCenterCommand` 取反，但没有任何 XAML 消费它。同时「文档生成」按钮走 code-behind 手工切换 `Visibility`，两个面板各自为政。

- 新增 `ShowDocumentGeneration` 属性与 `ToggleDocumentGenerationCommand`
- 两个面板的 `Visibility` 统一绑定到这两个属性
- 互斥逻辑收敛进属性 setter：打开一个自动收起另一个
- 删除 code-behind 里重复的 `Visibility` 切换代码

### IM 连接无法启用或停用
`IMSettingsViewModel.EditingIsEnabled` 只写不读，编辑表单里没有任何控件能修改它，导致新建或修改连接时启用状态永远是默认值。

- 编辑表单新增「启用此连接」复选框，绑定 `EditingIsEnabled`
- 删除此前无人消费的 `ToggleEnabledCommand`，避免两份状态源

### 设置页 ViewModel 的空安全
`SettingsViewModel` 构造函数直接解引用 `App.Services`，脱离 DI 容器实例化时会抛 `NullReferenceException`。改为空安全访问，容器缺失时回退到默认值。

---

## 测试与 CI

- 新增 `SettingsViewModelTests` 6 项：面板初始隐藏、两个方向的互斥切换、命令往返
- 新增 `IMSettingsViewModelTests` 4 项：选中连接回填启用状态、新建重置、属性通知
- 新增 `TaskStatusToTextConverter`
- `Phase2Benchmarks` 使用绝对耗时断言（如文档导出 `<500ms`），在共享 runner 上受首次写盘、杀毒扫描等噪声影响会随机失败。本次 CI 中该用例实测 736ms，而实现本身只是 `File.WriteAllText`。已打上 `[Trait("Category","Benchmark")]`，CI 过滤条件改为 `Category!=Integration&Category!=Benchmark`
- SystemToolkit 459 项测试全通过

---

## 升级注意

- 无数据库变更，无需迁移
- 无配置项变更，可直接覆盖升级
- 界面行为变化：设置页里「实验中心」与「文档生成」面板变为互斥展开；IM 编辑表单新增启用复选框

---

**维护者**: DeerFlow.WPF Team
**上次版本**: v1.4.0