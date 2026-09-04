using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MelonLoader;
using MelonLoader.Utils;

namespace BlackjackCouple
{
    /// <summary>
    /// 21点情侣版本地化系统。
    /// 配置文件：UserData\BlackjackCouple.cfg（MelonLoader UserData 目录）
    /// 结构：
    ///   [General] Language = auto / en / zh
    ///   [en] key = 英文文案
    ///   [zh] key = 中文文案
    ///   [Persona] key = 覆盖值（可选，优先于 [en]/[zh]）
    /// 首次运行自动生成默认 cfg；玩家可直接编辑 cfg 修改文本或翻译成其它语言。
    /// </summary>
    public static class Localization
    {
        // 当前实际生效语言（解析后）："en" / "zh"
        public static string CurrentLanguage = "zh";

        // ---- key 清单（与 EnTexts / ZhTexts 索引对齐，也用于生成默认 cfg）----
        private static readonly string[] KeyOrder =
        {
            "interact_button", "hit", "stand", "double", "restart", "return",
            "status_title", "player_title", "dealer_title", "bot_name",
            "opening_line", "restarting", "bust_text", "blackjack_text", "dealer_turn", "dealer_bust",
            "player_prefix", "bot_prefix", "hand_points", "empty_hand",
            "suit_spade", "suit_heart", "suit_diamond", "suit_club",
            "rank_j", "rank_q", "rank_k", "rank_a",
            "reward_blackjack", "reward_win", "push_text",
            "count_two", "count_one", "lose_due",
            "iou_kiss_name", "iou_kiss_line", "iou_kiss_due",
            "iou_headpat_name", "iou_headpat_line", "iou_headpat_due",
            "iou_cuddle_name", "iou_cuddle_line", "iou_cuddle_due",
            "iou_intimate_name", "iou_intimate_line", "iou_intimate_due",
            "iou_talk_name", "iou_talk_line", "iou_talk_due",
            "iou_outside_name", "iou_outside_line", "iou_outside_due",
            "iou_block_text",
            "iou_block_detail",
            "iou_double_block",
            "iou_settled",
            "help", "help_text",
        };

        private static readonly string[] EnTexts =
        {
            "Blackjack", "Hit", "Stand", "Double", "Play Again", "Back",
            "Status", "Player", "Dealer", "Bot",
            ": Win and there's a reward, lose and there's a punishment ♥~||: Beat me and I'll reward you, lose and you'll owe me~||: One round? Win and you get a prize, lose and you pay up~",
            "Shuffling... here we go again~||Shuffling the deck, ready in a sec~||Fresh shuffle, one more round~",
            "Aww~ busted...||Busted, that's the round~||Overshot... you busted~", "21! I win||21! Lucky break~||21! This one's mine~",
            "'s turn...||'s move...|| Now it's my turn~", " busted!|| Oh no, I busted...|| Darn, I went bust~",
            "You: ", "{0}: ", " ({0} pts)", "(empty)",
            "♠", "♥", "♦", "♣",
            "J", "Q", "K", "A",
            "Lucky me, here you go~ +{0}$||21?! Fine, take {0}$~||A natural 21! {0}$ is yours, don't get used to it~",
            "You win, here's {0}$||Not bad, here's your {0}$~||You got me this time, {0}$ for you~",
            "Push, this round doesn't count||Same points, this one's void~||A draw, let's call it even~",
            "two", "one", "I win this round~ You owe me {0} IOU, and I'm keeping track—no weaseling out!||You lose~ {0} IOU(s), written in my little book!||Can't beat me, so {0} IOU(s) it is~",
            "Kiss", "You owe me a kiss~||The price of losing: a kiss~||Remember, you owe me one kiss~", "Days past and my kiss is still unpaid! Did you just forget all about me?! I'm keeping count, don't you dare!||That kiss has been overdue for days! Do you even take your promise seriously?!||Where's my kiss?! Days gone by—did you already forget?! I remember every single one~",
            "Headpat", "Ten minutes of headpats, no arguing||I want headpats for ten minutes, now you owe it~||Lose to me and give me ten minutes of headpats~", "Where are those headpats?! Days of nothing—you never even took it seriously, did you?!||The headpats are days late! Are you dodging or just forgetting?!||My headpats?! It's been days—don't tell me you forgot completely~",
            "Cuddle Sleep", "Sleep with me tonight||Tonight you stay with me~||You owe me a night of cuddles~", "You promised to stay with me tonight, and now days have slipped by! If you never meant it, why promise?!||You said you'd sleep with me—days now, and nothing! Are your promises really that cheap?!||How long have you put off staying with me?! Did you ever even mean it?!",
            "Deep Interaction", "You owe me a deep interaction||I want a deep interaction, mark it on the tab~||Lose and settle it with a deep interaction~", "That deep interaction is still unpaid after all these days—are you planning to welch?! I remember every single bit!||The deep interaction is long overdue! Forgotten, or just refusing to own it?!||This debt has dragged on for days, and you act like it never happened?! I've been keeping count~",
            "Good Chat", "Keep me good company||You owe me a good long chat~||Lose to me, and chat with me a while~", "Days without a word! How much longer will you put off the talk you owe me?! Do I even matter to you?!||The chat you owe me is days late! You think you can just shrug it off?!||What happened to our good talk? Days and you won't even look at me—what's that supposed to mean?!",
            "Go Out Together", "Take me out for a walk||You owe me a walk outside~||Lose and take me out for a stroll~", "We agreed to go out, and days passed with nothing! Did you just forget our promise?!||How long have you put off taking me out?! Say you'll do it and then drop it—real nice~||Our walk is way overdue! Don't tell me you never planned to keep it~",
            "You still owe me and you dare come play?! Settle your debt first, or don't expect me to care!||Clear your debt first, then we talk games—otherwise, no way!||You haven't paid what you owe, and you want another round?! Settle up first~",
            "Still owes: ",
            "Doubled the bet, now pay double~ You owe me: {0}||Double or lose double! Owe me: {0}||Doubled and lost, two IOUs on the books: {0}",
            "All settled~||That counts as paid, good~||Settled. That's more like it~",
            "Rules", "Rules: reach 21 without busting (J/Q/K=10, A=11).",
        };

        private static readonly string[] ZhTexts =
        {
            "21点", "要牌", "停牌", "加倍", "再来一局", "返回",
            "状态", "玩家", "庄家", "Bot",
            "：赢了有奖励，输了有惩罚♥～||：想赢我？赢了有赏，输了要还账哦～||：陪我来一局嘛，输了可不许赖账～",
            "洗牌中…再来一局～||洗牌啦，马上开始～||换个牌运，再来一局～",
            "啊~爆牌了…||爆了爆了，这局没啦～||过啦过啦，爆牌了…", "21点！我赢了||21点！运气爆棚～||21点！这把稳了",
            " 的回合…|| 该我出牌…|| 轮到我了…", " 爆牌啦！|| 哎呀，我爆了…|| 坏了坏了，我爆牌了…",
            "你：", "{0}：", "（{0} 点）", "空",
            "♠", "♥", "♦", "♣",
            "J", "Q", "K", "A",
            "运气真好，拿去花吧~ +{0}$||21点？服了你了，{0}$ 赏你～||居然是21点！{0}$ 归你，下次可没这么好运～",
            "你赢啦，赏你的 {0}$||算你厉害，这 {0}$ 拿去～||赢了我的钱，{0}$ 拿好，别太得意～",
            "平手，这局不算||同点，这局作废～||打平了，重新来～",
            "两张", "一张", "这局我赢了~ 你欠我{0}欠条，我可记着呢，别想赖账！||输了吧~ {0}张欠条，白纸黑字我可记下了！||赢不了我，就乖乖欠我{0}张欠条吧～",
            "亲亲", "你欠我一个亲亲~||输给我的代价，就是一个亲亲哦～||记住了，你欠我一个吻～", "欠我的亲亲都拖几天了！你居然忘得一干二净，压根没把我放心上吧？这笔账我可给你记着呢！||亲亲欠了这么多天还不还？！你到底有没有把答应我的事当回事啊？！||说好的亲亲呢？！拖到现在，你是不是早就忘光了？我可一笔笔都记得！",
            "摸头", "摸头十分钟，没得商量||我要你摸头十分钟，现在欠下了～||输给我的话，摸头十分钟哦～", "说好的摸头呢？拖了这么多天一点动静都没有，你是不是根本没当回事？！||摸头欠了这么久，你是打算赖掉还是忘掉了？！||我的摸头呢？！都拖几天了，你该不会忘得一干二净吧？",
            "依偎睡觉", "今晚陪我睡觉||今晚你要陪我一起睡～||欠我一晚的依偎～", "答应陪我睡觉的，结果拖到现在！你要是不想兑现，当初就别答应我！||说好陪我睡的，拖了这么多天！你的承诺就这么不值钱吗？！||陪我睡觉这事你拖了多久了？！是不是根本没往心里去？",
            "深入互动", "欠我一次深入交流||我要一次深入交流，记在账上了～||输给我，就用一次深入交流来还～", "那笔深入交流欠了这么多天还不还，你是打算赖账到底吗？！我这儿可记得清清楚楚！||说好的深入交流呢？！拖了这么久，你是忘了还是不想认账？！||这笔账欠了这么多天，你居然当没这回事？我可一直记着呢！",
            "好好聊天", "陪我好好说说话||欠我一场好好聊天～||输给我的话，要陪我聊聊天～", "这么多天连话都不愿跟我说！欠我的聊天你打算拖到什么时候？你心里还有没有我？！||欠我的聊天拖了这么多天！你该不会觉得随便就能赖过去吧？！||说好的好好聊天呢？都几天了，你理都不理我，到底什么意思？！",
            "一起出门", "带我出去走走||欠我一次出门散步～||输给我，就带我出去逛逛～", "说好一起出门，拖了这么多天连影子都没有！你是不是早把我们的约定忘光了？！||一起出门这事你拖多久了？！答应我的事转头就忘，可真行！||说好的出门散步呢？！拖到现在，你该不会压根没打算兑现吧？",
            "欠我的账还没还清，就想来找我玩？！先把欠的还上再来，不然别想让我理你！||账都没清就想来玩？先把欠我的还了，否则免谈！||你欠我的还没还呢，就想开下一局？还清了再来找我！",
            "还欠着：",
            "加倍一时爽，输了双倍还账哦～欠我：{0}||加倍输了，两张欠条可都记着呢：{0}||加倍赢了好说，输了可是双倍欠账：{0}",
            "已还清～||这就算还上了，乖～||还清了，这还差不多～",
            "说明", "规则：接近21点且别爆牌即赢（J/Q/K=10，A=11）。",
        };

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> Zh = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> Persona = new Dictionary<string, string>();

        // 当前生效表（语言表 + [Persona] 覆盖后的最终结果）
        private static readonly Dictionary<string, string> Table = new Dictionary<string, string>();

        private static string _languageSetting = "auto";
        private static bool _languageResolved = false;

        /// <summary>cfg 绝对路径：E:\Hgame\factorial-omega-win-64\UserData\BlackjackCouple.cfg</summary>
        public static string ConfigPath =>
            Path.Combine(MelonEnvironment.UserDataDirectory, "BlackjackCouple.cfg");

        /// <summary>
        /// 初始化：填充默认文案 -> 确保 cfg 存在 -> 解析 cfg -> 构建生效表。
        /// 注意：auto 语言不在启动早期访问游戏 Localization（会触发
        /// LocaleManager.DetectGameLocale() 空引用 NRE），延迟到首次 T() 调用时再解析。
        /// </summary>
        public static void Initialize()
        {
            FillDefaults();
            if (!File.Exists(ConfigPath))
            {
                try { WriteDefaultConfig(); }
                catch (Exception e) { MelonLogger.Error("[BlackjackCouple] 生成默认 cfg 失败: " + e); }
            }
            ParseConfig();
            string s = _languageSetting.Trim().ToLowerInvariant();
            if (s == "en" || s == "zh")
                CurrentLanguage = s;      // 显式语言，无需访问游戏
            else
                CurrentLanguage = "zh";   // auto：先按中文构建，首次 T() 时再按游戏语言刷新
            BuildTable();
            MelonLogger.Msg("[BlackjackCouple] 本地化初始化完成，语言=" + CurrentLanguage + "，cfg=" + ConfigPath);
        }

        /// <summary>按 key 取当前生效文案；缺失返回 key 本身（便于排查）</summary>
        public static string T(string key)
        {
            EnsureLanguageResolved();
            if (Table.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)) return v;
            // 兜底：默认表（防止 cfg 中被删除或置空）
            if (CurrentLanguage == "zh" && Zh.TryGetValue(key, out var z) && !string.IsNullOrEmpty(z)) return z;
            if (CurrentLanguage == "en" && En.TryGetValue(key, out var e) && !string.IsNullOrEmpty(e)) return e;
            return key;
        }

        // 多候选文案分隔符：cfg/默认表中一个 key 可写"候选1||候选2||候选3"，TRandom 随机取其一
        private const string VariantSep = "||";
        private static readonly System.Random Rng = new System.Random();

        /// <summary>
        /// 多候选随机文案：若该 key 配置了多个候选（用 "||" 分隔），随机返回一个；
        /// 单候选/未配置则等同 T(key)。用于对话类台词（欠条/催债/结算等）避免重复枯燥。
        /// </summary>
        public static string TRandom(string key)
        {
            string v = T(key);
            if (string.IsNullOrEmpty(v)) return v;
            int idx = v.IndexOf(VariantSep, StringComparison.Ordinal);
            if (idx < 0) return v;
            string[] parts = v.Split(new[] { VariantSep }, StringSplitOptions.None);
            return parts.Length <= 1 ? v : parts[Rng.Next(parts.Length)];
        }

        /// <summary>auto 语言懒解析：仅在首次取文案时触发（此时游戏已运行，Localization 就绪）</summary>
        private static void EnsureLanguageResolved()
        {
            if (_languageResolved) return;
            _languageResolved = true;
            string s = _languageSetting.Trim().ToLowerInvariant();
            if (s == "en" || s == "zh") return;   // 显式语言已定，无需检测

            string detected = "zh";
            try { detected = DetectGameLanguage(); }
            catch { }
            if (detected != CurrentLanguage)
            {
                CurrentLanguage = detected;
                BuildTable();
            }
        }

        // ---------- 内部实现 ----------

        private static void FillDefaults()
        {
            En.Clear();
            Zh.Clear();
            for (int i = 0; i < KeyOrder.Length; i++)
            {
                string k = KeyOrder[i];
                if (i < EnTexts.Length) En[k] = EnTexts[i];
                if (i < ZhTexts.Length) Zh[k] = ZhTexts[i];
            }
        }

        private static void WriteDefaultConfig()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# BlackjackCouple 本地化配置（21点情侣版）");
            sb.AppendLine("# 修改后重启游戏生效。");
            sb.AppendLine("# [General] Language: auto=跟随游戏当前语言 / en=英文 / zh=中文");
            sb.AppendLine("[General]");
            sb.AppendLine("Language=auto");
            sb.AppendLine();
            sb.AppendLine("# 英文文案（key=value）");
            sb.AppendLine("[en]");
            for (int i = 0; i < KeyOrder.Length; i++)
                sb.AppendLine(KeyOrder[i] + "=" + EnTexts[i]);
            sb.AppendLine();
            sb.AppendLine("# 中文文案（key=value）");
            sb.AppendLine("[zh]");
            for (int i = 0; i < KeyOrder.Length; i++)
                sb.AppendLine(KeyOrder[i] + "=" + ZhTexts[i]);
            sb.AppendLine();
            sb.AppendLine("# [Persona] 预留人格覆盖：此段 key 的取值优先于 [en]/[zh]，可写任意语言");
            sb.AppendLine("# 示例：#interact_button=Blackjack (Jun's style)");
            sb.AppendLine("[Persona]");
            File.WriteAllText(ConfigPath, sb.ToString(), new UTF8Encoding(false));
        }

        private static void ParseConfig()
        {
            Persona.Clear();
            _languageSetting = "auto";
            string section = "";
            foreach (var raw in File.ReadAllLines(ConfigPath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                switch (section.ToLowerInvariant())
                {
                    case "general":
                        if (key.Equals("Language", StringComparison.OrdinalIgnoreCase))
                            _languageSetting = val;
                        break;
                    case "en": En[key] = val; break;
                    case "zh": Zh[key] = val; break;
                    case "persona": Persona[key] = val; break;
                }
            }
        }

        private static void BuildTable()
        {
            Table.Clear();
            var src = CurrentLanguage == "zh" ? Zh : En;
            foreach (var kv in src) Table[kv.Key] = kv.Value;
            // [Persona] 覆盖
            foreach (var kv in Persona)
            {
                if (!string.IsNullOrEmpty(kv.Value)) Table[kv.Key] = kv.Value;
            }
        }

        /// <summary>
        /// auto：本 MOD 固定中文。
        /// 注意：IL2CPP 下访问 LocalizationSettings.SelectedLocale，在游戏语言系统就绪前
        /// 必然抛 NullReferenceException（其内部走 LocaleIdentifier.op_Inequality →
        /// Il2CppObjectBaseToIntPtrNotNull，对空对象判空比较也拦不住）。故彻底不触碰
        /// 游戏 Localization，auto 即中文。
        /// </summary>
        private static string DetectGameLanguage()
        {
            return "zh";
        }
    }
}
