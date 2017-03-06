// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32.SafeHandles;
using System.IO;
using System.Threading.Tasks;

namespace System.Net.Sockets
{
    public partial class Socket
    {
#if MONO
        private Socket Unix_GetOrCreateAcceptSocket(Socket acceptSocket, bool unused, string propertyName, out SafeCloseSocket handle)
#else
        private Socket GetOrCreateAcceptSocket(Socket acceptSocket, bool unused, string propertyName, out SafeCloseSocket handle)
#endif
        {
            // AcceptSocket is not supported on Unix.
            if (acceptSocket != null)
            {
                throw new PlatformNotSupportedException();
            }

            handle = null;
            return null;
        }

        private static void CheckTransmitFileOptions(TransmitFileOptions flags)
        {
            // Note, UseDefaultWorkerThread is the default and is == 0.
            // Unfortunately there is no TransmitFileOptions.None.
            if (flags != TransmitFileOptions.UseDefaultWorkerThread)
            {
                throw new PlatformNotSupportedException(SR.net_sockets_transmitfileoptions_notsupported);
            }
        }

#if MONO
        private void Unix_SendFileInternal(string fileName, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags)
#else
        private void SendFileInternal(string fileName, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags)
#endif
        {
            CheckTransmitFileOptions(flags);

            // Open the file, if any
            // Open it before we send the preBuffer so that any exception happens first
            FileStream fileStream = OpenFile(fileName);

            SocketError errorCode = SocketError.Success;
            using (fileStream)
            {
                // Send the preBuffer, if any
                // This will throw on error
                if (preBuffer != null && preBuffer.Length > 0)
                {
                    Send(preBuffer);
                }

                // Send the file, if any
                if (fileStream != null)
                {
                    // This can throw ObjectDisposedException.
                    errorCode = SocketPal.SendFile(_handle, fileStream);
                }
            }

            if (errorCode != SocketError.Success)
            {
                SocketException socketException = new SocketException((int)errorCode);
                UpdateStatusAfterSocketError(socketException);
                if (NetEventSource.IsEnabled) NetEventSource.Error(this, socketException);
                throw socketException;
            }

            // Send the postBuffer, if any
            // This will throw on error
            if (postBuffer != null && postBuffer.Length > 0)
            {
                Send(postBuffer);
            }
        }

        private async Task SendFileInternalAsync(FileStream fileStream, byte[] preBuffer, byte[] postBuffer)
        {
            SocketError errorCode = SocketError.Success;
            using (fileStream)
            {
                // Send the preBuffer, if any
                // This will throw on error
                if (preBuffer != null && preBuffer.Length > 0)
                {
                    // Using "this." makes the extension method kick in
                    await this.SendAsync(new ArraySegment<byte>(preBuffer), SocketFlags.None).ConfigureAwait(false);
                }

                // Send the file, if any
                if (fileStream != null)
                {
                    var tcs = new TaskCompletionSource<bool>();

                    // This can throw ObjectDisposedException.
                    errorCode = SocketPal.SendFileAsync(_handle, fileStream, (bytesTransferred, socketError) => 
                    {
                        if (socketError != SocketError.Success)
                        {
                            // Synchronous exception from SendFileAsync
                            SocketException socketException = new SocketException((int)errorCode);
                            UpdateStatusAfterSocketError(socketException);
                            if (NetEventSource.IsEnabled) NetEventSource.Error(this, socketException);
                            tcs.SetException(socketException);
                        }

                        tcs.SetResult(true);
                    });

                    await tcs.Task.ConfigureAwait(false);
                }
            }

            if (errorCode != SocketError.Success)
            {
                // Synchronous exception from SendFileAsync
                SocketException socketException = new SocketException((int)errorCode);
                UpdateStatusAfterSocketError(socketException);
                if (NetEventSource.IsEnabled) NetEventSource.Error(this, socketException);
                throw socketException;
            }

            // Send the postBuffer, if any
            // This will throw on error
            if (postBuffer != null && postBuffer.Length > 0)
            {
                // Using "this." makes the extension method kick in
                await this.SendAsync(new ArraySegment<byte>(postBuffer), SocketFlags.None).ConfigureAwait(false);
            }
        }

#if MONO
        private IAsyncResult Unix_BeginSendFileInternal(string fileName, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags, AsyncCallback callback, object state)
#else
        private IAsyncResult BeginSendFileInternal(string fileName, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags, AsyncCallback callback, object state)
#endif
        {
            CheckTransmitFileOptions(flags);

            // Open the file, if any
            // Open it before we send the preBuffer so that any exception happens first
            FileStream fileStream = OpenFile(fileName);

            return TaskToApm.Begin(SendFileInternalAsync(fileStream, preBuffer, postBuffer), callback, state);
        }

#if MONO
        private void Unix_EndSendFileInternal(IAsyncResult asyncResult)
#else
        private void EndSendFileInternal(IAsyncResult asyncResult)
#endif
        {
            TaskToApm.End(asyncResult);
        }
    }
}
