// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.WebSockets
{
    internal sealed class WebSocketHandle
    {
        /// <summary>Per-thread cached StringBuilder for building of strings to send on the connection.</summary>
        [ThreadStatic]
        private static StringBuilder t_cachedStringBuilder;

        /// <summary>Default encoding for HTTP requests. Latin alphabet no 1, ISO/IEC 8859-1.</summary>
        private static readonly Encoding s_defaultHttpEncoding = Encoding.GetEncoding(28591);

        /// <summary>Size of the receive buffer to use.</summary>
        private const int DefaultReceiveBufferSize = 0x1000;
        /// <summary>GUID appended by the server as part of the security key response.  Defined in the RFC.</summary>
        private const string WSServerGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        /// <summary>Shared, lazily-initialized handler for when using default options.</summary>
        private static SocketsHttpHandler s_defaultHandler;

        private readonly CancellationTokenSource _abortSource = new CancellationTokenSource();
        private WebSocketState _state = WebSocketState.Connecting;
        private WebSocket _webSocket;

        public static WebSocketHandle Create() => new WebSocketHandle();

        public static bool IsValid(WebSocketHandle handle) => handle != null;

        public WebSocketCloseStatus? CloseStatus => _webSocket?.CloseStatus;

        public string CloseStatusDescription => _webSocket?.CloseStatusDescription;

        public WebSocketState State => _webSocket?.State ?? _state;

        public string SubProtocol => _webSocket?.SubProtocol;

        public static void CheckPlatformSupport() { /* nop */ }

        public void Dispose()
        {
            _state = WebSocketState.Closed;
            _webSocket?.Dispose();
        }

        public void Abort()
        {
            _abortSource.Cancel();
            _webSocket?.Abort();
        }

        public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) =>
            _webSocket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);

        public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) =>
            _webSocket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);

        public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>
            _webSocket.ReceiveAsync(buffer, cancellationToken);

        public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            _webSocket.ReceiveAsync(buffer, cancellationToken);

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken) =>
            _webSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);

        public Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken) =>
            _webSocket.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
        
        public async Task ConnectAsyncCore(Uri uri, CancellationToken cancellationToken, ClientWebSocketOptions options)
        {
            HttpResponseMessage response = null;
            SocketsHttpHandler handler = null;
            bool disposeHandler = true;
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                if (options._requestHeaders?.Count > 0) // use field to avoid lazily initializing the collection
                {
                    using (cancellationToken.Register(s => ((Socket)s).Dispose(), socket))
                    using (_abortSource.Token.Register(s => ((Socket)s).Dispose(), socket))
                    {
                        try
                        {
                            await socket.ConnectAsync(address, port).ConfigureAwait(false);
                        }
                        catch (ObjectDisposedException ode)
                        {
                            // If the socket was disposed because cancellation was requested, translate the exception
                            // into a new OperationCanceledException.  Otherwise, let the original ObjectDisposedexception propagate.
                            CancellationToken token = cancellationToken.IsCancellationRequested ? cancellationToken : _abortSource.Token;
                            if (token.IsCancellationRequested)
                            {
                                throw new OperationCanceledException(new OperationCanceledException().Message, ode, token);
                            }
                        }
                    }
                    cancellationToken.ThrowIfCancellationRequested(); // in case of a race and socket was disposed after the await
                    _abortSource.Token.ThrowIfCancellationRequested();
                    return socket;
                }

                // Create the security key and expected response, then build all of the request headers
                KeyValuePair<string, string> secKeyAndSecWebSocketAccept = CreateSecKeyAndSecWebSocketAccept();
                AddWebSocketHeaders(request, secKeyAndSecWebSocketAccept.Key, options);

                // Create the handler for this request and populate it with all of the options.
                // Try to use a shared handler rather than creating a new one just for this request, if
                // the options are compatible.
                if (options.Credentials == null &&
                    !options.UseDefaultCredentials &&
                    options.Proxy == null &&
                    options.Cookies == null &&
                    options.RemoteCertificateValidationCallback == null &&
                    options._clientCertificates?.Count == 0)
                {
                    disposeHandler = false;
                    handler = s_defaultHandler;
                    if (handler == null)
                    {
                        handler = new SocketsHttpHandler()
                        {
                            PooledConnectionLifetime = TimeSpan.Zero,
                            UseProxy = false,
                            UseCookies = false,
                        };
                        if (Interlocked.CompareExchange(ref s_defaultHandler, handler, null) != null)
                        {
                            handler.Dispose();
                            handler = s_defaultHandler;
                        }
                    }
                }
                else
                {
                    handler = new SocketsHttpHandler();
                    handler.PooledConnectionLifetime = TimeSpan.Zero;
                    handler.CookieContainer = options.Cookies;
                    handler.UseCookies = options.Cookies != null;
                    handler.SslOptions.RemoteCertificateValidationCallback = options.RemoteCertificateValidationCallback;

                    if (options.UseDefaultCredentials)
                    {
                        handler.Credentials = CredentialCache.DefaultCredentials;
                    }
                    else
                    {
                        handler.Credentials = options.Credentials;
                    }

                    if (options.Proxy == null)
                    {
                        handler.UseProxy = false;
                    }
                    else if (options.Proxy != ClientWebSocket.DefaultWebProxy.Instance)
                    {
                        handler.Proxy = options.Proxy;
                    }

                    if (options._clientCertificates?.Count > 0) // use field to avoid lazily initializing the collection
                    {
                        Debug.Assert(handler.SslOptions.ClientCertificates == null);
                        handler.SslOptions.ClientCertificates = new X509Certificate2Collection();
                        handler.SslOptions.ClientCertificates.AddRange(options.ClientCertificates);
                    }
                }
            }

            lastException?.Throw();

            Debug.Fail("We should never get here. We should have already returned or an exception should have been thrown.");
            throw new WebSocketException(SR.net_webstatus_ConnectFailure);
        }

        /// <summary>Creates a byte[] containing the headers to send to the server.</summary>
        /// <param name="uri">The Uri of the server.</param>
        /// <param name="options">The options used to configure the websocket.</param>
        /// <param name="secKey">The generated security key to send in the Sec-WebSocket-Key header.</param>
        /// <returns>The byte[] containing the encoded headers ready to send to the network.</returns>
        private static byte[] BuildRequestHeader(Uri uri, ClientWebSocketOptions options, string secKey)
        {
            StringBuilder builder = t_cachedStringBuilder ?? (t_cachedStringBuilder = new StringBuilder());
            Debug.Assert(builder.Length == 0, $"Expected builder to be empty, got one of length {builder.Length}");
            try
            {
                builder.Append("GET ").Append(uri.PathAndQuery).Append(" HTTP/1.1\r\n");

                // Add all of the required headers, honoring Host header if set.
                string hostHeader = options.RequestHeaders[HttpKnownHeaderNames.Host];
                builder.Append("Host: ");
                if (string.IsNullOrEmpty(hostHeader))
                {
                    linkedCancellation =
                        externalAndAbortCancellation =
                        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _abortSource.Token);
                }
                else
                {
                    builder.Append(hostHeader).Append("\r\n");
                }

                using (linkedCancellation)
                {
                    response = await new HttpMessageInvoker(handler).SendAsync(request, externalAndAbortCancellation.Token).ConfigureAwait(false);
                    externalAndAbortCancellation.Token.ThrowIfCancellationRequested(); // poll in case sends/receives in request/response didn't observe cancellation
                }

                // Add all of the additionally requested headers
                foreach (string key in options.RequestHeaders.AllKeys)
                {
                    if (string.Equals(key, HttpKnownHeaderNames.Host, StringComparison.OrdinalIgnoreCase))
                    {
                        // Host header handled above
                        continue;
                    }

                    builder.Append(key).Append(": ").Append(options.RequestHeaders[key]).Append("\r\n");
                }

                // Add the optional subprotocols header
                if (options.RequestedSubProtocols.Count > 0)
                {
                    builder.Append(HttpKnownHeaderNames.SecWebSocketProtocol).Append(": ");
                    builder.Append(options.RequestedSubProtocols[0]);
                    for (int i = 1; i < options.RequestedSubProtocols.Count; i++)
                    {
                        builder.Append(", ").Append(options.RequestedSubProtocols[i]);
                    }
                    builder.Append("\r\n");
                }

                // Get or create the buffer to use
                const int MinBufferSize = 125; // from ManagedWebSocket.MaxControlPayloadLength
                ArraySegment<byte> optionsBuffer = options.Buffer.GetValueOrDefault();
                Memory<byte> buffer =
                    optionsBuffer.Count >= MinBufferSize ? optionsBuffer : // use the provided buffer if it's big enough
                    default; // or let WebSocket.CreateFromStream use its default
                    // options.ReceiveBufferSize is ignored, as we rely on the buffer inside the SocketsHttpHandler stream

                // Get the response stream and wrap it in a web socket.
                Stream connectedStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                Debug.Assert(connectedStream.CanWrite);
                Debug.Assert(connectedStream.CanRead);
                _webSocket = WebSocket.CreateFromStream(
                    connectedStream,
                    isServer: false,
                    subprotocol,
                    options.KeepAliveInterval,
                    buffer);
            }
            catch (Exception exc)
            {
                if (_state < WebSocketState.Closed)
                {
                    string header = options.Cookies.GetCookieHeader(uri);
                    if (!string.IsNullOrWhiteSpace(header))
                    {
                        builder.Append(HttpKnownHeaderNames.Cookie).Append(": ").Append(header).Append("\r\n");
                    }
                }

                // End the headers
                builder.Append("\r\n");

                // Return the bytes for the built up header
                return s_defaultHttpEncoding.GetBytes(builder.ToString());
            }
            finally
            {
                // Disposing the handler will not affect any active stream wrapped in the WebSocket.
                if (disposeHandler)
                {
                    handler?.Dispose();
                }
            }
        }

        /// <param name="secKey">The generated security key to send in the Sec-WebSocket-Key header.</param>
        private static void AddWebSocketHeaders(HttpRequestMessage request, string secKey, ClientWebSocketOptions options)
        {
            request.Headers.TryAddWithoutValidation(HttpKnownHeaderNames.Connection, HttpKnownHeaderNames.Upgrade);
            request.Headers.TryAddWithoutValidation(HttpKnownHeaderNames.Upgrade, "websocket");
            request.Headers.TryAddWithoutValidation(HttpKnownHeaderNames.SecWebSocketVersion, "13");
            request.Headers.TryAddWithoutValidation(HttpKnownHeaderNames.SecWebSocketKey, secKey);
            if (options._requestedSubProtocols?.Count > 0)
            {
                // Make sure we clear the builder
                builder.Clear();
            }
        }

        /// <summary>
        /// Creates a pair of a security key for sending in the Sec-WebSocket-Key header and
        /// the associated response we expect to receive as the Sec-WebSocket-Accept header value.
        /// </summary>
        /// <returns>A key-value pair of the request header security key and expected response header value.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA5350", Justification = "Required by RFC6455")]
        private static KeyValuePair<string, string> CreateSecKeyAndSecWebSocketAccept()
        {
            string secKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            using (SHA1 sha = SHA1.Create())
            {
                return new KeyValuePair<string, string>(
                    secKey,
                    Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(secKey + WSServerGuid))));
            }
        }

        /// <summary>Read and validate the connect response headers from the server.</summary>
        /// <param name="stream">The stream from which to read the response headers.</param>
        /// <param name="options">The options used to configure the websocket.</param>
        /// <param name="expectedSecWebSocketAccept">The expected value of the Sec-WebSocket-Accept header.</param>
        /// <param name="cancellationToken">The CancellationToken to use to cancel the websocket.</param>
        /// <returns>The agreed upon subprotocol with the server, or null if there was none.</returns>
        private async Task<string> ParseAndValidateConnectResponseAsync(
            Stream stream, ClientWebSocketOptions options, string expectedSecWebSocketAccept, CancellationToken cancellationToken)
        {
            // Read the first line of the response
            string statusLine = await ReadResponseHeaderLineAsync(stream, cancellationToken).ConfigureAwait(false);

            // Depending on the underlying sockets implementation and timing, connecting to a server that then
            // immediately closes the connection may either result in an exception getting thrown from the connect
            // earlier, or it may result in getting to here but reading 0 bytes.  If we read 0 bytes and thus have
            // an empty status line, treat it as a connect failure.
            if (string.IsNullOrEmpty(statusLine))
            {
                throw new WebSocketException(SR.Format(SR.net_webstatus_ConnectFailure));
            }

            const string ExpectedStatusStart = "HTTP/1.1 ";
            const string ExpectedStatusStatWithCode = "HTTP/1.1 101"; // 101 == SwitchingProtocols

            // If the status line doesn't begin with "HTTP/1.1" or isn't long enough to contain a status code, fail.
            if (!statusLine.StartsWith(ExpectedStatusStart, StringComparison.Ordinal) || statusLine.Length < ExpectedStatusStatWithCode.Length)
            {
                throw new WebSocketException(WebSocketError.HeaderError);
            }

            // If the status line doesn't contain a status code 101, or if it's long enough to have a status description
            // but doesn't contain whitespace after the 101, fail.
            if (!statusLine.StartsWith(ExpectedStatusStatWithCode, StringComparison.Ordinal) ||
                (statusLine.Length > ExpectedStatusStatWithCode.Length && !char.IsWhiteSpace(statusLine[ExpectedStatusStatWithCode.Length])))
            {
                throw new WebSocketException(SR.net_webstatus_ConnectFailure);
            }

            // Read each response header. Be liberal in parsing the response header, treating
            // everything to the left of the colon as the key and everything to the right as the value, trimming both.
            // For each header, validate that we got the expected value.
            bool foundUpgrade = false, foundConnection = false, foundSecWebSocketAccept = false;
            string subprotocol = null;
            string line;
            while (!string.IsNullOrEmpty(line = await ReadResponseHeaderLineAsync(stream, cancellationToken).ConfigureAwait(false)))
            {
                int colonIndex = line.IndexOf(':');
                if (colonIndex == -1)
                {
                    throw new WebSocketException(WebSocketError.HeaderError);
                }

                string headerName = line.SubstringTrim(0, colonIndex);
                string headerValue = line.SubstringTrim(colonIndex + 1);

                // The Connection, Upgrade, and SecWebSocketAccept headers are required and with specific values.
                ValidateAndTrackHeader(HttpKnownHeaderNames.Connection, "Upgrade", headerName, headerValue, ref foundConnection);
                ValidateAndTrackHeader(HttpKnownHeaderNames.Upgrade, "websocket", headerName, headerValue, ref foundUpgrade);
                ValidateAndTrackHeader(HttpKnownHeaderNames.SecWebSocketAccept, expectedSecWebSocketAccept, headerName, headerValue, ref foundSecWebSocketAccept);

                // The SecWebSocketProtocol header is optional.  We should only get it with a non-empty value if we requested subprotocols,
                // and then it must only be one of the ones we requested.  If we got a subprotocol other than one we requested (or if we
                // already got one in a previous header), fail. Otherwise, track which one we got.
                if (string.Equals(HttpKnownHeaderNames.SecWebSocketProtocol, headerName, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(headerValue))
                {
                    string newSubprotocol = options.RequestedSubProtocols.Find(requested => string.Equals(requested, headerValue, StringComparison.OrdinalIgnoreCase));
                    if (newSubprotocol == null || subprotocol != null)
                    {
                        throw new WebSocketException(
                            WebSocketError.UnsupportedProtocol,
                            SR.Format(SR.net_WebSockets_AcceptUnsupportedProtocol, string.Join(", ", options.RequestedSubProtocols), subprotocol));
                    }
                    subprotocol = newSubprotocol;
                }
            }
            if (!foundUpgrade || !foundConnection || !foundSecWebSocketAccept)
            {
                throw new WebSocketException(SR.net_webstatus_ConnectFailure);
            }

            return subprotocol;
        }

        /// <summary>Validates a received header against expected values and tracks that we've received it.</summary>
        /// <param name="targetHeaderName">The header name against which we're comparing.</param>
        /// <param name="targetHeaderValue">The header value against which we're comparing.</param>
        /// <param name="foundHeaderName">The actual header name received.</param>
        /// <param name="foundHeaderValue">The actual header value received.</param>
        /// <param name="foundHeader">A bool tracking whether this header has been seen.</param>
        private static void ValidateAndTrackHeader(
            string targetHeaderName, string targetHeaderValue,
            string foundHeaderName, string foundHeaderValue,
            ref bool foundHeader)
        {
            bool isTargetHeader = string.Equals(targetHeaderName, foundHeaderName, StringComparison.OrdinalIgnoreCase);
            if (!foundHeader)
            {
                if (isTargetHeader)
                {
                    if (!string.Equals(targetHeaderValue, foundHeaderValue, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new WebSocketException(SR.Format(SR.net_WebSockets_InvalidResponseHeader, targetHeaderName, foundHeaderValue));
                    }
                    foundHeader = true;
                }
            }
            else
            {
                if (isTargetHeader)
                {
                    throw new WebSocketException(SR.Format(SR.net_webstatus_ConnectFailure));
                }
            }
        }

        /// <summary>Reads a line from the stream.</summary>
        /// <param name="stream">The stream from which to read.</param>
        /// <param name="cancellationToken">The CancellationToken used to cancel the websocket.</param>
        /// <returns>The read line, or null if none could be read.</returns>
        private static async Task<string> ReadResponseHeaderLineAsync(Stream stream, CancellationToken cancellationToken)
        {
            StringBuilder sb = t_cachedStringBuilder;
            if (sb != null)
            {
                t_cachedStringBuilder = null;
                Debug.Assert(sb.Length == 0, $"Expected empty StringBuilder");
            }
            else
            {
                sb = new StringBuilder();
            }

            var arr = new byte[1];
            char prevChar = '\0';
            try
            {
                // TODO: Reading one byte is extremely inefficient.  The problem, however,
                // is that if we read multiple bytes, we could end up reading bytes post-headers
                // that are part of messages meant to be read by the managed websocket after
                // the connection.  The likely solution here is to wrap the stream in a BufferedStream,
                // though a) that comes at the expense of an extra set of virtual calls, b) 
                // it adds a buffer when the managed websocket will already be using a buffer, and
                // c) it's not exposed on the version of the System.IO contract we're currently using.
                while (await stream.ReadAsync(arr, 0, 1, cancellationToken).ConfigureAwait(false) == 1)
                {
                    // Process the next char
                    char curChar = (char)arr[0];
                    if (prevChar == '\r' && curChar == '\n')
                    {
                        break;
                    }
                    sb.Append(curChar);
                    prevChar = curChar;
                }

                if (sb.Length > 0 && sb[sb.Length - 1] == '\r')
                {
                    sb.Length = sb.Length - 1;
                }

                return sb.ToString();
            }
            finally
            {
                sb.Clear();
                t_cachedStringBuilder = sb;
            }
        }
    }
}
