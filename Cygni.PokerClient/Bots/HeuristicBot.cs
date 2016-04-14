using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Cygni.PokerClient.Communication.Events;
using Cygni.PokerClient.Communication.Requests;
using Cygni.PokerClient.Game;
using Action = Cygni.PokerClient.Game.Action;

using System.Diagnostics;

namespace Cygni.PokerClient.Bots
{
	class HeuristicBot : AbstractBot
	{
		const int ESTIMATE_SAMPLE_COUNT = 500;
		const int STARTING_CHIPS = 10000;

		private static Logger logger = LogManager.GetCurrentClassLogger();

		private HandEvaluator evaluator = new HandEvaluator();
		private Random random = new Random();

		private List<Card> myCards = new List<Card>();
		private List<Card> communityCards = new List<Card>();
		private List<Card> availableCards = new List<Card>();

		private float winEstimate;
		private bool estimateOutOfDate = true;

		private long chips;
		private long spentThisPlay;

		public override string Name {
			get {
				return "Hyperion";
			}
		}

		public float WinEstimate { get { return winEstimate; } }

		public override Action Act(ActionRequest request, GameState state) {
			UpdateEstimate(state);

			string can_do = string.Join(", ", request.PossibleActions.Select(x => x.ActionType.ToString()));
			logger.Debug("{0}, spent ${3}, Win Estimate: {1} (can {2})", 
				state.CurrentPlayState, winEstimate, can_do.Trim(), spentThisPlay);

			Action choice = Decide(request, state);
			// Always check if able.
			if (choice == null || (choice.ActionType == ActionType.FOLD && request.Check != null))
				choice = request.Check;
			if (choice == null)
				choice = request.Fold;
			
			spentThisPlay += choice.Amount;
			return choice;
		}

		Action Decide(ActionRequest request, GameState state) {
			if (state.CurrentPlayState == PlayState.PRE_FLOP)
				return Decide_PreFlop(request, state);
			else if (state.CurrentPlayState == PlayState.FLOP)
				return Decide_PostFlop(request, state);
			else if (state.CurrentPlayState == PlayState.TURN)
				return Decide_PostTurn(request, state);
			else
				return Decide_PostRiver(request, state);
		}

		double SpentFraction { get { return spentThisPlay / (double)chips; } }

		const double PF_LAST_RESORT_THRESHOLD = 0.3;

		double PF_Optimistic { get { return winEstimate - 0.8; } }
		double PF_Optimistic_Raise_Fraction { get { return 0.25 + PF_Optimistic * 1.0 + SpentFraction * 0.1; } }
		double PF_Optimistic_Call_Fraction { get { return 0.4 + PF_Optimistic * 1.0 + SpentFraction * 0.1; } }

		double PF_Cautious { get { return winEstimate - 0.5 + SpentFraction * 0.1; } }
		double PF_Cautious_Call_Fraction { get { return 0.25 + PF_Cautious * 0.25; } }

		double PF_Optimistic_Raise_Limit { get { return chips * PF_Optimistic_Raise_Fraction; } }
		double PF_Optimistic_Call_Limit { get { return chips * PF_Optimistic_Call_Fraction; } }
		double PF_Cautious_Call_Limit { get { return chips * PF_Cautious_Call_Fraction; } }

		Action Decide_PreFlop(ActionRequest request, GameState state) {
			var may_raise = request.Raise != null;
			var may_call = request.Call != null;

			// Go all in as a last resort when really low on money.
			if (chips <= state.BigBlind && winEstimate > PF_LAST_RESORT_THRESHOLD)
				return request.AllIn;
			else if ((chips - spentThisPlay) <= state.SmallBlind)
				return request.AllIn;

			// Be very cautious about calling for unusually large amounts.
			if (may_call && spentThisPlay > 0 && request.Call.Amount > spentThisPlay * 3) {
				if (PF_Optimistic > 0)
					return request.Call;
				else
					return request.Check;
			}

			// Raise/call up to a portion of our chips if estimated really good chances.
			if (PF_Optimistic > 0 && may_raise && request.Raise.Amount < PF_Optimistic_Raise_Limit)
				return request.Raise;
			else if (PF_Optimistic > 0 && may_call && request.Call.Amount < PF_Optimistic_Call_Limit)
				return request.Call;

			// Call up to a portion of out chips if estimated ok chances.
			if (PF_Cautious > 0 && may_call && request.Call.Amount < (chips * PF_Cautious_Call_Limit - spentThisPlay))
				return request.Call;

			// Always try to check if able.
			return request.Check;
		}

		Action Decide_PostFlop(ActionRequest request, GameState state) {
			return Decide_PreFlop(request, state);
		}

		Action Decide_PostTurn(ActionRequest request, GameState state) {
			return Decide_PreFlop(request, state);
		}

		Action Decide_PostRiver(ActionRequest request, GameState state) {
			return Decide_PreFlop(request, state);
		}

		void Reset() {
			myCards.Clear();
			communityCards.Clear();
			availableCards.Clear();
			for (Rank r = Rank.DEUCE; r <= Rank.ACE; r++) {
				availableCards.Add(new Card(r, Suit.CLUBS));
				availableCards.Add(new Card(r, Suit.DIAMONDS));
				availableCards.Add(new Card(r, Suit.HEARTS));
				availableCards.Add(new Card(r, Suit.SPADES));
			}
			estimateOutOfDate = true;
			chips = STARTING_CHIPS;
			spentThisPlay = 0;
		}

		void UpdateEstimate(GameState state) {
			if (!estimateOutOfDate)
				return;
			float estimate;
			if (state.CurrentPlayState == PlayState.PRE_FLOP)
				estimate = EstimatePreFlop();
			else
				estimate = EstimatePostFlop();
			int other_players = state.PlayersInCurrentPlay.Count - 1;
			winEstimate = (float)Math.Pow(estimate, other_players);
			if (winEstimate > 1)
				Debugger.Break();
			estimateOutOfDate = false;
		}

		List<Card> RandomDraw() {
			int first = random.Next(availableCards.Count);
			int second;
			do {
				second = random.Next(availableCards.Count);
			} while (first == second);
			List<Card> draw = new List<Card>();
			draw.Add(availableCards[first]);
			draw.Add(availableCards[second]);
			return draw;
		}

		float EstimatePreFlop() {
			int win_count = 0;
			bool has_pair = myCards[0].Rank == myCards[1].Rank;
			for (int sample_index = 0; sample_index < ESTIMATE_SAMPLE_COUNT; sample_index++) {
				var other_hand = RandomDraw();
				// Check for pairs...
				bool other_has_pair = other_hand[0].Rank == other_hand[1].Rank;
				if (has_pair && !other_has_pair) {
					win_count++;
					continue;
				} else if (other_has_pair && !has_pair) {
					continue;
				} else if (other_has_pair && has_pair) {
					if (myCards[0].Rank > other_hand[0].Rank)
						win_count++;
					continue;
				}
				// No pairs, check high ranks...
				Rank max_rank = myCards[0].Rank > myCards[1].Rank ? myCards[0].Rank : myCards[1].Rank;
				Rank other_max_rank = other_hand[0].Rank > other_hand[1].Rank ? other_hand[0].Rank : other_hand[1].Rank;
				if (max_rank > other_max_rank) {
					win_count++;
					continue;
				} else if (max_rank < other_max_rank) {
					continue;
				}
				// Tie, check low ranks...
				Rank min_rank = myCards[0].Rank < myCards[1].Rank ? myCards[0].Rank : myCards[1].Rank;
				Rank other_min_rank = other_hand[0].Rank < other_hand[1].Rank ? other_hand[0].Rank : other_hand[1].Rank;
				if (min_rank > other_min_rank)
					win_count++;
			}
			if (win_count > ESTIMATE_SAMPLE_COUNT)
				Debugger.Break();
			return win_count / (float)ESTIMATE_SAMPLE_COUNT;
		}

		float EstimatePostFlop() {
			int win_count = 0;
			PokerHand my_hand_type = evaluator.Evaluate(myCards.Concat(communityCards).ToArray());
			for (int sample_index = 0; sample_index < ESTIMATE_SAMPLE_COUNT; sample_index++) {
				var other_hand = RandomDraw();
				PokerHand other_hand_type = evaluator.Evaluate(other_hand.Concat(communityCards).ToArray());
				if (my_hand_type > other_hand_type)
					win_count++;
				else if (my_hand_type == other_hand_type && TieBreak(other_hand))
					win_count++;
			}
			if (win_count > ESTIMATE_SAMPLE_COUNT)
				Debugger.Break();
			return win_count / (float)ESTIMATE_SAMPLE_COUNT;
		}

		bool TieBreak(List<Card> other_hand) {
			var my_sorted = myCards.Concat(communityCards).OrderByDescending(c => c.Rank).ToArray();
			var other_sorted = other_hand.Concat(communityCards).OrderByDescending(c => c.Rank).ToArray();
			for (int i = 0; i < my_sorted.Length; i++) {
				if (my_sorted[i].Rank > other_sorted[i].Rank)
					return true;
				else if (my_sorted[i].Rank < other_sorted[i].Rank)
					return false;
			}
			return false;
		}

		protected override void OnPlayIsStarted(PlayIsStartedEvent e, GameState state) {
			Reset();
		}

		protected override void OnYouHaveBeenDealtACard(YouHaveBeenDealtACardEvent e, GameState state)
		{
			myCards.Add(e.Card);
			availableCards.Remove(e.Card);
			estimateOutOfDate = true;
		}

		protected override void OnCommunityHasBeedDealtACard(CommunityHasBeenDealtACardEvent e, GameState state)
		{
			communityCards.Add(e.Card);
			availableCards.Remove(e.Card);
			estimateOutOfDate = true;
		}

		protected override void OnYouWonAmountEvent(YouWonAmountEvent e, GameState state)
		{
			if (e.YourChipAmount - chips < 5000)
				logger.Info("Big loss @ table {0}, play {1}", state.TableId, state.PlayIndex);
			chips = e.YourChipAmount;
		}

		protected override void OnShowDown(ShowDownEvent e, GameState state)
		{
		}

		protected override void OnPlayerBetBigBlind(PlayerBetBigBlindEvent e, GameState state)
		{
			if (e.Player.Name == Name)
				spentThisPlay += e.BigBlind;
		}

		protected override void OnPlayerBetSmallBlind(PlayerBetSmallBlindEvent e, GameState state)
		{
			if (e.Player.Name == Name)
				spentThisPlay += e.SmallBlind;
		}
	}
}

