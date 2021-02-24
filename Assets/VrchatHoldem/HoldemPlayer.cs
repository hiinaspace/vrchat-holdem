using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// Per-player state tracking.
/// </summary>
public class HoldemPlayer : UdonSharpBehaviour
{
    public HoldemGame game;
    public int seatIdx;

    public UnityEngine.UI.Text uiChips, uiBet, uiHole0, uiHole1, uiJoinLeaveText, uiDealer, uiCallCheckText, uiActTimer, uiBestHand;

    public UnityEngine.UI.Button
        uiCallCheck, uiFold, uiConfirm, uiJoinLeaveButton, uiCallButton, uiMinRaiseButton;

    public GameObject goBetUi, goChipDisplay;

    public UnityEngine.UI.Image uiStatusColor;

    public GameObject goHoleCards, goReadyToggle;

    void Start()
    {
    }

    private string OwnerName()
    {
        var owner = Networking.GetOwner(gameObject);
        if (owner == null) return "(Editor)";
        return owner.displayName;
    }
    private int OwnerId()
    {
        var owner = Networking.GetOwner(gameObject);
        if (owner == null) return -2;
        return owner.playerId;
    }

    public UnityEngine.UI.Toggle uiReady;
    public bool IsSeated()
    {
        return activePlayerId == OwnerId();
    }

    // Hack to disambiguate this object being explicitly active vs taken over by the room master
    // after player disconnect. If activePlayerId = Networking.GetOwner(gameObject).playerId, then
    // we're explicitly owned. To explicitly disown, set to a negative value.
    [UdonSynced] int activePlayerId = -1;

    bool isLocallySeated()
    {
        return IsSeated() && Networking.IsOwner(gameObject);
    }

    public void ToggleControl()
    {
        if (isLocallySeated()) { 
            ReleaseControl(); 
        } else
        {
            TakeControl();
        }
    }

    public void TakeControl()
    {
        if (IsSeated()) return; // can't take over

        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        // not sure if this can take effect immediately, but let's try
        activePlayerId = Networking.LocalPlayer == null ? -2 : Networking.LocalPlayer.playerId;
    }

    public void ReleaseControl()
    {
        if (Networking.IsMaster || isLocallySeated())
        {
            // if master is taking over
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            // no valid player will have a negative id (I think)
            activePlayerId = -1;
        }
    }

    // committed. internal logic should ensure that the bet is actually valid for
    // the game state.
    [UdonSynced] public int committedEpoch = -1;

    const int FOLD = -1;

    // pending/committed bet, -1 for FOLD , 0 for CHECK
    // -1 for FOLD during IDLE counts as sitting out,
    // as does not having enough chips for the big blind.
    [UdonSynced] public int bet = FOLD;

    [UdonSynced] public bool playerReady = false;
    private int updateSeenEpoch = -1;

    public const int
        TABLE_UNINITIALIZED = 0,
        TABLE_IDLE = 1,
        TABLE_READY = 2,
        TABLE_PLAYING = 3,
        TABLE_WINNER = 5
        ;

     public const int
        PLAYER_DEAD = 0,
        PLAYER_PENDING = 1,
        PLAYER_ACTING = 2,
        PLAYER_COMMITED = 3;

    public const int
        NOT_PLAYING = 0, PREFLOP = 1, FLOP = 2, TURN = 3, RIVER = 4, SHOWDOWN = 5;

    private void Update()
    {
        // XXX apparently udon will run some Update()s before Starts() so
        // HoldemGame's start might not initialize this
        if (game == null) return;
        // don't bother if game isn't initialized
        if (game.tableState == TABLE_UNINITIALIZED) return;

        var locallySeated = isLocallySeated();
        var seated = IsSeated();

        uiReady.interactable = locallySeated;
        goReadyToggle.SetActive(locallySeated || !seated);

        if (locallySeated)
        {
            // one ui read
            playerReady = uiReady.isOn;

            uiJoinLeaveButton.interactable = !playerReady;
            if (playerReady)
            {
                uiJoinLeaveText.text = "Ready";
            }
            else
            {
                uiJoinLeaveText.text = "Leave";
            }
        } else
        {
            if (seated)
            {
                uiJoinLeaveButton.interactable = false;
                uiJoinLeaveText.text = $"{OwnerName()}";
            } else
            {
                uiJoinLeaveButton.interactable = true;
                uiJoinLeaveText.text = "Join";
            }
        }

        var playerState = game.playerState[seatIdx];
        var inPlay = playerState != PLAYER_DEAD;

        uiDealer.enabled = game.tableState == TABLE_PLAYING && game.dealerSeat == seatIdx;

        var winner = game.tableState == TABLE_WINNER;
        bool showdown = false;
        if (winner)
        {
            int challengers = 0;
            for (int i = 0; i < 10; ++i)
            {
                if (game.playerState[i] == PLAYER_COMMITED) challengers++;
            }
            if (challengers > 1) showdown = true;
        }

        // display at least blank cards if in play
        goHoleCards.SetActive(inPlay);

        var holes = inPlay && (locallySeated || game.headsUp || showdown);
        uiHole0.enabled = holes;
        uiHole1.enabled = holes;
        uiBestHand.enabled = holes && game.bettingRound >= FLOP;

        var acting = playerState == PLAYER_ACTING;
        var valid = game.IsValidBet(bet, seatIdx);

        uiStatusColor.enabled = seated;
        uiStatusColor.color =
            playerState == PLAYER_DEAD ? Color.black :
            playerState == PLAYER_PENDING ? Color.white :
            playerState == PLAYER_ACTING ? Color.green :
            playerState == PLAYER_COMMITED ? Color.blue : Color.red;

        goBetUi.SetActive(acting);

        uiBet.text = GetBet();

        goChipDisplay.SetActive(seated);
        var stack = game.stacks[seatIdx];
        if (acting && bet > 0)
        {
            var left = stack - bet;
            uiChips.text = $"{left} (total {stack})";
        }
        else
        {
            uiChips.text = $"{stack}";
        }

        uiCallCheck.interactable = locallySeated && acting && valid;
        uiFold.interactable = locallySeated && acting;

        uiConfirm.interactable = uiPending && (valid || bet == -1);
        var uiConfirmColors = uiConfirm.colors;
        uiConfirmColors.normalColor = committedEpoch == game.epoch ? Color.yellow : Color.white;
        uiConfirmColors.disabledColor = playerState == PLAYER_COMMITED ? Color.blue : Color.grey;
        uiConfirm.colors = uiConfirmColors;

        var uiFoldColors = uiFold.colors;
        uiFoldColors.normalColor = (bet == -1 && uiPending) ? Color.red : Color.white;
        uiFold.colors = uiFoldColors;

        var uiCallCheckColors = uiCallCheck.colors;
        uiCallCheckColors.normalColor = bet >= 0 && uiPending ? Color.green : Color.white;
        uiCallCheck.colors = uiCallCheckColors;

        uiCallCheckText.text =
            bet == 0 ? "Check" :
            bet == game.stacks[seatIdx] ? "All-in" :
            bet == -1 ? "(fold)" :
            !valid ? "(invalid bet)" :
            bet > game.currentBet ? 
              (game.currentBet > 0 ?  "Re-Raise" : "Raise") :
              game.currentBet > 0 ? "Call" : "Check";

        uiActTimer.enabled = acting;
        if (acting)
        {
            var now = Networking.LocalPlayer == null ? (int)(Time.time * 1000) : Networking.GetServerTimeInMilliseconds();
            var timeout = game.lastTransitionMillis;
            var remaining = game.actionTimeoutSecs - (now - timeout) / 1000;
            uiActTimer.text = $"{remaining / 60}:{remaining % 60} to act";
        }

        // transition check
        if (updateSeenEpoch != game.epoch)
        {
            updateSeenEpoch = game.epoch;
            // edge-triggered
            if (locallySeated)
            {
                if (acting)
                {
                    // start off with a call
                    bet = Mathf.Min(stack, game.currentBet - game.roundContribution[seatIdx]);
                    // require the confirm again
                    uiPending = false;
                } else
                {
                    // once it comes around to us again, make sure we aren't
                    // accidentally already commited from the past
                    bet = 0;
                    committedEpoch = -1;
                }
            }
            if (game.tableState == TABLE_PLAYING) {
                uiHole0.text = game.unicard(game.holeCards0[seatIdx]);
                uiHole1.text = game.unicard(game.holeCards1[seatIdx]);
                var bestHand =
                    game.bettingRound == RIVER ? game.BestPlayerHandSeat(seatIdx) :
                    game.bettingRound == TURN ? game.BestPlayerHandTurnSeat(seatIdx) :
                    game.bettingRound == FLOP ? game.BestPlayerHandFlop(seatIdx) : 0;
                if (bestHand > 0UL)
                {
#if !COMPILER_UDONSHARP
                    uiBestHand.text = $"{HoldemGame.HandClass(bestHand)}";
#else
                    uiBestHand.text = $"{game.HandClass(bestHand)}";
#endif
                } else
                {
                    uiBestHand.text = "";
                }
            } 
        }
    }

    private string GetBet()
    {
        var totalBet = game.roundContribution[seatIdx] + bet;
        if (bet > 0)
        {
            if (totalBet == game.stacks[seatIdx])
            {
                return $"{bet} (All-in)";
            }
            if (game.currentBet == totalBet)
            {
                return $"{bet} (Call)";
            } else if (totalBet > game.currentBet)
            {
                if (bet < game.minimumRaise)
                {
                    return $"{bet} (+{game.minimumRaise - bet} to min-raise)";
                } else
                {
                    return $"{bet} (Raise)";
                }
            } else
            {
                return $"{bet} (+{game.currentBet - totalBet} to call)";

            }
        } else if (bet == 0)
        {
            return "(Check)";
        }
        return "(Fold)";
    }

    private bool uiPending = false;
    public void UiConfirm()
    {
        if (!isLocallySeated()) return;

        if (uiPending)
        {
            committedEpoch = game.epoch;
        }
    }

    public void UiFold()
    {
        if (!isLocallySeated()) return;
        if (!uiPending)
        {
            bet = -1;
        }
        uiPending = !uiPending;
    }
    public void UiCallCheck()
    {
        if (!isLocallySeated()) return;
        uiPending = !uiPending;
    }

    public void UiBetClear() { AdjustBet(-bet); }
    public void UiBetD1000() { AdjustBet(-1000); }
    public void UiBetD100() { AdjustBet(-100); }
    public void UiBetD10() { AdjustBet(-10); }
    public void UiBetP10() { AdjustBet(10); }
    public void UiBetP100() { AdjustBet(100); }
    public void UiBetP1000() { AdjustBet(1000); }
    public void UiBetAllIn() { AdjustBet(int.MaxValue); }
    public void UiBetMinRaise()
    {
        if (!isLocallySeated()) return;
        bet = Mathf.Min(game.minimumRaise, game.stacks[seatIdx]);
    }

    public void UiBetCall()
    {
        if (!isLocallySeated()) return;
        bet = Mathf.Min(game.currentBet - game.roundContribution[seatIdx], game.stacks[seatIdx]);
    }

    private void AdjustBet(int delta)
    {
        if (!isLocallySeated()) return;
        bet = Mathf.Min(Mathf.Max(0, bet + delta), game.stacks[seatIdx]);
    }

    override public void OnDeserialization()
    {
        if (Networking.IsMaster) game.Transition();
    }
}