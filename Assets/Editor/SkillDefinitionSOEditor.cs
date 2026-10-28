using UnityEditor;
using UnityEngine;

/// <summary>
/// 为技能等级提供按效果类型分别添加的配置界面。
/// 旧格式效果会显示迁移按钮，迁移时保留旧数据作为备份。
/// </summary>
[CustomEditor(typeof(SkillDefinitionSO))]
public class SkillDefinitionSOEditor : Editor
{
    private SerializedProperty levelConfigsProperty;

    private void OnEnable()
    {
        levelConfigsProperty = serializedObject.FindProperty("levelConfigs");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawSkillProperties();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("技能等级与效果", EditorStyles.boldLabel);

        if (levelConfigsProperty == null)
        {
            EditorGUILayout.HelpBox("找不到 levelConfigs 配置字段。", MessageType.Error);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        for (int i = 0; i < levelConfigsProperty.arraySize; i++)
        {
            SerializedProperty levelConfig = levelConfigsProperty.GetArrayElementAtIndex(i);
            if (DrawLevelConfig(levelConfig, i))
                return;
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("添加技能等级", GUILayout.Height(28)))
        {
            int index = levelConfigsProperty.arraySize;
            levelConfigsProperty.InsertArrayElementAtIndex(index);
            SerializedProperty newLevel = levelConfigsProperty.GetArrayElementAtIndex(index);
            SerializedProperty levelNumber = newLevel.FindPropertyRelative("level");
            if (levelNumber != null)
                levelNumber.intValue = index + 1;

            serializedObject.ApplyModifiedProperties();
            return;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSkillProperties()
    {
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(iterator, true);
                continue;
            }

            if (iterator.propertyPath == "levelConfigs")
                continue;

            EditorGUILayout.PropertyField(iterator, true);
        }
    }

    /// <returns>True if an action changed the serialized structure and GUI should return early.</returns>
    private bool DrawLevelConfig(SerializedProperty levelConfig, int index)
    {
        SerializedProperty levelProperty = levelConfig.FindPropertyRelative("level");
        SerializedProperty typedEffects = levelConfig.FindPropertyRelative("typedEffects");
        SerializedProperty legacyEffects = levelConfig.FindPropertyRelative("effects");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        if (levelProperty != null)
            EditorGUILayout.PropertyField(levelProperty, new GUIContent($"等级 {index + 1}"));

        if (GUILayout.Button("删除等级", GUILayout.Width(72)))
        {
            levelConfigsProperty.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return true;
        }

        EditorGUILayout.EndHorizontal();

        if (typedEffects == null)
        {
            EditorGUILayout.HelpBox("找不到 typedEffects 字段。请确认技能效果数据脚本已正确导入。", MessageType.Error);
            EditorGUILayout.EndVertical();
            return false;
        }

        if (legacyEffects != null && legacyEffects.arraySize > 0 && typedEffects.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                $"检测到 {legacyEffects.arraySize} 条旧格式效果。迁移会创建新的独立效果对象，并保留旧数据作为备份。",
                MessageType.Warning);

            if (GUILayout.Button("迁移旧效果到新类型"))
            {
                Undo.RecordObject(target, "迁移旧技能效果");
                MigrateLegacyEffects(legacyEffects, typedEffects);
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                return true;
            }
        }
        else if (legacyEffects != null && legacyEffects.arraySize > 0 && typedEffects.arraySize > 0)
        {
            EditorGUILayout.HelpBox("旧格式数据仍保留作为备份；当前说明与战斗逻辑优先读取下方的新效果列表。", MessageType.Info);
        }

        EditorGUILayout.LabelField("效果列表", EditorStyles.boldLabel);

        for (int effectIndex = 0; effectIndex < typedEffects.arraySize; effectIndex++)
        {
            SerializedProperty effectProperty = typedEffects.GetArrayElementAtIndex(effectIndex);
            object managedEffect = effectProperty.managedReferenceValue;
            string effectLabel = managedEffect != null
                ? managedEffect.GetType().Name
                : $"效果 {effectIndex + 1}";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{effectIndex + 1}. {effectLabel}", EditorStyles.boldLabel);
            if (GUILayout.Button("删除", GUILayout.Width(48)))
            {
                typedEffects.DeleteArrayElementAtIndex(effectIndex);
                serializedObject.ApplyModifiedProperties();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndVertical();
                return true;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(effectProperty, GUIContent.none, true);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("添加效果", EditorStyles.miniBoldLabel);

        if (DrawAddEffectButtons(typedEffects))
        {
            EditorGUILayout.EndVertical();
            return true;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
        return false;
    }

    private bool DrawAddEffectButtons(SerializedProperty typedEffects)
    {
        if (DrawAddButtonRow(typedEffects,
                ("添加攻击", new DamageEffect()),
                ("添加伤害修正", new DamageModifierEffect()),
                ("添加治疗", new HealEffect()),
                ("添加压力伤害", new StressDamageEffect()),
                ("添加压力恢复", new StressHealEffect())))
            return true;

        if (DrawAddButtonRow(typedEffects,
                ("添加前进", new MoveForwardEffect()),
                ("添加后退", new MoveBackwardEffect()),
                ("添加精准修正", new AccuracyModifierEffect()),
                ("添加暴击修正", new CriticalModifierEffect()),
                ("添加状态", new ApplyStatusEffect())))
            return true;

        return false;
    }

    private bool DrawAddButtonRow(SerializedProperty typedEffects, params (string label, SkillEffectBase effect)[] buttons)
    {
        EditorGUILayout.BeginHorizontal();
        foreach (var button in buttons)
        {
            if (!GUILayout.Button(button.label, GUILayout.MinHeight(25)))
                continue;

            int newIndex = typedEffects.arraySize;
            typedEffects.InsertArrayElementAtIndex(newIndex);
            typedEffects.GetArrayElementAtIndex(newIndex).managedReferenceValue = button.effect;
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.EndHorizontal();
            return true;
        }
        EditorGUILayout.EndHorizontal();
        return false;
    }

    private static void MigrateLegacyEffects(SerializedProperty legacyEffects, SerializedProperty typedEffects)
    {
        typedEffects.ClearArray();

        for (int i = 0; i < legacyEffects.arraySize; i++)
        {
            SerializedProperty oldEffect = legacyEffects.GetArrayElementAtIndex(i);
            SerializedProperty typeProperty = oldEffect.FindPropertyRelative("effectType");
            SerializedProperty valueProperty = oldEffect.FindPropertyRelative("value");
            SerializedProperty conditionProperty = oldEffect.FindPropertyRelative("condition");
            SerializedProperty statusProperty = oldEffect.FindPropertyRelative("statusEffect");
            SerializedProperty durationProperty = oldEffect.FindPropertyRelative("durationRounds");
            SerializedProperty chargesProperty = oldEffect.FindPropertyRelative("charges");

            if (typeProperty == null)
                continue;

            float value = valueProperty != null ? valueProperty.floatValue : 0f;
            SkillEffectCondition condition = conditionProperty != null
                ? (SkillEffectCondition)conditionProperty.enumValueIndex
                : SkillEffectCondition.None;
            StatusEffectDefinitionSO status = statusProperty != null
                ? statusProperty.objectReferenceValue as StatusEffectDefinitionSO
                : null;
            int duration = durationProperty != null ? durationProperty.intValue : 0;
            int charges = chargesProperty != null ? chargesProperty.intValue : 0;

            SkillEffectBase converted = null;
            switch ((SkillEffectType)typeProperty.enumValueIndex)
            {
                case SkillEffectType.Damage:
                    converted = new DamageEffect();
                    break;
                case SkillEffectType.Heal:
                    converted = new HealEffect(value);
                    break;
                case SkillEffectType.StressDamage:
                    converted = new StressDamageEffect(value);
                    break;
                case SkillEffectType.StressHeal:
                    converted = new StressHealEffect(value);
                    break;
                case SkillEffectType.MoveForward:
                    converted = new MoveForwardEffect(value);
                    break;
                case SkillEffectType.MoveBackward:
                    converted = new MoveBackwardEffect(value);
                    break;
                case SkillEffectType.DamageModifier:
                    converted = new DamageModifierEffect(value, condition);
                    break;
                case SkillEffectType.AccuracyModifier:
                    converted = new AccuracyModifierEffect(value, condition);
                    break;
                case SkillEffectType.CriticalModifier:
                    converted = new CriticalModifierEffect(value, condition);
                    break;
                case SkillEffectType.ApplyStatus:
                    converted = new ApplyStatusEffect(status, duration, charges);
                    break;
            }

            if (converted == null)
                continue;

            int newIndex = typedEffects.arraySize;
            typedEffects.InsertArrayElementAtIndex(newIndex);
            typedEffects.GetArrayElementAtIndex(newIndex).managedReferenceValue = converted;
        }
    }
}
