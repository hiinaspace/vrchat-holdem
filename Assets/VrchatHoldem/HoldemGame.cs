using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Texas Holdem game state and logic.
/// room master runs the state transitions and logic.
/// /// </summary>
public class HoldemGame : UdonSharpBehaviour
{
    const int SEATS = 10;
    public HoldemPlayer[] players;

    // packed game state to fit through udon's networking. max 182 bytes as experimentally determined
    // in mahjong. TODO recheck assumptions, maybe it's better?
    [HideInInspector] [UdonSynced] string gameState0;
    [HideInInspector] [UdonSynced] string gameState1;

    private byte byt(int i)
    {
        if (i < 0 || i > 255) Log($"{i} out of range");
        return (byte)i;
    }

    private uint unt(int i)
    {
        if (i < 0) Log($"{i} negative, clamping to uint 0");
        return (uint)Mathf.Max(0, i);
    }

    private void DumpState()
    {
        string s = $"now {GetTimeMillis()} epoch {epoch} state {tableState} bettingRound {bettingRound} headsUp {headsUp} " +
            $"dealer {dealerSeat} acting {actingSeat} \n" +
            $"{flop0} {flop1} {flop2} {turn} {river} \n" +
            $"bb {bigBlind} minRaise {minimumRaise} currentBet {currentBet}\n" +
            $"last transition {lastTransitionMillis} elapsed {GetTimeMillis() - lastTransitionMillis}\n";

        for (int i = 0; i < SEATS; ++i)
        {
            s += $"seat {i} state {playerState[i]} " +
                $"seated {players[i].IsSeated()} ready {players[i].playerReady} commit {players[i].committedEpoch} " +
                $"hole0 {holeCards0[i]} hole1 {holeCards1[i]}" +
                $" stack {stacks[i]} potCon {potContribution[i]} roundCon {roundContribution[i]}\n";
        }
        Debug.Log(s);
    }

    private void SerializeState()
    {
        // increment epoch
        epoch = (epoch + 1) % 255;
        DumpState();

        byte[] buf = new byte[182];
        int n = 0;
        buf[n++] = byt(epoch);
        buf[n++] = byt(tableState);
        buf[n++] = byt(bettingRound);
        buf[n++] = (byte)(headsUp ? 1 : 0);
        buf[n++] = (byte)(wonByFold ? 1 : 0);
        buf[n++] = byt(dealerSeat);
        buf[n++] = byt(actingSeat);

        buf[n++] = byt(flop0);
        buf[n++] = byt(flop1);
        buf[n++] = byt(flop2);
        buf[n++] = byt(turn);
        buf[n++] = byt(river);

        WriteInt(unt(bigBlind), n, buf); n += 4;
        WriteInt(unt(minimumRaise), n, buf); n += 4;
        WriteInt(unt(currentBet), n, buf); n += 4;

        // XXX can't figure out how to write signed integers, so have this hack
        // lastTransitionMillis uses Networking.GetServerTImeMillis which seems to
        // go negative depending on the server instance?
        WriteInt(unt(Mathf.Abs(lastTransitionMillis)), n, buf); n += 4;
        buf[n++] = lastTransitionMillis >= 0 ? (byte)1 : (byte)0;

        for (int i = 0; i < SEATS; ++i)
        {
            buf[n++] = byt(playerState[i]);

            buf[n++] = byt(holeCards0[i]);
            buf[n++] = byt(holeCards1[i]);

            // XXX +1 so I can use the -1 sentinel
            WriteInt(unt(Mathf.Max(-1, stacks[i]) + 1), n, buf); n += 4;
            WriteInt(unt(potContribution[i]), n, buf); n += 4;
            WriteInt(unt(roundContribution[i]), n,  buf); n += 4;
        }

        var frame = SerializeFrame(buf);
        gameState0 = new string(frame, 0, maxSyncedStringSize);
        gameState1 = new string(frame, maxSyncedStringSize, maxSyncedStringSize);
    }

    private void DeserializeState()
    {
        if (gameState0.Length != maxSyncedStringSize || gameState1.Length != maxSyncedStringSize) return;
        var frame = new char[maxPacketCharSize];
        gameState0.CopyTo(0, frame, 0, maxSyncedStringSize);
        gameState1.CopyTo(0, frame, gameState0.Length, maxSyncedStringSize);

        var buf = DeserializeFrame(frame);
        var n = 0;

        epoch = buf[n++];
        tableState = buf[n++];
        bettingRound = buf[n++];
        headsUp = buf[n++] > 0;
        wonByFold = buf[n++] > 0;
        dealerSeat = buf[n++];
        actingSeat = buf[n++];

        flop0 = buf[n++];
        flop1 = buf[n++];
        flop2 = buf[n++];
        turn =  buf[n++];
        river = buf[n++];

        bigBlind = (int)ReadInt(n, buf); n += 4;
        minimumRaise = (int)ReadInt(n, buf); n += 4;
        currentBet = (int)ReadInt(n, buf); n += 4;

        // XXX that weird negative thing
        var transitionValue = (int)ReadInt(n, buf); n += 4;
        var transitionPositive = buf[n++] > 0;
        lastTransitionMillis = transitionPositive ? transitionValue : -transitionValue;


        for (int i = 0; i < SEATS; ++i)
        {
            playerState[i] = buf[n++];

            holeCards0[i] = buf[n++];
            holeCards1[i] = buf[n++];

            // XXX -1
            stacks[i] = (int)ReadInt(n, buf) - 1; n += 4;
            potContribution[i] = (int)ReadInt(n, buf); n += 4;
            roundContribution[i] = (int)ReadInt(n, buf); n += 4;
        }
    }
    
    // called when udon updates its synced variables
    override public void OnDeserialization()
    {
        if (!Networking.IsMaster)
        {
            DeserializeState();
        }
    }

    void WriteInt(uint i, int pos, byte[] buf)
    {
        buf[pos] = (byte)(i >> 24);
        buf[pos + 1] = (byte)((i >> 16) & 255);
        buf[pos + 2] = (byte)((i >> 8) & 255);
        buf[pos + 3] = (byte)(i & 255);
    }
    uint ReadInt(int n, byte[] buf)
    {
        uint pack = buf[n];
        pack = (pack << 8) + buf[n + 1];
        pack = (pack << 8) + buf[n + 2];
        pack = (pack << 8) + buf[n + 3];
        return pack;
    }

    private const int maxSyncedStringSize = 105;
    private const int maxPacketCharSize = maxSyncedStringSize * 2;
    // 14 bits leftover to do something with
    // possibly packing player id + seqNo to monitor per-player packet loss.
    private const int headerCharSize = 2;
    // for simplicity of the byte -> 7bit packing, which packs 7 bytes to 8 chars, 56 bits at a time
    // header size at 2 makes this a nice round 208 chars or 182 bytes
    private const int maxDataCharSize = (int)((maxPacketCharSize - headerCharSize) / 8f) * 8;
    private const int maxDataByteSize = maxDataCharSize / 8 * 7;

    private char[] SerializeFrame(byte[] buf)
    {
        var frame = new char[maxPacketCharSize];
        int n = 0;
        for (int i = 0; i < maxDataByteSize;)
        {
            // pack 7 bytes into 56 bits;
            ulong pack = buf[i++];
            pack = (pack << 8) + buf[i++];
            pack = (pack << 8) + buf[i++];

            pack = (pack << 8) + buf[i++];
            pack = (pack << 8) + buf[i++];
            pack = (pack << 8) + buf[i++];
            pack = (pack << 8) + buf[i++];
            //DebugLong("packed: ", pack);

            // unpack into 8 7bit asciis
            frame[n++] = (char)((pack >> 49) & (ulong)127);
            frame[n++] = (char)((pack >> 42) & (ulong)127);
            frame[n++] = (char)((pack >> 35) & (ulong)127);
            frame[n++] = (char)((pack >> 28) & (ulong)127);

            frame[n++] = (char)((pack >> 21) & (ulong)127);
            frame[n++] = (char)((pack >> 14) & (ulong)127);
            frame[n++] = (char)((pack >> 7) & (ulong)127);
            frame[n++] = (char)(pack & (ulong)127);
            //DebugChars("chars: ", chars, n - 8);
        }
        return frame;
    }

    private byte[] DeserializeFrame(char[] frame)
    {
        var packet = new byte[maxDataByteSize];
        int n = 0;
        for (int i = 0; i < maxDataByteSize;)
        {
            //DebugChars("deser: ", chars, n);
            // pack 8 asciis into 56 bits;
            ulong pack = frame[n++];
            pack = (pack << 7) + frame[n++];
            pack = (pack << 7) + frame[n++];
            pack = (pack << 7) + frame[n++];

            pack = (pack << 7) + frame[n++];
            pack = (pack << 7) + frame[n++];
            pack = (pack << 7) + frame[n++];
            pack = (pack << 7) + frame[n++];
            //DebugLong("unpacked: ", pack);

            // unpack into 7 bytes
            packet[i++] = (byte)((pack >> 48) & (ulong)255);
            packet[i++] = (byte)((pack >> 40) & (ulong)255);
            packet[i++] = (byte)((pack >> 32) & (ulong)255);
            packet[i++] = (byte)((pack >> 24) & (ulong)255);

            packet[i++] = (byte)((pack >> 16) & (ulong)255);
            packet[i++] = (byte)((pack >> 8) & (ulong)255);
            packet[i++] = (byte)((pack >> 0) & (ulong)255);
        }
        return packet;
    }

    public const int
        TABLE_UNINITIALIZED = 0,
        TABLE_IDLE = 1,
        TABLE_READY = 2,
        TABLE_PLAYING = 3,
        TABLE_WINNER = 5
        ;
    public int tableState = TABLE_UNINITIALIZED;

    public const int
        NOT_PLAYING = 0, PREFLOP = 1, FLOP = 2, TURN = 3, RIVER = 4, SHOWDOWN = 5;
    public int bettingRound = NOT_PLAYING;

    // if in heads-up, no more player action possible so advance betting rounds on a timeout
    public bool headsUp = false;
    // all but one player folded; to avoid revealing cards in that case.
    public bool wonByFold = false;

    public const int
        PLAYER_DEAD = 0,
        PLAYER_PENDING = 1,
        PLAYER_ACTING = 2,
        PLAYER_COMMITED = 3
        ;
    public int[] playerState = new int[SEATS];

    // as room master,
    // read external state (player, master-local UI, passage of time) and transition state,
    // serializing for other players.
    // for non-masters, the deserialization recovers the transition to play local animations and logs.
    public void Transition()
    {
        int[] bankBySeatOwner = new int[SEATS];
        bool[] playerIsSeated = new bool[SEATS];
        bool[] playerIsReady = new bool[SEATS];
        for (int i = 0; i < SEATS; i++)
        {
            var player = players[i];
            playerIsSeated[i] = player.IsSeated();
            playerIsReady[i] = player.playerReady;
            if (playerIsSeated[i])
            {
                var owner = Networking.GetOwner(player.gameObject);
                var balance = bank.GetBalance(owner);
                // if not in the bank
                if (balance == -1)
                {
                    // start out
                    Log($"seat {i} new player, giving {initialStackSize} chips");
                    balance = initialStackSize;
                    bank.SetBalance(owner, balance);
                }
                bankBySeatOwner[i] = balance;
            }
        }

        var nowMillis = GetTimeMillis();
        var actingPlayerBet = players[actingSeat].bet;
        var actingPlayerCommited = players[actingSeat].committedEpoch == epoch;

        // pack into pure state function
        object[] newState = CalculateTransition(
            tableState, lastTransitionMillis, bettingRound, actingSeat, dealerSeat,
            currentBet, minimumRaise, headsUp, wonByFold,
            playerState, stacks, roundContribution, potContribution,
            flop0, flop1, flop2, turn, river, holeCards0, holeCards1,
             // external state
            nowMillis, bigBlind,
            winnerTimeoutSecs, readyTimeoutSecs,
            actionTimeoutSecs, headsUpTimeoutSecs,
            playerIsSeated, playerIsReady, bankBySeatOwner,
            actingPlayerBet, actingPlayerCommited,
            null /* deck for unit tests */
        );

        if (newState != null)
        {
            // and extract. not efficient but probably fine
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

            SerializeState();
        }
    }

    // the whole thing but unit testable
    public
#if !COMPILER_UDONSHARP
        static
#endif
        object[] CalculateTransition(
            // internal state
            int tableState, int lastTransitionMillis, int bettingRound, int actingSeat, int dealerSeat,
            int currentBet, int minimumRaise, bool headsUp, bool wonByFold,
            // per player state, XXX mutated in place for simplicity
            int[] playerState, int[] stacks, int[] roundContribution, int[] potContribution,

            // deal
            int flop0, int flop1, int flop2, int turn, int river,
            int[] holeCards0, int[] holeCards1,

            // external state
            int nowMillis, int bigBlind,
            int winnerTimeoutSecs, int readyTimeoutSecs,
            int actionTimeoutSecs, int headsUpTimeoutSecs,
            bool[] playerIsSeated, bool[] playerIsReady, int[] bankBySeatOwner,
            int actingPlayerBet, bool actingPlayerCommited,
            // XXX for unit testing, the deck that will be dealt from instead of random;
            // udon doesn't support seeding the RNG (outside of a small 32-bit int).
            // will be null in game
            int[] deck
        )
    {
        int newTableState = CalculateTableTransition(
                 tableState, lastTransitionMillis,
                 nowMillis, bettingRound, wonByFold, bigBlind, winnerTimeoutSecs,
                 readyTimeoutSecs, playerIsSeated, playerIsReady, stacks);
        bool changed = false;

        if (newTableState != tableState)
        {
            Log($"table from {tableState} to {newTableState}");
            lastTransitionMillis = nowMillis;
            changed = true;

            // edge triggered stuff
            if (tableState == TABLE_READY && newTableState == TABLE_PLAYING)
            {
                bool[] playerStackedAndReady = new bool[SEATS];
                // initialize ready players
                for (int i = 0; i < SEATS; i++)
                {
                    if (playerIsSeated[i] && playerIsReady[i] && stacks[i] >= bigBlind)
                    {
                        playerState[i] = PLAYER_PENDING;
                        playerStackedAndReady[i] = true;
                    }
                    roundContribution[i] = 0;
                    potContribution[i] = 0;
                }
                Log($"game started, dealing game");

                var setup = SetupBlinds(
                    dealerSeat, bigBlind, playerState, stacks, roundContribution, playerStackedAndReady);
                actingSeat = (int)setup[0];
                dealerSeat = (int)setup[1];

                // XXX hack for unit testing; in-game will run shuffle
                if (deck == null) deck = ShuffleDeck();

                // deal all 10 seats and community cards
                int deal = 0;
                for (int i = 0; i < SEATS; ++i)
                {
                    holeCards0[i] = deck[deal++];
                    holeCards1[i] = deck[deal++];
                    // sort higher for nice display;
                    if (holeCards1[i] > holeCards0[i])
                    {
                        var swap = holeCards0[i];
                        holeCards0[i] = holeCards1[i];
                        holeCards1[i] = swap;
                    }
                }
                // burn cards per tradition
                deal++;
                flop0 = deck[deal++];
                flop1 = deck[deal++];
                flop2 = deck[deal++];
                deal++;
                turn = deck[deal++];
                deal++;
                river = deck[deal++];

                bettingRound = PREFLOP;
                currentBet = bigBlind;
                minimumRaise = bigBlind;
            }
            if (newTableState == TABLE_WINNER)
            {
                if (wonByFold)
                {
                    // sweep it up
                    int winner = 0;
                    int winnings = 0;
                    for (int i = 0; i < SEATS; i++)
                    {
                        if (playerState[i] != PLAYER_DEAD) winner = i;
                        winnings += potContribution[i];
                    }
                    Log($"game won by {winner} by fold");
                    stacks[winner] += winnings;
                }
                else
                {
                    Log($"game showdown.");
                    ulong[] handValues = GetHandValues(
                        playerState, holeCards0, holeCards1, flop0, flop1, flop2, turn, river);
                    int[] playerRanksByHandValues = RankPlayers(handValues);

                    int[] winnings = DividePot(potContribution, playerRanksByHandValues);
                    for (int i = 0; i < SEATS; ++i)
                    {
                        stacks[i] += winnings[i];
                    }

                }
            }
        }
        tableState = newTableState;

        if (tableState == TABLE_PLAYING)
        {
            object[] newGameState = CalculateGameTransition(
                lastTransitionMillis, bettingRound, actingSeat,
                currentBet, minimumRaise, headsUp, playerState,
                stacks, roundContribution, potContribution,
                nowMillis,
                actionTimeoutSecs, headsUpTimeoutSecs, bigBlind, dealerSeat, playerIsSeated[actingSeat],
                actingPlayerBet, actingPlayerCommited);
            if (newGameState != null)
            {
                // have new state
                lastTransitionMillis = nowMillis;
                bettingRound = (int)newGameState[0];
                actingSeat = (int)newGameState[1];
                currentBet = (int)newGameState[2];
                minimumRaise = (int)newGameState[3];
                headsUp = (bool)newGameState[4];
                wonByFold = (bool)newGameState[5];
                // arrays got mutated by reference
                changed = true;
            }
        } else if (tableState != TABLE_WINNER) // XXX if winner, the stacks were just adjusted
        {
            // sync stack to bank
            for (int i = 0; i < SEATS; i++)
            {
                // 0 for unseated players
                if (stacks[i] != bankBySeatOwner[i])
                {
                    changed = true;
                    Log($"Adjusting stack of {i} to bank value {i}");
                    stacks[i] = bankBySeatOwner[i];
                }
            }
        }

        if (changed)
        {
            return new object[] {
                tableState, lastTransitionMillis, bettingRound, actingSeat, dealerSeat,
                currentBet, minimumRaise, headsUp, wonByFold,
                playerState, stacks, roundContribution, potContribution,
                flop0, flop1, flop2, turn, river, holeCards0, holeCards1
            };
        } else
        {
            return null;
        }
    }
    private 
#if !COMPILER_UDONSHARP
        static
#endif
        object[] SetupBlinds(
        int lastDealerSeat,
        int bigBlind, 
        // XXX mutated through
        int[] playerState, int[] stacks, int[] roundContribution,
        bool[] playerStackedAndReady)
    {
        var dealerSeat = NextReadySeat(lastDealerSeat, playerStackedAndReady);
               var littleBlindSeat = NextReadySeat(dealerSeat, playerStackedAndReady);
        var bigBlindSeat = NextReadySeat(littleBlindSeat, playerStackedAndReady);
        int actingSeat;
        // if only two players, then the dealer posts the small blind and dealer acts first before the flop.
        if (bigBlindSeat == dealerSeat)
        {
            bigBlindSeat = littleBlindSeat;
            littleBlindSeat = dealerSeat;

            actingSeat = dealerSeat;
        }
        else
        {
            actingSeat = NextReadySeat(bigBlindSeat, playerStackedAndReady);
        }
        Log($"dealer {dealerSeat}, little {littleBlindSeat} big {bigBlindSeat}");

        roundContribution[littleBlindSeat] = bigBlind / 2;
        stacks[littleBlindSeat] -= bigBlind / 2;

        roundContribution[bigBlindSeat] = bigBlind;
        stacks[bigBlindSeat] -= bigBlind;

        playerState[actingSeat] = PLAYER_ACTING;
        return new object[]
        {
            actingSeat, dealerSeat
        };
    }

    private
#if !COMPILER_UDONSHARP
        static
#endif
        int NextReadySeat(int start, bool[] playerStackedAndReady)
    {
        int i = (start + 1) % SEATS;
        while (i != start)
        {
            if (playerStackedAndReady[i])
                return i;
            i = (i + 1) % SEATS;
        }
        return start;
    }



    private 
#if !COMPILER_UDONSHARP
        static
#endif
        int[] ShuffleDeck()
    {
        int[] deck = new int[52];
        for (int i = 0; i < 52; ++i)
        {
            deck[i] = i;
        }
        int swap;
        for (int i = 51; i >= 1; --i)
        {
            var j = Random.Range(0, i + 1); // range max is exclusive
            swap = deck[j];
            deck[j] = deck[i];
            deck[i] = swap;
        }
        return deck;
    }

    // from the current internal state plus external state, the new state
    // packed into a tuple returns null if there's no serializable change.
    // plenty of garbage, but we're not doing it every frame at least.
    // "table" layer is idle/ready and stacks
    // idle --(players ready and stacked) --> ready --(timeout)--> playing --(win) --> win --(timeout)--> idle
    //  --(0 players seated)-->idle
    //  --(not playing and <2 players ready)-->idle
    public
#if !COMPILER_UDONSHARP
        static
#endif
        int CalculateTableTransition(
            // internal state
            int currentTableState, int lastTransitionMillis, 

            // external state
            int nowMillis, int bettingRound, bool wonByFold, int bigBlind, 
            int winnerTimeoutSecs, int readyTimeoutSecs, 
            bool[] playerIsSeated, bool[] playerIsReady, int[] stacks
        )
    {
        int readyAndStacked = 0;
        int seated = 0;
        for (int i = 0; i < SEATS; ++i)
        {
            if (playerIsSeated[i]) seated++;
            if (playerIsSeated[i] && playerIsReady[i] && stacks[i] >= bigBlind) readyAndStacked++;
        }

        var elapsedSecs = (nowMillis - lastTransitionMillis) / 1000;
        switch (currentTableState)
        {
            case TABLE_UNINITIALIZED:
                Log($"initialized table");
                return TABLE_IDLE;
            case TABLE_IDLE:
                if (readyAndStacked > 1)
                {
                    Log($"{readyAndStacked} players ready and stacked, ready");
                    return TABLE_READY;
                }
                break;
            case TABLE_READY:
                if (readyAndStacked < 2)
                {
                    Log($"{readyAndStacked} players ready, need more, back to idle");
                    return TABLE_IDLE;
                }
                if (elapsedSecs > readyTimeoutSecs)
                {
                    Log($"{elapsedSecs} passed, time to deal");
                    return TABLE_PLAYING;
                }
                break;
            case TABLE_PLAYING:
                if (seated < 1)
                {
                    Log($"all players left, returning to idle");
                    return TABLE_IDLE;
                }
                if (bettingRound > RIVER || wonByFold)
                {
                    Log($"game state in winning state, table_winner");
                    return TABLE_WINNER;
                }
                break;
            case TABLE_WINNER:
                if (elapsedSecs > winnerTimeoutSecs)
                {
                    Log($"winner display timeout, returning to idle");
                    return TABLE_IDLE;
                }
                break;
        }
        return currentTableState;
    }

    // if table idle or table winner -> null
    // if table playing and not playing -> deal
    // if table playing and playing -> next
    // if table playing and showdown -> null

    // game state doesn't matter about cards until showdown

    // game is bettingRound/dealer/acting/bigBlind/currentBet/minRaise/stacks/round/pot/player(DEAD/PENDING/ACTING/COMMIT)/timeout


    // fixed game state on deal bigBlind, dealerSeat
    //
    // changing per round are bettingRound, playerState, stack/round/pot, actingSeat

    // cards not matter

    // can transition the acting player first, 
    //  ACTING --(not seated)-->DEAD
    //  ACTING --(timeout)--> DEAD
    //  ACTING --(valid commit)--> COMMMITED
    // 
    // only transition on acting player, the not_seated check can run on each individually
    // as in if a player leaves in the middle of a round, they're pending until they become acting, then they immediately fold


    
    // from the current internal state plus external state, the new state
    // packed into a tuple returns null if there's no serializable change.
    // plenty of garbage, but we're not doing it every frame at least.
    public 
#if !COMPILER_UDONSHARP
        static
#endif
        object[] CalculateGameTransition(
            // internal state
            int lastTransitionMillis, int bettingRound, int actingSeat,
            int currentBet, int minimumRaise, bool headsUp,  
            // per player state, XXX mutated in place for simplicity
            int[] currentPlayerState,
            int[] stacks, int[] roundContribution, int[] potContribution,
            // external state
            int nowMillis,
            int actTimeoutSecs, int headsUpTimeoutSecs, int bigBlind, int dealerSeat, bool actingPlayerSeated,
            int actingPlayerBet, bool actingPlayerCommitted
        )
    {
        var elapsed = (nowMillis - lastTransitionMillis) / 1000;
        if (headsUp)
        {
            // XXX does check if players left/died during headsup (except for table check 0 players -> IDLE)
            // I could do a won-by-fold check here, but I think it'll otherwise work fine.
            if (elapsed < headsUpTimeoutSecs) return null;
            Log($"headsup timeout, round to {bettingRound + 1}");
            // increment round, change nothing else
            return new object[] {
                bettingRound + 1, 0, 0, 0, true, false,
                // all the same
                currentPlayerState, stacks, roundContribution, potContribution
            };
        }

        // waiting on acting player
        bool validBet = 
            actingPlayerBet <= stacks[actingSeat] &&
            (roundContribution[actingSeat] + actingPlayerBet == currentBet || // call/check
             actingPlayerBet == stacks[actingSeat] || // all in
             (roundContribution[actingSeat] + actingPlayerBet - currentBet) >= minimumRaise // (re) raise of appropriate size
            );

        if ((actingPlayerBet == -1 && actingPlayerCommitted) || elapsed > actTimeoutSecs || !actingPlayerSeated)
        {
            Log($"acting player {actingSeat} folded or timed out");
            currentPlayerState[actingSeat] = PLAYER_DEAD;
            // current bet and stacks stay the same
            // advance to next actor/round
        }
        else if (actingPlayerCommitted && validBet)
        {
            Log($"acting player {actingSeat} committed to bet of {actingPlayerBet}");
            // update currentBet, minimum raise, stacks/contribution
            stacks[actingSeat] -= actingPlayerBet;
            roundContribution[actingSeat] += actingPlayerBet;
            
            // if they bet more than the current, more action. if they simply
            // called/checked or went all-in below, no more action
            if (actingPlayerBet > currentBet)
            {
                // ratchet up minimum raise
                minimumRaise = roundContribution[actingSeat] - currentBet;
                Log($"acting player {actingSeat} raised to {minimumRaise}");
                currentBet = roundContribution[actingSeat];

                // flip all previously COMMITED to PENDING if they have chips to bet;
                for (int i = 0; i < SEATS; i++)
                {
                    if (currentPlayerState[i] == PLAYER_COMMITED && stacks[i] > 0) 
                        currentPlayerState[i] = PLAYER_PENDING;
                }
            }

            currentPlayerState[actingSeat] = PLAYER_COMMITED;
        } else
        {
            return null; // waiting;
        }

        // if we're here, next action
        int nextActor = (actingSeat + 1) % SEATS;
        while (nextActor != actingSeat && currentPlayerState[nextActor] != PLAYER_PENDING)
            nextActor = (nextActor + 1) % SEATS;

        int alivePlayers = 0;
        for (int i = 0; i < SEATS; i++)
        {
            // if player has more chips to play
            if (currentPlayerState[i] != PLAYER_DEAD) alivePlayers++;
        }

        bool roundClosed = (nextActor == actingSeat) || alivePlayers < 2;
        if (roundClosed)
        {
            int pendingPlayers = 0;
            // rake
            for (int i = 0; i < SEATS; i++)
            {
                potContribution[i] += roundContribution[i];
                roundContribution[i] = 0;
                // flip player to pending if they can still play
                if (bettingRound < RIVER && currentPlayerState[i] == PLAYER_COMMITED && stacks[i] > 0)
                {
                    currentPlayerState[i] = PLAYER_PENDING;
                    pendingPlayers++;
                }
            }
            Log($"round {bettingRound} over, {pendingPlayers} players pending, {alivePlayers} players alive");

            if (bettingRound == RIVER)
            {
                // showdown
                return new object[] {
                    SHOWDOWN, 0, 0, bigBlind, false /* headsUp */, false /* wonByFold */,
                    currentPlayerState, stacks, roundContribution, potContribution
                };
            } else if (pendingPlayers > 1) 
            {
                // regular new round
                int roundStart = dealerSeat;
                while (currentPlayerState[roundStart] != PLAYER_PENDING)
                    roundStart = (roundStart + 1) % SEATS;
                currentPlayerState[roundStart] = PLAYER_ACTING;

                return new object[] {
                    // most of this mutated in place.
                    bettingRound + 1, roundStart, 0, bigBlind, false /* headsUp */, false /* wonByFold */,
                    currentPlayerState, stacks, roundContribution, potContribution
                };

            }
            else if (alivePlayers == 1)
            {
                // they won by fold ; keep betting round the same
                return new object[] {
                    bettingRound, 0, 0, bigBlind, false, true /* wonByFold */,
                    currentPlayerState, stacks, roundContribution, potContribution
                };
            }
            else
            {
                // heads-up new round
                // flip players back to committed, no more action possible
                for (int i = 0; i < SEATS; i++)
                {
                    if (currentPlayerState[i] == PLAYER_PENDING) currentPlayerState[i] = PLAYER_COMMITED;
                }
                return new object[] {
                    bettingRound + 1, 
                    // doesn't matter
                    0, 0, bigBlind, true /* heads up */, false /* wonByFold */,
                    // mutated in place, and now fixed for the rest of the game
                    currentPlayerState, stacks, roundContribution, potContribution
                };
            }
        }
        else
        {
            Log($"action moving to {nextActor} in round {bettingRound}");
            // same round, new player
            currentPlayerState[nextActor] = PLAYER_ACTING;
            return new object[] {
                // most of this mutated in place.
                bettingRound, nextActor, currentBet, minimumRaise, false /* headsUp */, false /* wonByFold */,
                currentPlayerState, stacks, roundContribution, potContribution
            };
        }
    }

    private int GetTimeMillis()
    {
        return Networking.LocalPlayer == null ? (int)(Time.time * 1000) :
             // XXX note that this doesn't really have any meaning, and can be negative
             // presumably remains consistent across an instance though
             Networking.GetServerTimeInMilliseconds();
    }

    public 
#if !COMPILER_UDONSHARP
        static
#endif
        ulong[] GetHandValues(
            int[] playerState, int[] holeCards0, int[] holeCards1,
            int flop0, int flop1, int flop2, int turn, int river)
    {
        ulong[] handValues = new ulong[SEATS];
        for (int i = 0; i < SEATS; ++i)
        {
            if (playerState[i] != PLAYER_DEAD)
            {
                handValues[i] = BestPlayerHand(
                    holeCards0[i], holeCards1[i],
                    flop0, flop1, flop2, turn, river);
            } 
        }
        return handValues;
    }

    // returns 
    public 
#if !COMPILER_UDONSHARP
        static
#endif
        int[] RankPlayers(ulong[] handValues)
    {
        ulong[] sortedHandValues = new ulong[SEATS];
        for (int i = 0; i < SEATS; ++i)
        {
            sortedHandValues[i] = handValues[i];
        }

        // sort descending
        for (int i = 1; i < SEATS; ++i)
        {
            int j = i - 1;
            ulong k = sortedHandValues[i];
            while (j >= 0 && sortedHandValues[j] < k)
            {
                sortedHandValues[j + 1] = sortedHandValues[j];
                j--;
            }
            sortedHandValues[j + 1] = k;
        }

        // write ranks. probably a way to do this within the sorting loop, but
        // I couldn't figure it out
        int[] playerRanksByHandValues = new int[SEATS];
        for (int i = 0; i < SEATS; ++i)
        {
            ulong value = handValues[i];
            int rank = -1;
            ulong last = ulong.MaxValue;
            for (int j = 0; j < SEATS; ++j)
            {
                ulong otherVal = sortedHandValues[j]; 
                if (last != otherVal)
                {
                    rank++;
                    last = otherVal;
                }
                if (otherVal == value)
                {
                    playerRanksByHandValues[i] = rank;
                    break;
                }
            }
        }

        return playerRanksByHandValues;
    }

    public ulong BestPlayerHandSeat(int seat)
    {
        return BestPlayerHand(holeCards0[seat], holeCards1[seat], flop0, flop1, flop2, turn, river);
    }

    public 
#if !COMPILER_UDONSHARP
        static
#endif
        ulong umax(ulong a, ulong b)
    {
        return a > b ? a : b;
    }

    // there are more efficient ways to do this yes.
    public 
#if !COMPILER_UDONSHARP
        static
#endif
        ulong BestPlayerHand(int h0, int h1, int flop0, int flop1, int flop2, int turn, int river)
    {

        ulong max =     EvaluateHand(h0, h1, flop0, flop1, flop2);
        max = umax(max, EvaluateHand(h0, h1, flop0, flop1, flop2));
        max = umax(max, EvaluateHand(h0, h1, flop0, flop1, flop2));
        max = umax(max, EvaluateHand(h0, h1, flop0, flop1, turn));  
        max = umax(max, EvaluateHand(h0, h1, flop0, flop1, river)); 

        max = umax(max, EvaluateHand(h0, h1, flop0, flop2, turn));
        max = umax(max, EvaluateHand(h0, h1, flop0, flop2, river)); 

        max = umax(max, EvaluateHand(h0, h1, flop0, turn, river));

        max = umax(max, EvaluateHand(h0, h1, flop1, flop2, turn));
        max = umax(max, EvaluateHand(h0, h1, flop1, flop2, river)); 

        max = umax(max, EvaluateHand(h0, h1, flop1, turn, river));

        max = umax(max, EvaluateHand(h0, h1, flop2, turn, river));

        max = umax(max, EvaluateHand(h0, flop0, flop1, flop2, turn)); 
        max = umax(max, EvaluateHand(h0, flop0, flop1, flop2, river));
        max = umax(max, EvaluateHand(h0, flop0, flop1, turn, river)); 
        max = umax(max, EvaluateHand(h0, flop0, flop2, turn, river)); 
        max = umax(max, EvaluateHand(h0, flop0, flop1, flop2, turn)); 

        max = umax(max, EvaluateHand(h1, flop0, flop1, flop2, river));
        max = umax(max, EvaluateHand(h1, flop0, flop1, turn, river));
        max = umax(max, EvaluateHand(h1, flop0, flop2, turn, river));
        max = umax(max, EvaluateHand(h1, flop1, flop2, turn, river));
        max = umax(max, EvaluateHand(h1, flop1, flop2, turn, river));
        // could save the other N evaluations of this but it's probably fine
        max = umax(max, EvaluateHand(flop0, flop1, flop2, turn, river));

        return max;
    }

    public ulong BestPlayerHandTurnSeat(int seat)
    {
        return BestPlayerHandTurn(holeCards0[seat], holeCards1[seat], flop0, flop1, flop2, turn);
    }

 public 
#if !COMPILER_UDONSHARP
        static
#endif
        ulong BestPlayerHandTurn(int h0, int h1, int flop0, int flop1, int flop2, int turn)
    {
        ulong max = EvaluateHand(h0, h1, flop0, flop1, flop2);
        max = umax(max, EvaluateHand(h0, h1, flop0, flop1, turn));
        max = umax(max, EvaluateHand(h0, h1, flop0, flop2, turn));
        max = umax(max, EvaluateHand(h0, h1, flop1, flop2, turn));
        max = umax(max, EvaluateHand(h0, flop0, flop1, flop2, turn));
        max = umax(max, EvaluateHand(h1, flop0, flop1, flop2, turn));
        return max;
    }
    
    public ulong BestPlayerHandFlop(int seat)
    {
        return EvaluateHand(holeCards0[seat], holeCards1[seat], flop0, flop1, flop2);
    }

    // Figure out split and side pots 
    public 
#if !COMPILER_UDONSHARP
        static
#endif
        int[] DividePot(int[] contributions, int[] playerRanksByHandValues)
    {
        int[] winnings = new int[SEATS];

        // add up all the chips
        int remainingPot = 0;
        for (int i = 0; i < SEATS; ++i)
        {
            remainingPot += contributions[i];
        }

        int rank = 0;
        while (remainingPot > 0)
        {
            int rankCount = 0;
            int[] playersAtRank = new int[SEATS];
            for (int i = 0; i < SEATS; ++i)
            {
                if (playerRanksByHandValues[i] == rank)
                    playersAtRank[rankCount++] = i;
            }

            // at this rank, ordered by smallest contrib to largest
            // in order to calculate side pots correctly.
            int[] playersAtRankByContrib = new int[rankCount];
            for (int i = 0; i < rankCount; ++i)
            {
                playersAtRankByContrib[i] = playersAtRank[i];
            }
            for (int i = 1; i < rankCount; ++i)
            {
                int k = playersAtRankByContrib[i];
                int kc = contributions[k];
                int j = i - 1;
                while (j >= 0 && contributions[playersAtRankByContrib[i]] > kc) {
                    playersAtRankByContrib[j + 1] = playersAtRankByContrib[j];
                    j--;
                }
                playersAtRankByContrib[j + 1] = k;
            }

            // the player at this rank who contributed the most (tallest stack)
            // determines how much is in this rank(side) pot ; i.e. if they
            // were the sole player at this rank this is how much they'd take
            var tallestPlayer = playersAtRank[rankCount - 1];
            var tallestPlayerContrib = contributions[tallestPlayer];

            var rankPot = 0;
            for (int j = 0; j < SEATS; ++j)
            {
                rankPot += Mathf.Min(contributions[j], tallestPlayerContrib);
            }

            // in the simple case of single winning player without any side/split pots
            // this is the entire pot.
            // if a there was a previous side pot then clamp to the remaining chips
            rankPot = Mathf.Min(remainingPot, rankPot);
            remainingPot -= rankPot;

            // now that we have the pot for this rank
            // the shortest stack gets their (divided) share,
            // then so on until the rankPot is gone.
            // odd pots go to the tallest stack player, or arbitrarily
            // whoever's seated furthest from dealer (higher seat idx)
            for (int i = 0; i < rankCount; ++i)
            {
                var playerIdx = playersAtRankByContrib[i];
                var playerContrib = contributions[playerIdx];

                // player can win as much as their contribution, from each other player
                var totalWinnable = 0;
                for (int j = 0; j < SEATS; ++j)
                {
                    totalWinnable += Mathf.Min(contributions[j], playerContrib);
                }

                // split with all equal-rank players.
                // in the simple case this is just the entire rank pot (which was the entire pot)
                var split = totalWinnable / rankCount;

                // subtract the actual winnings from rank pot
                split = Mathf.Min(rankPot, split);
                rankPot -= split;
                // give to player
                winnings[playerIdx] += split;
            }

            // any odd chips
            if (rankPot > 0)
            {
                winnings[tallestPlayer] += rankPot;
            }

            rank++;
        }
        return winnings;
    }


    public bool IsValidBet(int bet, int seatIdx)
    {
        return
            bet <= stacks[seatIdx] &&
            (roundContribution[seatIdx] + bet == currentBet || // call
             bet == stacks[seatIdx] || // all in
             bet >= minimumRaise // (re) raise of appropriate size
            );
    }
    // incrementing int of current state, used to check that seat commits are for the current action
    public int epoch = 0;

    // the last time something happened, used for timeouts.
    public int lastTransitionMillis;

    public int bigBlind = 50;

    // chip count per seat
    public int[] stacks = new int[SEATS];

    // two parallel arrays for seats's hole cards. int[][] bad in udon
    public int[] holeCards0 = new int[SEATS];
    public int[] holeCards1 = new int[SEATS];

    // community cards
    int flop0, flop1, flop2, turn, river;

    // in most states, the seat idx that currently needs to act
    public int actingSeat = 2;
    // dealer seat index
    public int dealerSeat = 0;

    // betting round state tracking
    // keep track of total pot contributions per seat for side pots
    int[] potContribution = new int[SEATS];
    // current bet to match
    public int currentBet = 0;
    // current minimum raise
    public int minimumRaise = 0;

    // per-seat bet/call for the round
    public int[] roundContribution = new int[SEATS];

    // round over if current seat is CHECKED/CALLED/(RE)RAISED/ALLIN and all
    // other seats are not WAITING.

    // how long after ready before next round
    int readyTimeoutSecs = 10;
    // how long to display headsup rounds
    int headsUpTimeoutSecs = 5;
    // how long to display winner
    int winnerTimeoutSecs = 15;

    // how long to wait for seat action before folding them
    public int actionTimeoutSecs = 90;

    public HoldemBank bank;

    public int initialStackSize = 5000;

    public UnityEngine.UI.Text uiFlop0, uiFlop1, uiFlop2, uiTurn, uiRiver, uiPot, uiGameStatus, uiRoundStatus, uiDebugToggleText, uiLog;
    public GameObject goFlop0, goFlop1, goFlop2, goTurn, goRiver;
    public GameObject goDebug;

    public UnityEngine.UI.Toggle uiDebugToggle;

    const float updateInterval = 0.5f;
    private float masterUpdateCountdown = 0;

    private int lastLocalEpoch = -1;

    private string winnerText = "winnerText";

    private void Start()
    {
        for (int i = 0; i < SEATS; ++i)
        {
            players[i].game = this;
            players[i].seatIdx = i;
        }
    }

    private void Update()
    {
        var now = GetTimeMillis();

        int readyAndStacked = 0;
        int seated = 0;
        int pending = 0;
        int commited = 0;
        int inPlay = 0;
        for (int i = 0; i < SEATS; ++i)
        {
            if (players[i].IsSeated())
            {
                seated++;
                if (players[i].playerReady && stacks[i] >= bigBlind) readyAndStacked++;
                if (playerState[i] != PLAYER_DEAD) inPlay++;
                if (playerState[i] == PLAYER_PENDING) pending++;
                if (playerState[i] == PLAYER_COMMITED) commited++;
            }
        }

        var since = (now - lastTransitionMillis) / 1000;

        uiGameStatus.text =
            tableState == TABLE_UNINITIALIZED ? "Uninitialized" :
            tableState == TABLE_IDLE ? $"Idle ({readyAndStacked} players ready)" :
            tableState == TABLE_READY ? 
              $"{readyAndStacked} players ready. Dealing in {readyTimeoutSecs - since}..." :
            tableState == TABLE_PLAYING ? ((
              bettingRound == PREFLOP ? "Pre-Flop" :
              bettingRound == FLOP ? "Flop" :
              bettingRound == TURN ? "Turn" : "River"
                ) + (headsUp ? $" (Heads-Up, next round in {headsUpTimeoutSecs - since})" :
                $" ({OwnerName(players[actingSeat].gameObject)} has {actionTimeoutSecs - since} to act)")) :
            winnerText + $"\nNext game in {winnerTimeoutSecs - since}...";
        int potTotal = 0, roundTotal = 0;
        for (int i = 0; i < SEATS; ++i)
        {
            potTotal += potContribution[i];
            roundTotal += roundContribution[i];
        }
        uiPot.text = $"{potTotal} (+{roundTotal} round)";
        uiRoundStatus.text = $"Current Bet: {currentBet} Min-raise: {minimumRaise}\n" +
            $"{inPlay} players, {pending} to act, {commited} commited";
        var dealt = tableState == TABLE_PLAYING || tableState == TABLE_WINNER;
        uiPot.enabled = dealt;
        uiRoundStatus.enabled = dealt;

        bool flopped = dealt && bettingRound >= FLOP;
        goFlop0.SetActive(flopped);
        goFlop1.SetActive(flopped);
        goFlop2.SetActive(flopped);
        goTurn.SetActive(dealt && bettingRound >= TURN);
        goRiver.SetActive(dealt && bettingRound >= RIVER);

        if (dealt)
        {
            uiFlop0.text = unicard(flop0);
            uiFlop1.text = unicard(flop1);
            uiFlop2.text = unicard(flop2);
            uiTurn.text = unicard(turn);
            uiRiver.text = unicard(river);
        }

        uiDebugToggleText.text = $"Enable Debug (Master: {OwnerName(gameObject)})";

        if (Networking.IsMaster)
        {
            goDebug.SetActive(uiDebugToggle.isOn);
            if ((masterUpdateCountdown -= Time.deltaTime) < 0)
            {
                masterUpdateCountdown = updateInterval;
                Transition();
            }
        }

        // local stuff
        if (lastLocalEpoch != epoch)
        {
            lastLocalEpoch = epoch;
            if (!Networking.IsMaster)
            {
                string s = $"new state: now {GetTimeMillis()} epoch {epoch} state {tableState} bettingRound {bettingRound} headsUp {headsUp} " +
                    $"dealer {dealerSeat} acting {actingSeat} \n" +
                    $"bb {bigBlind} minRaise {minimumRaise} currentBet {currentBet}\n" +
                    $"last transition {lastTransitionMillis} elapsed {GetTimeMillis() - lastTransitionMillis}\n" +
                    $"playerState [{string.Join(",", playerState)}]" +
                    $"stacks [{string.Join(",", stacks)}]" +
                    $"potCon {string.Join(",", potContribution)} " +
                    $"roundCon {string.Join(",", roundContribution)})\n" +
                    $"";
                Log(s);
            }
            // edge-triggered stuff
            if (tableState == TABLE_WINNER)
            {
                winnerText = "";
                int[] winnings;
                ulong[] handValues;
                if (wonByFold)
                {
                    winnings = new int[SEATS];
                    int winner = 0;
                    for (int i = 0; i < SEATS; i++)
                    {
                        if (playerState[i] != PLAYER_DEAD) { winner = i; break; }
                    }
                    winnings[winner] = potTotal;
                    handValues = null;
                } else
                {
                    handValues = GetHandValues(
                        playerState, holeCards0, holeCards1, flop0, flop1, flop2, turn, river);
                    int[] playerRanksByHandValues = RankPlayers(handValues);
                    winnings = DividePot(potContribution, playerRanksByHandValues);
                }
                for (int i = 0; i < SEATS; ++i)
                {
                    if (playerState[i] != PLAYER_DEAD && winnings[i] > 0)
                    {
                        winnerText += $"{OwnerName(players[i].gameObject)} wins {winnings[i]}";
                        if (!wonByFold)
                        {
                            winnerText += $" with {HandClass(handValues[i])}";
                        }
                        winnerText += "\n";
                    }
                    Log(winnerText);
                }
            }

            // copy commited stacks to (local) bank in case we become the master later
            for (int i = 0; i < SEATS; i++)
            {
                var player = players[i];
                if (player.IsSeated())
                {
                    bank.SetBalance(Networking.GetOwner(player.gameObject), stacks[i]);
                }
            }
        }
    }

    private string OwnerName(GameObject obj)
    {
        var owner = Networking.GetOwner(obj);
        if (owner == null) return "(Editor)";
        return owner.displayName;
    }

    public void OnPlayerLeft(VRCPlayerApi player)
    {
        // try and pick up change early;
        if (Networking.IsMaster) Transition();

    }

    private 
#if !COMPILER_UDONSHARP
        static
#endif
        string[] suits = new string[] { "♦", "♥", "♣", "♠" };
    private 
#if !COMPILER_UDONSHARP
        static
#endif
        string[] ranks = new string[] { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };

    public string unicard(int card)
    {
        if (card < 26)
        {
            return $"<color=red>{ranks[card % 13]}{suits[card / 13]}</color>";
        } else
        {
            return $"{ranks[card % 13]}{suits[card / 13]}";
        }
    }

    // there are better evaluators in:
    // https://www.codingthewheel.com/archives/poker-hand-evaluator-roundup/
    // but this one is mine, and I don't think I stuff any of the table-based lookups
    // into the udon heap reasonably.
    public
        // udonsharp can't deal with static methods for some reason, but luckily we can
        // hide it in the preprocessor so it still works for unit testing.
#if !COMPILER_UDONSHARP
        static
#endif
        ulong EvaluateHand(int a, int b, int c, int d, int e)
    {
        int swap, r0, r1, r2, r3, r4;
        // flush check
        bool flush = true;
        int suit = a / 13;
        if (b / 13 != suit) flush = false;
        else if (c / 13 != suit) flush = false;
        else if (d / 13 != suit) flush = false;
        else if (e / 13 != suit) flush = false;
        // rank sort 
        // 2 bias to diasambiguate "no pair" from 2, 
        // and get numeral cards to line up (2=2, 10=10, 11=J, 12=Q, 13=K, 14=A)
        r0 = a % 13 + 2; 
        r1 = b % 13 + 2;
        if (r0 > r1) { swap = r1; r1 = r0; r0 = swap; }
        r2 = c % 13 + 2;
        if (r1 > r2)
        {
            swap = r2; r2 = r1; r1 = swap;
            if (r0 > r1) { swap = r1; r1 = r0; r0 = swap; }
        }
        r3 = d % 13 + 2;
        if (r2 > r3)
        {
            swap = r3; r3 = r2; r2 = swap;
            if (r1 > r2)
            {
                swap = r2; r2 = r1; r1 = swap;
                if (r0 > r1) { swap = r1; r1 = r0; r0 = swap; }
            }
        }
        r4 = e % 13 + 2;
        if (r3 > r4)
        {
            swap = r4; r4 = r3; r3 = swap;
            if (r2 > r3)
            {
                swap = r3; r3 = r2; r2 = swap;
                if (r1 > r2)
                {
                    swap = r2; r2 = r1; r1 = swap;
                    if (r0 > r1) { swap = r1; r1 = r0; r0 = swap; }
                }
            }
        }
        // wheel straights are ranked lower than other straights so it's a bit weird.
        bool straight = true, wheelStraight = false;
        if (r1 != r0 + 1) straight = false;
        else if (r2 != r1 + 1) straight = false;
        else if (r3 != r2 + 1) straight = false;
        else if (r4 != r3 + 1)
        {
            straight = false;
            // ace-low "wheel" straight
            if (r4 == 14 && r0 == 2)
            {
                wheelStraight = true;
            } 
        }

        int pair0 = 0, pair1 = 0, triple = 0;
        if (!straight)
        {
            // check for same rank
            if (r1 == r0)
            {
                pair0 = r1;
            }
            if (r2 == r1)
            {
                if (pair0 == r1)
                {
                    triple = r2;
                    // zero the first pair so we can detect the second one
                    // consistently
                    pair0 = 0; 
                } else
                {
                    pair0 = r2;
                }
            }
            if (r3 == r2)
            {
                if (triple == r2)
                {
                    pair1 = r3; // quad
                    pair0 = r3;
                } else if (pair0 == r2)
                {
                    triple = r3;
                    pair0 = 0;
                } else if (pair0 == r1)
                {
                    pair1 = r3;
                } else
                {
                    pair0 = r3;
                }
            }
            if (r4 == r3)
            {
                if (triple == r3)
                {
                    pair0 = r4; // quad
                    pair1 = r4; 
                }
                else if (pair1 == r3) // big full house
                {
                    triple = r4;
                    pair1 = 0;
                }
                else if (pair0 == r3)
                {
                    triple = r4;
                    pair0 = 0;
                } else if (pair0 == r2 || pair0 == r1 || triple == r2) // two pair or small full house
                {
                    pair1 = r4;
                } else
                {
                    pair0 = r4;
                }
            }
        }
        
        // 4 bits cover the 13 card ranks 23456789TJQKA , which are  biased upward
        // by 2 so that e.g. "no pair" is represented by '0', and 2 is e.g. a pair
        // of 2s.
        //
        // [1 bit straight flush]
        // [1 bit wheel-straight flush]
        // [1 bit 4 of a kind] (both pairs are the same for the rank)
        // [1 bit full house]
        // [1 bit flush]
        // [1 bit straight]
        // [1 bit wheel-straight] (ranked lower than other straights)
        // [4 bit 3 of a kind]
        // [4 bit 2nd pair (the higher pair rank if present)]
        // [4 bit, 1st pair]
        // [5 x 4 bit kickers (the hand spelled out in rank order)]
        //
        // spelling out the kickers is slightly redundant with the pair cards, but it's
        // at least correct for evaluation (the pair/triple/4 of a kind conditions are higher bits)

        ulong r = (ulong)(straight && flush ? 1 : 0);
        r = (r << 1) + (ulong)(wheelStraight && flush ? 1 : 0);
        r = (r << 1) + (ulong)(pair0 > 0 && pair0 == pair1 ? 1 : 0);
        r = (r << 1) + (ulong)(triple > 0 && pair0 > 0 ? 1 : 0);
        r = (r << 1) + (ulong)(flush ? 1 : 0);
        r = (r << 1) + (ulong)(straight ? 1 : 0);
        r = (r << 1) + (ulong)(wheelStraight ? 1 : 0);
        r = (r << 4) + (ulong)triple;
        r = (r << 4) + (ulong)pair1;
        r = (r << 4) + (ulong)pair0;

        r = (r << 4) + (ulong)r4;
        r = (r << 4) + (ulong)r3;
        r = (r << 4) + (ulong)r2;
        r = (r << 4) + (ulong)r1;
        r = (r << 4) + (ulong)r0;
        return r;
    }

    public 
#if !COMPILER_UDONSHARP
        static
#endif
        string HandClass(ulong v)
    {
        int highCard = (int)((v >> 16) & 15UL) - 2;

        // hand bits after the pairs/kickers
        int h = (int)(v >> 32);
        if (((h >> 6) & 1) == 1)
        {
            if (((ulong)(v >> 16) & (ulong)15) == (ulong)14)
            {
                return "Royal Flush";
            } else
            {
                return "Straight Flush";
            }
        }
        if (((h >> 5) & 1) == 1)
        {
            return "Straight Flush (Wheel)";
        }
        if (((h >> 4) & 1) == 1)
        {
            int card = (int)((v >> 20) & 15UL) - 2;
            return $"Four of a Kind ({ranks[card]})";
        }
        if (((h >> 3) & 1) == 1)
        {
            int triple = (int)((v >> 28) & 15UL) -2;
            int pair = (int)((v >> 20) & 15UL) -2;
            return $"Full House ({ranks[triple]}/{ranks[pair]})";
        }
        if (((h >> 2) & 1) == 1)
        {
            return "Flush";
        }
        if (((h >> 1) & 1) == 1)
        {
            return "Straight";
        }
        if ((h & 1) == 1)
        {
            return "Straight (Wheel)";
        }
        if (((ulong)(v >> 28) & (ulong)15) > (ulong)0)
        {
            int triple = (int)((v >> 28) & 15UL) - 2;
            return $"Three of a Kind ({ranks[triple]})";
        }
        if (((ulong)(v >> 24) & (ulong)15) > (ulong)0)
        {
            int pair1 = (int)((v >> 24) & 15UL) -2;
            int pair0 = (int)((v >> 20) & 15UL) -2;
            return $"Two Pairs ({ranks[pair1]}/{ranks[pair0]})";
        }
        if (((ulong)(v >> 20) & (ulong)15) > (ulong)0)
        {
            int pair = (int)((v >> 20) & 15UL) -2;
            return $"Pair ({ranks[pair]})";
        }

        return $"High Card ({ranks[highCard]})";
    }

    const int LOGLEN = 24;
    private string[] logLines = new string[LOGLEN];
    private int logIdx = 0;

    private 
#if !COMPILER_UDONSHARP
        static
#endif
        void Log(string log)
    {
        Debug.Log("[Holdem] " + log);
#if COMPILER_UDONSHARP
        logLines[logIdx] = $"{System.DateTime.Now} " + log;
        logIdx = (logIdx + 1) % LOGLEN;
        uiLog.text = "";
        for (int i = 0, j = logIdx; i < LOGLEN; i++, j = (j + 1) % LOGLEN)
        {
            uiLog.text += "\n" + logLines[j];
        }
#endif
    }

    public void GiveMoney()
    {
        if (!Networking.IsMaster) return;
        for (int i = 0; i < SEATS; i++)
        {
            if (players[i].IsSeated())
            {
                var newStack = stacks[i] + initialStackSize;
                bank.SetBalance(Networking.GetOwner(players[i].gameObject), newStack);
            }
        }
        SerializeState();
    }

    public void ResetGame()
    {
        if (!Networking.IsMaster) return;
        Log("RESET GAME BUTTON PRESSED");
        tableState = TABLE_UNINITIALIZED;
        Transition();
    }
}