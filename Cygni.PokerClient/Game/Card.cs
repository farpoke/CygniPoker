using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cygni.PokerClient.Game
{
    public class Card
    {
        public Card()
        {

        }

        public Card(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        public Rank Rank { get; set; }
        public Suit Suit { get; set; }

        public override string ToString()
        {
            return string.Format("{0} of {1}", Rank, Suit);
        }

		public override int GetHashCode()
		{
			return Rank.GetHashCode() ^ Suit.GetHashCode() << 5;
		}

		public override bool Equals(object obj)
		{
			Card other = obj as Card;
			return (other != null) && (other.Rank == Rank) && (other.Suit == Suit);
		}
    }
}
