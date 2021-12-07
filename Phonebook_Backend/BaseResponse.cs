using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace AbbTech
{
    class BaseResponse
    {
        public int user_id { get; set; }
        public Operation_type operation_type { get; set; }
        public Operation_status operation_status { get; set; }
    }
    public enum Operation_status
    {
        fail,
        success
    }
    public enum Operation_type
    {
        add,
        edit,
        delete
    }
}
