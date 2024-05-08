using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service_RFID_BIG_READER.Reader_DLL
{
    internal static class TopicSub
    {
        //Chủ đề nhận phản hồi về tin nhắn đi vào
        public const string IN_MESSAGE_FEEDBACK = "in/message/feedback";
        //Chủ đề nhận phản hồi về tin nhắn đi ra
        public const string OUT_MESSAGE_FEEDBACK = "out/message/feedback";
        // Chủ đề nhận yêu cầu đồng bộ dữ liệu
        public const string SYNC_DATABASE_SERVICE = "sync/databse/service";
    }
}
