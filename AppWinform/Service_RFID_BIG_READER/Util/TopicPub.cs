using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service_RFID_BIG_READER.Util
{
    internal static class TopicPub
    {
        public const string IN_MESSAGE = "in/message";
        public const string IN_FAIL_ENC_PASS = "in/message/fail/pass";

        public const string OUT_MESSAGE = "out/message";
        public const string OUT_FAIL_ENC_PASS = "out/message/fail/pass";
     
        public const string READER_STATUS = "reader/status";
        public const string CALL_SYNC_DATABSE_SERVICE = "call/sync/database/service";

    }
}
