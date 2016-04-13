using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cygni.PokerClient.Communication.Requests
{
    public class RegisterForPlayRequest:TexasRequest
    {
        public string Name { get; set; }
        public string Room { get; set; }

        public RegisterForPlayRequest(string name, string room)
            : base("se.cygni.texasholdem.communication.message.request.RegisterForPlayRequest")
        {
            Name = name;
            Room = room;
        }
    }
}
