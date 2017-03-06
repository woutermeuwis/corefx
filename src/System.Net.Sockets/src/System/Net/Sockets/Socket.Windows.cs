// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32.SafeHandles;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace System.Net.Sockets
{
    public partial class Socket
    {
        private DynamicWinsockMethods _dynamicWinsockMethods;

        private void EnsureDynamicWinsockMethods()
        {
            if (_dynamicWinsockMethods == null)
            {
                _dynamicWinsockMethods = DynamicWinsockMethods.GetMethods(_addressFamily, _socketType, _protocolType);
            }
        }

#if MONO
        private bool Windows_AcceptEx(SafeCloseSocket listenSocketHandle, SafeCloseSocket acceptSocketHandle, IntPtr buffer, int len, int localAddressLength, int remoteAddressLength, out int bytesReceived, SafeHandle overlapped)
#else
        internal bool AcceptEx(SafeCloseSocket listenSocketHandle,
            SafeCloseSocket acceptSocketHandle,
            IntPtr buffer,
            int len,
            int localAddressLength,
            int remoteAddressLength,
            out int bytesReceived,
            SafeHandle overlapped)
#endif
        {
            EnsureDynamicWinsockMethods();
            AcceptExDelegate acceptEx = _dynamicWinsockMethods.GetDelegate<AcceptExDelegate>(listenSocketHandle);

            return acceptEx(listenSocketHandle,
                acceptSocketHandle,
                buffer,
                len,
                localAddressLength,
                remoteAddressLength,
                out bytesReceived,
                overlapped);
        }

#if MONO
        private void Windows_GetAcceptExSockaddrs(IntPtr buffer, int receiveDataLength, int localAddressLength, int remoteAddressLength, out IntPtr localSocketAddress, out int localSocketAddressLength, out IntPtr remoteSocketAddress, out int remoteSocketAddressLength)
#else
        internal void GetAcceptExSockaddrs(IntPtr buffer,
            int receiveDataLength,
            int localAddressLength,
            int remoteAddressLength,
            out IntPtr localSocketAddress,
            out int localSocketAddressLength,
            out IntPtr remoteSocketAddress,
            out int remoteSocketAddressLength)
#endif
        {
            EnsureDynamicWinsockMethods();
            GetAcceptExSockaddrsDelegate getAcceptExSockaddrs = _dynamicWinsockMethods.GetDelegate<GetAcceptExSockaddrsDelegate>(_handle);

            getAcceptExSockaddrs(buffer,
                receiveDataLength,
                localAddressLength,
                remoteAddressLength,
                out localSocketAddress,
                out localSocketAddressLength,
                out remoteSocketAddress,
                out remoteSocketAddressLength);
        }

#if MONO
        private bool Windows_DisconnectEx(SafeCloseSocket socketHandle, SafeHandle overlapped, int flags, int reserved)
#else
        internal bool DisconnectEx(SafeCloseSocket socketHandle, SafeHandle overlapped, int flags, int reserved)
#endif
        {
            EnsureDynamicWinsockMethods();
            DisconnectExDelegate disconnectEx = _dynamicWinsockMethods.GetDelegate<DisconnectExDelegate>(socketHandle);

            return disconnectEx(socketHandle, overlapped, flags, reserved);
        }

#if MONO
        private bool Windows_DisconnectExBlocking(SafeCloseSocket socketHandle, IntPtr overlapped, int flags, int reserved)
#else
        internal bool DisconnectExBlocking(SafeCloseSocket socketHandle, IntPtr overlapped, int flags, int reserved)
#endif
        {
            EnsureDynamicWinsockMethods();
            DisconnectExDelegateBlocking disconnectEx_Blocking = _dynamicWinsockMethods.GetDelegate<DisconnectExDelegateBlocking>(socketHandle);

            return disconnectEx_Blocking(socketHandle, overlapped, flags, reserved);
        }

#if MONO
        private bool Windows_ConnectEx(SafeCloseSocket socketHandle, IntPtr socketAddress, int socketAddressSize, IntPtr buffer, int dataLength, out int bytesSent, SafeHandle overlapped)
#else
        internal bool ConnectEx(SafeCloseSocket socketHandle,
            IntPtr socketAddress,
            int socketAddressSize,
            IntPtr buffer,
            int dataLength,
            out int bytesSent,
            SafeHandle overlapped)
#endif
        {
            EnsureDynamicWinsockMethods();
            ConnectExDelegate connectEx = _dynamicWinsockMethods.GetDelegate<ConnectExDelegate>(socketHandle);

            return connectEx(socketHandle, socketAddress, socketAddressSize, buffer, dataLength, out bytesSent, overlapped);
        }

#if MONO
        private SocketError Windows_WSARecvMsg(SafeCloseSocket socketHandle, IntPtr msg, out int bytesTransferred, SafeHandle overlapped, IntPtr completionRoutine)
#else
        internal SocketError WSARecvMsg(SafeCloseSocket socketHandle, IntPtr msg, out int bytesTransferred, SafeHandle overlapped, IntPtr completionRoutine)
#endif
        {
            EnsureDynamicWinsockMethods();
            WSARecvMsgDelegate recvMsg = _dynamicWinsockMethods.GetDelegate<WSARecvMsgDelegate>(socketHandle);

            return recvMsg(socketHandle, msg, out bytesTransferred, overlapped, completionRoutine);
        }

#if MONO
        private SocketError Windows_WSARecvMsgBlocking(IntPtr socketHandle, IntPtr msg, out int bytesTransferred, IntPtr overlapped, IntPtr completionRoutine)
#else
        internal SocketError WSARecvMsgBlocking(IntPtr socketHandle, IntPtr msg, out int bytesTransferred, IntPtr overlapped, IntPtr completionRoutine)
#endif
        {
            EnsureDynamicWinsockMethods();
            WSARecvMsgDelegateBlocking recvMsg_Blocking = _dynamicWinsockMethods.GetDelegate<WSARecvMsgDelegateBlocking>(_handle);

            return recvMsg_Blocking(socketHandle, msg, out bytesTransferred, overlapped, completionRoutine);
        }

#if MONO
        private bool Windows_TransmitPackets(SafeCloseSocket socketHandle, IntPtr packetArray, int elementCount, int sendSize, SafeNativeOverlapped overlapped, TransmitFileOptions flags)
#else
        internal bool TransmitPackets(SafeCloseSocket socketHandle, IntPtr packetArray, int elementCount, int sendSize, SafeNativeOverlapped overlapped, TransmitFileOptions flags)
#endif
        {
            EnsureDynamicWinsockMethods();
            TransmitPacketsDelegate transmitPackets = _dynamicWinsockMethods.GetDelegate<TransmitPacketsDelegate>(socketHandle);

            return transmitPackets(socketHandle, packetArray, elementCount, sendSize, overlapped, flags);
        }

#if MONO
        private static IntPtr[] Windows_SocketListToFileDescriptorSet(IList socketList)
#else
        internal static IntPtr[] SocketListToFileDescriptorSet(IList socketList)
#endif
        {
            if (socketList == null || socketList.Count == 0)
            {
                return null;
            }

            IntPtr[] fileDescriptorSet = new IntPtr[socketList.Count + 1];
            fileDescriptorSet[0] = (IntPtr)socketList.Count;
            for (int current = 0; current < socketList.Count; current++)
            {
                if (!(socketList[current] is Socket))
                {
                    throw new ArgumentException(SR.Format(SR.net_sockets_select, socketList[current].GetType().FullName, typeof(System.Net.Sockets.Socket).FullName), nameof(socketList));
                }

                fileDescriptorSet[current + 1] = ((Socket)socketList[current])._handle.DangerousGetHandle();
            }
            return fileDescriptorSet;
        }

        // Transform the list socketList such that the only sockets left are those
        // with a file descriptor contained in the array "fileDescriptorArray".
#if MONO
        private static void Windows_SelectFileDescriptor(IList socketList, IntPtr[] fileDescriptorSet)
#else
        internal static void SelectFileDescriptor(IList socketList, IntPtr[] fileDescriptorSet)
#endif
        {
            // Walk the list in order.
            //
            // Note that the counter is not necessarily incremented at each step;
            // when the socket is removed, advancing occurs automatically as the
            // other elements are shifted down.
            if (socketList == null || socketList.Count == 0)
            {
                return;
            }

            if ((int)fileDescriptorSet[0] == 0)
            {
                // No socket present, will never find any socket, remove them all.
                socketList.Clear();
                return;
            }

            lock (socketList)
            {
                for (int currentSocket = 0; currentSocket < socketList.Count; currentSocket++)
                {
                    Socket socket = socketList[currentSocket] as Socket;

                    // Look for the file descriptor in the array.
                    int currentFileDescriptor;
                    for (currentFileDescriptor = 0; currentFileDescriptor < (int)fileDescriptorSet[0]; currentFileDescriptor++)
                    {
                        if (fileDescriptorSet[currentFileDescriptor + 1] == socket._handle.DangerousGetHandle())
                        {
                            break;
                        }
                    }

                    if (currentFileDescriptor == (int)fileDescriptorSet[0])
                    {
                        // Descriptor not found: remove the current socket and start again.
                        socketList.RemoveAt(currentSocket--);
                    }
                }
            }
        }

#if MONO
        private Socket Windows_GetOrCreateAcceptSocket(Socket acceptSocket, bool checkDisconnected, string propertyName, out SafeCloseSocket handle)
#else
        private Socket GetOrCreateAcceptSocket(Socket acceptSocket, bool checkDisconnected, string propertyName, out SafeCloseSocket handle)
#endif
        {
            // If an acceptSocket isn't specified, then we need to create one.
            if (acceptSocket == null)
            {
                acceptSocket = new Socket(_addressFamily, _socketType, _protocolType);
            }
            else
            {
                if (acceptSocket._rightEndPoint != null && (!checkDisconnected || !acceptSocket._isDisconnected))
                {
                    throw new InvalidOperationException(SR.Format(SR.net_sockets_namedmustnotbebound, propertyName));
                }
            }

            handle = acceptSocket._handle;
            return acceptSocket;
        }

#if MONO
        private void Windows_SendFileInternal(string fileName, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags)
#else
        private void SendFileInternal(string fileName, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags)
#endif
        {
            // Open the file, if any
            FileStream fileStream = OpenFile(fileName);

            SocketError errorCode;
            using (fileStream)
            {
                SafeFileHandle fileHandle = fileStream?.SafeFileHandle;

                // This can throw ObjectDisposedException.
                errorCode = SocketPal.SendFile(_handle, fileHandle, preBuffer, postBuffer, flags);
            }

            if (errorCode != SocketError.Success)
            {
                SocketException socketException = new SocketException((int)errorCode);
                UpdateStatusAfterSocketError(socketException);
                if (NetEventSource.IsEnabled) NetEventSource.Error(this, socketException);
                throw socketException;
            }

            // If the user passed the Disconnect and/or ReuseSocket flags, then TransmitFile disconnected the socket.
            // Update our state to reflect this.
            if ((flags & (TransmitFileOptions.Disconnect | TransmitFileOptions.ReuseSocket)) != 0)
            {
                SetToDisconnected();
                _remoteEndPoint = null;
            }
        }

#if MONO
        private IAsyncResult Windows_BeginSendFileInternal(string fileName, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags, AsyncCallback callback, object state)
#else
        private IAsyncResult BeginSendFileInternal(string fileName, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags, AsyncCallback callback, object state)
#endif
        {
            FileStream fileStream = OpenFile(fileName);

            TransmitFileAsyncResult asyncResult = new TransmitFileAsyncResult(this, state, callback);
            asyncResult.StartPostingAsyncOp(false);

            SocketError errorCode = SocketPal.SendFileAsync(_handle, fileStream, preBuffer, postBuffer, flags, asyncResult);

            // Check for synchronous exception
            if (!CheckErrorAndUpdateStatus(errorCode))
            {
                throw new SocketException((int)errorCode);
            }

            asyncResult.FinishPostingAsyncOp(ref Caches.SendClosureCache);

            return asyncResult;
        }

#if MONO
        private void Windows_EndSendFileInternal(IAsyncResult asyncResult)
#else
        private void EndSendFileInternal(IAsyncResult asyncResult)
#endif
        {
            TransmitFileAsyncResult castedAsyncResult = asyncResult as TransmitFileAsyncResult;
            if (castedAsyncResult == null || castedAsyncResult.AsyncObject != this)
            {
                throw new ArgumentException(SR.net_io_invalidasyncresult, nameof(asyncResult));
            }

            if (castedAsyncResult.EndCalled)
            {
                throw new InvalidOperationException(SR.Format(SR.net_io_invalidendcall, "EndSendFile"));
            }

            castedAsyncResult.InternalWaitForCompletion();
            castedAsyncResult.EndCalled = true;

            // If the user passed the Disconnect and/or ReuseSocket flags, then TransmitFile disconnected the socket.
            // Update our state to reflect this.
            if (castedAsyncResult.DoDisconnect)
            {
                SetToDisconnected();
                _remoteEndPoint = null;
            }

            if ((SocketError)castedAsyncResult.ErrorCode != SocketError.Success)
            {
                SocketException socketException = new SocketException(castedAsyncResult.ErrorCode);
                UpdateStatusAfterSocketError(socketException);
                if (NetEventSource.IsEnabled) NetEventSource.Error(this, socketException);
                throw socketException;
            }

        }

#if MONO
        private ThreadPoolBoundHandle Windows_GetOrAllocateThreadPoolBoundHandle()
#else
        internal ThreadPoolBoundHandle GetOrAllocateThreadPoolBoundHandle()
#endif
        {
            // There is a known bug that exists through Windows 7 with UDP and
            // SetFileCompletionNotificationModes.
            // So, don't try to enable skipping the completion port on success in this case.
            bool trySkipCompletionPortOnSuccess = !(CompletionPortHelper.PlatformHasUdpIssue && _protocolType == ProtocolType.Udp);
            return _handle.GetOrAllocateThreadPoolBoundHandle(trySkipCompletionPortOnSuccess);
        }
    }
}
