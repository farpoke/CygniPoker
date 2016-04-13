using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cygni.PokerClient.Communication;
using Cygni.PokerClient.Communication.Requests;
using Cygni.PokerClient.Game;
using Cygni.PokerClient.Communication.Events;

namespace Cygni.PokerClient.Bots
{
    abstract class AbstractBot
    {

        public string Name {
            get { return "Phoenix-" + GetType().Name; }
        }

        public abstract Game.Action Act(ActionRequest request, GameState state);

        public void UpdateFrom(TexasMessage msg, GameState state) {
            if (msg is PlayerBetBigBlindEvent)
                OnPlayerBetBigBlind(msg as PlayerBetBigBlindEvent, state);
            if (msg is PlayerBetSmallBlindEvent)
                OnPlayerBetSmallBlind(msg as PlayerBetSmallBlindEvent, state);
            if (msg is PlayerCalledEvent)
                OnPlayerCalled(msg as PlayerCalledEvent, state);
            if (msg is PlayerCheckedEvent)
                OnPlayerChecked(msg as PlayerCheckedEvent, state);
            if (msg is PlayerFoldedEvent)
                OnPlayerFolded(msg as PlayerFoldedEvent, state);
            if (msg is PlayerQuitEvent)
                OnPlayerQuit(msg as PlayerQuitEvent, state);
            if (msg is PlayerRaisedEvent)
                OnPlayerRaisedEvent(msg as PlayerRaisedEvent, state);
            if (msg is PlayerWentAllInEvent)
                OnPlayerWentAllIn(msg as PlayerWentAllInEvent, state);
            if (msg is ServerIsShuttingDownEvent)
                OnServerIsShuttingDown(msg as ServerIsShuttingDownEvent, state);
            if (msg is ShowDownEvent)
                OnShowDown(msg as ShowDownEvent, state);
            if (msg is TableChangedStateEvent)
                OnTableStateChanged(msg as TableChangedStateEvent, state);
            if (msg is TableIsDoneEvent)
                OnTableIsDone(msg as TableIsDoneEvent, state);
            if (msg is PlayIsStartedEvent)
                OnPlayIsStarted(msg as PlayIsStartedEvent, state);
            if (msg is CommunityHasBeenDealtACardEvent)
                OnCommunityHasBeedDealtACard(msg as CommunityHasBeenDealtACardEvent, state);
            if (msg is YouHaveBeenDealtACardEvent)
                OnYouHaveBeenDealtACard(msg as YouHaveBeenDealtACardEvent, state);
            if (msg is YouWonAmountEvent)
                OnYouWonAmountEvent(msg as YouWonAmountEvent, state);
        }

        protected virtual void OnPlayerBetBigBlind(PlayerBetBigBlindEvent e, GameState state) { }
        protected virtual void OnPlayerBetSmallBlind(PlayerBetSmallBlindEvent e, GameState state) { }
        protected virtual void OnPlayerCalled(PlayerCalledEvent e, GameState state) { }
        protected virtual void OnPlayerChecked(PlayerCheckedEvent e, GameState state) { }
        protected virtual void OnPlayerFolded(PlayerFoldedEvent e, GameState state) { }
        protected virtual void OnPlayerQuit(PlayerQuitEvent e, GameState state) { }
        protected virtual void OnPlayerRaisedEvent(PlayerRaisedEvent e, GameState state) { }
        protected virtual void OnPlayerWentAllIn(PlayerWentAllInEvent e, GameState state) { }
        protected virtual void OnServerIsShuttingDown(ServerIsShuttingDownEvent e, GameState state) { }
        protected virtual void OnShowDown(ShowDownEvent e, GameState state) { }
        protected virtual void OnTableStateChanged(TableChangedStateEvent e, GameState state) { }
        protected virtual void OnTableIsDone(TableIsDoneEvent e, GameState state) { }
        protected virtual void OnPlayIsStarted(PlayIsStartedEvent e, GameState state) { }
        protected virtual void OnCommunityHasBeedDealtACard(CommunityHasBeenDealtACardEvent e, GameState state) { }
        protected virtual void OnYouHaveBeenDealtACard(YouHaveBeenDealtACardEvent e, GameState state) { }
        protected virtual void OnYouWonAmountEvent(YouWonAmountEvent e, GameState state) { }
    }
}
