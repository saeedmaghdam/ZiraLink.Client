using System.Net;

namespace ZiraLink.Client
{
    public class HttpResponseModel
    {
        public string ContentType { get; set; }
        public IEnumerable<KeyValuePair<string, IEnumerable<string>>> Headers { get; set; }
        public string StringContent { get; set; }
        public byte[] Bytes { get; set; }
        public HttpStatusCode HttpStatusCode { get; set; }
        public bool IsSuccessStatusCode { get; set; }
    }
}
