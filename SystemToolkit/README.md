# SystemToolkit - 系统工具箱

> 基于 .NET 10 + WPF 的 Windows 系统工具集。菜单只展示已实现功能：文件管理、进程与监控（含服务/守护）、网络工具、系统工具（注册表/快速启动/系统信息/Hosts）、开发辅助与数据工具，共 26 个功能页面。

<div align="center">

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)
![C#](https://img.shields.io/badge/C%23-14.0-239120?style=flat-square&logo=c-sharp)
![WPF](https://img.shields.io/badge/WPF-Windows-007ACC?style=flat-square&logo=windows)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-lightgrey?style=flat-square)

</div>

> **仓库说明**: 本仓库包含 SystemToolkit 完整源代码、Core 服务、单元测试和项目文档。适用于 Windows 10/11 平台，采用 .NET 10.0 + WPF 技术栈，遵循 MIT 开源许可协议。

---

## 功能特性（已实现）

### 文件管理
- 批量重命名：正则 / 前缀后缀 / 搜索替换，支持预览
- 文件去重：哈希检测，每组保留第一个
- 大文件查找
- 目录对比：增/删/改/相同四类差异，可选哈希内容比对

### 进程与监控
- 进程监控（结束进程/设置优先级需确认）
- CPU/内存实时监控
- 磁盘空间分析与清理建议
- Windows 服务启停、启动类型设置（需确认）
- 告警与守护：资源阈值告警历史、进程守护（可配置崩溃自动重启）

### 网络工具
- Ping 测试
- 端口占用查看（netstat 关联进程）
- 端口扫描（限授权主机，单次最多 512 端口）
- 路由追踪（ICMP TTL 递增）
- HTTP 请求测试（GET/POST/PUT/PATCH/DELETE）
- 文件下载（进度条、可取消）

### 开发辅助
- AES 加解密 / Hash（文本与文件）/ Base64
- JSON/XML 格式化、正则测试与语法校验
- 代码生成（内置 C# 类/控制器、Python 函数、JSON 配置模板）
- 文本工具：命名风格转换、行排序/去重/去空行、全角半角、字数统计
- 离线工具箱（54 项）：JSON 校验/压缩/排序/指针展开还原、CSV/JSON 互转与 CSV 结构检查、JSON 转义、URL/HTML/HEX/Base32/Base64URL/二进制定长编解码、CRC32 校验、查询参数解析构建、时间戳与日期互转、Cron 五段展开、GUID 批量生成、随机密码、密码强度评估、整数进制转换、HEX/RGB/HSL 颜色转换、GZip 压缩解压、文本统计与字素反转、大小写风格转换、ROT13、摩斯电码编解码、正则转义、文本差异（LCS）、数字千分位格式化、秒数转时长、字节单位换算；全部本地执行、无网络依赖，输入输出设有体积与规模上限

### 数据工具
- CSV 解析预览与导出
- 日志文件分析（多格式时间戳解析、级别统计、重复错误告警）

### 系统工具
- 注册表工具：HKCU/HKLM 读取、自启动无效项扫描、分支导出 .reg 备份、.reg 恢复（UAC）、写入/删除值与键（均需确认）
- 快速启动：程序/URL/文件夹，管理员启动需确认
- 系统信息：OS 版本、CPU/内存、运行时长、磁盘容量、网络适配器（IP/MAC，只读）
- Hosts 编辑器：自动加载、语法检查、写前自动备份，保存需管理员

---

## 🚀 快速开始

### 环境要求
- Windows 10/11
- .NET 10.0 Runtime
- Visual Studio 2022/2026 (开发)

### 编译运行

```bash
# 克隆项目
git clone https://github.com/yourusername/SystemToolkit.git

# 进入项目目录
cd SystemToolkit

# 还原 NuGet 包
dotnet restore

# 编译项目
dotnet build -c Release

# 运行应用
dotnet run --project SystemToolkit.App
```

### 发布应用

```bash
# 发布为独立应用
dotnet publish SystemToolkit.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## 📦 项目结构

```
SystemToolkit/
├── SystemToolkit.App/              # 主应用程序 (WPF)
│   ├── ViewModels/                 # 导航视图模型
│   └── Views/                      # 功能页面
├── SystemToolkit.Core/             # 核心业务逻辑
│   ├── Models/                     # 数据模型
│   └── Services/                   # 业务服务
└── SystemToolkit.Tests/            # 单元测试
```

---

## 🛠️ 技术栈

- **框架**: .NET 10.0, WPF
- **架构**: MVVM (CommunityToolkit.MVVM)
- **日志**: Serilog
- **依赖注入**: Microsoft.Extensions.DependencyInjection
- **系统 API**: System.Management, System.ServiceProcess

---

## 📝 使用说明

### 文件批量重命名

1. 选择目标文件夹
2. 设置重命名规则（搜索/替换、前缀/后缀）
3. 点击"预览"查看效果
4. 确认无误后点击"执行重命名"

### 网络 Ping 测试

1. 输入目标主机地址（域名或 IP）
2. 点击"开始 Ping"
3. 查看往返时间、TTL 等信息

### AES 加密解密

1. 选择"AES 加解密"标签页
2. 设置密钥和 IV（初始化向量）
3. 输入要加密/解密的内容
4. 点击"加密"或"解密"按钮

---

## 🔐 权限说明

部分功能需要管理员权限：
- 注册表操作 (HKLM 部分)
- Windows 服务管理
- 进程终止
- 系统文件访问

应用会在需要时自动请求提升权限。

---

## 📄 许可证

MIT License - 详见 [LICENSE](LICENSE) 文件

---

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📧 联系

- **项目主页**: [GitHub](https://github.com/yourusername/SystemToolkit)
- **问题反馈**: [Issues](https://github.com/yourusername/SystemToolkit/issues)

---

## 🌟 致谢

感谢 [MonkeyCode](https://monkeycode-ai.com/?ic=019e4e77-519b-70dc-82ea-2a833e5e93da) 提供 AI 辅助开发支持。

---

<div align="center" style="margin-top: 20px;">

**邀请注册**: [MonkeyCode AI](https://monkeycode-ai.com/?ic=019e4e77-519b-70dc-82ea-2a833e5e93da)

Made with ❤️ by SystemToolkit Team

</div>
