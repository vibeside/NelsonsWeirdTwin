using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NelsonsWeirdTwin
{
    public class Warn
    {
        public string Reason = "";
        public DateTime Timestamp = DateTime.MinValue;
        public ulong IssuerID = 0;
    }
    [Serializable]
    public record WarnItem
    {
        public ulong User { get; set; } = 0;
        public List<Warn> CurrentWarns { get; set; } = [];

        public uint ExpiredWarns {  get; set; } = 0;

    }
}
