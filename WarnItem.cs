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
        public long Timestamp = 0;
        public long IssuerID = 0;
        public Warn(string reason, long timestamp, long issuer)
        {
            Reason = reason;
            Timestamp = timestamp;
            IssuerID = issuer;
        }
    }
    [Serializable]
    public record WarnItem
    {
        public long User { get; set; } = 0;
        public List<Warn> CurrentWarns { get; set; } = [];

        public uint ExpiredWarns {  get; set; } = 0;

    }
}
