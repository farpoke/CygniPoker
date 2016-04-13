using System;
using System.Linq;
using Cygni.PokerClient.Communication.Requests;
using Cygni.PokerClient.Game;
using NLog;
using Action = Cygni.PokerClient.Game.Action;

namespace Cygni.PokerClient.Bots
{
    class SimpleBot : AbstractBot
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private HandEvaluator evaluator = new HandEvaluator();

        public override Action Act(ActionRequest request, GameState state) {
            if (state.CurrentPlayState == PlayState.PRE_FLOP) {
                //Check if possible
                if (request.Check != null)
                    return request.Check;

                //if we a suited card, call
                if (state.OwnCards.Any(c => c.Rank > Rank.TEN))
                    return request.Call;

                //The other players want play, but our hand is not looking that promising... fold
                return request.Fold;
            }

            var hand = evaluator.Evaluate(state.OwnCards.Concat(state.CommunityCards).ToArray());
            logger.Debug("I hold a {0}", hand);
            // Let's go ALL IN if hand is better than or equal to THREE_OF_A_KIND
            if (request.AllIn != null && hand > PokerHand.TWO_PAIRS) {
                return request.AllIn;
            }

            // Otherwise, be more careful CHECK if possible.
            if (request.Check != null) {
                return request.Check;
            }

            // We have either CALL or RAISE left

            // Do I have something better than a pair and can RAISE?
            if (request.Raise != null && hand > PokerHand.ONE_PAIR) {
                return request.Raise;
            }

            // If we have a pair, then call
            if (request.Call != null && hand >= PokerHand.ONE_PAIR) {
                return request.Call;
            }

            //Arrrghhh.. I give up
            return request.Fold;
        }
    }
}