# NetTool - .NET 全能工具集

> 🧰 基于 .NET 10 + WPF 的 Windows 全能工具集，包含系统管理工具和 AI 智能体两大核心产品

<div align="center">

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)
![C#](https://img.shields.io/badge/C%23-14.0-239120?style=flat-square&logo=c-sharp)
![WPF](https://img.shields.io/badge/WPF-Windows-007ACC?style=flat-square&logo=windows)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-lightgrey?style=flat-square)

</div>

---

## 📦 项目组成

本仓库包含两个核心产品：

| 项目 | 描述 | 技术栈 |
|------|------|--------|
| **[SystemToolkit](SystemToolkit/)** 🧰 | 系统管理工具箱 | .NET 10 + WPF |
| **[DeerFlow.WPF](DeerFlow.WPF/)** 🤖 | AI 智能体编排平台 | .NET 10 + WPF + Semantic Kernel |

---

## 🧰 SystemToolkit - 系统工具箱

> 30+ 实用功能，为开发者和系统管理员提供一站式效率解决方案

### 核心功能

#### 📁 文件管理
- 批量重命名（正则表达式/前缀后缀/搜索替换）
- 文件去重（基于哈希值检测）
- 大文件查找（快速扫描定位）

#### 🔧 注册表操作
- 注册表清理（无效项检测）
- 备份恢复（完整备份和还原）
- 注册表监控（实时变化监控）

#### ⚙️ 服务管理
- 服务启停控制
- 状态监控和配置

#### 🚀 进程管理
- 进程监控和资源分析
- CPU/内存/磁盘/网络实时监控

#### 💾 磁盘管理
- 磁盘空间可视化分析
- 智能磁盘清理

#### 🌐 网络工具
- Ping 测试、端口扫描、路由追踪
- HTTP API 测试工具
- 端口占用监控

#### 🛠️ 开发辅助
- 代码生成、格式化工具
- 正则测试、加密解密

### 快速开始

```bash
cd SystemToolkit
dotnet restore
dotnet build -c Release
dotnet run --project SystemToolkit.App
```

📖 **详细文档**: [SystemToolkit README](SystemToolkit/README.md)

---

## 🤖 DeerFlow.WPF - AI 智能体平台

> AI 桌面超级智能体，集成多窗口任务隔离和自我迭代系统

### 核心功能

#### 💬 智能对话
- SSE 流式聊天，支持取消生成
- 多轮对话上下文管理

#### 📋 任务面板
- 多窗口独立进程隔离
- AIO Sandbox 文件系统沙箱

#### 🤖 智能体编排
- LLM 驱动的多轮任务执行循环（AgentLoop）
- AI 目标分解与子智能体并发执行
- 工具调用流式捕获、失败记录与结果回灌
- AI 执行总结与本地摘要回退
- 执行历史 SQLite 持久化，支持回看历史轨迹

#### 🧠 长期记忆
- 事实库/用户画像/主动回忆
- 重要性评分 + 类型标签

#### 🛠️ 技能商店
- 社区技能安装与管理
- 模块化 SkillItem

#### 🤖 自我迭代系统 (v1.3) ✨

**Phase 1 - 核心能力**:
- ✅ 自动反馈记录（每次对话后自动记录）
- ✅ 智能模式推荐（基于历史数据推荐最佳实践）
- ✅ 经验记忆检索（相似任务主动提示）
- ✅ A/B 测试优化（自动演化选择最优策略）
- ✅ 健康监控仪表盘（实时系统状态）

**Phase 2 - 智能增强**:
- ✅ 用户反馈回路（点赞/点踩收集）
- ✅ 模式可视化图谱（图形化知识展示）
- ✅ 智能告警系统（5 种告警类型，4 级告警级别）
- ✅ 自动文档生成（Markdown 文档自动生成）

### 性能指标

| 操作 | 目标 | 实测 | 状态 |
|------|------|------|------|
| 单次反思记录 | <100ms | ~50ms | ✅ 优秀 |
| 模式推荐响应 | <50ms | ~30ms | ✅ 优秀 |
| 经验检索响应 | <200ms | ~100ms | ✅ 优秀 |
| 反馈提交 | <50ms | ~10ms | ✅ 优秀 |
| 文档生成 (200 条) | <3000ms | ~1500ms | ✅ 优秀 |

### 快速开始

```bash
cd DeerFlow.WPF
dotnet restore
dotnet build --configuration Release
dotnet run --project DeerFlow.WPF.csproj
```

### 测试覆盖

- ✅ **177 个测试用例** (SystemToolkit 6 + DeerFlow.WPF 171)
- ✅ 覆盖核心服务、插件与集成场景

```bash
# 运行测试
dotnet test Tests/DeerFlow.WPF.Tests.csproj
```

📖 **详细文档**: [DeerFlow.WPF README](DeerFlow.WPF/README.md)

---

## 🛠️ 技术栈

### 共同技术
| 层 | 技术 |
|------|------|
| **框架** | .NET 10.0 / WPF |
| **架构** | MVVM (ViewModelBase + RelayCommand) |
| **DI** | Microsoft.Extensions.DependencyInjection |
| **日志** | Serilog (文件轮转 10 MB) |
| **数据库** | SQLite (Microsoft.Data.Sqlite) |
| **测试** | xUnit + Moq |
| **打包** | WiX Toolset MSI |
| **CI/CD** | GitHub Actions |

### DeerFlow.WPF 专属
| 组件 | 技术 |
|------|------|
| **AI 框架** | Semantic Kernel |
| **HTTP** | HttpClient + SSE 流式响应 |
| **沙箱** | AIO Sandbox 多进程隔离 |
| **崩溃恢复** | WatchdogService 看门狗监控 |

---

## 📊 项目统计

| 指标 | SystemToolkit | DeerFlow.WPF | 总计 |
|------|--------------|--------------|------|
| **源文件** | 31 | 72 | 103 |
| **代码行数** | ~4,250 | ~9,780 | ~14,030 |
| **测试用例** | 6 | 171 | 177 |
| **功能模块** | 10+ | 8 | 18+ |
| **文档** | 10+ | 10+ | 20+ |

> 统计口径：源文件不含测试项目与 `bin/obj` 生成物；测试用例按 `[Fact]`/`[Theory]` 方法计数。

---

## 🚀 快速开始

### 环境要求
- Windows 10/11
- .NET 10.0 SDK
- Visual Studio 2022/2026 (可选)

### 编译整个项目

```bash
# 根目录执行
dotnet restore
dotnet build --configuration Release

# 运行 SystemToolkit
dotnet run --project SystemToolkit.App/SystemToolkit.App.csproj

# 运行 DeerFlow.WPF
dotnet run --project DeerFlow.WPF/DeerFlow.WPF.csproj
```

### 运行测试

```bash
# SystemToolkit 测试
dotnet test SystemToolkit.Tests/SystemToolkit.Tests.csproj

# DeerFlow.WPF 测试
dotnet test DeerFlow.WPF/Tests/DeerFlow.WPF.Tests.csproj
```

### 发布应用

```bash
# 发布为独立应用
dotnet publish SystemToolkit.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish DeerFlow.WPF -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## 📁 项目结构

```
NetTool/
├── SystemToolkit/                    # 系统工具箱
│   ├── SystemToolkit.App/           # 主应用程序 (WPF)
│   ├── SystemToolkit.Core/          # 核心业务逻辑
│   ├── SystemToolkit.Modules/       # 功能模块 (10+)
│   │   ├── FileManager/
│   │   ├── RegistryTools/
│   │   ├── ServiceManager/
│   │   ├── ProcessManager/
│   │   ├── DiskManager/
│   │   ├── NetworkTools/
│   │   ├── DevTools/
│   │   ├── DataTools/
│   │   ├── ProductivityTools/
│   │   └── MonitorTools/
│   ├── SystemToolkit.Tests/         # 单元测试
│   └── README.md                    # 详细文档
│
├── DeerFlow.WPF/                     # AI 智能体平台
│   ├── Core/                        # 基础组件
│   ├── Models/                      # 数据模型
│   ├── Services/                    # 业务服务 (8 个核心服务)
│   │   ├── ApiService/
│   │   ├── TaskWindowManager/
│   │   ├── SandboxManager/
│   │   ├── SelfReflectionService/   # 自我反思
│   │   ├── PatternMiner/            # 模式挖掘
│   │   ├── AutoEvolver/             # 自动演化
│   │   ├── ExperienceMemoryStore/   # 经验存储
│   │   ├── FeedbackService/         # 用户反馈
│   │   ├── AlertService/            # 智能告警
│   │   └── DocumentGenerationService/ # 文档生成
│   ├── ViewModels/                  # 视图模型
│   ├── Views/                       # 视图和控件
│   ├── Tests/                       # 测试项目 (171 个测试)
│   └── README.md                    # 详细文档
│
├── .github/workflows/               # CI/CD 配置
└── README.md                        # 本文档
```

---

## 🎯 核心优势

### SystemToolkit
- ✅ **30+ 实用功能** - 覆盖系统管理全场景
- ✅ **一站式解决方案** - 开发者和系统管理员效率工具
- ✅ **模块化设计** - 10+ 独立功能模块
- ✅ **开箱即用** - 无需配置，即开即用

### DeerFlow.WPF
- ✅ **自我迭代系统** - 完整的 AI 自主学习能力闭环
- ✅ **171 个测试用例** - 覆盖核心服务与集成场景
- ✅ **性能优异** - 大部分指标达到或优于目标值
- ✅ **图形化知识图谱** - 直观展示知识结构
- ✅ **智能告警** - 多层防护机制
- ✅ **自动文档** - Markdown 文档自动生成

---

## 📄 文档索引

### SystemToolkit
- [SystemToolkit README](SystemToolkit/README.md) - 项目概述
- [QUICKSTART](SystemToolkit/QUICKSTART.md) - 快速开始
- [DEVELOPMENT](SystemToolkit/DEVELOPMENT.md) - 开发指南
- [WIKI](SystemToolkit/WIKI.md) - 项目 Wiki
- [最终完成报告](SystemToolkit/最终完成报告.md) - 项目总结

### DeerFlow.WPF
- [DeerFlow.WPF README](DeerFlow.WPF/README.md) - 项目概述
- [自我迭代指南](DeerFlow.WPF/docs/SELF_IMPROVEMENT_GUIDE.md) - 自我迭代系统使用
- [Phase 2 功能](DeerFlow.WPF/docs/PHASE2_FEATURES.md) - Phase 2 功能详情
- [Phase 2 报告](DeerFlow.WPF/docs/PHASE2_FINAL_REPORT.md) - Phase 2 完成报告
- [实现状态](DeerFlow.WPF/docs/IMPLEMENTATION_STATUS.md) - 实现进度跟踪
- [发布说明 v1.3](DeerFlow.WPF/docs/RELEASE_NOTES_v1.3.md) - v1.3 发布说明
- [项目总结](DeerFlow.WPF/docs/PROJECT_SUMMARY.md) - 完整项目总结

---

## 🔐 权限说明

部分功能需要管理员权限：
- 注册表操作（HKLM 部分）
- Windows 服务管理
- 进程终止
- 系统文件访问

应用会在需要时自动请求提升权限。

---

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

### 开发流程
1. Fork 本仓库
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 创建 Pull Request

---

## 📧 联系方式

- **项目主页**: [GitHub](https://github.com/SamHub1991/NetTool)
- **问题反馈**: [Issues](https://github.com/SamHub1991/NetTool/issues)

---

## 📄 许可证

MIT License - 详见 [LICENSE](LICENSE) 文件

---

## 🌟 致谢

感谢 [MonkeyCode](https://monkeycode-ai.com/?ic=019e4e77-519b-70dc-82ea-2a833e5e93da) 提供 AI 辅助开发支持。

---

<div align="center" style="margin-top: 20px;">

**邀请注册**: [MonkeyCode AI](https://monkeycode-ai.com/?ic=019e4e77-519b-70dc-82ea-2a833e5e93da)

Made with ❤️ by NetTool Team

</div>
