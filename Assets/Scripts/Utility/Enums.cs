public enum GameMode
{
    Explore,
    InteractionMenu,
    Battle,
    Pause,
}

/// <summary>
/// 战斗指令类型。
/// </summary>
public enum BattleCommandType
{
    Attack,       // 攻击
    Item,         // 道具
    SwapPosition, // 交换位置
    Escape,       // 逃跑
}

public enum ActiveMap
{
    Player,
    UI,
    Battle,
    None,
}

public enum CameraView
{
    Explore,
    Battle,
    BatteResult,
}

public enum PlayerJob
{
    Any,

    Warrior剑士,//剑士
    Apothecary药师,
    Cleric神官,
    Dacer舞者,
    Hunter猎人,
    Merchant商人,//商人
    Scholar学士,//学士
    Thief盗贼,//盗贼
}

public enum GrowthRank {S,A,B,C,D}

public enum ItemType
{
    Equipment = 0,//装备
    Consumable = 1,//消耗品
}

public enum ItemIconKey
{

    Weapon,
    Sword,
    Spear,
    Dagger,
    Axe,
    Bow,
    Staff,
    Head,
    Body,
    Accssroy,
    Healing,
    SP,
    Revive,
    Cure,
    KeyItem,
}

public enum PanelType
{
    Item,
    Sell,
    Buy,
    Equipment,
}

public enum EquipmentCategory
{
    Weapon,
    Shield,
    Head,
    Body,
    Accessory,
}

public enum EquipSlot
{
    Sword,
    Spear,
    Dagger,
    Axe,
    Bow,
    Staff,
    Shield,
    Head,
    Body,
    Accessory1,
    Accessory2,
}

public enum WeaqonType
{
    None = 0,

    Sword,
    Spear,
    Dagger,
    Axe,
    Bow,
    Staff,
}

public enum StatType
{
    MaxHP=1,
    MaxSP=2,
    PActk=3,
    PDef=4,
    MAtk=5,
    MDef=6,
    Speed=7,
    Accuracy=8,
    Evasion=9,
    CurrentHP=100,
    CurrentSP=101,
    CurrenBP=102,
}
/// <summary>
/// 敌人站位方式
/// </summary>
public enum EnemyLayoutFormation
{
    Line = 0,
    BossTriangle = 1,
}
/// <summary>
/// 淡入淡出效果
/// </summary>
public enum FadeStyle
{
    PanelFade,
    WipeMask

}