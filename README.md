#  RDL-Core

[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Version](https://img.shields.io/badge/version-1.0.0-green.svg)](https://github.com/Ichi-Studio/RDLCore)

**RDL Modernization Architect** - 将文档模板（PDF/Word）转换为现代 RDLC 报表定义的智能转换引擎。

## 概述

RDL-Core 是一个专注于报表现代化的 .NET 10 解决方案，能够从 PDF 和 Word 文档模板中提取视觉元素和嵌入逻辑，并将其转换为符合 RDL 2016 规范的 RDLC 报表定义文件。

### 核心能力

- **文档解析**: Word (.docx) OpenXML 结构解析、PDF 几何与逻辑角色识别、OCR 文本提取
- **逻辑提取**: Word 域代码解析、PDF 交互脚本提取、条件分支与计算公式识别
- **RDLC 生成**: 符合 RDL 2016 Schema 规范、Tablix 动态数据区域构建、VBScript 表达式合成
- **验证服务**: XML Schema (XSD) 验证、表达式沙箱验证、视觉一致性测试

## 转换管道

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│  Perception │ -> │ Decompose   │ -> │  Synthesis  │ -> │ Translation │ -> │ Validation  │
│  文档感知    │    │  逻辑分解    │    │  Schema合成  │    │  表达式翻译  │    │  验证部署    │
└─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘
```

## 项目结构

```
RdlCore/
├── src/
│   ├── RdlCore.Abstractions/     # 接口、模型与枚举定义
│   ├── RdlCore.Parsing/          # 文档解析模块 (Word/PDF)
│   ├── RdlCore.Logic/            # 逻辑提取与翻译模块
│   ├── RdlCore.Generation/       # RDLC Schema 生成模块
│   ├── RdlCore.Rendering/        # 渲染与验证模块
│   ├── RdlCore.Agent/            # Agent 核心编排
│   ├── RdlCore.Cli/              # 命令行工具
│   └── RdlCore.WebApi/           # Web API 服务
├── tests/
│   ├── RdlCore.Parsing.Tests/
│   ├── RdlCore.Logic.Tests/
│   ├── RdlCore.Generation.Tests/
│   └── RdlCore.Integration.Tests/
├── Directory.Build.props
├── Directory.Packages.props
└── RdlCore.sln
```

## 快速开始

### 环境要求

- .NET 10 SDK
- Visual Studio 2022 17.x+ 或 VS Code

### 安装与构建

```bash
# 克隆仓库
git clone https://github.com/Ichi-Studio/RDLCore.git
cd RDLCore

# 还原依赖
dotnet restore

# 构建解决方案
dotnet build

# 运行测试
dotnet test
```

### CLI 使用

```bash
# 转换 Word 文档
-rdl convert Invoice.docx --dataset InvoiceData --output ./reports/

# 转换 PDF 文档
-rdl convert Report.pdf --dataset ReportData --output ./reports/

# 验证 RDLC 文件
-rdl validate ./reports/Invoice.rdlc
```

### Web API 使用

```bash
# 启动 Web API
cd src/RdlCore.WebApi
dotnet run
```

API 将在 `http://localhost:5000` 启动，提供以下端点：

| 端点 | 方法 | 描述 |
|------|------|------|
| `/api/convert` | POST | 上传文档进行转换 |
| `/api/validate` | POST | 验证 RDLC 文件 |
| `/api/preview` | POST | 预览转换结果 |

### 代码集成

```csharp
// 注册服务
services.AddRdlCoreParsing();
services.AddRdlCoreLogic();
services.AddRdlCoreGeneration();
services.AddRdlCoreRendering();
services.AddRdlCoreAgent();

// 使用转换管道
var pipeline = serviceProvider.GetRequiredService<IConversionPipeline>();
var result = await pipeline.ConvertAsync(documentStream, DocumentType.Word, options);
```

## 表达式转换示例

| 源格式 | 目标 RDL 表达式 |
|--------|----------------|
| `{ MERGEFIELD CustomerName }` | `=Fields!CustomerName.Value` |
| `{ IF Total > 100 "High" "Low" }` | `=IIf(Fields!Total.Value > 100, "High", "Low")` |
| `{ DATE \@ "yyyy-MM-dd" }` | `=Format(Globals!ExecutionTime, "yyyy-MM-dd")` |
| `{ PAGE }` | `=Globals!PageNumber` |

## 技术栈

| 库 | 版本 | 用途 |
|---|------|------|
| `DocumentFormat.OpenXml` | >= 3.0.0 | Word 文档解析 |
| `Syncfusion.Pdf.Net.Core` | >= 25.0.0 | PDF 解析与 OCR |
| `SkiaSharp` | >= 2.88.0 | 跨平台图像处理 |
| `ReportViewerCore.NETCore` | >= 15.1.0 | RDLC 渲染引擎 |
| `System.CommandLine` | >= 2.0.0 | CLI 命令解析 |

## 质量标准

| 指标 | 目标 | 测量方式 |
|------|------|---------|
| Schema 合规性 | 100% | XSD 验证 |
| 表达式语法准确率 | 100% | 解析器验证 |
| 布局保真度 (SSIM) | > 0.95 | 视觉对比 |
| 字段映射准确率 | > 99% | 单元测试 |

## 许可证

MIT License - 详见 [LICENSE](LICENSE) 文件

## 贡献

欢迎提交 Issue 和 Pull Request！

---

