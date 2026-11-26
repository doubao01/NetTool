# SystemToolkit - 快速开始指南

## 🚀 5 分钟快速上手

### 前置要求

在开始之前，请确保已安装：
- **.NET 10.0 SDK** ([下载链接](https://dotnet.microsoft.com/download/dotnet/10.0))
- **Visual Studio 2022** (推荐) 或 **VSCode**
- **Windows 10/11** 操作系统

### 步骤 1: 下载项目

```bash
git clone https://github.com/doubao01/NetTool.git
cd SystemToolkit
```

### 步骤 2: 还原依赖

```bash
dotnet restore
```

### 步骤 3: 编译项目

```bash
dotnet build -c Release
```

### 步骤 4: 运行应用

```bash
dotnet run --project SystemToolkit.App
```

---

## 📖 文档导航

### 用户文档
- **[README.md](README.md)** - 项目介绍和功能列表
- **[RELEASE_NOTES.md](RELEASE_NOTES.md)** - 版本说明和已知问题

### 开发文档
- **[DEVELOPMENT.md](DEVELOPMENT.md)** - 开发指南和架构说明
- **[PROJECT_COMPLETE.md](PROJECT_COMPLETE.md)** - 完成说明和代码统计

---

## 🎯 核心功能速览

### 文件管理
- 批量重命名文件
- 查找重复文件
- 扫描大文件

### 网络工具
- Ping 测试
- 端口扫描
- HTTP API 测试
- 路由追踪

### 系统监控
- CPU/内存实时监控
- 进程管理
- Windows 服务控制

### 开发辅助
- 代码生成器
- JSON/XML 格式化
- 加密解密工具

---

## 💡 使用示例

### 示例 1: 批量重命名图片

```
1. 左侧菜单：📁 文件管理 → 批量重命名
2. 选择目录：C:\Users\YourName\Pictures
3. 设置规则：搜索"IMG_" 替换为"Photo_"
4. 点击"加载文件"预览
5. 确认后点击"执行重命名"
```

### 示例 2: 监控系统性能

```
1. 左侧菜单：📈 系统监控 → CPU/内存
2. 实时查看使用率
3. 历史趋势图表
4. 告警通知
```

---

## 🐛 遇到问题？

### 常见问题解答

**Q: 运行时提示需要管理员权限？**
A: 部分功能（如服务管理、进程终止）需要管理员权限。右键点击应用，选择"以管理员身份运行"。

**Q: 某些功能显示"开发中"？**
A: 菜单只展示已实现功能，未接入 UI 的能力不会出现在导航中。

**Q: 如何报告 Bug？**
A: 访问 GitHub Issues: https://github.com/doubao01/NetTool/issues

---

## 🌟 邀请注册

**邀请注册**: [MonkeyCode AI](https://monkeycode-ai.com/?ic=019e4e77-519b-70dc-82ea-2a833e5e93da)

---

**文档版本**: 1.4.0  
**最后更新**: 2026-11-26
