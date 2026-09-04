using System;
using System.Collections;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// MelonLoader MOD 元数据
[assembly: MelonInfo(typeof(BlackjackCouple.Main), "Blackjack Couple", "1.0.0", "AROiiisss")]
[assembly: MelonGame(null, null)]
[assembly: MelonColor(255, 220, 120, 80)]

namespace BlackjackCouple
{
    /// <summary>
    /// MDRG 21点情侣版 MOD 主入口
    /// 功能：
    ///  1. 提供打开 21 点小游戏的入口（当前用控制台命令 / 手动调用）
    ///  2. 每日检测点名制 IOU 欠条：清偿 + 过期惩罚
    ///  3. 维护 BlackjackGui 实例并注入 Bot 名字
    /// </summary>
    public class Main : MelonMod
    {
        private static Main _instance;
        private BlackjackGui _gui;
        private bool _gameStarted = false;
        private float _gameStartTime; // 牌局开始时刻（Time.time），用于异常退出超时兜底

        // 实时清偿检测的冷却计时（秒）：互动后尽快清偿欠条，又不至于每帧都查
        private float _iouCheckCooldown;

        // 出门欠条"已还清"补弹：
        //  出门欠条清偿时若立即弹窗会被出门动画打断/遮挡；且 MDRG 室内外切换未必是
        //  Unity 场景切换（游戏内是场景对象引用），无法可靠判定"回家"时机。
        //  因此采用简单可靠的时间延迟：欠条清偿时挂起，延迟 OutsidePopupDelay 秒后再弹，
        //  避开出门瞬间的动画重叠，无需任何场景名/状态探测。
        private float _outsidePendingStartTime;                     // 出门欠条挂起建立时刻（Time.time），用于延迟计时
        private const float OutsidePopupDelay = 10f;                // 补弹延迟（秒）：出门欠条清偿后延迟多久再弹"已还清"

        // 出门欠条补弹检查的轮询冷却（秒）
        private const float OutsidePopupPollInterval = 0.5f;
        private float _outsidePopupCooldown;

        // 出门欠条"已还清"非模态提示（toast）：不弹 OkPopup 模态框（会与室外互动按钮
        // 重叠遮挡），改为屏幕顶部居中的非模态提示条，raycastTarget=false 不拦截任何点击，
        // 显示 ToastDisplaySeconds 秒后自动淡出。
        // 样式：深色底 + 金色文字（单层，已验证可正常显示；三层金边/描边方案在部分 Il2Cpp 下不渲染，故回退）
        private Transform _outsideToastParent;
        private Image _outsideToastBg;      // 深色内衬层
        private Text _outsideToastText;
        private int _outsideToastVersion;
        private const float ToastDisplaySeconds = 2.0f;

        // 牌局最长时长兜底（秒）：超过该值强制复位 _gameStarted，
        // 防止玩家打完牌局未点"返回"（直接切走/换场景）导致 _gameStarted 永久为 true、欠条检测从此被跳过
        private const float GameMaxDuration = 300f;

        // kiss 专属检测：游戏无 lastKissedAt 且 lastInteractAt 会被任意互动/退出互动刷新（误清根因），
        // 改为轮询监控 Live2DController.IsKissing 上升沿（false→true 才算一次真亲吻），
        // 触发时经 IOUSystem.NotifyKiss 写入 kiss 专属时间戳（kiss 欠条唯一清偿信号）。
        // 整体 try 包裹 + 低频轮询：避免 FindObjectOfType / 类型解析异常中断 OnLateUpdate
        //（否则后续出门欠条补弹等逻辑会被跳过，表现为"出门弹窗消失"）。
        private Live2DController _live2d;
        private bool _wasKissing;
        private float _kissCheckCooldown;
        private const float KissCheckInterval = 0.2f;

        public override void OnInitializeMelon()
        {
            _instance = this;
            LoggerInstance.Msg("[BlackjackCouple] 21点情侣版 MOD 已加载");

            // 加载本地化：首次运行自动生成 UserData\BlackjackCouple.cfg，随后按配置确定语言
            try { Localization.Initialize(); }
            catch (Exception e) { LoggerInstance.Error("[BlackjackCouple] 本地化初始化失败: " + e); }

            // 注：GameVariables.DayPassed 是方法而非事件，无法挂钩；
            // 欠条清偿/过期检测由 OnLateUpdate 定时兜底（约每 1 秒一次，牌局外执行）。
        }

        public override void OnLateUpdate()
        {
            // 亲吻检测：每帧读取 Live2DController.IsKissing 上升沿（与牌局无关，任何场景都检测）
            DetectKiss(GameVariables.Current);

            // 牌局进行中跳过实时检测，避免清偿/过期弹窗遮挡牌桌；
            // 超过 GameMaxDuration 仍未复位则强制复位，防玩家未点"返回"直接切走导致检测永久停止
            if (_gameStarted)
            {
                if (Time.time - _gameStartTime < GameMaxDuration) return;
                _gameStarted = false;
                LoggerInstance.Msg("[BlackjackCouple] 牌局超时(" + GameMaxDuration + "s)自动复位欠条检测");
            }

            // 出门欠条补弹检查：约每 0.5 秒一次，到达延迟时间即补弹"已还清"
            _outsidePopupCooldown -= Time.deltaTime;
            if (_outsidePopupCooldown <= 0f)
            {
                _outsidePopupCooldown = OutsidePopupPollInterval;
                CheckPendingOutsidePopups();
            }

            // 实时清偿检测：约每 1 秒一次（亲吻等互动后，欠条能尽快消除并提示"已还清"）
            _iouCheckCooldown -= Time.deltaTime;
            if (_iouCheckCooldown > 0f) return;
            _iouCheckCooldown = 1f;

            CheckIOUS();
        }

        /// <summary>欠条检测：清偿 + 过期惩罚弹窗</summary>
        private void CheckIOUS()
        {
            try
            {
                HandleIOUCheck(GameVariables.Current);
            }
            catch (Exception e)
            {
                LoggerInstance.Error("[BlackjackCouple] 欠条检测异常: " + e);
            }
        }

        /// <summary>
        /// 亲吻检测：轮询读取 Live2DController.IsKissing 上升沿（false→true）记为一次真亲吻，
        /// 写入 kiss 专属时间戳。非亲吻互动（说话/摸头/退出互动等）不会刷新该信号，
        /// 从根上解决"没亲却清、随便什么互动都算亲"的误清问题。
        /// 整体 try 包裹：任何异常只记录日志，绝不影响 OnLateUpdate 其余逻辑（出门补弹等）。
        /// </summary>
        private void DetectKiss(GameVariables gv)
        {
            if (gv == null) return;
            try
            {
                _kissCheckCooldown -= Time.deltaTime;
                if (_kissCheckCooldown > 0f) return;
                _kissCheckCooldown = KissCheckInterval;

                if (_live2d == null)
                    _live2d = UnityEngine.Object.FindObjectOfType<Live2DController>();

                bool now = false;
                if (_live2d != null)
                {
                    try { now = _live2d.IsKissing; }
                    catch { now = false; }
                }

                if (now && !_wasKissing)
                {
                    IOUSystem.NotifyKiss(gv);
                    LoggerInstance.Msg("[BlackjackCouple] 检测到亲吻，记录 kiss 时间戳");
                }
                _wasKissing = now;
            }
            catch (Exception e)
            {
                LoggerInstance.Error("[BlackjackCouple] kiss 检测异常: " + e);
            }
        }

        /// <summary>
        /// 执行一次欠条检测并弹出结果。出门欠条的"已还清"会被 IOUSystem 挂起，
        /// 这里仅在新挂起产生时记录家场景，等待场景切回时补弹（见 OnSceneLoaded）。
        /// </summary>
        private void HandleIOUCheck(GameVariables gv)
        {
            if (gv == null) return;
            bool wasPending = IOUSystem.HasPendingOutsidePopups();
            var popups = IOUSystem.CheckAll(gv);
            foreach (var p in popups)
            {
                if (p.Length >= 2)
                    UiOverlay.Instance.OkPopup(p[0], p[1]);
            }
            if (!wasPending && IOUSystem.HasPendingOutsidePopups())
            {
                // 出门欠条开始挂起：记录挂起时刻，延迟 OutsidePopupDelay 秒后补弹"已还清"
                _outsidePendingStartTime = Time.time;
            }
        }

        /// <summary>
        /// 出门欠条挂起补弹检查（约每 0.5 秒）：
        /// 挂起时间达到 OutsidePopupDelay 后补弹"已还清"。纯时间延迟，不依赖场景名/
        /// 状态反射，无论出门多久、期间如何切换场景，延迟到点必定弹出。
        /// </summary>
        private void CheckPendingOutsidePopups()
        {
            if (!IOUSystem.HasPendingOutsidePopups()) return;
            if (Time.time - _outsidePendingStartTime < OutsidePopupDelay) return;
            LoggerInstance.Msg("[BlackjackCouple] 出门欠条延迟(" + OutsidePopupDelay + "s)到点，补弹'已还清'");
            DrainAndShowOutsidePopups();
        }

        /// <summary>取出并展示全部挂起的出门欠条"已还清"提示，复位延迟计时。
        /// 【官方方案】开发团队 Sheep 推荐使用游戏原生非模态浮动文本
        /// UiOverlay.ShowFloatingTextAtMouse(msg)，不遮挡按钮、自动消失。
        /// （自建 UGUI toast 不渲染；OkPopup 模态遮挡按钮，均弃用）</summary>
        private void DrainAndShowOutsidePopups()
        {
            var pending = IOUSystem.DrainPendingOutsidePopups();
            LoggerInstance.Msg("[BlackjackCouple] 出门欠条补弹：" + (pending == null ? 0 : pending.Count) + " 条");
            if (pending == null) { _outsidePendingStartTime = 0f; return; }
            foreach (var p in pending)
            {
                string msg = p.Length >= 2 ? (p[0] + "：" + p[1]) : (p.Length >= 1 ? p[0] : "");
                if (!string.IsNullOrEmpty(msg))
                {
                    try
                    {
                        UiOverlay.Instance.ShowFloatingTextAtMouse(msg);
                        LoggerInstance.Msg("[BlackjackCouple] 出门提示浮动文本已弹出: " + msg);
                    }
                    catch (Exception e)
                    {
                        LoggerInstance.Error("[BlackjackCouple] ShowFloatingTextAtMouse 调用失败: " + e);
                        // 兜底：浮动文本失败时退回原生 OkPopup，保证提示不丢
                        try { UiOverlay.Instance.OkPopup("已还清", msg); }
                        catch (Exception e2) { LoggerInstance.Error("[BlackjackCouple] OkPopup 兜底失败: " + e2); }
                    }
                }
            }
            _outsidePendingStartTime = 0f;
        }

        // ---------- 非模态出门提示（toast） ----------

        /// <summary>屏幕顶部居中非模态提示条：深紫底 + 白字（贴近游戏按钮 UI 配色），2 秒后自动淡出，不拦截任何点击</summary>
        private void ShowOutsideToast(string text)
        {
            try
            {
                EnsureOutsideToastParent();
                _outsideToastVersion++;
                int ver = _outsideToastVersion;
                Color textColor = new Color(1f, 1f, 1f, 1f);
                Color bgColor = new Color(0.18f, 0.04f, 0.29f, 0.95f);

                if (_outsideToastBg == null)
                {
                    // 纯色块单层 Image + Text，位置临时放屏幕正中(0,0)排除 CanvasScaler 偏移因素
                    _outsideToastBg = ToastCreateImage("BlackjackCouple_OutsideToastBg", new Vector2(0, 0), new Vector2(806, 66), bgColor);
                    _outsideToastText = ToastCreateText("BlackjackCouple_OutsideToastText", text, 28, new Vector2(0, 0), new Vector2(780, 58), textColor);
                    // 诊断：创建后实际位置 / 父 CanvasScaler / 字体
                    try
                    {
                        var rt = _outsideToastBg.GetComponent<RectTransform>();
                        LoggerInstance.Msg("[BlackjackCouple] toast 诊断: worldPos=" + _outsideToastBg.transform.position
                            + " rect=" + rt.rect + " anchoredPos=" + rt.anchoredPosition);
                        var scaler = _outsideToastParent.GetComponentInParent<CanvasScaler>();
                        LoggerInstance.Msg("[BlackjackCouple] toast 父 CanvasScaler: " + (scaler != null
                            ? (scaler.referenceResolution + " scaleFactor=" + scaler.transform.localScale.x) : "无"));
                        LoggerInstance.Msg("[BlackjackCouple] toast 字体: " + (ToastGetDefaultFont() != null ? "正常" : "NULL"));
                    }
                    catch (Exception e) { LoggerInstance.Error("[BlackjackCouple] toast 诊断失败: " + e); }
                }
                else
                {
                    _outsideToastText.text = text;
                    _outsideToastText.color = textColor;
                }

                // 淡入缓冲，不瞬间弹出
                _outsideToastBg.color = new Color(bgColor.r, bgColor.g, bgColor.b, 0f);
                _outsideToastText.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
                MelonCoroutines.Start(ToastAnimateIn(ver));
                MelonCoroutines.Start(ToastFadeOut(ver));
                LoggerInstance.Msg("[BlackjackCouple] 出门提示 toast 已创建: " + text);
            }
            catch (Exception e)
            {
                LoggerInstance.Error("[BlackjackCouple] 出门提示 toast 创建失败: " + e);
                // 清理半成品对象，避免下次创建时误入 else 分支（引用残留但对象已损坏）
                if (_outsideToastText != null) { UnityEngine.Object.Destroy(_outsideToastText.gameObject); _outsideToastText = null; }
                if (_outsideToastBg != null) { UnityEngine.Object.Destroy(_outsideToastBg); _outsideToastBg = null; }
            }
        }

        /// <summary>获取游戏原生 UI Canvas 作为 toast 父节点，借用游戏自身渲染层级。
        /// 实验结论：①独立 ScreenSpaceOverlay Canvas 不可见（游戏 UI 全部为 ScreenSpaceCamera+layer5，
        /// 自建 overlay 不纳入渲染）；②UiOverlay 根 "Everything" 及其子节点均无 Canvas 组件。
        /// 故直接挂到场景中 ScreenSpaceCamera 的主 UI Canvas（优先 Game GUI）下。</summary>
        private void EnsureOutsideToastParent()
        {
            if (_outsideToastParent != null) return;
            try
            {
                // 优先：场景中 ScreenSpaceCamera 且 active 的游戏 UI Canvas
                var all = UnityEngine.Object.FindObjectsOfType<Canvas>();
                if (all != null && all.Length > 0)
                {
                    Canvas best = null;
                    int bestScore = -1;
                    foreach (var c in all)
                    {
                        if (c == null || !c.gameObject.activeInHierarchy) continue;
                        if (c.renderMode != RenderMode.ScreenSpaceCamera) continue;
                        int score;
                        if (c.name == "Game GUI") score = 100;
                        else if (c.name == "Canvas") score = 90;
                        else if (c.name == "MenuDialog") score = 80;
                        else if (c.name == "SpeechSayDialog") score = 60;
                        else score = 50;
                        if (score > bestScore) { bestScore = score; best = c; }
                    }
                    if (best != null)
                    {
                        _outsideToastParent = best.transform;
                        LoggerInstance.Msg("[BlackjackCouple] toast 父节点=ScreenSpaceCamera Canvas: " + best.name
                            + " camera=" + (best.worldCamera != null ? best.worldCamera.name : "null"));
                    }
                }
            }
            catch (Exception e)
            {
                LoggerInstance.Error("[BlackjackCouple] 查找游戏 UI Canvas 失败: " + e);
            }
            if (_outsideToastParent == null)
            {
                try
                {
                    var overlay = UiOverlay.Instance;
                    if (overlay != null)
                    {
                        Canvas[] children = null;
                        try { children = overlay.GetComponentsInChildren<Canvas>(true); } catch { }
                        if (children != null && children.Length > 0)
                        {
                            _outsideToastParent = children[0].transform;
                            LoggerInstance.Msg("[BlackjackCouple] toast 父节点=UiOverlay子Canvas: " + _outsideToastParent.name);
                        }
                        else
                        {
                            LoggerInstance.Warning("[BlackjackCouple] UiOverlay 下无子 Canvas，退化挂 Everything 根");
                            _outsideToastParent = overlay.transform;
                        }
                    }
                    else
                    {
                        LoggerInstance.Warning("[BlackjackCouple] UiOverlay.Instance 为 null");
                    }
                }
                catch (Exception e)
                {
                    LoggerInstance.Error("[BlackjackCouple] 获取原生 Canvas 失败: " + e);
                }
            }
            if (_outsideToastParent == null)
            {
                // 兜底：取场景任意一个已有 Canvas 作为父节点
                var any = UnityEngine.Object.FindObjectOfType<Canvas>();
                if (any != null)
                {
                    _outsideToastParent = any.transform;
                    LoggerInstance.Warning("[BlackjackCouple] 兜底使用场景 Canvas: " + _outsideToastParent.name);
                }
            }
            if (_outsideToastParent == null)
            {
                LoggerInstance.Error("[BlackjackCouple] 未找到任何原生 Canvas 作为 toast 父节点");
                return;
            }
            // 诊断：父 Canvas 的实际渲染状态
            try
            {
                var pc = _outsideToastParent.GetComponent<Canvas>();
                LoggerInstance.Msg("[BlackjackCouple] toast 父 Canvas: name=" + _outsideToastParent.name
                    + " activeInHierarchy=" + _outsideToastParent.gameObject.activeInHierarchy
                    + " renderMode=" + (pc != null ? pc.renderMode.ToString() : "无Canvas组件")
                    + " sortingOrder=" + (pc != null ? pc.sortingOrder.ToString() : "N/A")
                    + " worldCamera=" + (pc != null && pc.worldCamera != null ? pc.worldCamera.name : "N/A"));
            }
            catch (Exception e)
            {
                LoggerInstance.Error("[BlackjackCouple] 父 Canvas 诊断失败: " + e);
            }
        }

        private Image ToastCreateImage(string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_outsideToastParent, false);
            go.layer = _outsideToastParent.gameObject.layer; // 继承父 UI 层（游戏 UI 相机只渲染 layer 5）
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Text ToastCreateText(string name, string text, int fontSize, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_outsideToastParent, false);
            go.layer = _outsideToastParent.gameObject.layer; // 继承父 UI 层（游戏 UI 相机只渲染 layer 5）
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var txt = go.AddComponent<Text>();
            txt.font = ToastGetDefaultFont();
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            txt.text = text;
            return txt;
        }

        private static Font ToastGetDefaultFont()
        {
            try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { }
            try { return Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            catch { }
            return null;
        }

        /// <summary>toast 淡入：0.30s 由透明到完全显示（内衬/文字同步）</summary>
        private IEnumerator ToastAnimateIn(int ver)
        {
            if (_outsideToastBg == null || _outsideToastText == null) yield break;
            Color ib = _outsideToastBg.color;
            Color it = _outsideToastText.color;
            float t = 0f;
            const float dur = 0.3f;
            while (t < dur)
            {
                if (ver != _outsideToastVersion) yield break;
                t += Time.deltaTime;
                float a = Mathf.Lerp(0f, 1f, t / dur);
                _outsideToastBg.color = new Color(ib.r, ib.g, ib.b, ib.a * a);
                _outsideToastText.color = new Color(it.r, it.g, it.b, it.a * a);
                yield return null;
            }
            if (ver != _outsideToastVersion) yield break;
            _outsideToastBg.color = new Color(ib.r, ib.g, ib.b, ib.a);
            _outsideToastText.color = new Color(it.r, it.g, it.b, it.a);
            LoggerInstance.Msg("[BlackjackCouple] toast 淡入完成");
        }

        /// <summary>toast 淡出：停留 ToastDisplaySeconds 后 0.3s 淡出并销毁</summary>
        private IEnumerator ToastFadeOut(int ver)
        {
            yield return new WaitForSeconds(ToastDisplaySeconds);
            if (ver != _outsideToastVersion) yield break;
            Color ib = _outsideToastBg != null ? _outsideToastBg.color : new Color(0f, 0f, 0f, 0f);
            Color it = _outsideToastText != null ? _outsideToastText.color : new Color(0f, 0f, 0f, 0f);
            float t = 0f;
            while (t < 0.3f)
            {
                if (ver != _outsideToastVersion) yield break;
                t += Time.deltaTime;
                float a = Mathf.Lerp(1f, 0f, t / 0.3f);
                if (_outsideToastBg != null) _outsideToastBg.color = new Color(ib.r, ib.g, ib.b, ib.a * a);
                if (_outsideToastText != null) _outsideToastText.color = new Color(it.r, it.g, it.b, it.a * a);
                yield return null;
            }
            if (ver != _outsideToastVersion) yield break;
            if (_outsideToastBg != null) UnityEngine.Object.Destroy(_outsideToastBg.gameObject);
            if (_outsideToastText != null) UnityEngine.Object.Destroy(_outsideToastText.gameObject);
            _outsideToastBg = null;
            _outsideToastText = null;
        }

        /// <summary>打开 21 点小游戏（供命令 / 其它 MOD / 后续 UI 入口调用）</summary>
        public static void OpenBlackjack()
        {
            if (_instance == null) return;
            _instance.StartCoroutineSafe();
        }

        // MelonMod 无 StartCoroutine，改用 MelonLoader 协程调度器
        private void StartCoroutineSafe()
        {
            MelonCoroutines.Start(LazyOpen());
        }

        private IEnumerator LazyOpen()
        {
            if (_gameStarted)
            {
                LoggerInstance.Msg("[BlackjackCouple] 已有一局进行中");
                yield break;
            }
            _gameStarted = true;
            _gameStartTime = Time.time;
            try
            {
                HandleIOUCheck(GameVariables.Current);
            }
            catch { }

            // 欠条过多限制：玩家未清偿欠条达到上限时，Bot 拒绝陪玩（与"再来一局"共用 PlayerBlocked 校验）
            try
            {
                var gv = GameVariables.Current;
                if (IOUSystem.PlayerBlocked(gv))
                {
                    IOUSystem.ShowOweBlock(gv);
                    LoggerInstance.Msg("[BlackjackCouple] 欠条过多，Bot 拒绝陪玩");
                    _gameStarted = false;
                    yield break;
                }
            }
            catch { }

            // 尝试获取 Bot 与玩家自定义名
            try
            {
                var gv = GameVariables.Current;
                if (gv != null)
                {
                    var bot = gv.botName;
                    if (!string.IsNullOrEmpty(bot))
                        BlackjackGui.BotDisplayName = bot;
                    var player = gv.playerName;
                    if (!string.IsNullOrEmpty(player))
                        BlackjackGui.PlayerDisplayName = player;
                }
            }
            catch { }

            // 查找场景中是否已存在 BlackjackGui；没有则动态创建
            if (_gui == null)
            {
                _gui = UnityEngine.Object.FindObjectOfType<BlackjackGui>();
            }
            if (_gui == null)
            {
                // 动态创建 BlackjackGui（UI 由其在运行时自建，无需场景绑定）
                var go = new GameObject("BlackjackCouple_Gui");
                _gui = go.AddComponent<BlackjackGui>();
                LoggerInstance.Msg("[BlackjackCouple] 已创建 BlackjackGui 实例");
            }

            _gui.StartGame(() => { _gameStarted = false; });
            yield break;
        }
    }
}
