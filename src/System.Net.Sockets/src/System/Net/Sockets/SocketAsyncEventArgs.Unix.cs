// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Net.Sockets
{
    public partial class SocketAsyncEventArgs : EventArgs, IDisposable
    {
        private IntPtr _acceptedFileDescriptor;
        private int _socketAddressSize;
        private SocketFlags _receivedFlags;
        private Action<int, byte[], int, SocketFlags, SocketError> _transferCompletionCallback;

#if MONO
        private int? Unix_SendPacketsDescriptorCount
#else
        internal int? SendPacketsDescriptorCount
#endif
        {
            get { return null; }
        }

#if MONO
        private void Unix_InitializeInternals()
#else
        private void InitializeInternals()
#endif
        {
            // No-op for *nix.
        }

#if MONO
        private void Unix_FreeInternals(bool calledFromFinalizer)
#else
        private void FreeInternals(bool calledFromFinalizer)
#endif
        {
            // No-op for *nix.
        }

#if MONO
        private void Unix_SetupSingleBuffer()
#else
        private void SetupSingleBuffer()
#endif
        {
            // No-op for *nix.
        }

#if MONO
        private void Unix_SetupMultipleBuffers()
#else
        private void SetupMultipleBuffers()
#endif
        {
            // No-op for *nix.
        }

#if MONO
        private void Unix_SetupSendPacketsElements()
#else
        private void SetupSendPacketsElements()
#endif
        {
            // No-op for *nix.
        }

#if MONO
        private void Unix_InnerComplete()
#else
        private void InnerComplete()
#endif
        {
            // No-op for *nix.
        }

#if MONO
        private void Unix_InnerStartOperationAccept(bool userSuppliedBuffer)
#else
        private void InnerStartOperationAccept(bool userSuppliedBuffer)
#endif
        {
            _acceptedFileDescriptor = (IntPtr)(-1);
        }

        private void AcceptCompletionCallback(IntPtr acceptedFileDescriptor, byte[] socketAddress, int socketAddressSize, SocketError socketError)
        {
            CompleteAcceptOperation(acceptedFileDescriptor, socketAddress, socketAddressSize, socketError);

            CompletionCallback(0, SocketFlags.None, socketError);
        }

        private void CompleteAcceptOperation(IntPtr acceptedFileDescriptor, byte[] socketAddress, int socketAddressSize, SocketError socketError)
        {
            _acceptedFileDescriptor = acceptedFileDescriptor;
            Debug.Assert(socketAddress == null || socketAddress == _acceptBuffer, $"Unexpected socketAddress: {socketAddress}");
            _acceptAddressBufferCount = socketAddressSize;
        }

#if MONO
        private unsafe SocketError Unix_DoOperationAccept(Socket socket, SafeCloseSocket handle, SafeCloseSocket acceptHandle)
#else
        internal unsafe SocketError DoOperationAccept(Socket socket, SafeCloseSocket handle, SafeCloseSocket acceptHandle)
#endif
        {
            if (_buffer != null)
            {
                throw new PlatformNotSupportedException(SR.net_sockets_accept_receive_notsupported);
            }

            Debug.Assert(acceptHandle == null, $"Unexpected acceptHandle: {acceptHandle}");

            IntPtr acceptedFd;
            int socketAddressLen = _acceptAddressBufferCount / 2;
            SocketError socketError = handle.AsyncContext.AcceptAsync(_acceptBuffer, ref socketAddressLen, out acceptedFd, AcceptCompletionCallback);

            if (socketError != SocketError.IOPending)
            {
                CompleteAcceptOperation(acceptedFd, _acceptBuffer, socketAddressLen, socketError);
                FinishOperationSync(socketError, 0, SocketFlags.None);
            }

            return socketError;
        }

#if MONO
        private void Unix_InnerStartOperationConnect()
#else
        private void InnerStartOperationConnect()
#endif
        {
            // No-op for *nix.
        }

        private void ConnectCompletionCallback(SocketError socketError)
        {
            CompletionCallback(0, SocketFlags.None, socketError);
        }

#if MONO
        private unsafe SocketError Unix_DoOperationConnect(Socket socket, SafeCloseSocket handle)
#else
        internal unsafe SocketError DoOperationConnect(Socket socket, SafeCloseSocket handle)
#endif
        {
            SocketError socketError = handle.AsyncContext.ConnectAsync(_socketAddress.Buffer, _socketAddress.Size, ConnectCompletionCallback);
            if (socketError != SocketError.IOPending)
            {
                FinishOperationSync(socketError, 0, SocketFlags.None);
            }
            return socketError;
        }

#if MONO
        private SocketError Unix_DoOperationDisconnect(Socket socket, SafeCloseSocket handle)
#else
        internal SocketError DoOperationDisconnect(Socket socket, SafeCloseSocket handle)
#endif
        {
            throw new PlatformNotSupportedException(SR.net_sockets_disconnect_notsupported);
        }

#if MONO
        private void Unix_InnerStartOperationDisconnect()
#else
        private void InnerStartOperationDisconnect()
#endif
        {
            throw new PlatformNotSupportedException(SR.net_sockets_disconnect_notsupported);
        }

        private Action<int, byte[], int, SocketFlags, SocketError> TransferCompletionCallback =>
            _transferCompletionCallback ?? (_transferCompletionCallback = TransferCompletionCallbackCore);

        private void TransferCompletionCallbackCore(int bytesTransferred, byte[] socketAddress, int socketAddressSize, SocketFlags receivedFlags, SocketError socketError)
        {
            CompleteTransferOperation(bytesTransferred, socketAddress, socketAddressSize, receivedFlags, socketError);

            CompletionCallback(bytesTransferred, receivedFlags, socketError);
        }

        private void CompleteTransferOperation(int bytesTransferred, byte[] socketAddress, int socketAddressSize, SocketFlags receivedFlags, SocketError socketError)
        {
            Debug.Assert(socketAddress == null || socketAddress == _socketAddress.Buffer, $"Unexpected socketAddress: {socketAddress}");
            _socketAddressSize = socketAddressSize;
            _receivedFlags = receivedFlags;
        }

#if MONO
        private void Unix_InnerStartOperationReceive()
#else
        private void InnerStartOperationReceive()
#endif
        {
            _receivedFlags = System.Net.Sockets.SocketFlags.None;
            _socketAddressSize = 0;
        }

#if MONO
        private unsafe SocketError Unix_DoOperationReceive(SafeCloseSocket handle, out SocketFlags flags)
#else
        internal unsafe SocketError DoOperationReceive(SafeCloseSocket handle, out SocketFlags flags)
#endif
        {
            int bytesReceived;
            SocketError errorCode;
            if (_buffer != null)
            {
                errorCode = handle.AsyncContext.ReceiveAsync(_buffer, _offset, _count, _socketFlags, out bytesReceived, out flags, TransferCompletionCallback);
            }
            else
            {
                errorCode = handle.AsyncContext.ReceiveAsync(_bufferListInternal, _socketFlags, out bytesReceived, out flags, TransferCompletionCallback);
            }

            if (errorCode != SocketError.IOPending)
            {
                CompleteTransferOperation(bytesReceived, null, 0, flags, errorCode);
                FinishOperationSync(errorCode, bytesReceived, flags);
            }

            return errorCode;
        }

#if MONO
        private void Unix_InnerStartOperationReceiveFrom()
#else
        private void InnerStartOperationReceiveFrom()
#endif
        {
            _receivedFlags = System.Net.Sockets.SocketFlags.None;
            _socketAddressSize = 0;
        }

#if MONO
        private unsafe SocketError Unix_DoOperationReceiveFrom(SafeCloseSocket handle, out SocketFlags flags)
#else
        internal unsafe SocketError DoOperationReceiveFrom(SafeCloseSocket handle, out SocketFlags flags)
#endif
        {
            SocketError errorCode;
            int bytesReceived = 0;
            int socketAddressLen = _socketAddress.Size;
            if (_buffer != null)
            {
                errorCode = handle.AsyncContext.ReceiveFromAsync(_buffer, _offset, _count, _socketFlags, _socketAddress.Buffer, ref socketAddressLen, out bytesReceived, out flags, TransferCompletionCallback);
            }
            else
            {
                errorCode = handle.AsyncContext.ReceiveFromAsync(_bufferListInternal, _socketFlags, _socketAddress.Buffer, ref socketAddressLen, out bytesReceived, out flags, TransferCompletionCallback);
            }

            if (errorCode != SocketError.IOPending)
            {
                CompleteTransferOperation(bytesReceived, _socketAddress.Buffer, socketAddressLen, flags, errorCode);
                FinishOperationSync(errorCode, bytesReceived, flags);
            }

            return errorCode;
        }

#if MONO
        private void Unix_InnerStartOperationReceiveMessageFrom()
#else
        private void InnerStartOperationReceiveMessageFrom()
#endif
        {
            _receiveMessageFromPacketInfo = default(IPPacketInformation);
            _receivedFlags = System.Net.Sockets.SocketFlags.None;
            _socketAddressSize = 0;
        }

        private void ReceiveMessageFromCompletionCallback(int bytesTransferred, byte[] socketAddress, int socketAddressSize, SocketFlags receivedFlags, IPPacketInformation ipPacketInformation, SocketError errorCode)
        {
            CompleteReceiveMessageFromOperation(bytesTransferred, socketAddress, socketAddressSize, receivedFlags, ipPacketInformation, errorCode);

            CompletionCallback(bytesTransferred, receivedFlags, errorCode);
        }

        private void CompleteReceiveMessageFromOperation(int bytesTransferred, byte[] socketAddress, int socketAddressSize, SocketFlags receivedFlags, IPPacketInformation ipPacketInformation, SocketError errorCode)
        {
            Debug.Assert(_socketAddress != null, "Expected non-null _socketAddress");
            Debug.Assert(socketAddress == null || _socketAddress.Buffer == socketAddress, $"Unexpected socketAddress: {socketAddress}");

            _socketAddressSize = socketAddressSize;
            _receivedFlags = receivedFlags;
            _receiveMessageFromPacketInfo = ipPacketInformation;
        }

#if MONO
        private unsafe SocketError Unix_DoOperationReceiveMessageFrom(Socket socket, SafeCloseSocket handle)
#else
        internal unsafe SocketError DoOperationReceiveMessageFrom(Socket socket, SafeCloseSocket handle)
#endif
        {
            bool isIPv4, isIPv6;
            Socket.GetIPProtocolInformation(socket.AddressFamily, _socketAddress, out isIPv4, out isIPv6);

            int socketAddressSize = _socketAddress.Size;
            int bytesReceived;
            SocketFlags receivedFlags;
            IPPacketInformation ipPacketInformation;
            SocketError socketError = handle.AsyncContext.ReceiveMessageFromAsync(_buffer, _offset, _count, _socketFlags, _socketAddress.Buffer, ref socketAddressSize, isIPv4, isIPv6, out bytesReceived, out receivedFlags, out ipPacketInformation, ReceiveMessageFromCompletionCallback);
            if (socketError != SocketError.IOPending)
            {
                CompleteReceiveMessageFromOperation(bytesReceived, _socketAddress.Buffer, socketAddressSize, receivedFlags, ipPacketInformation, socketError);
                FinishOperationSync(socketError, bytesReceived, receivedFlags);
            }
            return socketError;
        }

#if MONO
        private void Unix_InnerStartOperationSend()
#else
        private void InnerStartOperationSend()
#endif
        {
            _receivedFlags = System.Net.Sockets.SocketFlags.None;
            _socketAddressSize = 0;
        }

#if MONO
        private unsafe SocketError Unix_DoOperationSend(SafeCloseSocket handle)
#else
        internal unsafe SocketError DoOperationSend(SafeCloseSocket handle)
#endif
        {
            int bytesSent;
            SocketError errorCode;
            if (_buffer != null)
            {
                errorCode = handle.AsyncContext.SendAsync(_buffer, _offset, _count, _socketFlags, out bytesSent, TransferCompletionCallback);
            }
            else
            {
                errorCode = handle.AsyncContext.SendAsync(_bufferListInternal, _socketFlags, out bytesSent, TransferCompletionCallback);
            }

            if (errorCode != SocketError.IOPending)
            {
                CompleteTransferOperation(bytesSent, null, 0, SocketFlags.None, errorCode);
                FinishOperationSync(errorCode, bytesSent, SocketFlags.None);
            }

            return errorCode;
        }

#if MONO
        private void Unix_InnerStartOperationSendPackets()
#else
        private void InnerStartOperationSendPackets()
#endif
        {
            throw new PlatformNotSupportedException();
        }

#if MONO
        private SocketError Unix_DoOperationSendPackets(Socket socket, SafeCloseSocket handle)
#else
        internal SocketError DoOperationSendPackets(Socket socket, SafeCloseSocket handle)
#endif
        {
            throw new PlatformNotSupportedException();
        }

#if MONO
        private void Unix_InnerStartOperationSendTo()
#else
        private void InnerStartOperationSendTo()
#endif
        {
            _receivedFlags = System.Net.Sockets.SocketFlags.None;
            _socketAddressSize = 0;
        }

#if MONO
        private SocketError Unix_DoOperationSendTo(SafeCloseSocket handle)
#else
        internal SocketError DoOperationSendTo(SafeCloseSocket handle)
#endif
        {
            int bytesSent;
            int socketAddressLen = _socketAddress.Size;
            SocketError errorCode;
            if (_buffer != null)
            {
                errorCode = handle.AsyncContext.SendToAsync(_buffer, _offset, _count, _socketFlags, _socketAddress.Buffer, ref socketAddressLen, out bytesSent, TransferCompletionCallback);
            }
            else
            {
                errorCode = handle.AsyncContext.SendToAsync(_bufferListInternal, _socketFlags, _socketAddress.Buffer, ref socketAddressLen, out bytesSent, TransferCompletionCallback);
            }

            if (errorCode != SocketError.IOPending)
            {
                CompleteTransferOperation(bytesSent, _socketAddress.Buffer, socketAddressLen, SocketFlags.None, errorCode);
                FinishOperationSync(errorCode, bytesSent, SocketFlags.None);
            }

            return errorCode;
        }

#if MONO
        private void Unix_LogBuffer(int size)
#else
        internal void LogBuffer(int size)
#endif
        {
            if (!NetEventSource.IsEnabled) return;

            if (_buffer != null)
            {
                NetEventSource.DumpBuffer(this, _buffer, _offset, size);
            }
            else if (_acceptBuffer != null)
            {
                NetEventSource.DumpBuffer(this, _acceptBuffer, 0, size);
            }
        }

#if MONO
        private void Unix_LogSendPacketsBuffers(int size)
#else
        internal void LogSendPacketsBuffers(int size)
#endif
        {
            throw new PlatformNotSupportedException();
        }

#if MONO
        private SocketError Unix_FinishOperationAccept(Internals.SocketAddress remoteSocketAddress)
#else
        private SocketError FinishOperationAccept(Internals.SocketAddress remoteSocketAddress)
#endif
        {
            System.Buffer.BlockCopy(_acceptBuffer, 0, remoteSocketAddress.Buffer, 0, _acceptAddressBufferCount);
            _acceptSocket = _currentSocket.CreateAcceptSocket(
                SafeCloseSocket.CreateSocket(_acceptedFileDescriptor),
                _currentSocket._rightEndPoint.Create(remoteSocketAddress));
            return SocketError.Success;
        }

#if MONO
        private SocketError Unix_FinishOperationConnect()
#else
        private SocketError FinishOperationConnect()
#endif
        {
            // No-op for *nix.
            return SocketError.Success;
        }

#if MONO
        private unsafe int Unix_GetSocketAddressSize()
#else
        private unsafe int GetSocketAddressSize()
#endif
        {
            return _socketAddressSize;
        }

#if MONO
        private unsafe void Unix_FinishOperationReceiveMessageFrom()
#else
        private unsafe void FinishOperationReceiveMessageFrom()
#endif
        {
            // No-op for *nix.
        }

#if MONO
        private void Unix_FinishOperationSendPackets()
#else
        private void FinishOperationSendPackets()
#endif
        {
            throw new PlatformNotSupportedException();
        }

        private void CompletionCallback(int bytesTransferred, SocketFlags flags, SocketError socketError)
        {
            if (socketError == SocketError.Success)
            {
                FinishOperationAsyncSuccess(bytesTransferred, flags);
            }
            else
            {
                if (_currentSocket.CleanedUp)
                {
                    socketError = SocketError.OperationAborted;
                }

                FinishOperationAsyncFailure(socketError, bytesTransferred, flags);
            }
        }
    }
}
