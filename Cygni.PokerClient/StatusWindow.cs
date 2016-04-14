using System;
using System.Collections.Generic;
using System.Linq;

using Gdk;

using NLog;

using Cygni.PokerClient.Communication;
using Cygni.PokerClient.Communication.Events;
using Cygni.PokerClient.Game;
using Action = Cygni.PokerClient.Game.Action;

namespace Cygni.PokerClient
{
	public partial class StatusWindow : Gtk.Window
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		List<long> chipHistory = new List<long>();

		List<Tuple<double,double>> estimateHistory = new List<Tuple<double,double>>();
		List<double> estimateShortTermHistory = new List<double>();

		List<double> diffHistory = new List<double>();
		List<double> correlationHistory = new List<double>();

		long _chips = 0;
		long Chips {
			get { return _chips; }
			set {
				_chips = value;
				chipHistory.Add(_chips);
				bottomLabelLeft.Text = "Chips: $" + _chips.ToString();
				estimateHistory.Add(new Tuple<double, double>(
					estimateShortTermHistory.FirstOrDefault(), estimateShortTermHistory.LastOrDefault()
				));
				estimateShortTermHistory.Clear();
				UpdateCorrelation ();
				drawingArea.QueueDraw();
			}
		}

		public string BotName {
			set {
				topStatus.Push(0, value);
			}
		}

		Gtk.TreeStore logModel;

		long tableId = -1;
		int playIndex = -1;

		Gtk.TreeIter tableLogIter;
		Gtk.TreeIter playLogIter;

		bool folded = false;

		public StatusWindow () :
			base(Gtk.WindowType.Toplevel)
		{
			this.Build();
			drawingArea.ExposeEvent += DrawingArea_ExposeEvent;
			SetupTreeView();
			SetupTextView();
		}

		void SetupTreeView() {
			for (int i = 0; i < 4; i++) {
				var text_column = new Gtk.TreeViewColumn();
				var text_renderer = new Gtk.CellRendererText();
				text_column.PackStart(text_renderer, true);
				text_column.AddAttribute(text_renderer, "text", i);
				if (i == 1 || i == 2)
					text_renderer.Xalign = 1;
				logView.AppendColumn(text_column);
			}
			//
			logModel = new Gtk.TreeStore(typeof(string), typeof(string), typeof(string), typeof(string));
			logView.Model = logModel;
		}

		void SetupTextView() {
			TextView_AddTag("win", "green");
			TextView_AddTag("loss", "red");
			TextView_AddTag("bigloss", "gray");
			TextView_AddTag("expensivefold", "gray");
		}

		void TextView_AddTag(string name, string foreground) {
			var tag = new Gtk.TextTag(name);
			tag.Foreground = foreground;
			/*Color clr = new Color();
			Color.Parse(foreground, ref clr);
			tag.ForegroundGdk = clr;*/
			tag.ForegroundSet = true;
			tag.Weight = Pango.Weight.Bold;
			tag.WeightSet = true;
			tableResultText.Buffer.TagTable.Add(tag);
		}

		public void UpdateFrom(TexasMessage msg, GameState state) {
			if (msg is PlayIsStartedEvent) OnPlayIsStarted(msg as PlayIsStartedEvent, state);
			else if (msg is YouWonAmountEvent) OnYouWonAmount(msg as YouWonAmountEvent, state);
			else if (msg is TableIsDoneEvent) OnTableIsDone(msg as TableIsDoneEvent);
			else if (msg is YouHaveBeenDealtACardEvent) OnYouHaveBeenDealtACard(msg as YouHaveBeenDealtACardEvent);
			else if (msg is CommunityHasBeenDealtACardEvent) OnCommunityHasBeenDealtACard(msg as CommunityHasBeenDealtACardEvent);
			else if (msg is ShowDownEvent) OnShowDown(msg as ShowDownEvent);
		}

		void NewTable(GameState state) {
			tableId = state.TableId;
			playIndex = -1;
			tableLogIter = logModel.AppendValues("Table " + tableId.ToString(), "", "", "");
			chipHistory.Clear();
			estimateHistory.Clear();
			estimateShortTermHistory.Clear();
			diffHistory.Clear();
			correlationHistory.Clear();
			_chips = 10000;
		}

		void NewPlay(GameState state) {
			playIndex = state.PlayIndex;
			playLogIter = logModel.AppendValues(tableLogIter, "Play " + playIndex.ToString(), "", "", "");
			folded = false;
		}

		void OnPlayIsStarted(PlayIsStartedEvent e, GameState state) {
			topLabelLeft.Text = "Table " + state.TableId.ToString();
			topLabelRight.Text = "Play " + state.PlayIndex.ToString();
			//
			if (state.TableId != tableId)
				NewTable(state);
			NewPlay(state);
		}

		void AddPlayLogMessage(params string[] values) {
			logModel.AppendValues(playLogIter, values);
		}

		void UpdatePlayLogValues(params string[] values) {
			logModel.SetValues(playLogIter, values);
		}

		void OnYouWonAmount(YouWonAmountEvent e, GameState state) {
            var diff = e.YourChipAmount - Chips;
            diffHistory.Add(diff / (double)Chips);
			Chips = e.YourChipAmount;
			var result = diff < 0 ? (diff < -5000 ? "Big Loss" : "Loss") : "Win";
			UpdatePlayLogValues(playIndex.ToString(), diff.ToString(), Chips.ToString(), result);
			if (diff < -5000) {
				AppendResultText("bigloss", "(Big loss @ table {0}, play {1})\n", tableId, playIndex);
			}
			if (folded && diff < -state.BigBlind * 50) {
				AppendResultText("expensivefold", "(Expensive fold @ table {0}, play {1})\n", tableId, playIndex);
			}
		}

		void DrawingArea_ExposeEvent (object o, Gtk.ExposeEventArgs args)
		{
			
			Rectangle area = args.Event.Area;
			var window = args.Event.Window;
			window.DrawRectangle(drawingArea.Style.WhiteGC, true, area);

			if (chipHistory.Count < 2)
				return;

			Gdk.GC gc_chips = new Gdk.GC(window);
			gc_chips.SetLineAttributes(2, LineStyle.Solid, CapStyle.Butt, JoinStyle.Bevel);

			Gdk.GC gc_red = new Gdk.GC(window);
			gc_red.RgbFgColor = new Color(255, 50, 50);

			Gdk.GC gc_blue = new Gdk.GC(window);
			gc_blue.RgbFgColor = new Color(128, 128, 255);

			int skip_count = chipHistory.Count - area.Width / 5;
			if (skip_count < 0)
				skip_count = 0;
			
			//Draw_Graph(area, window, 1, 2, false, estimateHistory.Skip(skip_count).Select(t => t.Item1), gc_red);
			Draw_Graph(area, window, 1, 2, false, estimateHistory.Skip(skip_count).Select(t => t.Item2), gc_blue);

			Draw_Graph(area, window, 5000, 60000, true, chipHistory.Skip(skip_count).Select(c => (double)c), gc_chips);

			Draw_Graph(area, window, 1, 2, false, correlationHistory.Skip(skip_count).Select(y => y+1), gc_red);
		}

		void Draw_Graph(Rectangle area, Gdk.Window window, double y_grid, double y_max, bool grid, IEnumerable<double> data, Gdk.GC gc) {
			int y_intervals = (int)Math.Ceiling(y_max / y_grid);

			double offset_x = area.Left;
			double offset_y = area.Bottom;
			double scale_x = area.Width / (double)(data.Count() + 1);
			double scale_y = -area.Height / (y_grid * y_intervals);

			if (grid) {
				for (int i = 0; i <= y_intervals; i++) {
					int y = (int)Math.Round(offset_y + scale_y * y_grid * i);
					window.DrawLine(drawingArea.Style.MidGC(Gtk.StateType.Normal), area.Left, y, area.Right, y);
				}
			}

			var points = data.Select( (y, i) => new Point(
				(int)Math.Round(offset_x + scale_x * i), 
				(int)Math.Round(offset_y + scale_y * y)) );
			window.DrawLines(gc, points.ToArray());
		}

		public void BotChose(Action action, double estimate) {
			AddPlayLogMessage("", action.Amount.ToString(), "", string.Format("{0}, Estimate: {1}", action.ActionType, estimate));
			if (action.ActionType == ActionType.FOLD)
				folded = true;
			bottomLabelRight.Text = "Estimate: " + estimate.ToString();
			estimateShortTermHistory.Add(estimate);
		}

		void AppendResultText(string tag_name, string format, params object[] values) {
			var formatted = string.Format(format, values);
			//
			var buffer = tableResultText.Buffer;
			var end = buffer.EndIter;
			var tag = buffer.TagTable.Lookup(tag_name);
			//
			buffer.InsertWithTags(ref end, formatted, tag);
			tableResultText.ScrollToIter(buffer.EndIter, 0, false, 0, 0);
			//
			logger.Info(formatted.Trim());
		}

		void OnTableIsDone(TableIsDoneEvent e) {
			if (Chips > 0) {
				AppendResultText("win", "Table {0} ended. Won after {1} plays!\n", tableId, playIndex+1);
			} else {
				AppendResultText("loss", "Table {0} ended. Lost after {1} plays.\n", tableId, playIndex+1);
			}
		}

		void OnYouHaveBeenDealtACard(YouHaveBeenDealtACardEvent e) {
			AddPlayLogMessage("", "", "", string.Format("Got a {0} of {1}", e.Card.Rank, e.Card.Suit));
		}

		void OnCommunityHasBeenDealtACard(CommunityHasBeenDealtACardEvent e) {
			AddPlayLogMessage("", "", "", string.Format("Community got a {0} of {1}", e.Card.Rank, e.Card.Suit));
		}

		void OnShowDown(ShowDownEvent e) {
			foreach (var p in e.PlayersShowDown) {
				AddPlayLogMessage("", "", "", string.Format("{0} had a {1}", p.Player.Name, p.Hand.PokerHand));
			}
		}

		void UpdateCorrelation() {
			if (chipHistory.Count < 2) {
				correlationHistory.Add(0);
				return;
			}

			System.Diagnostics.Debug.Assert(diffHistory.Count == estimateHistory.Count);

			var chip_data = diffHistory.Reverse<double>().Take(200);
			var estimate_data = estimateHistory.Reverse<Tuple<double,double>>().Take(200).Select(t => t.Item2);

			var chip_mean = chip_data.Average();
			var estimate_mean = estimate_data.Average();

			var cov_sum = chip_data.Zip(estimate_data, (v1, v2) => (v1 - chip_mean) * (v2 - estimate_mean)).Sum();
			var chip_var = chip_data.Sum(x => Math.Pow(x - chip_mean, 2));
			var estimate_var = estimate_data.Sum(x => Math.Pow(x - estimate_mean, 2));

			double corr = cov_sum / Math.Sqrt(chip_var * estimate_var);
			correlationHistory.Add(corr);
		}
	}
}

