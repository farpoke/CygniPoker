using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Cygni.PokerClient.Bots;
using Cygni.PokerClient.Communication;
using Cygni.PokerClient.Communication.Requests;
using Cygni.PokerClient.Communication.Responses;
using Cygni.PokerClient.Game;
using NLog;
using NLog.Targets;
using Cygni.PokerClient.Communication.Events;
using Gtk;

namespace Cygni.PokerClient
{
    class Program
    {
        private const string serverName = "poker.cygni.se";
        private const int portNumber = 4711;
        private const string roomName = "TRAINING";
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static HeuristicBot bot;
		private static StatusWindow statusWindow;

		private static bool shouldQuit = false;

        static void PrintInfo() {
            var assembly = Assembly.GetExecutingAssembly();
            var title_attr = assembly.GetCustomAttribute<AssemblyTitleAttribute>();
            logger.Info("{0} v{1}", title_attr.Title, assembly.GetName().Version);
            logger.Info("Bot:{0} {1}", bot.Name, bot.GetType().Name);
        }

		static void PumpGtk() {
			while (Application.EventsPending())
				Application.RunIteration();
		}

        static void RunGame() {
            using (var socket = new TexasServerSocket(serverName, portNumber)) {

                var gameState = new GameState();

                logger.Info("Connecting to {0}:{1}...", serverName, portNumber);
                socket.Connect();

                logger.Info("Entering {0}, waiting for play to start...", roomName);
                socket.Send(new RegisterForPlayRequest(bot.Name, roomName));

                while (!shouldQuit) {
                    foreach (var msg in socket.Receive()) {
                        if (msg is ActionRequest) {
                            var request = msg as ActionRequest;
                            var action = bot.Act(request, gameState);
                            var response = new ActionResponse(action, request.RequestId);
                            logger.Debug("Bot chose to {0} for ${1}", action.ActionType, action.Amount);
							statusWindow.BotChose(action, bot.WinEstimate);
                            socket.Send(response);
                        }
                        else {
                            gameState.UpdateFrom(msg);
							bot.UpdateFrom(msg, gameState);
							statusWindow.UpdateFrom(msg, gameState);
                            if (msg is TableIsDoneEvent) {
                                logger.Info("View at http://{0}/showgame/table/{1}", serverName, gameState.TableId);
								return;
                            }
                        }
						PumpGtk();
						if (shouldQuit)
							break;
					}
					PumpGtk();
                }
            }
        }

        static void Main(string[] args) {
            try {
				Application.Init();
				statusWindow = new StatusWindow();
				statusWindow.Destroyed += (sender, e) => { shouldQuit = true; };
				statusWindow.Show();
				//
				bot = new HeuristicBot();
				statusWindow.BotName = bot.Name;
				//
                PrintInfo();
				while (!shouldQuit)
                	RunGame();
            }
            catch (ServerShutdownException) {
                logger.Fatal("Server Shutdown. Bye!");
            }
			finally {
				if (statusWindow != null)
					statusWindow.Destroy();
			}
        }
    }
}
