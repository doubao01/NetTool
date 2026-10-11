# User Instruction Memory

This file records user instructions, preferences, and teachings for reference in future interactions.

## Format

### User Instruction Entry

[User Instruction Summary]
- Date: [YYYY-MM-DD]
- Context: [Mentioned scenario or time]
- Instructions:
  - [Content of user teaching or instruction, described line by line]

### Project Knowledge Entry

[Project Knowledge Summary]
- Date: [YYYY-MM-DD]
- Context: Discovered by Agent while performing [specific task description]
- Category: [Operations & Deployment|Build Methods|Testing Methods|Troubleshooting & Debugging|Workflow & Collaboration|Environment Configuration]
- Instructions:
  - [Specific knowledge points, described line by line]

## Deduplication Strategy
- Before adding a new entry, check for similar or identical instructions.
- If a duplicate is found, skip the new entry or merge it with the existing one.
- When merging, update the context or date information.
- This helps avoid redundant entries and keeps the memory file tidy.

## Entries

[Project Knowledge Summary]
- Date: 2026-10-10
- Context: Discovered by Agent while building and testing SystemToolkit on a Linux machine
- Category: Build Methods
- Instructions:
  - SystemToolkit 项目（Core/App/Tests）目标框架为 `net10.0-windows`，`SystemToolkit/Directory.Build.props` 已固化 `EnableWindowsTargeting=true`，非 Windows 平台直接构建/测试即可，无需额外传参。
  - 构建：`dotnet build SystemToolkit/SystemToolkit.sln -c Debug`（当前 0 警告 0 错误，StyleCop.Analyzers 已移除）。
  - 测试：`dotnet test SystemToolkit/SystemToolkit.Tests/SystemToolkit.Tests.csproj -c Debug`（xunit + FluentAssertions + Moq，313 用例；离线工具箱目录测试 `Catalog_ContainsExactlyFiftyFourUniqueToolsWithChineseMetadata` 会逐工具执行 Example，新工具示例必须可确定性运行）。
  - CI 在 `windows-latest` 上运行，会执行全部测试；Linux 上仅用于本地快速验证。

[Project Knowledge Summary]
- Date: 2026-10-10
- Context: Discovered by Agent while validating tests cross-platform
- Category: Environment Configuration
- Instructions:
  - 本开发环境无全局 dotnet。存在可用的 SDK：`/tmp/dotnet.CLQ7YIlBo/dotnet`（.NET 10.0.401），需设置 `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`（缺少 libicu）方可运行。
  - SystemToolkit 中依赖 Windows 专属能力的测试（注册表、WMI/系统信息、netstat 端口快照）已用 `OperatingSystem.IsWindows()` 前置返回守卫，保证测试套件在非 Windows 环境同样全绿。
  - `UtilityWorkbenchService.Compress` 对空输入需返回固定合法空 GZip 成员（`GZipStream` 零输入时不输出头部），否则解压校验失败。

[Project Knowledge Summary]
- Date: 2026-10-11
- Context: Discovered by Agent while adding batch tools to UtilityWorkbenchService
- Category: Troubleshooting & Debugging
- Instructions:
  - SystemToolkit.Tests 的 FluentAssertions `.Throw<T>().Where(...)` 表达式树内不允许 `is` 模式匹配（CS8122，即使 net10 目标）；用 `e.GetType() == typeof(X) || ...` 代替。
  - xUnit Theory 的 InlineData 值个数必须与方法参数（含可选参数展开口径）一致；同一 Theory 内不要混用不同长度的行。
  - 位流解码（Base32 类）用「解出字节后重新编码并与输入比对」做规范化校验时，构造非零尾部位反例必须先确认目标串不满足重新编码等价，否则会误判为无效。
  - 新增工具三件套同步点：Core 目录与 Execute 分派、Tests 目录断言（数量+id 清单）、MainViewModel 菜单文案与两份 README 计数。

[User Instruction Summary]
- Date: 2026-10-11
- Context: NetTool 仓库提交与推送
- Instructions:
  - 不要为改动创建功能分支，直接在 master 上提交并推送；临时分支需删除（本地与远程）。
