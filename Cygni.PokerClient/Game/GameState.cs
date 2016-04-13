using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using Cygni.PokerClient.Communication;
using Cygni.PokerClient.Communication.Events;
using NLog;

namespace Cygni.PokerClient.Game
{


    public class GameState
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public long TableId { get; private set; }

        public PlayState CurrentPlayState { get; private set; }
        public double BigBlind { get; private set; }
        public double SmallBlind { get; private set; }
        public List<Card> OwnCards { get; private set; }
        public List<Card> CommunityCards { get; private set; }

        public List<GamePlayer> PlayersAtTable { get; private set; }

        public List<GamePlayer> PlayersInCurrentPlay { get; private set; }

        public double Pot { get; set; }

		public int PlayIndex { get; private set; }

        public GameState()
        {
            OwnCards = new List<Card>();
            CommunityCards = new List<Card>();
            PlayersAtTable = new List<GamePlayer>();
            PlayersInCurrentPlay = new List<GamePlayer>();
        }

        public void UpdateFrom(TexasMessage msg)
        {
            if (msg is PlayerBetBigBlindEvent)
                OnPlayerBetBigBlind(msg as PlayerBetBigBlindEvent);
            if (msg is PlayerBetSmallBlindEvent)
                OnPlayerBetSmallBlind(msg as PlayerBetSmallBlindEvent);
            if (msg is PlayerCalledEvent)
                OnPlayerCalled(msg as PlayerCalledEvent);
            if (msg is PlayerCheckedEvent)
                OnPlayerChecked(msg as PlayerCheckedEvent);
            if (msg is PlayerFoldedEvent)
                OnPlayerFolded(msg as PlayerFoldedEvent);
            if (msg is PlayerQuitEvent)
                OnPlayerQuit(msg as PlayerQuitEvent);
			if (msg is PlayerForcedFoldedEvent)
				OnPlayerForcedFolded(msg as PlayerForcedFoldedEvent);
            if (msg is PlayerRaisedEvent)
                OnPlayerRaisedEvent(msg as PlayerRaisedEvent);
            if (msg is PlayerWentAllInEvent)
                OnPlayerWentAllIn(msg as PlayerWentAllInEvent);
            if (msg is ServerIsShuttingDownEvent)
                OnServerIsShuttingDown(msg as ServerIsShuttingDownEvent);
            if (msg is ShowDownEvent)
                OnShowDown(msg as ShowDownEvent);
            if (msg is TableChangedStateEvent)
                OnTableStateChanged(msg as TableChangedStateEvent);
            if (msg is TableIsDoneEvent)
                OnTableIsDone(msg as TableIsDoneEvent);
            if (msg is PlayIsStartedEvent)
                OnPlayIsStarted(msg as PlayIsStartedEvent);
            if (msg is CommunityHasBeenDealtACardEvent)
                OnCommunityHasBeedDealtACard(msg as CommunityHasBeenDealtACardEvent);
            if (msg is YouHaveBeenDealtACardEvent)
                OnYouHaveBeenDealtACard(msg as YouHaveBeenDealtACardEvent);
            if (msg is YouWonAmountEvent)
                OnYouWonAmountEvent(msg as YouWonAmountEvent);
            if (msg is UnknownMessage)
                logger.Error("Unknow message received from server: " + ((msg as UnknownMessage).StringData));
        }

        private void OnYouWonAmountEvent(YouWonAmountEvent e)
        {
            logger.Info("You won ${0} and now have ${1}", e.WonAmount, e.YourChipAmount);
        }

        private void OnYouHaveBeenDealtACard(YouHaveBeenDealtACardEvent e)
        {
            logger.Debug("You have been dealt a card {0}", e.Card);
            OwnCards.Add(e.Card);
        }

        private void OnTableIsDone(TableIsDoneEvent e)
        {
            logger.Info("Table is done");
            Reset();
        }

        private void Reset()
        {
            PlayersAtTable.Clear();
            PlayersInCurrentPlay.Clear();
            OwnCards.Clear();
            CommunityCards.Clear();
        }

        private void OnTableStateChanged(TableChangedStateEvent e)
        {
            CurrentPlayState = e.State;
        }

        private void OnShowDown(ShowDownEvent e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Showdown between: " + string.Join(", ", e.PlayersShowDown.Select(p => p.Player)));
			//sb.AppendLine("Showdown between: " + string.Join(", ", PlayersInCurrentPlay));
            foreach (var p in e.PlayersShowDown)
            {
                sb.AppendLine(string.Format("{0} shows {1} wins ${2}", p.Player, p.Hand.PokerHand, p.WonAmount));
            }
            logger.Info(sb.ToString());
        }

        private void OnServerIsShuttingDown(ServerIsShuttingDownEvent e)
        {
            logger.Warn("Server is shutting down");
            throw new ServerShutdownException();
        }

        private void OnPlayerWentAllIn(PlayerWentAllInEvent e)
        {
            logger.Debug("{0} went all-in for ${1}", e.Player, e.AllInAmount);
            Pot += e.AllInAmount;
        }

        private void OnPlayerRaisedEvent(PlayerRaisedEvent e)
        {
            logger.Debug("{0} raised ${1}", e.Player, e.RaiseBet);
            Pot += e.RaiseBet;
        }

        private void OnPlayerQuit(PlayerQuitEvent e)
        {
            logger.Info("{0} quit", e.Player);
            PlayersAtTable.Remove(e.Player);
            PlayersInCurrentPlay.Remove(e.Player);
        }

        private void OnPlayerForcedFolded(PlayerForcedFoldedEvent e)
        {
            logger.Debug("{0} was forced to fold", e.Player);
            PlayersInCurrentPlay.Remove(e.Player);
        }

        private void OnPlayerFolded(PlayerFoldedEvent e)
        {
            logger.Debug("{0} folded", e.Player);
            PlayersInCurrentPlay.Remove(e.Player);
        }

        private void OnPlayerChecked(PlayerCheckedEvent e)
        {
            logger.Debug("{0} checked", e.Player);
            PlayersInCurrentPlay.Remove(e.Player);
        }

        private void OnPlayerCalled(PlayerCalledEvent e)
        {
            logger.Debug("{0} called for ${1}", e.Player, e.CallBet);
            Pot += e.CallBet;
        }

        private void OnCommunityHasBeedDealtACard(CommunityHasBeenDealtACardEvent e)
        {
            logger.Debug("Community card dealt: {0}", e.Card);
            CommunityCards.Add(e.Card);
        }

        private void OnPlayIsStarted(PlayIsStartedEvent e)
        {
            Reset();
			if (TableId == e.TableId)
				PlayIndex++;
			else
				PlayIndex = 0;
            TableId = e.TableId;
            PlayersAtTable.AddRange(e.Players);
            PlayersInCurrentPlay.AddRange(e.Players);
            BigBlind = e.BigBlindAmount;
			SmallBlind = e.SmallBlindAmount;
			logger.Info("Play started, table {0}, play {1}", TableId, PlayIndex);
        }

        private void OnPlayerBetSmallBlind(PlayerBetSmallBlindEvent e)
        {
            logger.Debug("{0} bet small blind ${1}", e.Player, e.SmallBlind);
            Pot += e.SmallBlind;
        }

        private void OnPlayerBetBigBlind(PlayerBetBigBlindEvent e)
        {
            logger.Debug("{0} bet big blind ${1}", e.Player, e.BigBlind);
            Pot += e.BigBlind;
        }
    }
}
