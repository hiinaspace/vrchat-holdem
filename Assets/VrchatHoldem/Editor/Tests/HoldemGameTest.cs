using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.Udon;

namespace Tests
{
    public class HoldemGameTest
    {

        private static string[] RCARDS = new string[] {
        "2D", "3D", "4D", "5D", "6D", "7D", "8D", "9D", "TD", "JD", "QD", "KD", "AD",
        "2H", "3H", "4H", "5H", "6H", "7H", "8H", "9H", "TH", "JH", "QH", "KH", "AH",
        "2C", "3C", "4C", "5C", "6C", "7C", "8C", "9C", "TC", "JC", "QC", "KC", "AC",
        "2S", "3S", "4S", "5S", "6S", "7S", "8S", "9S", "TS", "JS", "QS", "KS", "AS"
        };
        static Dictionary<string, int> CARDS = new Dictionary<string, int>();
        static HoldemGameTest()
        {
            for (int i = 0; i < 52; ++i)
            {
                CARDS[RCARDS[i]] = i;
            }
        }
        private ulong hand(string hand)
        {
            var cards = hand.Split(' ');
            return HoldemGame.EvaluateHand(
                CARDS[cards[0]], CARDS[cards[1]], CARDS[cards[2]], CARDS[cards[3]], CARDS[cards[4]]);
        }

        [Test]
        public void HandEvaluator()
        {
            Assert.AreEqual("High Card (A)", HoldemGame.HandClass(hand("2C 3C 4C TD AC")));
            Assert.AreEqual("Pair (2)", HoldemGame.HandClass(hand("2C 2D 4C TD AC")));
            Assert.AreEqual("Two Pairs (3/2)", HoldemGame.HandClass(hand("2C 2D 3C 3D AC")));
            Assert.AreEqual("Three of a Kind (2)", HoldemGame.HandClass(hand("2C 2D 2S 3D AC")));
            Assert.AreEqual("Straight (Wheel)", HoldemGame.HandClass(hand("2C 3S 4S 5D AC")));
            Assert.AreEqual("Straight", HoldemGame.HandClass(hand("3C 4S 5S 6D 7C")));
            Assert.AreEqual("Flush", HoldemGame.HandClass(hand("3S TS 5S 6S 7S")));
            Assert.AreEqual("Full House (10/3)", HoldemGame.HandClass(hand("TS TC TC 3S 3S")));
            Assert.AreEqual("Four of a Kind (10)", HoldemGame.HandClass(hand("TS TC TC TS 4S")));
            Assert.AreEqual("Straight Flush", HoldemGame.HandClass(hand("9S TS JS QS KS")));
            Assert.AreEqual("Royal Flush", HoldemGame.HandClass(hand("TS JS QS KS AS")));

            // high card
            Assert.Greater(
                hand("2C 3C 4C TD AC"),
                hand("2H 3H 4H TH KD"));
            // straight flushes
            Assert.Greater(
                HoldemGame.EvaluateHand(1, 2, 3, 4, 5),
                HoldemGame.EvaluateHand(0, 1, 2, 3, 4));
        }

        [Test]
        public void DividePot()
        {
            // simple
            Assert.That(
                HoldemGame.DividePot(
                                new int[] { 100, 100,     0, 0, 0, 0, 0, 0, 0, 0 },
                                new int[] { 0  , 1,        -1, -1, -1, -1, -1, -1, -1, -1 }),
                Is.EqualTo(     new int[] { 200, 0,        0, 0, 0, 0, 0, 0, 0, 0}));

            // split
            Assert.That(
                HoldemGame.DividePot(
                                new int[] { 100, 100,       0, 0, 0, 0, 0, 0, 0, 0 },
                                new int[] { 0  , 0,        -1, -1, -1, -1, -1, -1, -1, -1 }),
                Is.EqualTo(     new int[] { 100, 100,       0, 0, 0, 0, 0, 0, 0, 0}));

        }

        [Test]
        public void DivideSideSplitPot()
        { 
            // side split pot
            Assert.That(
                HoldemGame.DividePot(
                                new int[] { 100, 150, 150,    0, 0, 0, 0, 0, 0, 0 },
                                new int[] { 0, 0, 1,          -1, -1, -1, -1, -1, -1, -1 }),
                Is.EqualTo(     new int[] { 150, 250, 0,      0, 0, 0, 0, 0, 0, 0}));
        }

        [Test]
        public void DivideSidePot()
        {
            Assert.That(
                HoldemGame.DividePot(
                                new int[] { 50, 100, 100,    0, 0, 0, 0, 0, 0, 0 },
                                new int[] { 0, 1, 2,        -1, -1, -1, -1, -1, -1, -1 }),
                Is.EqualTo(     new int[] { 150, 100, 0,     0, 0, 0, 0, 0, 0, 0}));
        }

        const int C = HoldemGame.PLAYER_COMMITED;

        [Test]
        public void RankPlayers()
        {
            Assert.That(
                RankPlayers(
                    new int[] { C, C, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                    new int[] { CARDS["TC"], CARDS["AC"], 0, 0, 0, 0, 0, 0, 0, 0 },
                    new int[] { CARDS["AD"], CARDS["AH"], 0, 0, 0, 0, 0, 0, 0, 0 },
                    CARDS["2C"], CARDS["3C"], CARDS["4C"],
                    CARDS["TS"], CARDS["TH"]),
                Is.EqualTo(new int[] { 0, 1, 2, 2, 2, 2, 2, 2, 2, 2 }));
        }

        [Test]
        public void RankPlayersTie()
        {
            Assert.That(
                RankPlayers(
                    new int[] { C, C, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                    new int[] { CARDS["AS"], CARDS["AC"], 0, 0, 0, 0, 0, 0, 0, 0 },
                    new int[] { CARDS["AD"], CARDS["AH"], 0, 0, 0, 0, 0, 0, 0, 0 },
                    CARDS["2C"], CARDS["3C"], CARDS["4C"],
                    CARDS["TS"], CARDS["TH"]),
                Is.EqualTo(new int[] { 0, 0, 1, 1, 1, 1, 1, 1, 1, 1 }));
        }
        [Test]
        public void RankPlayersTie2()
        {
            Assert.That(
                RankPlayers(
                    new int[] { C, C, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                    new int[] { CARDS["AS"], CARDS["AH"], 0, 0, 0, 0, 0, 0, 0, 0 },
                    new int[] { CARDS["AD"], CARDS["AD"], 0, 0, 0, 0, 0, 0, 0, 0 },
                    CARDS["2C"], CARDS["2H"], CARDS["TC"],
                    CARDS["3H"], CARDS["6C"]),
                Is.EqualTo(new int[] { 0, 0, 1, 1, 1, 1, 1, 1, 1, 1 }));
        }

        [Test]
        public void RankPlayersTieLater()
        {
            Assert.That(
                RankPlayers(
                    new int[] { 0, 0, 0, 0, C, C, 0, 0, 0, 0, },
                    new int[] { 0, 0, 0, 0, CARDS["AS"], CARDS["AC"], 0, 0, 0, 0,},
                    new int[] { 0, 0, 0, 0, CARDS["AD"], CARDS["AH"], 0, 0, 0, 0,},
                    CARDS["2C"], CARDS["3C"], CARDS["4C"],
                    CARDS["TS"], CARDS["TH"]),
                Is.EqualTo(new int[] { 1, 1, 1, 1, 0, 0, 1, 1, 1, 1 }));
        }

        [Test]
        public void RankPlayersSplitPotOthers()
        {
            Assert.That(
                RankPlayers(
                    new int[] { 0, 0, 0, C, C, C, 0, 0, 0, 0, },
                    new int[] { 0, 0, 0, CARDS["3S"], CARDS["AS"], CARDS["AC"], 0, 0, 0, 0,},
                    new int[] { 0, 0, 0, CARDS["4S"], CARDS["AD"], CARDS["AH"], 0, 0, 0, 0,},
                    CARDS["2C"], CARDS["3C"], CARDS["4C"],
                    CARDS["TS"], CARDS["TH"]),
                Is.EqualTo(new int[] { 2, 2, 2, 1, 0, 0, 2, 2, 2, 2 }));
        }

        private int[] RankPlayers(
            int[] playerState, int[] holeCards0, int[] holeCards1,
            int flop0, int flop1, int flop2, int turn, int river)
        {
            var values = HoldemGame.GetHandValues(
                playerState, holeCards0, holeCards1, flop0, flop1, flop2, turn, river);
            return HoldemGame.RankPlayers(values);
        }

        struct GameState
        {

            static int actionTimeoutSecs = 1, headsUpTimeoutSecs = 1;
            static int bigBlind = 2; 
            static int dealerSeat = 0;

            public int bettingRound;
            public int actingSeat;

            public int currentBet;
            public int minimumRaise;
            public bool headsUp;
            public bool wonByFold;

            public int[] playerState;
            public int[] stacks;
            public int[] roundContribution;
            public int[] potContribution;

            public GameState(object[] pack)
            {
                bettingRound = (int)pack[0];
                actingSeat = (int)pack[1];
                currentBet = (int)pack[2];
                minimumRaise = (int)pack[3];
                headsUp = (bool)pack[4];
                wonByFold = (bool)pack[5];
                playerState = (int[])pack[6];
                stacks = (int[])pack[7];
                roundContribution = (int[])pack[8];
                potContribution = (int[])pack[9];
            }
            public GameState CalculateTransition(
                int lastTransitionMillis,
                int nowMillis, bool actingPlayerSeated, int actingPlayerBet, bool actingPlayerCommited)
            {
                var newState = HoldemGame.CalculateGameTransition(
                    lastTransitionMillis, bettingRound, actingSeat,
                    currentBet, minimumRaise, headsUp,
                    // copy so we can test against the old one
                    (int[])playerState.Clone(),
                    (int[])stacks.Clone(),
                    (int[])roundContribution.Clone(),
                    (int[])potContribution.Clone(),
                    nowMillis,
                    actionTimeoutSecs, headsUpTimeoutSecs, bigBlind, dealerSeat, actingPlayerSeated,
                    actingPlayerBet, actingPlayerCommited);

                GameState newS = this;
                if (newState != null) newS = new GameState(newState);
                Validate(newS, this, actingPlayerSeated, actingPlayerBet, actingPlayerCommited);
                return newS;
            }

            // extract game state from table for testing
            public GameState(HoldemState s)
            {
                bettingRound = s.bettingRound;
                actingSeat = s.actingSeat;
                currentBet = s.currentBet;
                minimumRaise = s.minimumRaise;
                headsUp = s.headsUp;
                wonByFold = s.wonByFold;
                playerState = s.playerState;
                stacks = s.stacks;
                roundContribution = s.roundContribution;
                potContribution = s.potContribution;
            }

            // safety properties/invariants
            public static void Validate(GameState s, GameState old,
                bool actingPlayerSeated, int actingPlayerBet, bool actingPlayerCommited)
            {
                int actingCount = 0, deadCount = 0, pendingCount = 0, commitedCount = 0;
                for (int i = 0; i < 10; i++)
                {
                    Assert.That(s.roundContribution[i], Is.GreaterThanOrEqualTo(0));
                    Assert.That(s.potContribution[i], Is.GreaterThanOrEqualTo(0));
                    if (s.playerState[i] == HoldemGame.PLAYER_DEAD)
                    {
                        Assert.That(old.playerState[i],
                            Is.EqualTo(HoldemGame.PLAYER_DEAD).Or.EqualTo(HoldemGame.PLAYER_ACTING));
                        deadCount++;
                    }
                    else
                    {
                        // unless we just started the round
                        if (old.bettingRound != HoldemGame.NOT_PLAYING)
                        {
                            // no ressurection
                            Assert.That(old.playerState[i], Is.Not.EqualTo(HoldemGame.PLAYER_DEAD));
                        }

                        if (s.playerState[i] == HoldemGame.PLAYER_PENDING)
                        {
                            Assert.That(s.stacks[i], Is.GreaterThan(0)); // has chips to bet
                            pendingCount++;
                        }
                        if (s.playerState[i] == HoldemGame.PLAYER_ACTING)
                        {
                            Assert.That(i, Is.EqualTo(s.actingSeat));
                            Assert.That(actingPlayerSeated , Is.True);
                            if (actingPlayerCommited)
                            {
                                // TODO should be an invalid bet
                            }
                            Assert.That(s.stacks[i] , Is.GreaterThan(0));
                            actingCount++;
                        }
                        if (s.playerState[i] == HoldemGame.PLAYER_COMMITED)
                        {
                            Assert.That(old.playerState[i],
                                Is.EqualTo(HoldemGame.PLAYER_COMMITED).Or.EqualTo(HoldemGame.PLAYER_ACTING));
                            if (s.stacks[i] > 0)
                            {
                                Assert.That(s.roundContribution[i], Is.EqualTo(s.currentBet));
                            }
                            commitedCount++;
                        }

                    }
                }

                if (s.wonByFold)
                {
                    Assert.That(deadCount, Is.EqualTo(9));
                    // the remaining player either committed, or everyone else folded on no bet
                    Assert.That(commitedCount + pendingCount, Is.EqualTo(1));
                }
                else if (s.bettingRound == HoldemGame.SHOWDOWN)
                {
                    Assert.That(commitedCount, Is.GreaterThan(1));
                    Assert.That(actingCount, Is.Zero);
                    Assert.That(pendingCount, Is.Zero);
                }
                else if (s.headsUp)
                {
                    Assert.That(actingCount, Is.Zero);
                    Assert.That(pendingCount, Is.Zero);
                    Assert.That(commitedCount, Is.GreaterThan(1));
                }
                else
                {
                    Assert.That(commitedCount + pendingCount + actingCount, Is.GreaterThan(1));
                    Assert.That(actingCount, Is.EqualTo(1));
                    if (s.bettingRound > old.bettingRound)
                    {
                        Assert.That(s.bettingRound, Is.EqualTo(old.bettingRound + 1));
                        if (s.bettingRound > HoldemGame.PREFLOP)
                        {
                            for (int i = 0; i < 10; i++)
                            {
                                Assert.That(s.roundContribution[i], Is.Zero);
                            }
                            Assert.That(s.currentBet, Is.Zero);
                        }
                        else
                        {
                            Assert.That(s.currentBet, Is.EqualTo(bigBlind));
                        }
                    }
                    else
                    {
                        Assert.That(s.bettingRound, Is.EqualTo(old.bettingRound));
                    }
                }
            }
        }

        [Test]
        public void TransitionGame()
        {
            GameState s;
            s.bettingRound = HoldemGame.PREFLOP;
            s.actingSeat = 0;
            s.currentBet = 2;
            s.minimumRaise = 2;
            s.headsUp = false;
            s.wonByFold = false;
            s.playerState = new int[10] { HoldemGame.PLAYER_ACTING, HoldemGame.PLAYER_PENDING, 0, 0, 0, 0, 0, 0, 0, 0 };
            s.stacks = new int[10] { 9, 8, 0, 0, 0, 0, 0, 0, 0, 0 };
            s.roundContribution = new int[10] { 1, 2, 0, 0, 0, 0, 0, 0, 0, 0 };
            s.potContribution = new int[10];

            s = s.CalculateTransition(
                lastTransitionMillis: 0, nowMillis: 1,
                actingPlayerSeated: true, actingPlayerBet: 0, actingPlayerCommited: false);
            Assert.That(s.actingSeat, Is.EqualTo(0));
            // call
            s = s.CalculateTransition(
                lastTransitionMillis: 0, nowMillis: 2,
                actingPlayerSeated: true, actingPlayerBet: 1, actingPlayerCommited: true);
            Assert.That(s.actingSeat, Is.EqualTo(1));
            Assert.That(s.currentBet, Is.EqualTo(2));
            Assert.That(s.stacks[1], Is.EqualTo(8));
            // check
            s = s.CalculateTransition(
                lastTransitionMillis: 2, nowMillis: 3,
                actingPlayerSeated: true, actingPlayerBet: 0, actingPlayerCommited: true);
            // new round
            Assert.That(s.stacks[0], Is.EqualTo(8));
            Assert.That(s.stacks[1], Is.EqualTo(8));
            Assert.That(s.bettingRound, Is.EqualTo(HoldemGame.FLOP));
            Assert.That(s.actingSeat, Is.EqualTo(0));
            Assert.That(s.currentBet, Is.EqualTo(0));
            Assert.That(s.potContribution[0], Is.EqualTo(2));
            Assert.That(s.potContribution[1], Is.EqualTo(2));
            // waiting...
            s = s.CalculateTransition(
                lastTransitionMillis: 3, nowMillis: 4,
                actingPlayerSeated: true, actingPlayerBet: 0, actingPlayerCommited: false);
            Assert.That(s.actingSeat, Is.EqualTo(0));
            // raise
            s = s.CalculateTransition(
                lastTransitionMillis: 4, nowMillis: 5,
                actingPlayerSeated: true, actingPlayerBet: 2, actingPlayerCommited: true);
            Assert.That(s.currentBet, Is.EqualTo(2));
            Assert.That(s.roundContribution[0], Is.EqualTo(2));
            Assert.That(s.stacks[0], Is.EqualTo(6));
            Assert.That(s.actingSeat, Is.EqualTo(1));
            // call
            s = s.CalculateTransition(
                lastTransitionMillis: 5, nowMillis: 6,
                actingPlayerSeated: true, actingPlayerBet: 2, actingPlayerCommited: true);
            Assert.That(s.stacks[1], Is.EqualTo(6));
            // new round
            Assert.That(s.bettingRound, Is.EqualTo(HoldemGame.TURN));
            Assert.That(s.stacks[0], Is.EqualTo(6));
            Assert.That(s.stacks[1], Is.EqualTo(6));
            Assert.That(s.potContribution[0], Is.EqualTo(4));
            Assert.That(s.potContribution[1], Is.EqualTo(4));
            Assert.That(s.actingSeat, Is.EqualTo(0));
            // check
            s = s.CalculateTransition(
                lastTransitionMillis: 6, nowMillis: 7,
                actingPlayerSeated: true, actingPlayerBet: 0, actingPlayerCommited: true);
            Assert.That(s.currentBet, Is.EqualTo(0));
            Assert.That(s.roundContribution[0], Is.EqualTo(0));
            Assert.That(s.stacks[0], Is.EqualTo(6));
            Assert.That(s.actingSeat, Is.EqualTo(1));
            // raise
            s = s.CalculateTransition(
                lastTransitionMillis: 7, nowMillis: 8,
                actingPlayerSeated: true, actingPlayerBet: 2, actingPlayerCommited: true);
            Assert.That(s.currentBet, Is.EqualTo(2));
            Assert.That(s.roundContribution[1], Is.EqualTo(2));
            Assert.That(s.stacks[1], Is.EqualTo(4));
            Assert.That(s.actingSeat, Is.EqualTo(0));
            // call
            s = s.CalculateTransition(
                lastTransitionMillis: 7, nowMillis: 8,
                actingPlayerSeated: true, actingPlayerBet: 2, actingPlayerCommited: true);
            Assert.That(s.stacks[0], Is.EqualTo(4));
            // new round
            Assert.That(s.bettingRound, Is.EqualTo(HoldemGame.RIVER));
            Assert.That(s.stacks[0], Is.EqualTo(4));
            Assert.That(s.stacks[1], Is.EqualTo(4));
            Assert.That(s.potContribution[0], Is.EqualTo(6));
            Assert.That(s.potContribution[1], Is.EqualTo(6));
            Assert.That(s.actingSeat, Is.EqualTo(0));
            // check
            s = s.CalculateTransition(
                lastTransitionMillis: 8, nowMillis: 9,
                actingPlayerSeated: true, actingPlayerBet: 0, actingPlayerCommited: true);
            Assert.That(s.actingSeat, Is.EqualTo(1));
            Assert.That(s.currentBet, Is.EqualTo(0));
            // check
            s = s.CalculateTransition(
                lastTransitionMillis: 8, nowMillis: 9,
                actingPlayerSeated: true, actingPlayerBet: 0, actingPlayerCommited: true);
            // it's over
            Assert.That(s.bettingRound, Is.EqualTo(HoldemGame.SHOWDOWN));
            Assert.That(s.potContribution[0], Is.EqualTo(6));
            Assert.That(s.potContribution[1], Is.EqualTo(6));
        }

        struct HoldemState
        {
            static int bigBlind = 2;
            static int winnerTimeoutSecs = 1;
            static int readyTimeoutSecs = 1;
            static int actionTimeoutSecs = 1;
            static int headsUpTimeoutSecs = 1;

            public int tableState;
            public int lastTransitionMillis;
            public int bettingRound;
            public int actingSeat;
            public int dealerSeat;

            public int currentBet;
            public int minimumRaise;
            public bool headsUp;
            public bool wonByFold;

            public int[] playerState;
            public int[] stacks;
            public int[] roundContribution;
            public int[] potContribution;

            public int flop0;
            public int flop1;
            public int flop2;
            public int turn;
            public int river;

            public int[] holeCards0;
            public int[] holeCards1;

            public HoldemState(object[] newState) {
                tableState = (int)newState[0];
                lastTransitionMillis = (int)newState[1];
                bettingRound = (int)newState[2];
                actingSeat = (int)newState[3];
                dealerSeat = (int)newState[4];

                currentBet = (int)newState[5];
                minimumRaise = (int)newState[6];
                headsUp = (bool)newState[7];
                wonByFold = (bool)newState[8];

                playerState = (int[])newState[9];
                stacks = (int[])newState[10];
                roundContribution = (int[])newState[11];
                potContribution = (int[])newState[12];

                flop0 = (int)newState[13];
                flop1 = (int)newState[14];
                flop2 = (int)newState[15];
                turn = (int)newState[16];
                river = (int)newState[17];
                holeCards0 = (int[])newState[18];
                holeCards1 = (int[])newState[19];
            }

            public HoldemState Transition(
                int nowMillis, bool[] playerIsSeated, bool[] playerIsReady, int[] bankBySeatOwner,
                int actingPlayerBet, bool actingPlayerCommited,
                int[] deck)
            {
                object[] newState = HoldemGame.CalculateTransition(
                    tableState, lastTransitionMillis, bettingRound, actingSeat, dealerSeat,
                    currentBet, minimumRaise, headsUp, wonByFold,

                    // clone so we can test old state
                    (int[])playerState.Clone(),
                    (int[])stacks.Clone(),
                    (int[])roundContribution.Clone(),
                    (int[])potContribution.Clone(),

                    flop0, flop1, flop2, turn, river, holeCards0, holeCards1,
                    // external state
                    nowMillis, bigBlind,
                    winnerTimeoutSecs, readyTimeoutSecs,
                    actionTimeoutSecs, headsUpTimeoutSecs,
                    playerIsSeated, playerIsReady, bankBySeatOwner,
                    actingPlayerBet, actingPlayerCommited,
                    deck);
                HoldemState newS = this;
                if (newState != null) newS = new HoldemState(newState);
                Validate(newS, this, playerIsSeated[actingSeat], actingPlayerBet, actingPlayerCommited);
                return newS;
            }

            // safety properties
            static void Validate(HoldemState s, HoldemState old, 
                bool actingPlayerSeated, int actingPlayerBet, bool actingPlayerCommited)
            {
                if (s.tableState == HoldemGame.TABLE_PLAYING)
                {
                    GameState.Validate(new GameState(s), new GameState(old),
                        actingPlayerSeated, actingPlayerBet, actingPlayerCommited);
                }
            }

        }

        [Test]
        public void CalculateTransition()
        {
            HoldemState s;
            s.tableState = HoldemGame.TABLE_UNINITIALIZED;
            s.lastTransitionMillis = 0;
            s.bettingRound = HoldemGame.NOT_PLAYING;
            s.actingSeat = 0;
            s.dealerSeat = 0;
            s.currentBet = 2;
            s.minimumRaise = 0;
            s.headsUp = false;
            s.wonByFold = false;
            s.playerState = new int[10];
            s.stacks = new int[10];
            s.roundContribution = new int[10];
            s.potContribution = new int[10];
            s.flop0 = 0;
            s.flop1 = 0;
            s.flop2 = 0;
            s.turn = 0;
            s.river = 0;
            s.holeCards0 = new int[10];
            s.holeCards1 = new int[10];

            bool[] playerIsSeated = new bool[10];
            bool[] playerIsReady = new bool[10];
            int[] bankBySeatOwner = new int[10];
            int[] deck = new int[52];
            deck[0] = CARDS["AS"]; deck[1] = CARDS["AC"];
            deck[2] = CARDS["2S"]; deck[3] = CARDS["2H"];
            // community (after burns)
            deck[21] = CARDS["AH"]; deck[22] = CARDS["AD"]; deck[23] = CARDS["KD"];
            deck[25] = CARDS["JD"];
            deck[27] = CARDS["TD"]; 

            s = s.Transition(0, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: false, deck);
            Assert.That(s.tableState, Is.EqualTo(HoldemGame.TABLE_IDLE));

            playerIsSeated[0] = true;
            bankBySeatOwner[0] = 10;
            playerIsSeated[1] = true;
            bankBySeatOwner[1] = 10;
            s = s.Transition(1, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: false, deck);
            Assert.That(s.tableState, Is.EqualTo(HoldemGame.TABLE_IDLE));
            Assert.That(s.stacks[0], Is.EqualTo(10));
            Assert.That(s.stacks[1], Is.EqualTo(10));

            playerIsReady[0] = true;
            s = s.Transition(2, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: false, deck);
            Assert.That(s.tableState, Is.EqualTo(HoldemGame.TABLE_IDLE));

            playerIsReady[1] = true;
            s = s.Transition(3, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: false, deck);
            Assert.That(s.tableState, Is.EqualTo(HoldemGame.TABLE_READY));
            Assert.That(s.lastTransitionMillis, Is.EqualTo(3));

            playerIsReady[0] = false;
            s = s.Transition(4, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: false, deck);
            Assert.That(s.tableState, Is.EqualTo(HoldemGame.TABLE_IDLE));

            playerIsReady[0] = true;
            s = s.Transition(5, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: false, deck);
            Assert.That(s.tableState, Is.EqualTo(HoldemGame.TABLE_READY));
            Assert.That(s.lastTransitionMillis, Is.EqualTo(5));

            s = s.Transition(5000, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: false, deck);
            Assert.That(s.tableState, Is.EqualTo(HoldemGame.TABLE_PLAYING));
            Assert.That(s.bettingRound, Is.EqualTo(HoldemGame.PREFLOP));
            Assert.That(s.lastTransitionMillis, Is.EqualTo(5000));
            Assert.That(s.dealerSeat, Is.EqualTo(1));
            // bb
            Assert.That(s.stacks[0], Is.EqualTo(8));
            Assert.That(s.roundContribution[0], Is.EqualTo(2));
            // lb
            Assert.That(s.stacks[1], Is.EqualTo(9));
            Assert.That(s.roundContribution[1], Is.EqualTo(1));
            Assert.That(s.actingSeat, Is.EqualTo(1));
            // cards
            Assert.That(s.flop0, Is.EqualTo(CARDS["AH"]));
            Assert.That(s.flop1, Is.EqualTo(CARDS["AD"]));
            Assert.That(s.flop2, Is.EqualTo(CARDS["KD"]));
            Assert.That(s.turn, Is.EqualTo(CARDS["JD"]));
            Assert.That(s.river, Is.EqualTo(CARDS["TD"]));
            Assert.That(s.holeCards0[0], Is.EqualTo(CARDS["AS"]));
            Assert.That(s.holeCards1[0], Is.EqualTo(CARDS["AC"]));
            Assert.That(s.holeCards0[1], Is.EqualTo(CARDS["2S"]));
            Assert.That(s.holeCards1[1], Is.EqualTo(CARDS["2H"]));

            // call
            s = s.Transition(5001, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 1, actingPlayerCommited: true, deck);
            Assert.That(s.actingSeat, Is.EqualTo(0));
            // check
            s = s.Transition(5002, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: true, deck);
            // flop
            Assert.That(s.bettingRound, Is.EqualTo(HoldemGame.FLOP));
            Assert.That(s.actingSeat, Is.EqualTo(1));
            // check
            s = s.Transition(5003, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: true, deck);

            Assert.That(s.actingSeat, Is.EqualTo(0));
            // check
            s = s.Transition(5004, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: true, deck);

            // turn
            Assert.That(s.bettingRound, Is.EqualTo(HoldemGame.TURN));
            Assert.That(s.actingSeat, Is.EqualTo(1));
            // check
            s = s.Transition(5005, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: true, deck);

            Assert.That(s.actingSeat, Is.EqualTo(0));
            // check
            s = s.Transition(5006, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: true, deck);

            // river
            Assert.That(s.bettingRound, Is.EqualTo(HoldemGame.RIVER));
            Assert.That(s.actingSeat, Is.EqualTo(1));
            // check
            s = s.Transition(5007, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: true, deck);

            Assert.That(s.actingSeat, Is.EqualTo(0));
            // check
            s = s.Transition(5008, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: true, deck);

            // showdown
            Assert.That(s.bettingRound, Is.EqualTo(HoldemGame.SHOWDOWN));

            // XXX takes one extra transition to update the table state from game state
            s = s.Transition(5009, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: true, deck);
            Assert.That(s.tableState, Is.EqualTo(HoldemGame.TABLE_WINNER));

            // 0 won
            Assert.That(s.stacks[0], Is.EqualTo(12));
            Assert.That(s.stacks[1], Is.EqualTo(8));

            // waiting
            s = s.Transition(5010, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: true, deck);
            Assert.That(s.tableState, Is.EqualTo(HoldemGame.TABLE_WINNER));
            // timeout
            s = s.Transition(10000, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: true, deck);
            Assert.That(s.tableState, Is.EqualTo(HoldemGame.TABLE_IDLE));

            // immediate ready
            s = s.Transition(10001, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: true, deck);
            Assert.That(s.tableState, Is.EqualTo(HoldemGame.TABLE_READY));

            // unseat
            playerIsSeated[0] = false;
            s = s.Transition(10002, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: true, deck);
            Assert.That(s.tableState, Is.EqualTo(HoldemGame.TABLE_IDLE));
        }

        private void RunGame(
            Dictionary<int, (int, string)> players, string cards, List<(int, int)> moves, Dictionary<int, int> finalStacks)
        {
            HoldemState s;
            s.tableState = HoldemGame.TABLE_UNINITIALIZED;
            s.lastTransitionMillis = 0;
            s.bettingRound = HoldemGame.NOT_PLAYING;
            s.actingSeat = 0;
            s.dealerSeat = 0;
            s.currentBet = 2;
            s.minimumRaise = 0;
            s.headsUp = false;
            s.wonByFold = false;
            s.playerState = new int[10];
            s.stacks = new int[10];
            s.roundContribution = new int[10];
            s.potContribution = new int[10];
            s.flop0 = 0;
            s.flop1 = 0;
            s.flop2 = 0;
            s.turn = 0;
            s.river = 0;
            s.holeCards0 = new int[10];
            s.holeCards1 = new int[10];

            bool[] playerIsSeated = new bool[10];
            bool[] playerIsReady = new bool[10];
            int[] bankBySeatOwner = new int[10];
            int[] deck = new int[52];
            foreach (var p in players)
            {
                playerIsSeated[p.Key] = true;
                playerIsReady[p.Key] = true;
                bankBySeatOwner[p.Key] = p.Value.Item1;
                var h = p.Value.Item2.Split(' ');
                deck[p.Key * 2    ] = CARDS[h[0]]; 
                deck[p.Key * 2 + 1] = CARDS[h[1]];
            }

            // community (after burns)
            var c = cards.Split(' ');
            deck[21] = CARDS[c[0]]; deck[22] = CARDS[c[1]]; deck[23] = CARDS[c[2]];
            deck[25] = CARDS[c[3]];
            deck[27] = CARDS[c[4]]; 

            // init
            s = s.Transition(0, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: false, deck);
            Assert.That(s.tableState, Is.EqualTo(HoldemGame.TABLE_IDLE));
            s = s.Transition(1, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: false, deck);
            Assert.That(s.tableState, Is.EqualTo(HoldemGame.TABLE_READY));
            s = s.Transition(10000, playerIsSeated, playerIsReady, bankBySeatOwner,
                actingPlayerBet: 0, actingPlayerCommited: false, deck);
            Assert.That(s.tableState, Is.EqualTo(HoldemGame.TABLE_PLAYING));
            var time = 10001;
            foreach (var move in moves)
            {
                var (i, bet) = move;
                Assert.That(s.actingSeat, Is.EqualTo(i));
                Assert.That(s.playerState[i], Is.EqualTo(HoldemGame.PLAYER_ACTING));
                s = s.Transition(time++, playerIsSeated, playerIsReady, bankBySeatOwner,
                    actingPlayerBet: bet, actingPlayerCommited: true, deck);
            }
            while (s.tableState != HoldemGame.TABLE_WINNER)
            {
                time += 1000;
                s = s.Transition(time, playerIsSeated, playerIsReady, bankBySeatOwner,
                    actingPlayerBet: 0, actingPlayerCommited: false, deck);
            }
            foreach (var f in finalStacks)
            {
                Assert.That(s.stacks[f.Key], Is.EqualTo(f.Value));
            }
        }

        int CHECK = 0, FOLD = -1;
        
        [Test]
        public void GameWinByFold()
        {
            RunGame(
                new Dictionary<int, (int, string)> {
                    { 0, (10, "AS AH") },
                    { 1, (10, "TS TH") }
                },
                "2S 2H TC 3H 6C",
                new List<(int, int)>
                {
                    (1, FOLD),
                },
                new Dictionary<int, int> { { 0, 11 }, { 1, 9 } });
        }

        [Test]
        public void GameWinByFold2()
        {
            RunGame(
                new Dictionary<int, (int, string)> {
                    { 0, (10, "AS AH") },
                    { 1, (10, "TS TH") }
                },
                "2S 2H TC 3H 6C",
                new List<(int, int)>
                {
                    (1, 1),
                    (0, FOLD),
                },
                new Dictionary<int, int> { { 0, 8 }, { 1, 12 } });
        }

        [Test]
        public void GameWinByShowdown()
        {
            RunGame(
                new Dictionary<int, (int, string)> {
                    { 0, (10, "AS AH") },
                    { 1, (10, "TS TH") }
                },
                "2S 2H TC 3H 6C",
                new List<(int, int)>
                {
                    (1, 1),
                    (0, CHECK),
                    // flop
                    (1, CHECK), (0, CHECK),
                    // turn
                    (1, CHECK), (0, CHECK),
                    // river
                    (1, CHECK), (0, CHECK),
                },
                new Dictionary<int, int> { { 0, 8 }, { 1, 12 } });
        }

        [Test]
        public void GameWinSplitPot()
        {
            RunGame(
                new Dictionary<int, (int, string)> {
                    { 0, (10, "AS AH") },
                    { 1, (10, "AC AD") }
                },
                "2S 2H TC 3H 6C",
                new List<(int, int)>
                {
                    (1, 1),
                    (0, CHECK),
                    // flop
                    (1, CHECK), (0, CHECK),
                    // turn
                    (1, CHECK), (0, CHECK),
                    // river
                    (1, CHECK), (0, CHECK),
                },
                new Dictionary<int, int> { { 0, 10 }, { 1, 10 } });
        }

        [Test]
        public void GameWinSplitPotAllIn()
        {
            RunGame(
                new Dictionary<int, (int, string)> {
                    { 0, (10, "AS AH") },
                    { 1, (10, "AC AD") }
                },
                "2S 2H TC 3H 6C",
                new List<(int, int)>
                {
                    (1, 9),
                    (0, 8),
                },
                new Dictionary<int, int> { { 0, 10 }, { 1, 10 } });
        }

        [Test]
        public void GameWinSidePot()
        {
            RunGame(
                new Dictionary<int, (int, string)> {
                    { 0, (5, "AS AH") },
                    { 1, (10, "7C 8D") }
                },
                "2S 2H TC 3H 6C",
                new List<(int, int)>
                {
                    (1, 4),
                    (0, 3),
                },
                new Dictionary<int, int> { { 0, 10 }, { 1, 5 } });
        }

        [Test]
        public void GameWinSidePotForceAllIn()
        {
            RunGame(
                new Dictionary<int, (int, string)> {
                    { 0, (5, "AS AH") }, // bb
                    { 1, (10, "7C 8D") }, // deal
                    { 2, (10, "3S 9D") } // lb
                },
                "2S 2H TC 3H 6C",
                new List<(int, int)>
                {
                    (1, 10), // more than 0 can call
                    (2, FOLD),
                    (0, 3), // all in
                },
                new Dictionary<int, int> { { 0, 11 }, { 1, 5 }, { 2, 9 } });
        }

        [Test]
        public void GameWinSplitSidePot()
        {
            RunGame(
                new Dictionary<int, (int, string)> {
                    { 0, (100, "AS AH") }, // bb
                    { 1, (150, "AC AD") }, // deal
                    { 2, (150, "3S 9D") } // lb
                },
                "2S 2H TC 3H 6C",
                new List<(int, int)>
                {
                    (1, 2), // call
                    (2, 1), // call
                    (0, 98), // all in
                    (1, 148), // all in
                    (2, 148), // all in
                },
                new Dictionary<int, int> { { 0, 150 }, { 1, 250 }, { 2, 0 } });
        }


        // UdonBehavior only really works in play mode apparently,
        // need to split into play mode test.
        //[UnityTest]
        //public IEnumerator TestFullInstantiation()
        //{
        //    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VrchatHoldem/HoldemGame.prefab");
        //    var go = Object.Instantiate(prefab);
        //    var game = go.AddUdonSharpComponent<HoldemGame>();
        //    Assert.That(game.tableState, Is.EqualTo(HoldemGame.TABLE_UNINITIALIZED));
        //    Assert.That(game.tableState, Is.EqualTo(HoldemGame.TABLE_IDLE));
        //}


    }
}
