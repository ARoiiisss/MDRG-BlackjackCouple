using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;

namespace BlackjackCouple
{
    /// <summary>
    /// 功能1：入口按钮挂进游戏交互界面（InteractStaticGui.buttonList），与抽鬼牌等小游戏并列。
    /// 仿照 MdrgAiDialog 的做法：Harmony Patch InteractState.EnterState 的 Postfix，
    /// 向 buttonList 追加"21点（情侣版）"按钮，点击回调 Main.OpenBlackjack()。
    /// 按钮文本按当前语言从 Localization 读取。
    /// </summary>
    [HarmonyPatch(typeof(InteractState))]
    public static class InteractStatePatch
    {
        [HarmonyPatch("EnterState")]
        [HarmonyPostfix]
        public static void AfterEnterState(InteractState __instance)
        {
            try
            {
                if (__instance._interactStaticGui == null) return;
                ButtonList buttonList = __instance._interactStaticGui.ButtonList;
                if (buttonList == null) return;

                // 追加按钮（order=0 排在最前，与 MdrgAiDialog 注入按钮同级）
                var modButtonList = ((Il2CppObjectBase)buttonList).Cast<IModificationPeriodButtonList>();
                ButtonListWrapper wrapper = modButtonList.AddButton(0, (CommonButtonColorType)0);
                wrapper.SetText(Localization.T("interact_button"));
                // 隐式转换：System.Action -> Il2CppSystem.Action（OnClick 属性类型）
                wrapper.OnClick = new System.Action(Main.OpenBlackjack);
                buttonList.UpdateCurrentButtonState();

                MelonLogger.Msg("[BlackjackCouple] 已在交互界面注入按钮: " + Localization.T("interact_button"));
            }
            catch (System.Exception e)
            {
                MelonLogger.Error("[BlackjackCouple] 注入交互按钮失败: " + e);
            }
        }
    }
}
