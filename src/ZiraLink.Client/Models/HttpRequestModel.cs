﻿namespace ZiraLink.Client.Models
{
    public class HttpRequestModel
    {
        public string RequestUrl { get; set; }
        public string Method { get; set; }
        public IEnumerable<KeyValuePair<string, IEnumerable<string>>> Headers { get; set; }
        public byte[] Bytes { get; set; }
    }
}
