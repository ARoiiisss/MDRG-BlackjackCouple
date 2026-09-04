using System;
using System.Collections;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppCards;
// Card/Rank/Suit 为 Il2CppCards.CardDeck 的嵌套类型，需 using 别名提升
using Card = Il2CppCards.CardDeck.Card;
using Rank = Il2CppCards.CardDeck.Rank;
using Suit = Il2CppCards.CardDeck.Suit;
using Il2CppInterop.Runtime.Attributes;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BlackjackCouple
{
    /// <summary>
    /// 21点情侣版 GUI —— 仿游戏内"抽鬼牌(OldMaid)"界面风格重做。
    /// 暗色氛围背景 / 玩家手牌底部横排 / 对手(庄家)区在上且盖牌 / 卡牌复用游戏自带 Cards 图集(CardScript) / 结算内嵌顶部状态条不弹窗。
    /// 仅改展示层，BlackjackRules / IOUSystem 规则与欠条逻辑不变。
    /// </summary>
    [RegisterTypeInIl2Cpp]
    public class BlackjackGui : StaticGuiBase
    {
        // 画布参考分辨率（CanvasScaler ScaleWithScreenSize 适配不同分辨率）
        private const float RefW = 1920f;
        private const float RefH = 1080f;

        // ---- 牌局数据（纯逻辑，驱动方式保持原样）----
        private CardDeck _deck;
        private readonly List<Card> _playerCards = new List<Card>();
        private readonly List<Card> _dealerCards = new List<Card>();
        private bool _playerStood;
        private bool _doubled;
        private bool _gameRunning;
        private bool _dealerHoleRevealed; // 庄家暗牌是否已翻开
        private bool _helpShown;
        private Action _onFinished;

        // ---- UI 引用（运行时自建）----
        private GameObject _root;
        private Text _playerText;
        private Text _dealerText;
        private Text _statusText;
        private Image _statusBar;
        private GameObject _playerCardRoot;
        private GameObject _dealerCardRoot;
        private Button _hitButton;
        private Button _standButton;
        private Button _doubleButton;
        private Button _restartButton;
        private Button _returnButton;
        private Button _helpButton;

        // 非模态赢钱到账提示（toast），不拦截任何按钮点击
        private Image _toast;
        private Text _toastText;

        // 外部注入：Bot 名字（由 Main 在打开时设置）；为空时回退本地化默认名
        public static string BotDisplayName = null;

        // 外部注入：玩家名字（由 Main 在打开时设置，读取玩家自定义名）；为空时回退本地化默认前缀
        public static string PlayerDisplayName = null;

        /// <summary>显示用的 Bot 名字：优先注入名（机器人自定义名），其次本地化默认名</summary>
        private static string BotName()
        {
            return string.IsNullOrEmpty(BotDisplayName) ? Localization.T("bot_name") : BotDisplayName;
        }

        /// <summary>显示用的玩家名字：优先注入名（玩家自定义名），其次本地化默认前缀</summary>
        private static string PlayerName()
        {
            return string.IsNullOrEmpty(PlayerDisplayName) ? Localization.T("player_prefix") : PlayerDisplayName;
        }

        public override void FillReferencesInherited()
        {
            base.FillReferencesInherited();
            // 运行时自建 UI，无需场景序列化引用
        }

        /// <summary>入口：开始一局 21 点</summary>
        public void StartGame(Action finished)
        {
            _onFinished = finished;
            EnsureUI();
            MelonCoroutines.Start(Game());
        }

        // ---------- UI 构建（暗色氛围，仿抽鬼牌） ----------

        private void EnsureUI()
        {
            if (_root != null) return;

            _root = new GameObject("BlackjackCouple_Root");
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.matchWidthOrHeight = 0.5f;
            _root.AddComponent<GraphicRaycaster>();
            UnityEngine.Object.DontDestroyOnLoad(_root);

            EnsureEventSystem();

            BuildBackground();
            BuildStatusBar();
            BuildDealerArea();
            BuildPlayerArea();
            BuildActionRow();
            BuildCornerButtons();

            _dealerText.text = BotName();
            _playerText.text = PlayerName();
            _statusText.text = BotName() + Localization.TRandom("opening_line");
        }

        /// <summary>暗色氛围背景：全屏深蓝黑底 + 中央暗色牌桌面板 + 四周暗角</summary>
        private void BuildBackground()
        {
            CreateImage(_root.transform, "OldMaid_Background", new Vector2(0, 0), new Vector2(RefW, RefH),
                new Color(0.045f, 0.05f, 0.10f, 1f));

            CreateImage(_root.transform, "OldMaid_TablePanel", new Vector2(0, 30), new Vector2(1760, 780),
                new Color(0.10f, 0.11f, 0.17f, 0.55f));

            Color vignette = new Color(0f, 0f, 0f, 0.50f);
            const float edge = 140f;
            CreateImage(_root.transform, "OldMaid_VignetteTop", new Vector2(0, RefH / 2f - edge / 2f), new Vector2(RefW, edge), vignette);
            CreateImage(_root.transform, "OldMaid_VignetteBottom", new Vector2(0, -RefH / 2f + edge / 2f), new Vector2(RefW, edge), vignette);
            CreateImage(_root.transform, "OldMaid_VignetteLeft", new Vector2(-RefW / 2f + edge / 2f, 0), new Vector2(edge, RefH), vignette);
            CreateImage(_root.transform, "OldMaid_VignetteRight", new Vector2(RefW / 2f - edge / 2f, 0), new Vector2(edge, RefH), vignette);
        }

        /// <summary>顶部偏上状态/结算信息条：深色半透明底 + 居中文字</summary>
        private void BuildStatusBar()
        {
            _statusBar = CreateImage(_root.transform, "OldMaid_StatusBar", new Vector2(0, 468), new Vector2(1680, 64),
                new Color(0f, 0f, 0f, 0.35f));
            _statusText = CreateText(_root.transform, "OldMaid_StatusText", Localization.T("status_title"), 30,
                new Vector2(0, 468), new Vector2(1600, 56), new Color(0.95f, 0.94f, 0.88f, 1f));
        }

        /// <summary>顶部庄家(对手)区：标题 + 卡牌横排容器</summary>
        private void BuildDealerArea()
        {
            _dealerText = CreateText(_root.transform, "OldMaid_DealerTitle", BotName(), 30,
                new Vector2(0, 320), new Vector2(900, 40), new Color(0.80f, 0.82f, 0.92f, 1f));
            _dealerCardRoot = CreateAnchor(_root.transform, "OldMaid_DealerCards", new Vector2(0, 242));
        }

        /// <summary>底部玩家手牌区：标题 + 卡牌横排容器</summary>
        private void BuildPlayerArea()
        {
            _playerText = CreateText(_root.transform, "OldMaid_PlayerTitle", PlayerName(), 30,
                new Vector2(0, -118), new Vector2(900, 40), new Color(0.80f, 0.82f, 0.92f, 1f));
            _playerCardRoot = CreateAnchor(_root.transform, "OldMaid_PlayerCards", new Vector2(0, -205));
        }

        /// <summary>底部操作按钮行：要牌 / 停牌 / 加倍 / 再来一局（结算后显示）</summary>
        private void BuildActionRow()
        {
            _hitButton = CreateButton(_root.transform, "OldMaid_BtnHit", Localization.T("hit"), new Vector2(-390, -420), OnHitPressed);
            _standButton = CreateButton(_root.transform, "OldMaid_BtnStand", Localization.T("stand"), new Vector2(-130, -420), OnStandPressed);
            _doubleButton = CreateButton(_root.transform, "OldMaid_BtnDouble", Localization.T("double"), new Vector2(130, -420), OnDoublePressed);
            _restartButton = CreateButton(_root.transform, "OldMaid_BtnRestart", Localization.T("restart"), new Vector2(390, -420), OnRestartPressed);
            _restartButton.gameObject.SetActive(false);
        }

        /// <summary>右下角：说明 + 返回</summary>
        private void BuildCornerButtons()
        {
            _helpButton = CreateButton(_root.transform, "OldMaid_BtnHelp", Localization.T("help"), new Vector2(700, -470), OnHelpPressed);
            _returnButton = CreateButton(_root.transform, "OldMaid_BtnReturn", Localization.T("return"), new Vector2(860, -470), Close);
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("BlackjackCouple_EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        private static Font GetDefaultFont()
        {
            try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { }
            try { return Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            catch { }
            return null;
        }

        private GameObject CreateAnchor(Transform parent, string name, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(1900, 300);
            return go;
        }

        private Image CreateImage(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Text CreateText(Transform parent, string name, string text, int fontSize, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject("BlackjackCouple_Text_" + name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var txt = go.AddComponent<Text>();
            txt.font = GetDefaultFont();
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            txt.text = text;
            return txt;
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 pos, Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(120, 56);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.16f, 0.17f, 0.24f, 0.92f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var tgo = new GameObject("Text");
            tgo.transform.SetParent(go.transform, false);
            var trt = tgo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var t = tgo.AddComponent<Text>();
            t.font = GetDefaultFont();
            t.fontSize = 24;
            t.color = new Color(0.93f, 0.92f, 0.86f, 1f);
            t.alignment = TextAnchor.MiddleCenter;
            t.text = label;

            btn.onClick.AddListener(onClick);
            return btn;
        }

        public void Close()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
            _gameRunning = false;
            if (_onFinished != null)
            {
                var f = _onFinished;
                _onFinished = null;
                f();
            }
        }

        // ---------- 牌局 ----------

        /// <summary>
        /// "再来一局"：先校验欠条上限，超限则拒绝并提示（与开局入口一致），
        /// 防止玩家通过"再来一局"绕过欠条堆积限制直接连玩。
        /// 被拒提示用非模态红色警告条（自动淡出、不挡按钮、连点只刷新同一条，不会堆叠弹窗）。
        /// </summary>
        private void OnRestartPressed()
        {
            var gv = GameVariables.Current;
            if (IOUSystem.PlayerBlocked(gv))
            {
                ShowWinToast(IOUSystem.GetBlockMessage(gv), new Color(1f, 0.38f, 0.38f, 1f));
                return;
            }
            MelonCoroutines.Start(Game());
        }

        public IEnumerator Game()
        {
            if (_gameRunning && _root != null) yield break;
            _gameRunning = true;
            _playerStood = false;
            _doubled = false;
            _dealerHoleRevealed = false;
            _helpShown = false;
            _playerCards.Clear();
            _dealerCards.Clear();

            // 再来一局的过渡缓冲：先显示洗牌提示（带淡入），再发牌，避免界面瞬间切换
            ShowStatus(Localization.TRandom("restarting"));
            yield return new WaitForSeconds(0.6f);

            _deck = CardDeck.GenerateFullDeck();
            _deck.Shuffle();

            DealTo(_playerCards, 2);
            DealTo(_dealerCards, 2);
            RefreshUI();   // 新牌 CanvasGroup 淡入

            ShowStatus(BotName() + Localization.TRandom("opening_line"));
            _restartButton.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.6f);

            int playerTotal = BlackjackRules.HandValue(_playerCards);
            int dealerTotal = BlackjackRules.HandValue(_dealerCards);

            // 玩家回合
            SetButtons(true, true, _playerCards.Count == 2, false);
            while (!_playerStood && !BlackjackRules.IsBust(playerTotal))
            {
                if (playerTotal >= BlackjackRules.Target) break;

                yield return WaitForPlayerDecision();

                // 加倍：只补一张并自动停牌
                if (_doubled && !_playerStood)
                {
                    DealTo(_playerCards, 1);
                    playerTotal = BlackjackRules.HandValue(_playerCards);
                    RefreshUI();
                    break;
                }

                if (_playerStood) break;

                DealTo(_playerCards, 1);
                playerTotal = BlackjackRules.HandValue(_playerCards);
                RefreshUI();
                SetButtons(true, true, _playerCards.Count == 2, false);

                if (BlackjackRules.IsBust(playerTotal))
                {
                    ShowStatus(Localization.TRandom("bust_text"));
                    yield return new WaitForSeconds(0.9f);
                    break;
                }
                if (playerTotal == BlackjackRules.Target)
                {
                    ShowStatus(Localization.TRandom("blackjack_text"));
                    yield return new WaitForSeconds(1.1f);
                    break;
                }
            }

            // 庄家回合（玩家未爆牌时，无论是否停牌都要让庄家要牌），进入前翻开暗牌
            if (!BlackjackRules.IsBust(playerTotal))
            {
                _dealerHoleRevealed = true;
                RefreshUI();
                ShowStatus(BotName() + Localization.TRandom("dealer_turn"));
                while (BlackjackRules.DealerWantsCard(dealerTotal, playerTotal))
                {
                    yield return new WaitForSeconds(0.6f);
                    DealTo(_dealerCards, 1);
                    dealerTotal = BlackjackRules.HandValue(_dealerCards);
                    RefreshUI();
                    if (BlackjackRules.IsBust(dealerTotal)) break;
                }
                if (BlackjackRules.IsBust(dealerTotal))
                {
                    ShowStatus(BotName() + Localization.TRandom("dealer_bust"));
                    yield return new WaitForSeconds(0.9f);
                }
            }
            else
            {
                _dealerHoleRevealed = true;
            }

            // 结算（内嵌状态条，不弹窗）
            SetButtons(false, false, false, false);
            yield return HandleOutcome(BlackjackRules.Resolve(playerTotal, dealerTotal));

            // 再来 / 返回（返回按钮常驻右下角，无需再显示）
            // 结算时 SetButtons 已把 restart 设为不可交互，这里必须显式恢复 interactable，否则按钮可见但点不动
            _restartButton.gameObject.SetActive(true);
            _restartButton.interactable = true;
            MelonCoroutines.Start(FadeInRestart());
            _gameRunning = false;
        }

        /// <summary>「再来一局」按钮淡入：0.35s 由透明渐显（与文字缓冲一致，避免结算后瞬间冒出）</summary>
        private IEnumerator FadeInRestart()
        {
            if (_restartButton == null) yield break;
            var img = _restartButton.GetComponent<Image>();
            var txt = _restartButton.transform.Find("Text") != null
                ? _restartButton.transform.Find("Text").GetComponent<Text>()
                : null;
            Color ic = img != null ? img.color : default;
            Color tc = txt != null ? txt.color : default;
            if (img != null) img.color = new Color(ic.r, ic.g, ic.b, 0f);
            if (txt != null) txt.color = new Color(tc.r, tc.g, tc.b, 0f);

            float t = 0f;
            const float dur = 0.35f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(0f, 1f, t / dur);
                if (img != null) img.color = new Color(ic.r, ic.g, ic.b, a);
                if (txt != null) txt.color = new Color(tc.r, tc.g, tc.b, a);
                yield return null;
            }
            if (img != null) img.color = ic;
            if (txt != null) txt.color = tc;
        }

        private void DealTo(List<Card> hand, int count)
        {
            if (_deck == null) return;
            for (int i = 0; i < count; i++)
                hand.Add(_deck.TakeRandom());
        }

        private void RefreshUI()
        {
            if (_playerText != null)
                _playerText.text = PlayerName() + "  "
                    + string.Format(Localization.T("hand_points"), BlackjackRules.HandValue(_playerCards));
            if (_dealerText != null)
            {
                if (_dealerHoleRevealed)
                    _dealerText.text = BotName() + "  " + string.Format(Localization.T("hand_points"), BlackjackRules.HandValue(_dealerCards));
                else
                    _dealerText.text = BotName() + "  " + Localization.TRandom("dealer_turn");
            }

            // 重建卡牌（复用游戏自带 CardScript 渲染，不使用程序化画牌面）
            RebuildCards(_playerCardRoot.transform, _playerCards, -1);
            RebuildCards(_dealerCardRoot.transform, _dealerCards, _dealerHoleRevealed ? -1 : 1);
        }

        /// <summary>重建卡牌区。hiddenIndex&gt;=0 表示该位置牌盖着（背面朝上）。</summary>
        private void RebuildCards(Transform root, List<Card> cards, int hiddenIndex)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var c = root.GetChild(i).gameObject;
                UnityEngine.Object.Destroy(c);
            }

            if (cards == null || cards.Count == 0) return;

            float spacing = 110f;
            float total = (cards.Count - 1) * spacing;
            float startX = -total / 2f;
            for (int i = 0; i < cards.Count; i++)
            {
                bool faceUp = !(hiddenIndex == i);
                CreateCard(root, cards[i], faceUp, new Vector2(startX + i * spacing, 0f));
            }

            // 整体淡入过渡：重建后整组牌从透明渐显（0.30s），避免发牌/补牌瞬间切换
            var cg = root.GetComponent<CanvasGroup>();
            if (cg == null) cg = root.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            MelonCoroutines.Start(FadeInCardGroup(cg));
        }

        /// <summary>卡牌区整体淡入：0.30s 由透明到不透明（与文字缓冲一致）</summary>
        private IEnumerator FadeInCardGroup(CanvasGroup cg)
        {
            float t = 0f;
            const float dur = 0.3f;
            while (t < dur)
            {
                if (cg == null) yield break;
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(0f, 1f, t / dur);
                yield return null;
            }
            if (cg != null) cg.alpha = 1f;
        }

        /// <summary>
        /// 方案A：复用游戏自带 Il2Cpp.CardScript 渲染每张牌。
        /// GameObject = Image + CardScript，设 cardScript._image / cardImageSource=Real / InternalCard（触发内部换图）/ Visible（正反）。
        /// </summary>
        private void CreateCard(Transform parent, Card card, bool faceUp, Vector2 anchoredPos)
        {
            var go = new GameObject("BJCard_" + card.Rank + "_" + card.Suit);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(88f, 124f);

            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;

            var cs = go.AddComponent<CardScript>();
            cs._image = img;
            cs.cardImageSource = CardScript.CardImageSource.Real;
            cs.InternalCard = card; // 无参构造 + Rank/Suit 属性；逻辑牌即游戏牌，无需映射
            cs.Visible = faceUp;    // false 显示牌背（庄家暗牌）
        }

        private static string Describe(List<Card> cards)
        {
            if (cards == null || cards.Count == 0) return Localization.T("empty_hand");
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < cards.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                var c = cards[i];
                if (c == null) { sb.Append('?'); continue; }
                sb.Append(SuitChar(c.Suit)).Append(RankText(c.Rank));
            }
            return sb.ToString();
        }

        private static string SuitChar(Suit s)
        {
            switch (s)
            {
                case Suit.Spade: return Localization.T("suit_spade");
                case Suit.Heart: return Localization.T("suit_heart");
                case Suit.Diamond: return Localization.T("suit_diamond");
                default: return Localization.T("suit_club");
            }
        }

        private static string RankText(Rank r)
        {
            switch (r)
            {
                case Rank.Jack: return Localization.T("rank_j");
                case Rank.Queen: return Localization.T("rank_q");
                case Rank.King: return Localization.T("rank_k");
                case Rank.Ace: return Localization.T("rank_a");
                default: return ((int)r).ToString();
            }
        }

        // ---------- 玩家决策 ----------

        private IEnumerator WaitForPlayerDecision()
        {
            WaitForPlayerDecisionToken token = new WaitForPlayerDecisionToken();
            _pendingDecision = token;
            while (_pendingDecision != null && !_pendingDecision.Decided)
                yield return null;
            _playerStood = _pendingDecision != null && _pendingDecision.Stood;
            _pendingDecision = null;
        }

        private class WaitForPlayerDecisionToken
        {
            public bool Decided;
            public bool Stood;
        }
        private WaitForPlayerDecisionToken _pendingDecision;

        public void OnHitPressed()
        {
            if (_pendingDecision != null)
            {
                _pendingDecision.Stood = false;
                _pendingDecision.Decided = true;
            }
        }

        public void OnStandPressed()
        {
            if (_pendingDecision != null)
            {
                _pendingDecision.Stood = true;
                _pendingDecision.Decided = true;
            }
        }

        public void OnDoublePressed()
        {
            if (_playerCards.Count != 2) return;
            if (_pendingDecision != null)
            {
                _doubled = true;
                _pendingDecision.Stood = false;
                _pendingDecision.Decided = true;
            }
        }

        /// <summary>右下角说明按钮：在状态条显示/收起玩法说明</summary>
        public void OnHelpPressed()
        {
            _helpShown = !_helpShown;
            ShowStatus(_helpShown ? Localization.T("help_text") : Localization.TRandom("opening_line"));
        }

        // ---------- 状态条文字动画 ----------

        private int _statusVersion;

        /// <summary>状态条显示入口：旧文字淡出、新文字淡入，切换有缓冲动画</summary>
        private void ShowStatus(string text)
        {
            if (_statusText == null) return;
            _statusVersion++;
            int ver = _statusVersion;
            MelonCoroutines.Start(AnimateStatus(text, ver));
        }

        /// <summary>文字切换动画：0.12s 淡出旧文字 → 换新文字 → 0.30s 淡入新文字</summary>
        private IEnumerator AnimateStatus(string text, int ver)
        {
            if (_statusText == null) yield break;
            Color c = _statusText.color;

            // 淡出旧文字
            const float fadeOut = 0.12f;
            float t = 0f;
            while (t < fadeOut)
            {
                if (ver != _statusVersion) yield break;
                t += Time.deltaTime;
                _statusText.color = new Color(c.r, c.g, c.b, Mathf.Lerp(c.a, 0f, t / fadeOut));
                yield return null;
            }

            _statusText.text = text;

            // 淡入新文字
            const float fadeIn = 0.30f;
            t = 0f;
            while (t < fadeIn)
            {
                if (ver != _statusVersion) yield break;
                t += Time.deltaTime;
                _statusText.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0f, c.a, t / fadeIn));
                yield return null;
            }
            _statusText.color = c;
        }

        // ---------- 非模态赢钱到账提示（toast） ----------

        private int _toastVersion;

        /// <summary>顶部醒目提示条：深色底 + 指定颜色文字（默认金色），2 秒后自动淡出销毁，raycastTarget=false 不拦截任何点击</summary>
        private void ShowWinToast(string text, Color textColor = default)
        {
            if (_root == null) return;
            if (textColor == default) textColor = new Color(1f, 0.88f, 0.35f, 1f);   // 默认金色（赢钱）
            _toastVersion++;
            int ver = _toastVersion;

            if (_toast == null)
            {
                _toast = CreateImage(_root.transform, "WinToast", new Vector2(0, 390), new Vector2(760, 70), new Color(0f, 0f, 0f, 0.72f));
                _toastText = CreateText(_root.transform, "WinToastText", text, 30, new Vector2(0, 390), new Vector2(740, 64), new Color(textColor.r, textColor.g, textColor.b, 1f));
            }
            else
            {
                _toastText.text = text;
                _toastText.color = new Color(textColor.r, textColor.g, textColor.b, 1f);
            }

            // 与状态条一致的淡入缓冲，不瞬间弹出
            _toast.color = new Color(_toast.color.r, _toast.color.g, _toast.color.b, 0f);
            _toastText.color = new Color(_toastText.color.r, _toastText.color.g, _toastText.color.b, 0f);
            MelonCoroutines.Start(AnimateToastIn(ver));
            MelonCoroutines.Start(FadeWinToast(ver));
        }

        /// <summary>toast 淡入：0.30s 由透明到完全显示（与状态条 AnimateStatus 节奏一致）</summary>
        private IEnumerator AnimateToastIn(int ver)
        {
            if (_toast == null || _toastText == null) yield break;
            Color ic = _toast.color;
            Color tc = _toastText.color;
            float t = 0f;
            const float dur = 0.3f;
            while (t < dur)
            {
                if (ver != _toastVersion) yield break;
                t += Time.deltaTime;
                float a = Mathf.Lerp(0f, 1f, t / dur);
                _toast.color = new Color(ic.r, ic.g, ic.b, a);
                _toastText.color = new Color(tc.r, tc.g, tc.b, a);
                yield return null;
            }
            if (ver != _toastVersion) yield break;
            _toast.color = new Color(ic.r, ic.g, ic.b, 0.72f);
            _toastText.color = new Color(tc.r, tc.g, tc.b, 1f);
        }

        private IEnumerator FadeWinToast(int ver)
        {
            yield return new WaitForSeconds(2.0f);
            if (ver != _toastVersion) yield break;
            if (_toastText != null)
            {
                Color c = _toastText.color;
                float t = 0f;
                while (t < 0.3f)
                {
                    if (ver != _toastVersion) yield break;
                    t += Time.deltaTime;
                    _toastText.color = new Color(c.r, c.g, c.b, Mathf.Lerp(c.a, 0f, t / 0.3f));
                    yield return null;
                }
            }
            if (ver != _toastVersion) yield break;
            if (_toast != null) UnityEngine.Object.Destroy(_toast);
            if (_toastText != null) UnityEngine.Object.Destroy(_toastText.gameObject);
            _toast = null;
            _toastText = null;
        }

        // ---------- 结算与欠条（内嵌状态条，不弹 OkPopup） ----------

        // 玩家赢钱奖励（游戏内货币 $），按用户要求由 100 调整为 10
        private const int RewardWin = 10;

        private IEnumerator HandleOutcome(BlackjackRules.Outcome outcome)
        {
            int iouCount = _doubled ? 2 : 1;

            switch (outcome)
            {
                case BlackjackRules.Outcome.PlayerBlackjack:
                case BlackjackRules.Outcome.PlayerWin:
                {
                    // 结果宣告后短暂停顿，再逐条展示赢钱话语（发钱在展示前执行）
                    yield return new WaitForSeconds(0.5f);
                    string winLine = outcome == BlackjackRules.Outcome.PlayerBlackjack
                        ? string.Format(Localization.TRandom("reward_blackjack"), RewardWin * iouCount)
                        : string.Format(Localization.TRandom("reward_win"), RewardWin * iouCount);
                    PayPlayer(RewardWin * iouCount);
                    // 到账提示只走金色 toast（非模态、淡入缓冲），状态条不再重复显示 winLine
                    ShowWinToast(winLine);
                    yield return new WaitForSeconds(1.2f);
                    break;
                }
                case BlackjackRules.Outcome.Push:
                    yield return new WaitForSeconds(0.5f);
                    ShowStatus(Localization.TRandom("push_text"));
                    yield return new WaitForSeconds(0.9f);
                    break;
                default:
                {
                    yield return new WaitForSeconds(0.5f);
                    string countWord = iouCount > 1 ? Localization.T("count_two") : Localization.T("count_one");
                    string loseLine = string.Format(Localization.TRandom("lose_due"), countWord);
                    var ious = IssueIOUs("player", iouCount);
                    ShowStatus(loseLine);
                    yield return new WaitForSeconds(1.0f);
                    // 欠条明细内嵌状态条展示（不弹窗，避免遮挡游戏界面按钮）
                    string detail = BuildIOUDetail(ious, _doubled);
                    if (!string.IsNullOrEmpty(detail)) ShowStatus(detail);
                    break;
                }
            }

            // 结算结果直接显示在顶部状态条内，界面不关闭，不弹窗
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>玩家赢：直接给玩家钱包发钱（Bot 请客）</summary>
        private void PayPlayer(int amount)
        {
            var gv = GameVariables.Current;
            if (gv == null) return;
            try { gv.AddMoney(amount); }
            catch { }
        }

        /// <summary>生成欠条，返回本次生成的欠条列表（用于弹窗明细）</summary>
        private List<IOUSystem.IOU> IssueIOUs(string holder, int count)
        {
            var list = new List<IOUSystem.IOU>();
            var gv = GameVariables.Current;
            if (gv == null) return list;
            for (int i = 0; i < count; i++)
                list.Add(IOUSystem.Issue(gv, holder));
            return list;
        }

        /// <summary>拼接欠条明细文本（内嵌状态条用），列出欠了些什么，让玩家知道要还什么。
        /// 普通（单张/未加倍）展示动作台词；加倍（双张）用独立文案区 iou_double_block，
        /// 明细列动作名而非把两张动作台词 A+B 拼接。</summary>
        private static string BuildIOUDetail(List<IOUSystem.IOU> ious, bool doubled)
        {
            if (ious == null || ious.Count == 0) return "";
            try
            {
                var sb = new System.Text.StringBuilder();
                if (doubled)
                {
                    // 加倍：双倍欠条独立文案区（可在 cfg 单独编辑），明细列出欠了哪些动作（按动作分组计数）
                    string names = BuildIOUActionNames(ious);
                    string tpl = Localization.TRandom("iou_double_block");
                    if (!string.IsNullOrEmpty(names))
                    {
                        if (!string.IsNullOrEmpty(tpl) && tpl.Contains("{0}"))
                            sb.Append(string.Format(tpl, names));
                        else if (!string.IsNullOrEmpty(tpl))
                            sb.Append(tpl + " " + names);
                        else
                            sb.Append(names);
                    }
                }
                else
                {
                    for (int i = 0; i < ious.Count; i++)
                    {
                        var act = IOUSystem.FindAction(ious[i].ActionName);
                        if (act == null) continue;
                        if (sb.Length > 0) sb.Append("  ");
                        sb.Append(act.Line);
                    }
                }
                if (sb.Length > 0) return "\n" + sb.ToString();
            }
            catch { }
            return "";
        }

        /// <summary>本次生成欠条的动作名汇总（按动作分组计数），如"亲亲×2、深入互动×1"</summary>
        private static string BuildIOUActionNames(List<IOUSystem.IOU> ious)
        {
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < ious.Count; i++)
            {
                if (ious[i] == null) continue;
                if (counts.TryGetValue(ious[i].ActionName, out var c)) counts[ious[i].ActionName] = c + 1;
                else counts[ious[i].ActionName] = 1;
            }
            var sb = new System.Text.StringBuilder();
            bool first = true;
            foreach (var kv in counts)
            {
                var act = IOUSystem.FindAction(kv.Key);
                if (act == null) continue;
                if (!first) sb.Append("、");
                first = false;
                sb.Append(act.Name);
                if (kv.Value > 1) sb.Append("×" + kv.Value);
            }
            return sb.ToString();
        }

        private void SetButtons(bool hit, bool stand, bool dbl, bool restart)
        {
            if (_hitButton != null) _hitButton.interactable = hit;
            if (_standButton != null) _standButton.interactable = stand;
            if (_doubleButton != null) _doubleButton.interactable = dbl;
            if (_restartButton != null) _restartButton.interactable = restart;
        }
    }
}
