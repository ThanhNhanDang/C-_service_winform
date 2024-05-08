using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service_RFID_BIG_READER.Reader_DLL
{
    internal static class TopicSub
    {
        public const string IN_MESSAGE_FEEDBACK = "in/message/feedback";
        public const string OUT_MESSAGE_FEEDBACK = "out/message/feedback";
        public const string SYNC_DATABASE_SERVICE = "sync/databse/service";
        public const string GET_READER_STATUS = "get/reader/status";
    }
}
