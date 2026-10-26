#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 编辑器专用开发者控制面板。
/// F8 显示/隐藏面板；F6 测试换位；F7 测试击退。
/// 压力值与精神状态按钮直接修改调试数据，只用于检查 UI，不模拟压力结算。
/// </summary>
public sealed class DeveloperControlPanel : MonoBehaviour
{
    private static DeveloperControlPanel instance;

    private bool isVisible;
    private Rect windowRect = new Rect(20f, 80f, 360f, 390f);
    private int selectedCharacterIndex;
    private int stressSliderValue;
    private CharacterRuntimeData sliderCharacter;
    private Vector2 scrollPosition;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreatePanel()
    {
        if (instance != null)
            return;

        GameObject panelObject = new GameObject("[Developer Control Panel]");
        DontDestroyOnLoad(panelObject);
        instance = panelObject.AddComponent<DeveloperControlPanel>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.f8Key.wasPressedThisFrame)
            isVisible = !isVisible;

        if (!isVisible)
        {
            if (keyboard.f6Key.wasPressedThisFrame)
                RunSwapTest();
            if (keyboard.f7Key.wasPressedThisFrame)
                RunKnockbackTest();
            return;
        }

        if (keyboard.f6Key.wasPressedThisFrame)
            RunSwapTest();
        if (keyboard.f7Key.wasPressedThisFrame)
            RunKnockbackTest();
    }

    private void OnGUI()
    {
        if (!isVisible)
            return;

        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "开发者控制面板");
    }

    private void DrawWindow(int windowId)
    {
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("战斗位置测试");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("F6：交换 1、2 号位"))
            RunSwapTest();
        if (GUILayout.Button("F7：击退 2 → 3"))
            RunKnockbackTest();
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);
        GUILayout.Label("压力条测试");
        List<CharacterRuntimeData> characters = GetPartyCharacters();
        if (characters.Count == 0)
        {
            GUILayout.Label("当前没有可供测试的队伍角色。");
        }
        else
        {
            selectedCharacterIndex = Mathf.Clamp(selectedCharacterIndex, 0, characters.Count - 1);
            CharacterRuntimeData character = characters[selectedCharacterIndex];

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", GUILayout.Width(42f)))
                selectedCharacterIndex = (selectedCharacterIndex - 1 + characters.Count) % characters.Count;
            GUILayout.Label($"{character.DisplayName}（{selectedCharacterIndex + 1}/{characters.Count}）");
            if (GUILayout.Button("▶", GUILayout.Width(42f)))
                selectedCharacterIndex = (selectedCharacterIndex + 1) % characters.Count;
            GUILayout.EndHorizontal();

            character = characters[selectedCharacterIndex];
            if (character != null)
            {
                if (sliderCharacter != character)
                {
                    sliderCharacter = character;
                    stressSliderValue = character.CurrentStress;
                }
                }

                GUILayout.Label($"压力值：{stressSliderValue} / 200");
                int nextValue = Mathf.RoundToInt(GUILayout.HorizontalSlider(stressSliderValue, 0f, 200f));
                if (nextValue != stressSliderValue)
                    stressSliderValue = nextValue;

                GUILayout.BeginHorizontal();
                GUILayout.Label("精确数值", GUILayout.Width(68f));
                string stressInput = GUILayout.TextField(stressSliderValue.ToString(), GUILayout.Width(70f));
                if (int.TryParse(stressInput, out int parsedStress))
                    stressSliderValue = Mathf.Clamp(parsedStress, 0, 200);
                GUILayout.Label("(0–200)");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("应用压力值"))
                    ApplyStressForDisplay(character, stressSliderValue);
                if (GUILayout.Button("设为 0"))
                {
                    stressSliderValue = 0;
                    ApplyStressForDisplay(character, 0);
                }
                if (GUILayout.Button("设为 100"))
                {
                    stressSliderValue = 100;
                    ApplyStressForDisplay(character, 100);
                }
                if (GUILayout.Button("设为 200"))
                {
                    stressSliderValue = 200;
                    ApplyStressForDisplay(character, 200);
                }
                GUILayout.EndHorizontal();

                GUILayout.Label($"当前精神状态：{GetSpiritStateLabel(character.SpiritState)}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("普通"))
                    SetSpiritStateForDisplay(character, CharacterSpiritState.Normal);
                if (GUILayout.Button("美德"))
                    SetSpiritStateForDisplay(character, CharacterSpiritState.Virtue);
                if (GUILayout.Button("折磨"))
                    SetSpiritStateForDisplay(character, CharacterSpiritState.Affliction);
                GUILayout.EndHorizontal();

                GUILayout.Label("提示：此处直接设置显示数据，不触发 100/200 压力阈值结算。");
            }
        }

        GUILayout.Space(8f);
        GUILayout.Label("F8：显示/隐藏面板");
        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private static void ApplyStressForDisplay(CharacterRuntimeData character, int value)
    {
        if (character == null)
            return;

        character.CurrentStress = Mathf.Clamp(value, 0, 200);
        CharacterStressSystem.NotifyCharacterUpdated(character);
        Debug.Log($"[开发者面板] {character.DisplayName} 的压力值已设为 {character.CurrentStress}。");
    }

    private static void SetSpiritStateForDisplay(CharacterRuntimeData character, CharacterSpiritState state)
    {
        if (character == null)
            return;

        character.SpiritState = state;
        CharacterStressSystem.NotifyCharacterUpdated(character);
        Debug.Log($"[开发者面板] {character.DisplayName} 的精神状态已设为 {GetSpiritStateLabel(state)}。");
    }

    private static string GetSpiritStateLabel(CharacterSpiritState state)
    {
        return state switch
        {
            CharacterSpiritState.Virtue => "美德",
            CharacterSpiritState.Affliction => "折磨",
            _ => "普通"
        };
    }

    private static List<CharacterRuntimeData> GetPartyCharacters()
    {
        if (PartyManager.Instance == null || PartyManager.Instance.PartyMembers == null)
            return new List<CharacterRuntimeData>();

        return PartyManager.Instance.PartyMembers
            .Where(character => character != null)
            .OrderBy(character => character.Position)
            .ToList();
    }

    private static void RunSwapTest()
    {
        if (GameModeManager.Instance == null)
            return;

        if (GameModeManager.Instance.CurrentGameMode == GameMode.Explore)
        {
            if (PartyManager.Instance != null)
            {
                PartyManager.Instance.SwapPosition(1, 2);
                Debug.Log("[开发者面板] 探索模式：已请求交换 1、2 号位。");
            }
            return;
        }

        if (GameModeManager.Instance.CurrentGameMode != GameMode.Battle)
            return;

        BattleFieldManager battleField = FindAnyObjectByType<BattleFieldManager>();
        if (battleField == null)
        {
            Debug.LogWarning("[开发者面板] 找不到 BattleFieldManager。");
            return;
        }

        BattleUnit actorUnit = battleField.SpawnedAllyUnits
            .FirstOrDefault(unit => unit != null && unit.Entity != null &&
                                    unit.Entity.RuntimeData.Position == 1);
        if (actorUnit == null)
        {
            Debug.LogWarning("[开发者面板] 找不到当前处于 1 号位的友军。");
            return;
        }

        bool started = battleField.TrySwapAllyPositions(actorUnit.Entity, 2);
        Debug.Log(started
            ? "[开发者面板] 已启动主动换位测试（1 → 2）。此调试入口不扣行动资源。"
            : "[开发者面板] 主动换位未启动，请检查目标槽位、位置限制或是否正在移动。");
    }

    private static void RunKnockbackTest()
    {
        if (GameModeManager.Instance == null ||
            GameModeManager.Instance.CurrentGameMode != GameMode.Battle)
        {
            Debug.Log("[开发者面板] F7 击退测试仅在战斗模式下可用。");
            return;
        }

        BattleFieldManager battleField = FindAnyObjectByType<BattleFieldManager>();
        if (battleField == null)
        {
            Debug.LogWarning("[开发者面板] 找不到 BattleFieldManager。");
            return;
        }

        BattleUnit actorUnit = battleField.SpawnedAllyUnits
            .FirstOrDefault(unit => unit != null && unit.Entity != null &&
                                    unit.Entity.RuntimeData.Position == 2);
        if (actorUnit == null)
        {
            Debug.LogWarning("[开发者面板] 找不到当前处于 2 号位的友军。");
            return;
        }

        bool started = battleField.TryKnockbackAllyToPosition(actorUnit.Entity, 3);
        Debug.Log(started
            ? "[开发者面板] 已启动击退测试（2 → 3）。"
            : "[开发者面板] 击退未启动，请检查目标槽位或是否正在移动。");
    }
}
#endif
