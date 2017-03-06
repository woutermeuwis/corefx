// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Net.Sockets
{
    internal unsafe sealed partial class ReceiveMessageOverlappedAsyncResult : BaseOverlappedAsyncResult
    {
        private int _socketAddressSize;

#if MONO
        private int Unix_GetSocketAddressSize()
#else
        internal int GetSocketAddressSize()
#endif
        {
            return _socketAddressSize;
        }

#if MONO
        private void Unix_CompletionCallback(int numBytes, byte[] socketAddress, int socketAddressSize, SocketFlags receivedFlags, IPPacketInformation ipPacketInformation, SocketError errorCode)
#else
        public void CompletionCallback(int numBytes, byte[] socketAddress, int socketAddressSize, SocketFlags receivedFlags, IPPacketInformation ipPacketInformation, SocketError errorCode)
#endif
        {
            Debug.Assert(_socketAddress != null, "_socketAddress was null");
            Debug.Assert(socketAddress == null || _socketAddress.Buffer == socketAddress, $"Unexpected socketAddress: {socketAddress}");

            _socketAddressSize = socketAddressSize;
            _socketFlags = receivedFlags;
            _ipPacketInformation = ipPacketInformation;

            base.CompletionCallback(numBytes, errorCode);
        }
    }
}
