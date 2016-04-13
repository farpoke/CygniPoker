using System;

namespace Cygni.PokerClient
{
	public partial class StatusWindow : Gtk.Window
	{
		public StatusWindow () :
			base(Gtk.WindowType.Toplevel)
		{
			this.Build();
		}
	}
}

