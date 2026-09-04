using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppCards;
// Card/Rank/Suit 为 Il2CppCards.CardDeck 的嵌套类型，需 using 别名提升
using Card = Il2CppCards.CardDeck.Card;
using Rank = Il2CppCards.CardDeck.Rank;
using Suit = Il2CppCards.CardDeck.Suit;

namespace BlackjackCouple
{
    /// <summary>
    /// 21点规则引擎（纯逻辑，不依赖 GUI，可独立单元测试）
    /// 规则：J/Q/K=10，A=11（固定，不做软硬切换）；玩家停牌后点数固定，
    /// 庄家没有"停牌阶段"，只需"超过玩家即赢"——因此庄家叫牌以玩家点数为基准：
    /// 庄家当前点数 ≥ 玩家点数即停（已达/反超，稳赢），低于玩家才继续要牌追赶（可能追爆）。
    /// </summary>
    public static class BlackjackRules
    {
        public const int Target = 21;

        // 一标准副牌各点数的静态张数分布（不计已出牌，简化概率估算）：
        // 点数 2~10 各 4 张；10/J/Q/K 合计 16 张；A 固定按 11 共 4 张
        private static readonly int[] DeckByValue = BuildDeckByValue();

        private static int[] BuildDeckByValue()
        {
            var d = new int[12];
            for (int v = 2; v <= 10; v++) d[v] = 4;
            d[10] = 16; // 10 / J / Q / K
            d[11] = 4;  // A（固定 11）
            return d;
        }

        /// <summary>单张牌点数映射（Rank 枚举：c2=2..c10, Jack=20, King=21, Queen=22, Ace=23）</summary>
        public static int CardValue(Rank rank)
        {
            switch (rank)
            {
                case Rank.Jack:
                case Rank.King:
                case Rank.Queen:
                    return 10;
                case Rank.Ace:
                    return 11;
                default:
                    return (int)rank; // c2~c10 恰好等于面值
            }
        }

        /// <summary>手牌总点数（所有牌直接相加，无软切换，与原版一致）</summary>
        public static int HandValue(IReadOnlyList<Card> cards)
        {
            int sum = 0;
            for (int i = 0; i < cards.Count; i++)
                sum += CardValue(cards[i].Rank);
            return sum;
        }

        public static bool IsBust(int value) => value > Target;

        /// <summary>
        /// 庄家是否要牌：基于概率的决策（一标准副牌静态分布）。
        /// 比较"停牌"与"再要一张"的期望收益，取更高者：
        ///   停牌收益：已反超玩家=1，追平=0.5，落后=0；
        ///   要牌收益：抽出后不爆且反超=1，追平/仍落后（未爆，需继续）=0.5，爆牌=0，按各点数张数加权平均。
        /// 效果：8v8 会追（几乎稳反超）；20v20/19v19/18v18 这类追平局不叫——
        ///      反超牌概率极低、爆牌率极高（20 点再要只有 2 点以下才不爆，21 点直接停牌）。
        /// </summary>
        public static bool DealerWantsCard(int dealerValue, int playerValue)
        {
            if (IsBust(dealerValue) || dealerValue >= Target) return false;
            if (IsBust(playerValue)) return false; // 防御：玩家爆牌时调用方本就不会进入本回合

            double stand = dealerValue > playerValue ? 1.0
                         : dealerValue == playerValue ? 0.5
                         : 0.0;

            double hit = 0.0;
            int total = 0;
            for (int v = 2; v <= 11; v++)
            {
                int cnt = DeckByValue[v];
                if (cnt == 0) continue;
                int nv = dealerValue + v;
                double w;
                if (nv > Target) w = 0.0;            // 爆牌
                else if (nv > playerValue) w = 1.0;  // 反超即赢
                else w = 0.5;                         // 追平/仍落后且未爆（还需继续，保守按平局计）
                hit += cnt * w;
                total += cnt;
            }
            if (total == 0) return false;
            hit /= total;

            return hit > stand;
        }

        public enum Outcome
        {
            PlayerBlackjack, // 玩家直接 21 点
            PlayerWin,
            DealerWin,
            Push,            // 平局
        }

        /// <summary>结算判定（玩家已停牌或爆牌后调用）</summary>
        public static Outcome Resolve(int playerValue, int dealerValue)
        {
            bool pBust = IsBust(playerValue);
            bool dBust = IsBust(dealerValue);

            if (pBust && dBust) return Outcome.DealerWin; // 双爆按玩家负（原版常见处理）
            if (pBust) return Outcome.DealerWin;
            if (dBust) return Outcome.PlayerWin;
            if (playerValue == dealerValue) return Outcome.Push;
            if (playerValue == Target) return Outcome.PlayerBlackjack;
            return playerValue > dealerValue ? Outcome.PlayerWin : Outcome.DealerWin;
        }
    }
}
