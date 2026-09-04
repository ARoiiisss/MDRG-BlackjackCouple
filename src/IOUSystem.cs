using System;
using System.Collections.Generic;
using Il2Cpp;
using Newtonsoft.Json;
using UnityEngine;

namespace BlackjackCouple
{
    /// <summary>
    /// 点名制 IOU 欠条系统（C# 实现，直接属性访问 GameVariables，无反射）。
    /// 机制：小游戏结算时败方被点名生成一张欠条，绑唯一时间戳检测变量；
    ///       清偿 = 检测变量变化即还清；限期 3 天未还 → Bot 心情 -10 + 催债台词。
    /// 每张欠条只盯一个检测点，天然无"一次互动误清多项"的去重问题。
    /// </summary>
    public static class IOUSystem
    {
        // 点名动作池（每项唯一时间戳检测变量，天然去重）
        public sealed class IOUAction
        {
            public string Key;      // 稳定标识（如 "kiss"），写入存档用于匹配，跨语言安全
            public string Field;    // gv 上的检测变量名
            public string LineKey;  // 生成欠条台词 key（本地化）
            public string DueKey;   // 过期催债台词 key（本地化）

            // 显示名 / 台词（全部走本地化）
            public string Name => Localization.T("iou_" + Key + "_name");
            public string Line => Localization.TRandom(LineKey);
            public string DueLine => Localization.TRandom(DueKey);
        }

        // 动作池：与方案定稿一致，6 种，不扩（Name/Line/DueLine 均从 Localization 读取）
        public static readonly IOUAction[] Actions = new IOUAction[]
        {
            new IOUAction { Key = "kiss",     Field = "lastKissedAt",         LineKey = "iou_kiss_line",     DueKey = "iou_kiss_due" },
            new IOUAction { Key = "headpat",  Field = "lastHeadpatedAt",      LineKey = "iou_headpat_line",  DueKey = "iou_headpat_due" },
            new IOUAction { Key = "cuddle",   Field = "lastCuddledAt",        LineKey = "iou_cuddle_line",   DueKey = "iou_cuddle_due" },
            new IOUAction { Key = "intimate", Field = "lastFuckedAt",         LineKey = "iou_intimate_line", DueKey = "iou_intimate_due" },
            new IOUAction { Key = "talk",     Field = "lastTalkedAt",         LineKey = "iou_talk_line",     DueKey = "iou_talk_due" },
            new IOUAction { Key = "outside",  Field = "lastOutsideWithBotAt", LineKey = "iou_outside_line",  DueKey = "iou_outside_due" },
        };

        // 单张欠条数据
        [Serializable]
        public sealed class IOU
        {
            public string ActionName;   // 动作 key（稳定标识，如 "kiss"，跨语言存档安全）
            public string Field;        // 检测变量名
            public long Snapshot;       // 生成时时间戳快照（gv 的 last* 为 int）
            public int DeadlineDay;     // 限期到期日（gv.Day + 3）
            public string Holder;       // "player"（玩家欠Bot）/ "bot"（Bot欠玩家）
            public bool Settled;        // 已还清
            // 兼容字段（历史存档遗留）：早期 kiss 用 lastInteractAt + OtherRefs 复合检测，
            // 现已改为 kiss 专属信号（lastKissedAt，见 KissStampKey / NotifyKiss），
            // 该字段不再写入新欠条，仅保留以便旧存档正常反序列化。
            public long[] OtherRefs;
        }

        // kiss 专属时间戳的持久化键（存于 gv.customData）。
        // 游戏没有 lastKissedAt，且 lastInteractAt 会被任意互动/退出互动刷新导致误清；
        // 由 Main 每帧检测 Live2DController.IsKissing 上升沿（false→true 才算一次真亲吻），
        // 触发时经 NotifyKiss 写入本键作为 kiss 欠条的唯一检测信号，其它互动不影响。
        public const string KissStampKey = "BlackjackCouple_LastKissAt";

        /// <summary>Main 检测到一次真亲吻时调用：把 kiss 时间戳写入 customData（kiss 欠条唯一清偿信号）</summary>
        public static void NotifyKiss(GameVariables gv)
        {
            try
            {
                var cd = gv?.customData;
                if (cd == null) return;
                cd.SetStringSpecialVariable(KissStampKey, gv.time.ToString());
            }
            catch { }
        }

        public const float PenaltyMood = 10f;   // 过期惩罚：心情 -10
        public const int DueDays = 3;           // 限期 3 天
        public const int MaxOweLimit = 2;       // 欠条上限：未清偿欠条达到该数则 Bot 拒绝陪玩

        /// <summary>统计未清偿欠条数。holder 传 "player" 只算玩家欠的；传 null/空则统计全部</summary>
        public static int CountOutstanding(GameVariables gv, string holder)
        {
            if (gv == null) return 0;
            var ious = LoadFromCustomData(gv);
            if (ious == null) return 0;
            int n = 0;
            for (int i = 0; i < ious.Count; i++)
            {
                IOU io = ious[i];
                if (io == null || io.Settled) continue;
                if (string.IsNullOrEmpty(holder) || string.Equals(io.Holder, holder, StringComparison.Ordinal))
                    n++;
            }
            return n;
        }

        /// <summary>玩家未清偿欠条是否已达上限（Bot 拒绝陪玩）。供开局入口与"再来一局"共用，防止绕过限制</summary>
        public static bool PlayerBlocked(GameVariables gv)
        {
            return gv != null && CountOutstanding(gv, "player") >= MaxOweLimit;
        }

        // 欠条超限提示防抖：同一时刻连点只会弹一次，避免 OkPopup 堆叠（打开牌桌入口用）
        private static float _lastBlockShownAt = -999f;
        private const float BlockCooldown = 3f;

        /// <summary>
        /// 弹出"欠条过多，Bot 拒绝陪玩"提示（title 为 Bot 名字）。
        /// 内容为通用文案 + 玩家未清偿欠条明细（列举欠了些什么）。
        /// 带时间防抖：3 秒窗口内重复调用不重复弹窗（防止连点堆叠多个模态弹窗）；
        /// 返回是否真的弹出了提示。
        /// </summary>
        public static bool ShowOweBlock(GameVariables gv)
        {
            if (Time.time - _lastBlockShownAt < BlockCooldown) return false;
            _lastBlockShownAt = Time.time;
            UiOverlay.Instance.OkPopup(GetBotName(gv), GetBlockMessage(gv));
            return true;
        }

        /// <summary>玩家未清偿欠条汇总（按动作分组统计数量），用于"拒绝陪玩"时列举欠了些什么。如"亲亲×2、出门×1"。</summary>
        public static string BuildOutstandingSummary(GameVariables gv)
        {
            if (gv == null) return "";
            var ious = LoadFromCustomData(gv);
            if (ious == null) return "";
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < ious.Count; i++)
            {
                IOU io = ious[i];
                if (io == null || io.Settled) continue;
                if (!string.Equals(io.Holder, "player", StringComparison.Ordinal)) continue;
                if (counts.TryGetValue(io.ActionName, out var c)) counts[io.ActionName] = c + 1;
                else counts[io.ActionName] = 1;
            }
            if (counts.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            bool first = true;
            foreach (var kv in counts)
            {
                var act = FindAction(kv.Key);
                if (act == null) continue;
                if (!first) sb.Append("、");
                first = false;
                sb.Append(act.Name);
                if (kv.Value > 1) sb.Append("×" + kv.Value);
            }
            return sb.ToString();
        }

        /// <summary>"拒绝陪玩"提示完整文案：通用文案 + 玩家未清偿欠条明细（列举欠了些什么）</summary>
        public static string GetBlockMessage(GameVariables gv)
        {
            string baseMsg = Localization.TRandom("iou_block_text");
            string detail = BuildOutstandingSummary(gv);
            if (!string.IsNullOrEmpty(detail))
                baseMsg += "\n" + Localization.T("iou_block_detail") + detail;
            return baseMsg;
        }

        // 动作 key -> 动作
        public static IOUAction FindAction(string key)
        {
            for (int i = 0; i < Actions.Length; i++)
                if (Actions[i].Key == key) return Actions[i];
            return null;
        }

        // 随机点名一个动作
        public static IOUAction RollAction()
        {
            return Actions[UnityEngine.Random.Range(0, Actions.Length)];
        }

        /// <summary>当前天数（gv.Day）</summary>
        public static int CurrentDay(GameVariables gv)
        {
            return gv != null ? gv.Day : 0;
        }

        /// <summary>
        /// 读取 gv 指定互动时间戳（int）。字段不存在返回 0。
        /// 全部为 GameVariables 的 public 属性，直接访问。
        /// </summary>
        public static long ReadTimestamp(GameVariables gv, string field)
        {
            if (gv == null) return 0L;
            switch (field)
            {
                case "lastInteractAt":       return gv.lastInteractAt;
                case "lastHeadpatedAt":      return gv.lastHeadpatedAt;
                case "lastCuddledAt":        return gv.lastCuddledAt;
                case "lastFuckedAt":         return gv.lastFuckedAt;
                case "lastTalkedAt":         return gv.lastTalkedAt;
                case "lastOutsideWithBotAt": return gv.lastOutsideWithBotAt;
                case "lastKissedAt":         return ReadKissStamp(gv);
                default: return 0L;
            }
        }

        /// <summary>读取 kiss 专属时间戳（customData 内 KissStampKey，无则 0）</summary>
        private static long ReadKissStamp(GameVariables gv)
        {
            try
            {
                var cd = gv.customData;
                if (cd == null) return 0L;
                string s = cd.GetStringSpecialVariableOrDefault(KissStampKey, "");
                long v;
                return long.TryParse(s, out v) ? v : 0L;
            }
            catch { return 0L; }
        }

        /// <summary>
        /// 生成一张欠条：败方被点名（随机动作），快照当前时间戳，限期 3 天。
        /// holder: "player" = 玩家欠Bot；"bot" = Bot欠玩家。
        /// </summary>
        public static IOU Issue(GameVariables gv, string holder)
        {
            IOUAction act = RollAction();
            IOU io = new IOU
            {
                ActionName = act.Key,
                Field = act.Field,
                Snapshot = ReadTimestamp(gv, act.Field),
                DeadlineDay = CurrentDay(gv) + DueDays,
                Holder = holder,
                Settled = false,
            };
            SaveToCustomData(gv, io);
            return io;
        }

        /// <summary>
        /// 检测所有欠条：清偿（变量变化即还清）+ 过期（心情-10 + 催债）。
        /// 返回本次需要弹窗的台词列表（title 固定为 Bot 名字）。
        /// 同字段多张欠条：靠 ClearedMarks 记录"最近一次清偿消费掉的时间戳"，
        /// 清偿条件 = 当前时间戳大于欠条快照 且 大于该字段上次清偿点，
        /// 因此一次互动只消费一张同类型欠条，欠几张就要互动几次（逐张还清）。
        /// </summary>
        public static List<string[]> CheckAll(GameVariables gv)
        {
            var popups = new List<string[]>();
            if (gv == null) return popups;

            var ious = LoadFromCustomData(gv);
            if (ious == null) return popups;

            var marks = LoadMarks(gv);
            int day = CurrentDay(gv);
            bool changed = false;

            // 正序遍历：先欠的先还，便于同字段多张按顺序逐张消费
            for (int i = 0; i < ious.Count; i++)
            {
                IOU io = ious[i];
                if (io.Settled) { ious.RemoveAt(i); i--; changed = true; continue; }

                IOUAction act = FindAction(io.ActionName);
                if (act == null) { ious.RemoveAt(i); i--; changed = true; continue; }

                // 统一用 act.Field（动作当前检测字段）读取与记账：
                // 兼容历史欠条（如旧 kiss 欠条存的是 lastInteractAt，而动作现绑 lastKissedAt），
                // 保证升级后仍按新检测信号判定。
                string field = act.Field;
                long now = ReadTimestamp(gv, field);
                long mark = marks.TryGetValue(field, out var mv) ? mv : 0L;

                if (now > io.Snapshot && now > mark)
                {
                    // 已还清：本次互动消费掉这一张，同字段后续欠条留待下一次互动
                    marks[field] = now;
                    var popup = new[] { GetBotName(gv), act.Line + " " + Localization.TRandom("iou_settled") };
                    if (string.Equals(field, "lastOutsideWithBotAt", StringComparison.Ordinal))
                        QueuePendingOutsidePopup(popup); // 出门欠条：挂起，等回家（场景切回）时再弹
                    else
                        popups.Add(popup);
                    ious.RemoveAt(i);
                    i--;
                    changed = true;
                    continue;
                }

                if (day >= io.DeadlineDay)
                {
                    // 过期未还：Bot 心情 -10 + 催债（第3天当天即到期，与"限期3天"语义一致）
                    ApplyMoodPenalty(gv);
                    popups.Add(new[] { GetBotName(gv), act.DueLine });
                    ious.RemoveAt(i);
                    i--;
                    changed = true;
                }
            }

            if (changed)
            {
                SaveToCustomData(gv, ious);
                SaveMarks(gv, marks);
            }
            return popups;
        }

        private static void ApplyMoodPenalty(GameVariables gv)
        {
            try { gv.Mood -= PenaltyMood; }
            catch { }
        }

        private static string GetBotName(GameVariables gv)
        {
            try
            {
                string n = gv.botName;
                if (!string.IsNullOrEmpty(n)) return n;
            }
            catch { }
            return Localization.T("bot_name");
        }

        // ---- 出门欠条"已还清"提示挂起 ----
        // 出门动画期间不弹窗（会和场景切换动画重叠），挂起队列等回家（场景切回）时由 Main 补弹。
        private static readonly List<string[]> PendingOutsidePopups = new List<string[]>();

        private static void QueuePendingOutsidePopup(string[] popup)
        {
            PendingOutsidePopups.Add(popup);
        }

        public static bool HasPendingOutsidePopups()
        {
            return PendingOutsidePopups.Count > 0;
        }

        public static List<string[]> DrainPendingOutsidePopups()
        {
            var all = new List<string[]>(PendingOutsidePopups);
            PendingOutsidePopups.Clear();
            return all;
        }

        // ---- 持久化：gv.customData（SpecialVariablesHolder）----
        private const string StorageKey = "BlackjackCouple_IOUS";
        // 每字段"最近一次清偿消费掉的时间戳"，保证同字段多张欠条一次互动只清一张
        private const string MarksKey = "BlackjackCouple_IOUMarks";

        private static readonly JsonSerializerSettings JsonSettings =
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };

        private static Dictionary<string, long> LoadMarks(GameVariables gv)
        {
            try
            {
                var cd = gv.customData;
                if (cd == null) return new Dictionary<string, long>();
                string json = cd.GetStringSpecialVariableOrDefault(MarksKey, "");
                if (string.IsNullOrEmpty(json)) return new Dictionary<string, long>();
                var marks = JsonConvert.DeserializeObject<Dictionary<string, long>>(json, JsonSettings);
                return marks ?? new Dictionary<string, long>();
            }
            catch { return new Dictionary<string, long>(); }
        }

        private static void SaveMarks(GameVariables gv, Dictionary<string, long> marks)
        {
            try
            {
                var cd = gv.customData;
                if (cd == null) return;
                string json = JsonConvert.SerializeObject(marks, JsonSettings);
                cd.SetStringSpecialVariable(MarksKey, json);
            }
            catch { }
        }

        private static List<IOU> LoadFromCustomData(GameVariables gv)
        {
            try
            {
                var cd = gv.customData;
                if (cd == null) return new List<IOU>();
                string json = cd.GetStringSpecialVariableOrDefault(StorageKey, "");
                if (string.IsNullOrEmpty(json)) return new List<IOU>();
                var list = JsonConvert.DeserializeObject<List<IOU>>(json, JsonSettings);
                return list ?? new List<IOU>();
            }
            catch { return new List<IOU>(); }
        }

        private static void SaveToCustomData(GameVariables gv, List<IOU> ious)
        {
            try
            {
                var cd = gv.customData;
                if (cd == null) return;
                string json = JsonConvert.SerializeObject(ious, JsonSettings);
                cd.SetStringSpecialVariable(StorageKey, json);
            }
            catch { }
        }

        private static void SaveToCustomData(GameVariables gv, IOU io)
        {
            var list = LoadFromCustomData(gv);
            list.Add(io);
            SaveToCustomData(gv, list);
        }
    }
}
