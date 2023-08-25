using ZiraLink.Client.Models;

namespace ZiraLink.Client.Framework.Helpers
{
    public interface IHttpHelper
    {
        Task<HttpResponseModel> CreateAndSendRequestAsync(string requestUrl, string method, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers, byte[] bytes, Uri internalUri);
        bool IsContentOfType(HttpResponseMessage responseMessage, string type);
        HttpMethod GetMethod(string method);
        string ReplaceUrls(string text, Uri oldUri, Uri newUri);
    }
}
