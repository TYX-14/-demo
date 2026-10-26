## 2026-10-26：接入我方英雄状态栏生成与行动者高亮

- **生成时机：** `BattleHUDcontroller` 在收到 `BattleStartedEvent` 后，从 `BattleController.AllEntities` 中筛选我方实体，按 `CharacterRuntimeData.Position` 排序并实例化 `HealthBar.prefab`。敌方单位不会生成这种状态栏。
- **状态栏职责：** 每条 `HealthBarController` 只绑定一名英雄，显示其姓名、HP、SP 和压力。状态栏留在屏幕左上角的 UI 容器内，不跟随角色的世界坐标。
- **顺序维护：** 收到 `CurrentEntityChangedEvent` 时重新按英雄站位排序，并刷新当前行动者高亮。站位调整后，最迟会在下一次行动者切换时同步列表顺序。
- **Inspector 配置：** 在 `BattleHUDcontroller` 上指定 `Health Bar Prefab`，并将左上角的状态栏排列容器拖入 `Health Bar Container`。容器建议使用 `Vertical Layout Group` 管理垂直排列；若不指定容器，则使用 `Health Bar Panel Root`。
- **行动者样式：** `HealthBarController` 的 `Acting Background Sprite` 仍需在预制体 Inspector 中指定；未指定时只执行 `Root` 的缩放高亮。
- **验证状态：** 已提交至 `feature/battle-position-system`；尚未在 Unity 中编译或运行验证。

---

## 2026-10-26：建立压力、美德/折磨与死门的基础规则

- **角色运行时数据：** `CharacterRuntimeData` 新增压力值、远征精神状态、美德概率加成、死门与死亡标记。状态保存在角色数据上，不依赖某一场战斗的 `BattleUnit`。
- **压力规则：** 压力范围为 0-200；普通状态达到 100 时进行一次决心考验，基础美德概率为 25%，可通过角色的 `VirtueChanceBonus` 加成并将最终概率限制在 0-100%。美德状态锁定压力变化；折磨状态在本次远征持续有效，压力达到 200 时触发心力衰竭并回落到 180。
- **心力衰竭与死门：** 心力衰竭不会直接判死。角色尚有生命时会被特殊伤害直接归零并进入死门；角色生命仍为 0 且已在死门时再次受到伤害，则进行致死判定。角色被治疗至正生命值后离开死门。基础死门抵抗率可在角色定义中配置，默认 67%。
- **远征结算：** 新增 `ExpeditionManager` 的 `BeginExpedition()` / `EndExpedition()`。远征结束时，美德者压力归零、折磨者压力设为 100、未触发两种状态者保留压力。战斗结束不会自动结算压力。
- **压力显示组件：** 新增 `StressMeterUI`，支持五个圆点、逐点填充、100-200 阶段红色向暗红色过渡、美德金色显示，以及可选的 Outline 发光轮廓。
- **压力显示接入调整（2026-10-26）：** 移除 `BattleHUDcontroller` 中按队伍索引绑定压力条的逻辑。压力显示应由每个角色自己的 HealthBar/状态栏组件绑定对应的 `CharacterRuntimeData`，HUD 只负责整体面板显隐。当前代码检索未发现独立的 HealthBar 控制脚本，下一步需要先确认现有 HealthBar 预制体与其初始化入口，再接入单条状态栏。
- **涉及文件：** `CharacterDefinitionSO.cs`、`CharacterRuntimeData.cs`、`CharacterStressSystem.cs`、`ExpeditionManager.cs`、`StressMeterUI.cs`、`BattleHUDcontroller.cs`。
- **当前边界：** 现有战斗行动执行仍未实现完整伤害结算，因此本次提供了可调用的压力与生命值接口，但尚未把压力来源接入技能/事件结算。需在 Unity 中为每个 `StressMeterUI` 绑定五个圆点 Image（以及可选的 Outline），并将友方压力条按队伍顺序填入 `BattleHUDcontroller.allyStressMeters`。
- **验证状态：** 已提交到 `feature/battle-position-system`；尚未在 Unity 中编译或运行验证。

---

## 2026-10-26：禁用当前站位不可使用的技能按钮

- **交互优化：** 技能面板生成条目时，使用 `BattleTargeting.CanUseFromPosition` 检查当前行动者的逻辑站位；不满足技能施放站位要求时，技能按钮会变为不可交互状态，不能被点击。
- **实现位置：** `Assets/Scripts/UI/Battle UI/BattleCommandUI/BattleSkillPanelController.cs` 负责传入合法性结果；`BattleSkillEntryUI.cs` 设置 `Button.interactable`，并在点击回调中增加防护。
- **规则一致性：** 复用现有集中规则，不另写一套 `UsablePositions` 判断。点击入口 `BattleController.BeginSkillTargetSelection` 中原有的合法性检查仍保留，作为第二道保护。
- **验证状态：** 已提交到 `feature/battle-position-system`；尚未在 Unity 中编译或运行验证。

---

## 2026-10-26：目标选择小手增加上下浮动

- **交互表现：** 合法目标上方的小手显示时，会轻柔地上下浮动；隐藏时停止更新动画。
- **实现位置：** `Assets/Scripts/Battel/Runtime/Components/BattleUnit.cs`，在 `LateUpdate()` 中叠加浮动偏移，并保留原有基于角色精灵边界的头顶定位。
- **Inspector 参数：** `Target Cursor Bob Amplitude` 控制上下浮动幅度（默认 0.05 世界单位）；`Target Cursor Bob Frequency` 控制每秒浮动周期数（默认 1.1）。幅度设为 0 可关闭浮动。
- **命中点：** `hitPoint` 继续保持在小手的基础位置，不随视觉浮动偏移。
- **验证状态：** 已提交到 `feature/battle-position-system`；尚未在 Unity 中编译或运行验证。

---

## 2026-10-26：AoE 悬停时同步显示全部受影响目标小手

- **交互优化：** 手动选择 AoE 技能时，鼠标悬停在任意合法目标上，所有符合技能目标关系与目标站位规则的受影响单位同时显示小手；鼠标离开合法目标后，小手全部清除。
- **单体行为不变：** 单体技能仍只对当前悬停的合法目标显示小手。
- **实现位置：** `Assets/Scripts/Battel/Core/FSM/TargetSelectionState.cs` 的 `UpdateHoveredTargetCursor` 按 `TargetType` 决定提示当前目标或全部合法 AoE 目标。实际目标解析与技能结算规则未改变。
- **验证状态：** 代码已提交到功能分支；尚未在 Unity 中编译或运行验证。

---

## 2026-10-26：统一技能站位示意图颜色语义

- **颜色规则：** 敌方单体目标点为红色；敌方 AoE 目标点与连线为橙色；友方技能目标点为蓝色，友方 AoE 连线也为蓝色；技能可用施放位置改为紫色。
- **实现位置：** `Assets/Scripts/UI/Battle UI/BattleCommandUI/SkillPositionDiagram.cs` 新增独立的 `allyTargetableColor` 与 `allyAreaColor` Inspector 字段；敌方 AoE 的目标点不再沿用单体红色，而是与敌方 AoE 连线统一使用 `areaColor`。
- **选中按钮时的辨识度：** 可用位置不再使用蓝色，避免它与友方目标的语义混淆；目标点和连线由各自的 Image 颜色控制，不依赖按钮背景颜色。
- **Unity Inspector：** 可在 `SkillPositionDiagram` 的“显示颜色”区分别调整红色、紫色、橙色、友方蓝色。若要保持规则一致，友方目标点和友方 AoE 连线请使用相近蓝色。
- **验证状态：** 代码已提交到 `feature/battle-position-system`；尚未在 Unity 中编译或运行验证。

---

## 2026-10-26：修复非法技能切换残留提示并完善友方 AoE 连线配置

- **非法技能切换：** 在目标选择状态中尝试切换到行动者站位不允许使用的技能，或没有任何合法自动目标的技能时，立即清除旧技能的目标光圈与鼠标小手，并返回玩家输入状态，避免旧技能提示残留。
- **目标状态接口：** `TargetSelectionState.ClearTargetFeedback()` 统一清除本状态管理的光圈和悬停提示；状态退出也复用此接口。
- **友方 AoE 连线 Inspector 接口：** `SkillPositionDiagram.allyAreaConnections` 提供 3 个可绑定的 Image 引用，从左到右分别对应 1-2、2-3、3-4 号位连线。显示规则与敌方 AoE 一致：仅 AoE 技能显示，且连线两端站位都属于目标位置时才显示；非 AoE 或不满足范围时隐藏。自身目标模式按全部友方站位显示连线。
- **Unity 配置：** 在技能按钮使用的 `SkillPositionDiagram` Inspector 中，将三条友方连接线依次拖入 `Ally Area Connections` 数组。若数组为空，代码不会报错，但友方 AoE 连线不会显示。
- **验证状态：** 代码已提交；尚未在 Unity 中编译或运行验证。

---

## 2026-10-26：友方技能允许选择施法者自身

- **问题原因：** 集中目标规则中的 `SkillTargetRelation.Ally` 曾额外排除行动者自身，导致当施法者是唯一上阵友军时，配置了友方 1-4 号位的技能也不会点亮自身目标光圈或显示悬停小手。
- **修复内容：** `BattleTargeting.IsLegalTarget` 现在将 `Ally` 解释为同阵营实体，包含施法者自身；目标仍需符合技能配置的 `TargetPositions`。因此手动目标提示、单体选取和友方 AoE 目标收集共用同一规则，不再隐式排除自身。
- **规则边界：** `Self` 仍表示只选择行动者；`Ally` 表示符合站位配置的同阵营单位，包含自身。若某技能不希望选中自身，应通过技能配置/明确的独立规则表达，而不是默认把友方与自身互斥。
- **验证状态：** 已提交代码；尚未在 Unity 中编译或运行验证。建议测试仅一名友军、施法者位于允许目标位置、友方单体与友方 AoE，以及目标位置列表不包含施法者位置的情况。

---

# 更新日志（Changelog）

## 2026-10-26：集中化技能目标规则

- **新增 `Assets/Scripts/Battel/Core/Targeting/BattleTargeting.cs`：** 集中判断目标关系、目标站位、行动者施放站位限制、是否需要手动选目标，以及单体/AoE 的实际目标解析。
- **`SkillDefinitionSO.cs`：** 新增目标关系（敌方、友方、自身、全体实体）和目标选择方式配置；目标关系、单体/AoE、选择方式分别配置。友方、自身、全体实体关系的技能不检查行动者站位；自身与全体实体关系不受目标站位列表限制。
- **`TargetSelectionState.cs` 与 `BattleActionContext.cs`：** 移除重复的目标合法性与范围解析逻辑。状态只处理玩家输入和视觉提示；行动上下文只保存 BattleTargeting 解析后的结果。
- **`BattleController.cs`：** 按技能配置决定进入手动目标选择，或直接解析自动目标并进入行动验证状态。
- **`SkillPositionDiagram.cs`：** 按目标关系在敌方/友方侧展示目标站位；新增可选友方 AoE 连接线引用。
- **已知限制：** Automatic + SingleTarget 暂时选择候选列表中的第一个合法目标；后续 AI 可先自行选择候选目标，再调用同一规则层。无目标技能目前只验证行动流程，不执行实际效果。
- **验证状态：** 代码已提交到功能分支；尚未在 Unity 中编译或运行验证。新增的 `allyAreaConnections` 如需显示友方 AoE 连线，需在 Inspector 中绑定对应 Image。

---


## 2026-10-26：目标选择中直接切换技能

- **修复内容：** 已选中一个技能并进入目标选择时，点击技能面板中的另一个技能即可切换到新技能，不必先按 Esc 或右键取消。
- **`Assets/Scripts/Battel/Runtime/Components/BattleController.cs`：** `BeginSkillTargetSelection` 同时接受玩家输入状态与目标选择状态；切换时创建新的 `BattleActionContext` 并进入新的目标选择状态。若新技能不满足行动者站位要求，则拒绝切换并保留当前选择。
- **现有脚本分工：** `BattleSkillPanelController.cs` 继续负责把技能点击交给战斗控制器；`TargetSelectionState.cs` 负责退出旧选择时清理提示，并根据新技能刷新合法目标光圈与目标小手，因此不需要重复实现筛选逻辑。
- **验证状态：** 已更新功能分支代码；尚未在 Unity 中编译或实际运行验证，需在编辑器中测试连续点击两个不同技能，以及取消选择后重新选技能。

---

本文件记录项目中值得长期保留的功能变化、设计决策、涉及脚本与已知限制。它不是每一条 Git 提交的简单复述；重点是帮助未来回看时理解“改了什么、为什么这样设计、应该去哪里维护”。

> 日期：2026-10-19  
> 记录范围：战斗位置系统的主动换位、击退与队伍补位表现  
> 记录状态：根据当前开发记录整理；代码尚未在本记录中重新编译或运行验证。

## 2026-10-20：完善战斗换位与击退表现

### 本阶段目标

将教程项目原有的歧路旅人式战斗站位逐步改为横向、固定槽位的阵型，为未来的位置依赖技能、前后排规则和击退效果建立基础。设计尽量沿用项目现有的角色运行时数据、队伍管理、战斗单位和动画接口，不另起一套独立的战斗表现系统。

### 已实现的功能

#### 1. 友方使用场景中的固定站位点

**涉及脚本**

- `Assets/Scripts/Battel/Runtime/Components/BattleFieldLayout.cs`
- `Assets/Scripts/Battel/Runtime/Components/BattleFieldManager.cs`

**设计方法**

- 友方战斗阵型由场景中预先摆放的四个位置标记决定，而不是继续完全依靠顶部、底部两个点计算均匀分布。
- `BattleFieldLayout` 暴露 `allySlot1`、`allySlot2`、`allySlot3`、`allySlot4` 等 Transform 引用，并通过 `GetAllySlotPos(int position)` 返回对应位置的世界坐标。
- `BattleFieldManager` 生成战斗单位时使用布局提供的位置。
- 敌方阵型逻辑仍保留原有线性分布与 BOSS 三角形阵型相关处理，没有因为友方改为固定槽位而整体重写。

**Unity 场景配置注意事项**

- 场景中需要放置四个友方位置标记，并把对应 Transform 拖到 `BattleFieldLayout` 的 Inspector 字段。
- 位置标记是用于定位的对象，不应误认为角色模型本身。
- 三人队伍只使用实际分配到的三个位置，不应凭空生成第四名角色。

#### 2. 角色运行时数据记录队伍位置

**涉及脚本**

- `Assets/Scripts/Game/Character/CharacterRuntimeData.cs`
- `Assets/Scripts/Game/Party/PartyManager.cs`

**设计方法**

- 角色运行时数据包含 `Position`，用于表达队伍中角色所处的 1 至 4 号位置。
- `PartyManager` 继续作为队伍成员数据与探索队列管理的主要入口，避免探索队伍和战斗阵型分别维护互相冲突的顺序。
- 位置改变后，队伍数据负责更新成员的位置关系；战斗场景则根据更新后的数据和固定槽位呈现阵型。
- 这一结构为后续“按位置限制技能”“从特定位置攻击”“前排或后排规则”等功能提供统一的数据基础。

**相关接口**

- `GetMemberAtPosition(int position)`：按位置查找队伍成员。
- `SwapPosition(int positionA, int positionB)`：交换两个位置上的成员。
- `MoveMemberToPosition(CharacterRuntimeData member, int targetPosition)`：将指定成员移动到目标位置，并整理中间位置。
- `RefreshFieldFollowers(bool resetPositions = true)`：刷新探索场景中的跟随队列。
- `RefreshLeader()`：刷新队伍领队。

> 注意：以上是当前开发记录中涉及的接口名称。修改前仍应以工作区内的实际代码为准，确认方法签名与实现没有发生变化。

#### 3. 主动换位：双方交换位置并播放正常移动动画

**涉及脚本**

- `Assets/Scripts/Game/Character/CharacterDefinitionSO.cs`
- `Assets/Scripts/Battel/Runtime/Components/BattleFieldManager.cs`
- `Assets/Scripts/Battel/Runtime/Components/BattleUnit.cs`

**设计方法**

- 角色定义中加入 `maxSwapDistance`，用于表示角色主动换位时允许跨越的最大格数。
- `BattleFieldManager.TrySwapAllyPositions(BattleEntity actor, int targetPosition, float duration = 0.35f)` 作为主动换位的主要入口。
- 主动换位是两个角色之间的互换，不是把一名角色挪走后让全队自动补位。
- 双方都使用普通移动表现，调用 `BattleUnit.MoveToPosition(...)`，因此会播放现有的移动/跑步动画状态。
- 换位逻辑与角色的机动能力关联，为不同职业设置不同的换位距离留下扩展空间。

**当前边界**

- 当前实现属于位置与动画层面的基础功能。
- 由于完整回合/行动经济流程尚未接入，现阶段不能认为主动换位已经扣除行动资源，也不能把 F6 调试测试当成正式战斗行动流程。

#### 4. 击退：目标后移，中间队员依次补位

**涉及脚本**

- `Assets/Scripts/Game/Party/PartyManager.cs`
- `Assets/Scripts/Battel/Runtime/Components/BattleFieldManager.cs`
- `Assets/Scripts/Battel/Runtime/Components/BattleUnit.cs`

**设计方法**

- `BattleFieldManager.TryKnockbackAllyToPosition(BattleEntity target, int targetPosition, float duration = 0.25f)` 作为友方击退入口。
- `PartyManager.MoveMemberToPosition(...)` 负责更新队伍数据并整理位置顺序。
- 当某名角色被击退到更靠后的槽位时，中间成员依次向前补位，最终让队伍的位置编号保持连续。
- 例如初始队列为 `[A1, B2, C3, D4]`，将 B 从 2 号位击退到 4 号位后，期望结果为 `[A1, C2, D3, B4]`。
- 击退后更新相关战斗单位的目标/归位位置，避免后续位移仍以旧位置作为 home position。
- 击退后的队伍位置会写回队伍运行时数据；按照当前设计，战斗结束后不自动恢复击退前的队形。

**动画区别是刻意设计的**

- 被击退角色：不播放跑步动画，而是通过 `ForcedMoveToPosition(...)` 快速移动到目标槽位，再做逐渐衰减的震荡表现，以突出其被强制移动、失去控制的状态。
- 被补位的队友：正常播放跑步/移动动画，向新的槽位补位。
- 如果队友的新目标位置与其当前 Transform 位置相同，则不调用普通移动动画，避免出现“角色没有实际移动却播放跑步”的问题。
- 击退者的快速位移时长按常规补位动画时长的 `0.55` 倍计算，让被击退者先到位，队友随后补齐队形。
- 角色使用晕眩动画还是闲置动画由项目现有动画资源情况决定；如果没有可用的晕眩动画，使用闲置动画即可，不额外创建新的动画系统。

#### 5. 击退期间显示眩晕图标，并在结束后隐藏

**涉及脚本**

- `Assets/Scripts/Battel/Runtime/Components/BattleUnit.cs`

**设计方法**

- `BattleUnit` 新增序列化字段：`[SerializeField] private GameObject star;`。
- `Awake()` 中在引用非空时将图标默认关闭。
- `ForcedMoveToPosition(...)` 开始时显示图标，快速位移与震荡流程结束后再关闭。
- 这样图标与击退表现共用同一个生命周期，不需要在外部另外维护一个计时器。

**Unity Inspector 配置**

- `BattleUnit` 预制体中存在名为 `Star` 的子对象，它被确认用于显示眩晕图标。
- 需要在预制体 Inspector 中将该 `Star` 子对象拖到 `star` 字段。
- 如果字段没有绑定，代码中的空引用检查会避免直接访问空对象，但图标也不会显示。

### 关键代码位置速查

| 文件 | 主要职责 |
| --- | --- |
| `Assets/Scripts/Game/Character/CharacterRuntimeData.cs` | 角色运行时数据，包括队伍位置属性 |
| `Assets/Scripts/Game/Party/PartyManager.cs` | 队伍成员、位置顺序、位置交换、击退后的数据补位、探索队列刷新 |
| `Assets/Scripts/Game/Character/CharacterDefinitionSO.cs` | 角色定义中的主动换位距离配置 `maxSwapDistance` |
| `Assets/Scripts/Battel/Runtime/Components/BattleFieldLayout.cs` | 友方固定槽位坐标、敌方原有阵型布局 |
| `Assets/Scripts/Battel/Runtime/Components/BattleFieldManager.cs` | 战斗单位生成、主动换位、击退、补位动画编排 |
| `Assets/Scripts/Battel/Runtime/Components/BattleUnit.cs` | 单位动画状态、普通移动、强制位移、震荡效果、眩晕图标显隐 |

> 项目目录目前使用 `Battel` 这一拼写。查找文件时请沿用实际目录名，不要擅自改成 `Battle`，以免误判文件不存在。

### 调试入口

`Assets/Scripts/Game/Party/PartyManager.cs` 中有仅在 `UNITY_EDITOR` 下使用的调试快捷键：

- **F6**：探索模式下测试 1、2 号位交换；战斗模式下测试 1、2 号位主动换位。
- **F7**：战斗模式下测试 2 号位队员被击退到 3 号位。

这些入口用于快速检查位置和动画表现，不代表正式战斗行动系统已完成，也不应作为最终玩家输入方案。

### 相关 Git 提交

以下链接用于查看相应提交的代码差异。它们记录了代码变化，但本日志不代表这些改动已经通过 Unity 编译或运行测试。

- [添加友方固定站位与位置补位接口](https://github.com/TYX-14/-demo/commits/feature/battle-position-system/)
- [实现战斗换位与击退位移基础](https://github.com/TYX-14/-demo/commit/80b022e074c2973d1f94c7418fe1b4e227792422)
- [添加战斗击退快速检查入口](https://github.com/TYX-14/-demo/commit/2463114d36a91754fb0b9be8cf32052a1bf84d16)
- [增强击退速度并添加衰减震荡表现](https://github.com/TYX-14/-demo/commit/0c290ce59a791a4c13aab727ad44588b6c0bbe89)
- [让击退位移快于队友补位](https://github.com/TYX-14/-demo/commit/db5e936182dfa5f2a536788441f23c4a46864442)
- [修复击退时未移动队友误播跑步动画](https://github.com/TYX-14/-demo/commit/8f274e6b06c1ccd94e177a9e960e83e76aab484f)
- [击退期间显示眩晕图标](https://github.com/TYX-14/-demo/commit/b3000d02045e0674f6a7779817af51a3e6f70e97)

### 尚待验证或后续完善

- [ ] 在 Unity 中确认 `BattleFieldLayout` 的四个友方槽位引用均已绑定，位置符合预期。
- [ ] 在 `BattleUnit` 预制体中确认 `star` 已绑定 `Star` 子对象。
- [ ] 在 Unity 中实际测试 F6 主动换位与 F7 击退，检查位置数据、角色视觉位置和补位顺序是否一致。
- [ ] 检查三人队伍是否只占用三个槽位，且探索跟随顺序与战斗阵型顺序一致。
- [ ] 测试击退者与补位队友同时移动时是否存在穿插、重叠、跳位或动画状态残留。
- [ ] 将换位/击退接入正式回合与行动资源规则，并明确哪些技能或状态允许执行这些操作。
- [ ] 决定正式玩家操作方式；当前 F6/F7 仅为编辑器调试入口。
- [ ] Unity 编译与运行测试尚需实际执行并记录结果。

### 开发约定

后续扩展战斗位置功能时，优先遵守以下约定：

1. 修改前检查工作区中的实际脚本和方法，不仅依据旧笔记推断代码。
2. 优先做最小范围修改，避免无关重构。
3. 先确认数据层的队伍位置规则，再调整战斗表现，避免探索队列与战斗阵型产生两套不一致的数据。
4. 复用现有的 `BattleUnit` 动画和移动接口；只有现有接口无法表达需求时才扩展。
5. 每次修改说明涉及文件、方法、行为变化、Inspector 配置和潜在影响。
6. 明确区分“代码已修改”“已在 Unity 编译”“已在场景中运行验证”这三个状态，不把它们混为一谈。

---

## 后续日志模板

以后新增一项重要功能时，可以复制以下结构追加到文件顶部，并填入真实日期：

```md
## YYYY-MM-DD：功能名称

### 目标与原因
- 为什么需要这个功能，解决什么问题。

### 实现方案
- 核心设计与数据流。
- 与现有系统的关系，以及刻意保留的边界。

### 涉及脚本
- `路径/脚本.cs`：负责的职责与修改的方法。

### 行为变化
- 玩家或游戏中能观察到什么变化。

### 配置与测试
- 需要在 Unity Inspector 中完成的配置。
- 已执行的测试及结果。
- 尚未验证的部分。

### 相关提交
- GitHub commit 链接。

### 已知限制与后续工作
- 尚未完成的内容和下一步计划。
```
