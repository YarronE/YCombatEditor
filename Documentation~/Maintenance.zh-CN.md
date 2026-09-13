# 源码、Release 与 Demo 同步维护

## 仓库结构

仓库根目录就是 Unity UPM 包：Runtime 是播放与数据代码，Editor 是编辑器，Integrations 是可选适配，Tests 是回归测试，Samples~ 是不含游戏美术的示例，Documentation~ 是文档，Tools~ 是维护工具。Releases~ 按版本保存 TGZ、源码 ZIP、清单和验证说明，不参与 Unity 编译。

包名继续使用 `com.ethan.act-action-editor`，避免已有资源与程序集引用因仓库更名而断开。仓库名称为 YCombatEditor；不因此批量改写命名空间、脚本 GUID 或现有动作资产。

## 唯一开发源与职责边界

维护者在动作 Demo 的 `Packages/com.ethan.act-action-editor` 中开发通用能力。YCombatEditor 保存供其他人使用的独立源码快照与版本化安装包。

- 应同步：轨道与事件模型、编辑交互、校验、通用预览、性能改进、可选适配接口、通用示例、测试与文档。
- 留在 Demo：具体角色或 Boss 的招式资产、伤害数值、AI/输入策略、关卡、美术音频、商店插件以及游戏专属接收器。
- 独立仓库有人修复源码时，应先审核并回移到 Demo 的包内，再进行下一次同步。不得用目录镜像强制覆盖独立修改。

每次通用改动的完成条件是：代码与相关测试完成、中文手册更新、CHANGELOG 更新、必要时更新本地规划，再同步独立源码。已发布安装包保持不变；需要新安装包时提升版本并重新构建、验证。

## 从 Demo 同步

在 Demo 根目录执行下列 PowerShell 命令，路径可替换为自己的工作目录：

```powershell
# 1. 导出指定文件清单；每次生成独立目录。
& Tools/ActionEditorValidation/Export-Preview.ps1

# 2. 验证刚生成的 TGZ，而不是只测试 Embedded 源码。
& Tools/ActionEditorValidation/Invoke-Tests.ps1 -UnityEditor '<Unity Editor exe>' -PackageArchive '<候选目录/package-name-version.tgz>' -BuildPlayer

# 3. 同步源码与制品，要求目标端仍与上次同步清单一致。
& Tools/ActionEditorValidation/Sync-YCombatEditor.ps1 -CandidateRoot '<候选目录>' -Destination '<YCombatEditor目录>'
```

首次同步已有空仓库时使用 `-Initialize`，仅允许覆盖初始 README；现有 LICENSE、.gitattributes 和 .git 保留。后续同步使用保存于目标目录的 `.ycombat-sync.json` 校验文件。目标端存在未回移的独立修改时脚本停止并列出路径。不会自动提交、推送或创建 GitHub Release。

仅同步开发源码时，最后一步增加 -SourceOnly（不复制安装包、不要求 release 验证结果）。复制当前源码不需要每次递增版本；新增或改变 Release 制品则必须使用新版本号。同版本下的制品若字节不同，脚本拒绝覆盖。提交前使用 `git diff` 核对源码和文档；发布时让 Git 标签对应经过测试的精确源码快照。

## 独立仓库自行构建与验证

需要 PowerShell 7、tar 和安装了 Windows Build Support 的 Unity；当前验证基线是 6000.3.6f1。

EditMode 测试包含真实 EditorWindow 重绘，批处理保留图形设备（不传 `-nographics`）。无可用图形设备的环境不能将这部分窗口验证标记为通过；批处理重绘也不等于人工观感验收。

```powershell
# 在独立仓库根目录运行；默认输出到 Releases~/<package.json版本>/。
& 'Tools~/Build-Release.ps1'

# 对 TGZ 创建临时隔离 Unity 工程，测试示例并构建 Windows Player。
& 'Tools~/Verify-Release.ps1' -UnityEditor '<Unity Editor exe>' -PackageArchive '<TGZ绝对路径>' -BuildPlayer
```

输出目录已存在时构建脚本拒绝覆盖，可用 `-OutputDirectory` 选择新的候选目录。Verify-Release 默认把测试项目与完整日志保存在系统临时目录的 YCombatEditorValidation 中，也可用 `-WorkRoot` 指定其他目录；不要指定到源码包内部。脚本不会在已打开的 Demo 上启动第二个 Unity。

构建脚本使用允许清单，仅打包编辑器包。它不会把 .git、旧 Releases~、Unity 缓存或游戏 Assets 加进包。SHA256SUMS.txt 可用于检查文件是否与此次导出一致；inventory.json 记录源码文件哈希。校验清单证明字节一致性，不证明源码权属或全部游戏功能已经实现。

## 发布与文档

每个 Release 应带有版本说明、TGZ、源码 ZIP、SHA256SUMS.txt、inventory.json 和简洁的 validation.json。Windows Player 构建是示例兼容性验证，安装编辑器应使用 TGZ，不能将示例 EXE 误作编辑器安装程序。

发布前检查 [ReleaseChecklist.zh-CN.md](ReleaseChecklist.zh-CN.md)。当前保留预览版本标识，既有代码来源核对和真实窗口验收未因此自动完成。实际 GitHub 上传、标签与 Release 状态需单独核实，不以本地文件存在代替在线发布。

轨道、按钮与默认值变动必须同次更新 [中文使用手册](UserGuide.zh-CN.md)；规划文档仅保存在本地，不纳入源码提交或安装包。新版本的历史说明写入 CHANGELOG，旧版本制品保持不可变。
