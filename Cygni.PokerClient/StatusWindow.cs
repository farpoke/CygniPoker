using System;
using System.Collections.Generic;
using System.Linq;

using Gdk;

using Cygni.PokerClient.Communication;
using Cygni.PokerClient.Communication.Events;
using Cygni.PokerClient.Game;

namespace Cygni.PokerClient
{
	public partial class StatusWindow : Gtk.Window
	{
		List<long> chipHistory = new List<long>();

		long _chips = 0;
		long Chips {
			get { return _chips; }
			set {
				_chips = value;
				chipHistory.Add(_chips);
				bottomLabelLeft.Text = "Chips: $" + _chips.ToString();
				drawingArea.QueueDraw();
			}
		}

		public StatusWindow () :
			base(Gtk.WindowType.Toplevel)
		{
			this.Build();
			drawingArea.ExposeEvent += DrawingArea_ExposeEvent;
		}

		public void UpdateFrom(TexasMessage msg, GameState state) {
			if (msg is PlayIsStartedEvent)
				OnPlayIsStarted(msg as PlayIsStartedEvent, state);
			if (msg is YouWonAmountEvent)
				OnYouWonAmount(msg as YouWonAmountEvent);
		}

		void OnPlayIsStarted(PlayIsStartedEvent e, GameState state) {
			topLabelLeft.Text = "Table " + state.TableId.ToString();
			topLabelRight.Text = "Play " + state.PlayIndex.ToString();
		}

		void OnYouWonAmount(YouWonAmountEvent e) {
			Chips = e.YourChipAmount;
		}

		void DrawingArea_ExposeEvent (object o, Gtk.ExposeEventArgs args)
		{
			Rectangle area = args.Event.Area;
			var window = args.Event.Window;
			window.DrawRectangle(drawingArea.Style.WhiteGC, true, area);

			if (chipHistory.Count < 2)
				return;

			const float y_grid_size = 5000f;
			int y_intervals = (int)Math.Ceiling(chipHistory.Max() / y_grid_size);

			float offset_x = area.Left;
			float offset_y = area.Bottom;
			float scale_x = area.Width / (float)chipHistory.Count;
			float scale_y = -area.Height / (y_grid_size * y_intervals);

			for (int i = 0; i <= y_intervals; i++) {
				int y = (int)Math.Round(offset_y + scale_y * y_grid_size * i);
				window.DrawLine(drawingArea.Style.MidGC(Gtk.StateType.Normal), area.Left, y, area.Right, y);
			}

			var points = chipHistory.Select( (c, i) => new Point(
				(int)Math.Round(offset_x + scale_x * i), 
				(int)Math.Round(offset_y + scale_y * c)) );
			window.DrawLines(drawingArea.Style.BlackGC, points.ToArray());
		}
	}
}

