using System.Collections.Generic;

namespace IoTSharp.Data.Taos.Protocols.TDWebSocket
{
    public class WSQueryRsp : WSActionRsp
    {

        public int req_id { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public long timing { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int id { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool is_update { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int affected_rows { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int fields_count { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<string> fields_names { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<int> fields_types { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<int> fields_lengths { get; set; }
        public int precision { get; set; }
    }


}