using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Cygni.PokerClient.Communication.Events;
using Cygni.PokerClient.Communication.Requests;
using Cygni.PokerClient.Game;
using Action = Cygni.PokerClient.Game.Action;

namespace Cygni.PokerClient.Bots
{
	class HeuristicBot : AbstractBot
	{
		const int ESTIMATE_SAMPLE_COUNT = 500;

		const int STARTING_CHIPS = 10000;

		const float RAISE_THRESHOLD = 0.8f;
		const float CALL_THRESHOLD = 0.5f;
		const float STAY_THRESHOLD = 0.2f;
		const float LAST_RESORT_THRESHOLD = 0.5f;

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

		private bool allIn;

		public override Action Act(ActionRequest request, GameState state) {
			UpdateEstimate(state);

			string can_do = string.Join(", ", request.PossibleActions.Select(x => x.ActionType.ToString()));
			logger.Debug("{0}, spent ${3}, Win Estimate: {1} (can {2})", 
				state.CurrentPlayState, winEstimate, can_do.Trim(), spentThisPlay);

			Action choice = Decide(request, state);
			spentThisPlay += choice.Amount;
			return choice;
		}

		Action Decide(ActionRequest request, GameState state) {
			long spend_limit = (long)((chips - spentThisPlay) * winEstimate);
			long action_limit = spend_limit - spentThisPlay;
			bool may_raise = (request.Raise != null) && (request.Raise.Amount < action_limit);
			bool may_call = (request.Call != null) && (request.Call.Amount < action_limit);

			if ((winEstimate > LAST_RESORT_THRESHOLD) && (request.AllIn.Amount < state.BigBlind))
				return request.AllIn; // Almost out of money, go all in on >50% chance.
			else if ((winEstimate > RAISE_THRESHOLD) && may_raise)
				return request.Raise; // Good chance to win, try to raise.
			else if ((winEstimate > CALL_THRESHOLD) && may_call)
				return request.Call; // Ok chance to win call if possible.
			else if ((winEstimate > STAY_THRESHOLD) && may_call && (state.CurrentPlayState > PlayState.TURN))
				return request.Call; // Almost over, keep calling if we got this far.
			else if (request.Check != null)
				return request.Check; // Always check if able.
			else if ((winEstimate > CALL_THRESHOLD) && (state.CurrentPlayState > PlayState.FLOP) && (request.AllIn != null) && !allIn)
				return request.AllIn; // Wan't to call, but have to all-in. But only if no one else went all-in first.
			else if ((winEstimate > STAY_THRESHOLD) && (state.CurrentPlayState > PlayState.TURN) && (request.AllIn != null) && !allIn)
				return request.AllIn; // Wan't to stay and call, but have to all-in. But only if no one else went all-in first.
			else
				return request.Fold; // No confidence, fold.
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
			allIn = false;
		}

		void UpdateEstimate(GameState state) {
			if (!estimateOutOfDate)
				return;
			if (state.CurrentPlayState == PlayState.PRE_FLOP)
				winEstimate = EstimatePreFlop();
			else
				winEstimate = EstimatePostFlop();
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
			return win_count / (float)ESTIMATE_SAMPLE_COUNT;
		}

		float EstimatePostFlop() {
			int win_count = 0;
			PokerHand my_hand_type = evaluator.Evaluate(myCards.Concat(communityCards).ToArray());
			for (int sample_index = 0; sample_index < ESTIMATE_SAMPLE_COUNT; sample_index++) {
				var other_hand = RandomDraw();
				PokerHand other_hand_type = evaluator.Evaluate(other_hand.Concat(communityCards).ToArray());
				if (my_hand_type >= other_hand_type)
					win_count++;
			}
			return win_count / (float)ESTIMATE_SAMPLE_COUNT;
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

		protected override void OnPlayerWentAllIn(PlayerWentAllInEvent e, GameState state)
		{
			allIn = true;
		}

		protected override void OnYouWonAmountEvent(YouWonAmountEvent e, GameState state)
		{
			chips = e.YourChipAmount;
		}

	}
}

