# DeerFlow.WPF

AI 桌面超级智能体 — 基于 WPF + MVVM 的智能体编排桌面应用，集成 AIO Sandbox 多窗口任务隔离和仿 Trae Solo 暗色主题 UI。

![Version](https://img.shields.io/badge/version-1.3.0-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![WPF](https://img.shields.io/badge/WPF-MVVM-orange)
![Build](https://img.shields.io/badge/build-passing-brightgreen)

---

## 功能特性

| 模块 | 功能 | 技术实现 |
|------|------|----------|
| 💬 **智能对话** | SSE 流式聊天，支持取消生成 | IAsyncEnumerable + CancellationToken |
| 📋 **任务面板** | 多窗口独立进程隔离 | Process.Start + 文件系统沙箱 |
| 🤖 **智能体编排** | LLM 驱动的多轮任务执行循环 | AgentLoop + FunctionCallContent 手动工具执行 |
| 🧠 **长期记忆** | 事实库 / 用户画像 / 主动回忆 | 重要性评分 + 类型标签 |
| 🛠 **技能商店** | 社区技能安装与管理 | 模块化 SkillItem |
| ⚙ **系统设置** | 模型配置 / 本地推理 / 沙箱开关 | 即时保存与重置 |
| 🛡 **崩溃恢复** | 看门狗进程监控 | 3 秒检查 + 30 秒心跳 + 3 次重启 |
| 🔬 **性能诊断** | 内存快照对比分析 | 50 MB 泄漏阈值自动告警 |
| 🤖 **自我迭代** | 自动学习优化系统 | SelfReflection + PatternMiner + AutoEvolver |

## 智能体编排

### 执行循环
- **多轮推理循环** - 「推理 → 工具调用 → 观察回灌」直至模型给出完成标记
- **AI 目标分解** - 一键把总体目标分解为可执行的编排步骤（失败回退原目标）
- **子智能体并发** - 多个子目标按并发上限同时执行，逐步轨迹标注来源
- **工具执行循环** - 流式捕获工具调用，手动执行并记录耗时与失败，结果回灌下一轮
- **无进展保护** - 连续无新观察自动终止，避免空转
- **AI 执行总结** - 运行结束后生成简明总结（LLM 不可用时回退本地摘要）

## 自我迭代系统

### 核心功能
- **自动反馈记录** - 每次对话后自动记录成功率和执行数据
- **智能模式推荐** - 基于历史任务推荐最佳实践
- **经验记忆检索** - 相似任务主动提示历史经验
- **A/B 测试优化** - 自动演化和选择最优策略
- **健康监控仪表盘** - HomePage 实时显示系统健康指标

### 性能指标
| 操作 | 目标 | 实测 |
|------|------|------|
| 单次反思记录 | <100ms | ~50ms ✅ |
| 模式推荐响应 | <50ms | ~30ms ✅ |
| 经验检索响应 | <200ms | ~100ms ✅ |
| 健康统计查询 | <100ms | ~20ms ✅ |

### 使用方式
```
SELF_IMPROVE-get_system_health        # 查看系统健康
SELF_IMPROVE-recommend_pattern 代码任务  # 请求模式推荐
SELF_IMPROVE-retrieve_experiences 性能优化 # 检索历史经验
SELF_IMPROVE-reflect_on_recent_tasks  # 查看最近反思
```

## 技术栈

| 层 | 技术 |
|----|------|
| **框架** | .NET 10.0 / WPF |
| **架构** | MVVM (ViewModelBase + RelayCommand) |
| **DI** | Microsoft.Extensions.DependencyInjection |
| **HTTP** | HttpClient + SSE 流式响应 |
| **日志** | Serilog (文件轮转 10 MB 限制) |
| **数据库** | SQLite (Microsoft.Data.Sqlite) |
| **测试** | xUnit + Moq (28 用例) |
| **打包** | WiX Toolset MSI |
| **CI/CD** | GitHub Actions |

## 快速开始

### 环境要求

- Windows 10 / 11
- .NET 10.0 SDK
- Visual Studio 2022（可选，dotnet CLI 可替代）

### 构建与运行

```powershell
# 克隆仓库
git clone <repo-url>
cd DeerFlow.WPF

# 还原依赖并编译
dotnet restore
dotnet build --configuration Release

# 运行应用
dotnet run --project DeerFlow.WPF.csproj
```

### 运行测试

```powershell
# 运行所有单元测试（排除集成测试，避免启动真实进程）
dotnet test Tests/DeerFlow.WPF.Tests.csproj --filter "Category!=Integration"

# 运行全部测试（含集成测试，需要桌面环境）
dotnet test Tests/DeerFlow.WPF.Tests.csproj
```

## 项目结构

```
DeerFlow.WPF/
├── Core/                       # 基础组件
│   ├── ViewModelBase.cs        # MVVM 基类 (INotifyPropertyChanged + IDisposable)
│   ├── RelayCommand.cs         # 命令绑定 (泛型 + 非泛型)
│   └── AsyncObservableCollection.cs  # 批量更新 + 通知抑制
├── Models/                     # 数据模型
│   └── Models.cs               # TaskModel, ChatMessage, MemoryItem, SkillItem, AgentConfig
├── Services/                   # 业务服务
│   ├── ApiService.cs           # 后端 AI SSE 流式通信 (支持 CancellationToken)
│   ├── TaskWindowManager.cs    # 独立窗口进程生命周期管理
│   ├── SandboxManager.cs       # 文件系统隔离 + 命令白名单
│   ├── WatchdogService.cs      # 看门狗崩溃自动恢复
│   ├── LoggerService.cs        # Serilog 封装日志
│   └── MemorySnapshotService.cs  # 内存诊断快照 + 泄漏检测
├── ViewModels/                 # 视图模型
│   ├── MainViewModel.cs        # 主窗口（WeakReference 页面缓存）
│   └── PageViewModels.cs       # 7 个子页面 ViewModel
├── Views/
│   ├── Controls/               # 可复用控件
│   │   ├── TitleBarControl     # WindowChrome 自定义标题栏
│   │   ├── SidebarControl      # 图标垂直导航
│   │   └── StatusBarControl    # 任务计数 + 内存占用
│   ├── Pages/                  # 功能页面
│   │   ├── HomePage            # 欢迎页 + 快速操作 + 最近任务
│   │   ├── ChatPage            # 对话列表 + 输入（VirtualizingStackPanel）
│   │   ├── TaskPanelPage       # 任务创建表单 + 列表
│   │   ├── AgentOrchestrationPage  # 工作流编排
│   │   ├── MemoryPage          # 记忆库搜索 + 列表
│   │   ├── SkillsPage          # 技能卡片网格
│   │   └── SettingsPage        # 配置表单
│   └── Windows/
│       └── TaskWindow          # 独立沙箱任务窗口
├── Themes/
│   └── DarkTheme.xaml          # Trae Solo 暗色主题
├── Tests/                      # 测试项目
│   ├── Core/                   # ViewModelBase + RelayCommand 测试
│   └── Services/               # 全部服务层测试
├── Installer/
│   └── DeerFlow.WPF.wxs        # WiX MSI 打包配置
├── .github/workflows/
│   └── build.yml               # CI/CD 自动构建 + 测试
├── App.xaml / App.xaml.cs      # 应用程序入口 + DI 容器
├── MainWindow.xaml             # 主窗口（WindowChrome 无边框）
└── DeerFlow.WPF.csproj        # 项目配置
```

## 架构概览

```
┌─────────────────────────────────────────────┐
│                  MainWindow                   │
│  ┌──────────┬──────────────────────────────┐ │
│  │ Sidebar  │   ContentControl (7 pages)  │ │
│  │          │   Home / Chat / TaskPanel    │ │
│  │          │   Agent / Memory / Skills    │ │
│  │          │   Settings                  │ │
│  ├──────────┴──────────────────────────────┤ │
│  │              StatusBar                   │ │
│  └─────────────────────────────────────────┘ │
│                    +                          │
│  ┌─────────────────────────────────────┐     │
│  │  TaskWindow (独立进程 × N)           │     │
│  │  ┌─────────────────────────────┐    │     │
│  │  │ Sandbox (文件系统隔离)       │    │     │
│  │  │ workspace/ temp/ output/    │    │     │
│  │  └─────────────────────────────┘    │     │
│  └─────────────────────────────────────┘     │
└─────────────────────────────────────────────┘
```

## 设计亮点

### 1. AIO Sandbox 多窗口进程隔离
每个任务启动为独立 `DeerFlow.WPF.exe` 进程，拥有专属文件系统沙箱（`%LocalAppData%/DeerFlow.WPF/sandboxes/{taskId}`），命令白名单限制，2 秒级资源监控。

### 2. Trae Solo 暗色主题
- 背景色系：`#1E1E24` / `#25252C` / `#2A2A33`
- 强调色：`#6C5CE7`（紫色）
- WindowChrome + ControlTemplate 自定义无边框窗口，圆角 10px，DropShadow
- 最大化状态自动调整 Margin 并移除圆角

### 3. 防泄漏架构
- **WeakReference 页面缓存**：导航复用 ViewModel，GC 自动回收
- **IDisposable 全链路**：ViewModelBase → 各子类逐层释放事件订阅 + CancellationTokenSource
- **AsyncObservableCollection 通知抑制**：批量操作仅一次 CollectionChanged

### 4. 崩溃自动恢复
WatchdogService 以 3 秒间隔轮询进程存活 + 30 秒心跳超时检测，单进程最多自动重启 3 次，指数退避冷却期。

## 性能优化清单

| 维度 | 措施 | 收益 |
|------|------|------|
| ViewModel 泄漏 | WeakReference 页面缓存 | 导航零泄漏 |
| 资源释放 | IDisposable 模式 | 事件订阅清理 |
| UI 渲染 | ListBox + VirtualizingStackPanel | 大数据量 10x+ 性能 |
| 异步取消 | CancellationToken 全链路 | 避免无效网络请求 |
| 批量更新 | 通知抑制 | 减少 UI 重复刷新 |
| 内存诊断 | MemorySnapshotService | 50 MB 阈值预警 |

## 文档

| 文档 | 说明 |
|------|------|
| [开发文档](开发文档.md) | 架构设计、文件布局、API 参考、扩展指南 |
| [用户手册](用户手册.md) | 安装步骤、界面导航、功能说明、FAQ、快捷键 |
| [可行性研究报告](可行性研究报告.md) | 市场分析、技术路径、资源评估、风险应对 |
| [任务计划](task_plan.md) | 4 阶段完整开发计划 |
| [进度追踪](progress.md) | 28 任务全部完成 |
| [研究发现](findings.md) | 11 项工程实践发现与决策记录 |

## CI/CD

GitHub Actions 工作流在 push/PR 时自动执行：

```
检出代码 → .NET SDK → 还原依赖 → Release 编译 → 单元测试 → 上传产出
```

配置文件：[.github/workflows/build.yml](.github/workflows/build.yml)

## 许可证

待定
