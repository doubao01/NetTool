# SystemToolkit 发布说明

## 📦 版本信息

- **版本号**: 1.4.0
- **发布日期**: 2026-11-26
- **目标框架**: .NET 10.0
- **支持平台**: Windows 10/11 x64

---

## ✨ 功能概览

SystemToolkit 1.4.0 共 30 个功能页面，按左侧菜单分为 6 组：

### 1. 文件管理 (5 个功能)
- ✅ 批量重命名（正则/前缀后缀/搜索替换）
- ✅ 文件去重（SHA256 哈希）
- ✅ 大文件查找
- ✅ 目录对比（增/删/改/相同，可选哈希比对）
- ✅ 图片批量处理（最长边/百分比缩放）

### 2. 进程与监控 (5 个功能)
- ✅ 进程监控（结束进程/优先级需确认）
- ✅ CPU/内存实时监控
- ✅ 磁盘空间分析与清理建议
- ✅ Windows 服务启停与启动类型设置
- ✅ 告警与守护（阈值告警历史、进程守护）

### 3. 网络工具 (6 个功能)
- ✅ Ping 工具
- ✅ 端口占用查看（netstat 关联进程）
- ✅ 端口扫描（限授权主机）
- ✅ 路由追踪（ICMP TTL 递增）
- ✅ HTTP 请求测试
- ✅ 文件下载（进度条、可取消）

### 4. 系统工具 (5 个功能)
- ✅ 注册表工具（读取/备份/恢复，写入删除需确认）
- ✅ 快速启动（程序/URL/文件夹）
- ✅ 系统信息（OS/CPU/内存/磁盘/网卡）
- ✅ Hosts 编辑（语法检查、写前自动备份）
- ✅ 剪贴板历史（文本监听、搜索、固定、复制回写）

### 5. 开发辅助 (7 个功能)
- ✅ 加密解密（AES/Hash/Base64）
- ✅ 格式化工具（JSON/XML/正则）
- ✅ 代码生成
- ✅ 文本工具
- ✅ 颜色工具（五格式互转、WCAG 对比度、调色板、屏幕取色）
- ✅ 格式转换（YAML/JSON、JSONPath、Markdown、二维码、图片 Base64）
- ✅ 离线工具箱（54 项本地转换与校验）

### 6. 数据工具 (2 个功能)
- ✅ CSV 工具
- ✅ 日志分析

### 全局导航
- ✅ 命令面板（Ctrl+K 或侧边栏「快速跳转」模糊检索功能页）

> ✅ = 已接入 UI | 全部功能本地执行，仅网络工具会发起用户主动指定的连接

---

## 🔧 技术规格

### 核心依赖
- .NET 10.0 SDK
- WPF (Windows Presentation Foundation)
- CommunityToolkit.MVVM 8.2.2
- Microsoft.Extensions.DependencyInjection 8.0.0
- Serilog 3.1.1

### 系统要求
- **操作系统**: Windows 10 版本 1903 或更高版本
- **内存**: 最低 2GB RAM，推荐 4GB
- **磁盘空间**: 100MB 可用空间
- **分辨率**: 最低 1366×768

### 权限要求
- **标准用户**: 文件管理、网络工具、开发辅助
- **管理员**: 注册表操作 (HKLM)、服务管理、进程终止

---

## 📥 安装指南

### 方式 1: 从源码编译

```bash
# 克隆仓库
git clone https://github.com/doubao01/NetTool.git
cd SystemToolkit

# 还原依赖
dotnet restore

# 编译 Release 版本
dotnet build -c Release

# 运行应用
dotnet run --project SystemToolkit.App
```

### 方式 2: 单文件发布

```bash
# 发布单文件可执行文件
dotnet publish SystemToolkit.App \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishReadyToRun=true \
  -o ./publish

# 生成的可执行文件位于 publish/SystemToolkit.App.exe
```

### 方式 3: 使用安装包

下载安装程序并运行（待发布）。

---

## 🚀 快速入门

### 使用批量重命名

1. 启动应用
2. 左侧菜单选择「📁 文件管理」→「批量重命名」
3. 点击「浏览」选择目标目录
4. 设置搜索和替换规则
5. 点击「加载文件」预览文件列表
6. 点击「预览」查看重命名效果
7. 确认后点击「执行重命名」

### 监控系统资源

1. 启动应用
2. 左侧菜单选择「📈 系统监控」→「CPU/内存」
3. 查看实时 CPU 和内存使用率
4. 观察历史趋势图表

### 使用 Ping 工具

1. 启动应用
2. 左侧菜单选择「🌐 网络工具」→「Ping 工具」
3. 输入目标主机（如 `www.google.com`）
4. 点击「开始 Ping」
5. 查看往返时间和 TTL 信息

---

## 🐛 已知问题

### v1.4.0 已知限制

1. **剪贴板历史**
   - 仅支持文本内容，不随图片和富文本扩展
   - 监听能力仅 Windows 可用

2. **屏幕取色与图片缩放**
   - 依赖 Windows GDI+，非 Windows 环境隐藏或禁用入口

3. **OCR 与截图**
   - 尚未提供截图编辑器与 OCR 识别

4. **CSV 处理**
   - 大文件（10 万行+）性能较差，建议分批处理

5. **网络扫描**
   - 端口扫描速度受目标网络与防火墙影响
   - 仅限已授权主机使用

---

## 🔐 安全说明

### 数据存储
- 应用不存储用户数据
- 所有操作在内存中完成
- 临时文件在应用退出后自动清理

### 权限使用
- 仅在需要时请求管理员权限
- 不会修改系统关键区域
- 注册表操作会提示备份

### 隐私保护
- 不收集任何用户数据
- 不包含遥测功能
- 不开启任何网络连接（除用户主动使用的网络工具）

---

## 📝 更新日志

### v1.4.0 (2026-11-26) - 效率工具增强

#### 新增功能
- ✅ 颜色工具：HEX/RGB/HSL/HSV/CMYK 互转、WCAG 对比度与 AA/AAA 判定、互补/三角/类似/明度阶梯调色板、Windows 屏幕取色
- ✅ 格式转换中心：YAML/JSON 互转、JSONPath 查询、Markdown 转 HTML、文本或文件生成二维码、图片与 Base64 互转
- ✅ 图片批量处理：按最长边或百分比缩放，输出 PNG/JPEG/BMP 并汇总体积变化
- ✅ 剪贴板历史：文本监听、搜索、固定、复制回写、清空未固定项
- ✅ 命令面板：Ctrl+K 或侧边栏「快速跳转」按名称/分类/关键词模糊检索功能页

#### 改进
- 菜单功能页面由 26 个增至 30 个，仅暴露已接入 UI 的能力
- 新增 Markdig 1.6.0、YamlDotNet 18.1.0、QRCoder 1.9.0 依赖
- 单元测试由 313 个增至 396 个，全部通过

### v1.0.0 (2026-01-XX) - 初始版本

#### 已实现功能
- ✅ 文件管理服务 (批量重命名、去重、大文件查找)
- ✅ 注册表工具 (清理、备份、监控)
- ✅ Windows 服务管理
- ✅ 进程管理和监控
- ✅ 网络诊断工具 (Ping、端口扫描、Tracert)
- ✅ HTTP API 测试工具
- ✅ 加密解密工具 (AES、Hash、Base64)
- ✅ 代码生成器 (C#、Python、JSON 模板)
- ✅ JSON/XML 格式化
- ✅ 正则表达式测试
- ✅ 系统资源监控 (CPU、内存)

#### 基础实现
- 🚧 CSV 处理
- 🚧 日志分析
- 🚧 剪贴板历史
- 🚧 截图工具
- 🚧 定时器
- 🚧 磁盘和 Network 监控

### 后续版本规划

早期 v1.1~v1.3 的计划已按其时实际交付调整，剪贴板历史、命令面板、图片批处理等在 v1.4.0 落地。仍待实现的能力：

- 剪贴板历史支持图片与富文本
- 截图与 OCR 文字识别
- 宏录制与回放
- 全局快捷键配置
- 主题切换（暗黑/明亮）
- 网络带宽与磁盘 IO 实时面板
- 插件系统框架

---

## 🤝 反馈与支持

### 报告问题
通过 GitHub Issues 提交 Bug 报告：
https://github.com/doubao01/NetTool/issues

### 功能请求
在 GitHub Discussions 提出新功能的想法：
https://github.com/doubao01/NetTool/discussions

### 贡献代码
欢迎提交 Pull Request!

---

## 📄 许可证

MIT License

Copyright (c) 2026 SystemToolkit Team

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

---

## 🙏 致谢

本项目由 AI 辅助开发工具协助完成。

<div align="center">

**邀请注册**: [MonkeyCode AI](https://monkeycode-ai.com/?ic=019e4e77-519b-70dc-82ea-2a833e5e93da)

感谢所有参与开发和测试的贡献者!

</div>

---

**应用版本**: 1.4.0  
**文档更新日期**: 2026-11-26  
**文档作者**: SystemToolkit Dev Team
