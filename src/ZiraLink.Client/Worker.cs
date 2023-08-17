using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using SharpCompress.Archives;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace ZiraLink.Client
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        private IModel _channel;

        public Worker(ILogger<Worker> logger) => _logger = logger;



        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Set up RabbitMQ connection and channels
            var factory = new ConnectionFactory { HostName = "localhost", Port = 5672, UserName = "guest", Password = "guest" };
            var connection = factory.CreateConnection();
            _channel = connection.CreateModel();

            var responseExchangeName = "response";
            var responseQueueName = "response_bus";
            var requestQueueName = "logon_request_bus";

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
            var consumer = new EventingBasicConsumer(_channel);
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

                    foreach (var header in requestModel.Headers)
                        httpRequestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());

                    var originalUri = new Uri(requestModel.RequestUrl);
                    var uri = new Uri(internalUri, originalUri.PathAndQuery);

                    httpRequestMessage.RequestUri = uri;
                    httpRequestMessage.Headers.Host = internalUri.Authority;
                    httpRequestMessage.Method = GetMethod(requestModel.Method);

                    var handler = new HttpClientHandler();
                    handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12; // Adjust the SSL/TLS protocol version as needed
                    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };

                    using var httpClient = new HttpClient(handler);

                    var httpResponseModel = new HttpResponseModel();
                    using (var responseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None))
                    {
                        httpResponseModel.HttpStatusCode = responseMessage.StatusCode;

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
                            //var newContent = Regex.Replace(stringContent, "", "");
                            httpResponseModel.StringContent = stringContent;
                        }

                        httpResponseModel.IsSuccessStatusCode = responseMessage.IsSuccessStatusCode;
                    }

                    //// Forward the request to the target application
                    //var response = await ForwardRequestToTargetApplication(host, internalUri, targetRequestDetails);
                    var responseBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(httpResponseModel));

                    // Publish the response to RabbitMQ
                    var responseProperties = _channel.CreateBasicProperties();
                    responseProperties.MessageId = requestID;

                    _channel.BasicPublish(exchange: responseExchangeName, routingKey: "", basicProperties: responseProperties, body: responseBytes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }

                _channel.BasicAck(ea.DeliveryTag, false);
            };

            _channel.BasicConsume(queue: requestQueueName, autoAck: false, consumer: consumer);

            // Wait for the cancellation token to be triggered
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task<HttpResponseModel> ForwardRequestToTargetApplication(string host, Uri internalUri, IDictionary<string, string> targetRequestDetails)
        {
            var path = targetRequestDetails["Path"];

            var handler = new HttpClientHandler();
            handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12; // Adjust the SSL/TLS protocol version as needed

            using var httpClient = new HttpClient(handler);

            var targetRequestMessage = CreateHttpRequestMessage(internalUri, targetRequestDetails);

            HttpResponseMessage response = null;

            do
            {
                response = await httpClient.SendAsync(targetRequestMessage, HttpCompletionOption.ResponseContentRead);

                if (response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.MovedPermanently)
                {
                    var redirectUrl = response.Headers.Location;

                    targetRequestMessage = CreateHttpRequestMessage(internalUri, targetRequestDetails);
                    targetRequestMessage.RequestUri = redirectUrl;
                    targetRequestMessage.Method = HttpMethod.Get; // Follow the redirect with a GET request
                }
            } while (response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.MovedPermanently);

            var httpResponse = new HttpResponseModel();
            httpResponse.ContentType = response.Content.Headers.ContentType != null ? response.Content.Headers.ContentType.MediaType : "";
            httpResponse.Headers = response.Headers.ToDictionary(h => h.Key, h => h.Value);

            if (!response.IsSuccessStatusCode)
            {
                httpResponse.HttpStatusCode = response.StatusCode;
                httpResponse.IsSuccessStatusCode = false;
                return httpResponse;
            }

            response.EnsureSuccessStatusCode();
            httpResponse.IsSuccessStatusCode = true;

            // Read the response content as bytes
            byte[] contentBytes = await response.Content.ReadAsByteArrayAsync();

            // Decompress the byte array
            byte[] decompressedBytes = await Decompress(response, contentBytes, httpResponse);
            decompressedBytes = contentBytes;

            if (path == "/")
            {
                var responseContent = Encoding.UTF8.GetString(decompressedBytes);
                responseContent = Regex.Replace(responseContent, internalUri.Authority, host);
                httpResponse.Bytes = Encoding.UTF8.GetBytes(responseContent);
            }
            else
            {
                httpResponse.Bytes = decompressedBytes;
            }

            return httpResponse;
        }

        private HttpRequestMessage CreateHttpRequestMessage(Uri internalUri, IDictionary<string, string> targetRequestDetails)
        {
            var targetRequestDetailsCopy = new Dictionary<string, string>(targetRequestDetails);
            var targetRequestUri = new Uri(internalUri, targetRequestDetailsCopy["Path"]);

            var targetRequestMessage = new HttpRequestMessage()
            {
                Method = new HttpMethod(targetRequestDetailsCopy["Method"]),
                RequestUri = targetRequestUri,
            };

            // Set headers from target request details
            targetRequestDetailsCopy.Remove("Method");
            targetRequestDetailsCopy.Remove("Path");

            foreach (var kvp in targetRequestDetailsCopy)
            {
                if (kvp.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    targetRequestMessage.Headers.Host = internalUri.Authority;
                }
                else
                {
                    targetRequestMessage.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                }
            }

            return targetRequestMessage;
        }

        private async Task<byte[]> Decompress(HttpResponseMessage response, byte[] compressedBytes, HttpResponseModel httpResponse)
        {
            var contentEncoding = response.Content.Headers.ContentEncoding.FirstOrDefault();

            if (contentEncoding == null)
            {
                return compressedBytes;
            }
            else if (contentEncoding == "deflate")
            {
                using (Stream compressedStream = await response.Content.ReadAsStreamAsync())
                {
                    using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                    {
                        using (MemoryStream decompressedStream = new MemoryStream())
                        {
                            await deflateStream.CopyToAsync(decompressedStream);
                            return decompressedStream.ToArray();
                        }
                    }
                }
            }
            else if (contentEncoding == "br")
            {
                using (Stream compressedStream = await response.Content.ReadAsStreamAsync())
                {
                    using (BrotliStream brotliStream = new BrotliStream(compressedStream, CompressionMode.Decompress))
                    {
                        using (MemoryStream decompressedStream = new MemoryStream())
                        {
                            await brotliStream.CopyToAsync(decompressedStream);
                            return decompressedStream.ToArray();
                        }
                    }
                }
            }
            else
            {
                using (MemoryStream compressedStream = new MemoryStream(compressedBytes))
                {
                    IArchive archive = ArchiveFactory.Open(compressedStream);

                    foreach (IArchiveEntry entry in archive.Entries)
                    {
                        if (!entry.IsDirectory)
                        {
                            using (MemoryStream entryStream = new MemoryStream())
                            {
                                //entry.WriteTo(entryStream, new ExtractionOptions { ExtractFullPath = false, Overwrite = true });
                                entry.WriteTo(entryStream);

                                // Decompressed data as byte array
                                byte[] decompressedData = entryStream.ToArray();

                                return decompressedData;
                            }
                        }
                    }
                }
            }

            // No supported compression method found
            throw new InvalidOperationException("Unsupported compression method.");
        }

        private bool IsContentOfType(HttpResponseMessage responseMessage, string type)
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
    }
}
