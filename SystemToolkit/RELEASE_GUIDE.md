# GitHub Release 发布指南

## 📝 手动创建 v1.4.0 Release

由于 GitHub CLI 需要认证，请通过网页手动创建 Release：

### 步骤 1: 访问 Release 页面

打开：https://github.com/doubao01/NetTool/releases/new

### 步骤 2: 填写发布信息

**Tag version**: `v1.4.0`

**Target**: `master`

**Release title**: 
```
SystemToolkit v1.4.0 - 效率工具增强
```

### 步骤 3: 填写发布说明

复制以下内容到描述框：

```markdown
## 🎉 发布说明

SystemToolkit 1.4.0 正式发布！这是一款基于 .NET 10 + WPF 的 Windows 全能系统工具集。

---

## ✨ 主要功能

### 📁 文件管理
- ✅ 批量重命名（正则/前缀后缀/搜索替换）
- ✅ 文件去重（基于 SHA256 哈希）
- ✅ 大文件查找
- ✅ 目录对比（可选哈希内容比对）
- ✅ 图片批量处理（最长边/百分比缩放）

### 🔧 系统工具
- ✅ 注册表工具（读取、备份、恢复、写入删除需确认）
- ✅ 快速启动（程序/URL/文件夹）
- ✅ 系统信息（OS/CPU/内存/磁盘/网卡）
- ✅ Hosts 编辑器（语法检查与写前备份）
- ✅ Windows 服务启停控制
- ✅ 进程监控和管理

### 🌐 网络工具
- ✅ Ping 连通性测试
- ✅ 端口占用查看与端口扫描
- ✅ 路由追踪（ICMP TTL 递增）
- ✅ HTTP 请求测试
- ✅ 文件下载（可取消）

### 🛠️ 开发辅助
- ✅ 代码生成器（C#/Python/JSON 模板）
- ✅ JSON/XML 格式化、正则测试
- ✅ AES 加解密、Hash、Base64
- ✅ 文本工具（命名风格、行处理、全半角）
- ✅ 颜色工具（五格式互转、WCAG 对比度、调色板、屏幕取色）
- ✅ 格式转换（YAML/JSON、JSONPath、Markdown、二维码、图片 Base64）
- ✅ 离线工具箱（54 项本地转换与校验）

### 📊 数据处理
- ✅ CSV 解析预览与导出
- ✅ 日志文件分析和统计

### 📈 系统监控
- ✅ CPU/内存实时监控
- ✅ 磁盘空间分析
- ✅ 告警与守护（阈值告警、进程守护）

### ⚡ 效率工具
- ✅ 剪贴板历史（文本监听、搜索、固定、复制回写）
- ✅ 命令面板（Ctrl+K 快速跳转）

---

## 🚀 快速开始

### 下载安装
1. 从 Assets 下载构建产物
2. 解压到任意目录
3. 运行 `SystemToolkit.App.exe`

### 系统要求
- Windows 10/11 (64 位)
- .NET 10.0 Runtime
- 2GB+ RAM
- 100MB 磁盘空间

---

## 📥 安装方式

### 方式 1: 下载已编译版本（推荐）
从本 Release 的 Assets 下载可直接运行的版本。

### 方式 2: 从源码编译
```bash
git clone https://github.com/doubao01/NetTool.git
cd NetTool/SystemToolkit
dotnet restore
dotnet build -c Release
dotnet run --project SystemToolkit.App
```

---

## 🐛 已知问题

- ⚠️ 部分系统功能需要管理员权限
- ⚠️ 剪贴板历史仅支持文本内容
- ⚠️ 屏幕取色与图片缩放依赖 Windows GDI+

---

## 📚 文档

- [README](https://github.com/doubao01/NetTool#readme) - 项目说明
- [WIKI](https://github.com/doubao01/NetTool/blob/master/WIKI.md) - 使用指南和 FAQ
- [开发文档](https://github.com/doubao01/NetTool/blob/master/DEVELOPMENT.md) - 开发者参考
- [快速开始](https://github.com/doubao01/NetTool/blob/master/QUICKSTART.md) - 5 分钟上手

---

## 🔧 技术栈

- **框架**: .NET 10.0 + WPF
- **架构**: MVVM (CommunityToolkit.MVVM)
- **语言**: C# 14.0
- **测试**: xUnit + Moq
- **CI/CD**: GitHub Actions

---

## 📊 项目统计

- **功能页面**: 30 个
- **离线工具箱**: 54 项
- **单元测试**: 396 个（全部通过）
- **支持平台**: Windows 10/11 x64

---

## 🙏 致谢

感谢所有参与开发和测试的贡献者！

<div align="center">

**邀请注册**: [MonkeyCode AI](https://monkeycode-ai.com/?ic=019e4e77-519b-70dc-82ea-2a833e5e93da)

</div>

---

**发布日期**: 2026-11-26  
**版本**: v1.4.0  
**License**: MIT
```

### 步骤 4: 选择分类

- ✅ 勾选 "Set as the latest release"

### 步骤 5: 点击发布

点击绿色按钮 **"Publish release"**

---

## 🎯 后续自动化

CI/CD 工作流已配置完成，当创建带版本号的 tag 时会自动：

1. 在 Windows 环境编译
2. 运行单元测试
3. 发布单文件应用
4. 创建 Release 并上传构建产物

### 触发自动发布的条件

```bash
# 推送版本 tag 时自动创建 Release
git tag -a v1.0.1 -m "Release message"
git push origin v1.0.1
```

GitHub Actions 会自动创建 Release 并上传构建产物。

---

## 📦 构建产物说明

自动发布后，Assets 将包含：

- `SystemToolkit-win-x64.zip` - Windows x64 单文件版本
  - 包含所有依赖
  - 无需安装 .NET Runtime
  - 解压即用

---

**创建完成日期**: 2026-11-26

<div align="center">

**邀请注册**: [MonkeyCode AI](https://monkeycode-ai.com/?ic=019e4e77-519b-70dc-82ea-2a833e5e93da)

</div>
