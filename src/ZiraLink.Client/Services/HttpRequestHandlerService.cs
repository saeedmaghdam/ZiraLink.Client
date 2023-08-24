using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using ZiraLink.Client.Framework.Services;
using System.Text;
using System.Text.Json;
using ZiraLink.Client.Models;
using System.Net;
using System.Text.RegularExpressions;

namespace ZiraLink.Client.Services
{
    public class HttpRequestHandlerService : IHttpRequestHandlerService
    {
        private readonly ILogger<HttpRequestHandlerService> _logger;
        private readonly IModel _channel;

        public HttpRequestHandlerService(ILogger<HttpRequestHandlerService> logger, IModel channel)
        {
            _logger = logger;
            _channel = channel;
        }

        public async Task InitializeHttpRequestConsumerAsync(string username)
        {
            var responseExchangeName = "response";
            var responseQueueName = "response_bus";
            var requestQueueName = $"{username}_request_bus";

            _channel.ExchangeDeclare(exchange: responseExchangeName,
                type: "direct",
                durable: false,
                autoDelete: false,
                arguments: null);

            _channel.QueueDeclare(queue: responseQueueName,
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);

            _channel.QueueBind(queue: responseQueueName,
               exchange: responseExchangeName,
               routingKey: "",
               arguments: null);

            _channel.QueueDeclare(queue: requestQueueName,
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);

            // Start consuming requests
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var requestID = ea.BasicProperties.MessageId;
                    var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var requestModel = JsonSerializer.Deserialize<HttpRequestModel>(body);

                    if (!ea.BasicProperties.Headers.TryGetValue("IntUrl", out var internalUrlByteArray))
                        throw new ApplicationException("Internal url not found");
                    if (!ea.BasicProperties.Headers.TryGetValue("Host", out var hostByteArray))
                        throw new ApplicationException("Host not found");
                    var internalUri = new Uri(Encoding.UTF8.GetString((byte[])internalUrlByteArray));
                    var host = Encoding.UTF8.GetString((byte[])hostByteArray);

                    var response = await CreateAndSendRequestAsync(requestModel, internalUri);

                    var responseBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));

                    var responseProperties = _channel.CreateBasicProperties();
                    responseProperties.MessageId = requestID;

                    _channel.BasicPublish(exchange: responseExchangeName, routingKey: "", basicProperties: responseProperties, body: responseBytes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }
                finally
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
            };

            _channel.BasicConsume(queue: requestQueueName, autoAck: false, consumer: consumer);
        }

        private static async Task<HttpResponseModel> CreateAndSendRequestAsync(HttpRequestModel requestModel, Uri internalUri)
        {
            var httpRequestMessage = new HttpRequestMessage();

            if (!HttpMethods.IsGet(requestModel.Method) &&
                !HttpMethods.IsHead(requestModel.Method) &&
                !HttpMethods.IsDelete(requestModel.Method) &&
                !HttpMethods.IsTrace(requestModel.Method))
            {
                var stream = new MemoryStream(requestModel.Bytes);
                var streamContent = new StreamContent(stream);
                httpRequestMessage.Content = streamContent;
            }

            var contentHeaders = new string[] { "Accept-Ranges", "Age", "Content-Disposition", "Content-Encoding", "Content-Language", "Content-Length", "Content-Location", "Content-Range", "Content-Type", "Expires", "Last-Modified", "Pragma", "Trailer", "Transfer-Encoding", "Vary", "Via", "Warning" };

            foreach (var header in requestModel.Headers)
            {
                if (header.Key.ToLower() == "Host".ToLower())
                {
                    httpRequestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, internalUri.Authority.ToString());
                    continue;
                }

                if (header.Key.ToLower() == "Referer".ToLower())
                {
                    var baseUri = new Uri($"{internalUri.Scheme}://{internalUri.Authority}");
                    var refererUri = new Uri(header.Value.First());
                    httpRequestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, new Uri(baseUri, refererUri.PathAndQuery).ToString());
                    continue;
                }

                httpRequestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());

                if (!contentHeaders.Contains(header.Key) && !header.Key.StartsWith(":") && !header.Key.StartsWith("Accept-"))
                    httpRequestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            var originalUri = new Uri(requestModel.RequestUrl);
            var uri = new Uri(internalUri, originalUri.PathAndQuery);

            httpRequestMessage.RequestUri = uri;
            httpRequestMessage.Headers.Host = internalUri.Authority;
            httpRequestMessage.Method = GetMethod(requestModel.Method);

            var handler = new HttpClientHandler();
            handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12; // Adjust the SSL/TLS protocol version as needed
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
            handler.AllowAutoRedirect = false;

            using var httpClient = new HttpClient(handler);

            var httpResponseModel = new HttpResponseModel();
            using (var responseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None))
            {
                httpResponseModel.HttpStatusCode = responseMessage.StatusCode;

                var isRedirected = responseMessage.StatusCode == HttpStatusCode.Redirect || responseMessage.StatusCode == HttpStatusCode.MovedPermanently;
                if (isRedirected)
                {
                    var redirectUrl = responseMessage.Headers.Location!.ToString();
                    httpResponseModel.IsRedirected = true;

                    var requestUri = new Uri(requestModel.RequestUrl);
                    var newUri = new Uri($"{requestUri.Scheme}://{requestUri.Authority}");
                    httpResponseModel.RedirectUrl = new Uri(newUri, redirectUrl).ToString();

                    return httpResponseModel;
                }

                var headers = new List<KeyValuePair<string, IEnumerable<string>>>();
                foreach (var header in responseMessage.Content.Headers)
                    headers.Add(new KeyValuePair<string, IEnumerable<string>>(header.Key, header.Value));

                foreach (var header in responseMessage.Headers)
                {
                    if (!headers.Any(x => x.Key == header.Key))
                        headers.Add(new KeyValuePair<string, IEnumerable<string>>(header.Key, header.Value));
                }

                headers = headers.Where(x => x.Key != "transfer-encoding").ToList();

                httpResponseModel.Headers = headers;

                var content = await responseMessage.Content.ReadAsByteArrayAsync();
                httpResponseModel.Bytes = content;
                if (IsContentOfType(responseMessage, "text/html") || IsContentOfType(responseMessage, "text/javascript"))
                {
                    var stringContent = Encoding.UTF8.GetString(content);
                    var requestUri = new Uri(requestModel.RequestUrl);
                    var newUri = new Uri($"{requestUri.Scheme}://{requestUri.Authority}");
                    var newContent = ReplaceUrls(stringContent, internalUri, newUri);
                    httpResponseModel.StringContent = newContent;
                }

                httpResponseModel.IsSuccessStatusCode = responseMessage.IsSuccessStatusCode;
            }

            return httpResponseModel;
        }

        private static bool IsContentOfType(HttpResponseMessage responseMessage, string type)
        {
            var result = false;

            if (responseMessage.Content?.Headers?.ContentType != null)
            {
                result = responseMessage.Content.Headers.ContentType.MediaType == type;
            }

            return result;
        }

        private static HttpMethod GetMethod(string method)
        {
            if (HttpMethods.IsDelete(method)) return HttpMethod.Delete;
            if (HttpMethods.IsGet(method)) return HttpMethod.Get;
            if (HttpMethods.IsHead(method)) return HttpMethod.Head;
            if (HttpMethods.IsOptions(method)) return HttpMethod.Options;
            if (HttpMethods.IsPost(method)) return HttpMethod.Post;
            if (HttpMethods.IsPut(method)) return HttpMethod.Put;
            if (HttpMethods.IsTrace(method)) return HttpMethod.Trace;
            return new HttpMethod(method);
        }

        private static string ReplaceUrls(string text, Uri oldUri, Uri newUri)
        {
            var newUrl = $"{newUri.Scheme}://{newUri.Authority}";

            var pattern = $"([H|h][T|t][T|t][P|p])://{oldUri.Authority}";
            var newText = Regex.Replace(text, pattern, newUrl);

            pattern = $"([H|h][T|t][T|t][P|p][S|s])://{oldUri.Authority}";
            newText = Regex.Replace(newText, pattern, newUrl);

            if (!oldUri.Authority.StartsWith("www"))
            {
                pattern = $"([H|h][T|t][T|t][P|p])://www.{oldUri.Authority}";
                newText = Regex.Replace(newText, pattern, newUrl);

                pattern = $"([H|h][T|t][T|t][P|p][S|s])://www.{oldUri.Authority}";
                newText = Regex.Replace(newText, pattern, newUrl);
            }

            return newText;
        }
    }
}
