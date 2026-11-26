# SystemToolkit v1.4 提升计划

## 1. 竞品调研（GitHub 常用功能）

| 参考项目 | Star | 语言 | 高频功能 |
|---|---|---|---|
| microsoft/PowerToys | 141k | C#/C++ | 颜色取色器、PowerToys Run 命令面板、Image Resizer 图片批量缩放、Advanced Paste 剪贴板历史、环境变量编辑器、Hosts 编辑器、文本提取器 |
| DevToys-app/DevToys | 32k | C# | JSON/YAML 互转、JSONPath 测试、Markdown 预览、二维码生成、哈希校验、图片 Base64、SQL 格式化 |
| QL-Win/QuickLook | 26k | C# | 空格键快速预览文件 |
| srwi/EverythingToolbar | 15k | C# | 本地文件名极速搜索 |

结论：本项目已覆盖 Hosts 编辑、批量重命名、哈希与编码。相对 PowerToys/DevToys，缺口集中在**颜色工具、格式转换（YAML/Markdown/JSONPath/二维码）、图片批处理、剪贴板历史、全局命令面板**五处。

## 2. 本版范围（按价值排序）

### P1 颜色工具
- HEX/RGB/HSL/HSV/CMYK 五格式互转，输入自动识别
- WCAG 对比度计算与 AA/AAA 判定，推荐前景色
- 互补/三角/类似色/明度阶梯调色板生成
- Windows 屏幕取色（点击取色，非 Windows 隐藏该按钮）

### P2 格式转换中心
- YAML 转 JSON、JSON 转 YAML
- JSONPath 查询（支持 `$`、`.key`、`[n]`、`[*]`、`..key` 递归）
- Markdown 转 HTML（CommonMark + 表格/任务列表）
- 文本或文件转二维码 PNG
- 图片文件与 Base64 互转

### P3 图片批量处理
- 按最长边或百分比批量缩放
- 输出 JPG/PNG/BMP 与质量参数，保留原目录结构
- 处理前预览目标尺寸与预计体积，处理结果汇总

### P4 剪贴板历史
- 监听文本剪贴板，落库到现有 `IProductivityService`
- 搜索、固定、复制回剪贴板、清空
- 敏感内容提示

### P5 命令面板
- Ctrl+K 唤起搜索框，按名称/分类模糊匹配功能页
- 回车或点击直接跳转

## 3. 验收口径
- Core 层能力全部有 xUnit 覆盖，跨平台可跑
- Windows 专属能力（屏幕取色、GDI 图像处理）用 `OperatingSystem.IsWindows()` 守卫
- `dotnet build` 0 错误，`dotnet test` 全绿
- README 与实际菜单条目保持一致

## 4. 非目标
- 不引入需要管理员权限的新功能
- 不做网络请求类工具，保持离线