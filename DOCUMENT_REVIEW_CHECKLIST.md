# 对外文档检查清单

检查日期：2026-07-28。检查依据为当前源码、项目文件和最近提交记录。

## 已归档

- `Expired/README.NET6编译说明.md` — **已过期**。内容只讨论 .NET 6 编译特殊处理；当前主程序目标框架为 `net10.0`，不宜作为公开入口文档。

## 新文档检查结果

- [ ] `Doc/newcfet2app 新命令行交互功能.md` — **建议保留**。`cli/CliParser.cs` 与 `Get/Set/Invoke/List/Goto/ExitCommand` 等实现对应，且项目已使用 `System.CommandLine` 完成命令与补全功能。发布前应按实际运行结果复核命令示例。
- [ ] `Doc/newcet2app 升级内容.md` — **建议保留为升级记录，不建议作为使用手册**。它描述 CLI 改造和项目升级背景；对外可保留在“变更记录”，但应避免把计划性措辞当作当前 API 承诺。
- [ ] `HowToMakeCfetThings.md` — **待确认**。它是唯一面向 Thing 开发者的教程，建议保留候选；但依赖 Core 的公开 API，应在 Core 文档定稿后做一次示例编译和术语复核。

## 发布前核对

- [ ] 新建一个面向 GitHub 的 `README.md`：项目用途、前置环境（.NET 10）、构建/运行步骤、CLI 示例、与 Core/通信模块的关系。
- [ ] 从仓库中排除 `bin/`、`obj/`、`dll/` 和用户级 `.user` 文件，再公开上传。
