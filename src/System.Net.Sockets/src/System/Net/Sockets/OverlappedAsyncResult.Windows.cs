// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using System.Collections.Generic;

namespace System.Net.Sockets
{
    // OverlappedAsyncResult
    //
    // This class is used to take care of storage for async Socket operation
    // from the BeginSend, BeginSendTo, BeginReceive, BeginReceiveFrom calls.
    internal partial class OverlappedAsyncResult : BaseOverlappedAsyncResult
    {
#if MONO
        internal WSABuffer _Windows_singleBuffer;
        internal WSABuffer[] _Windows_wsaBuffers;
#else
        internal WSABuffer _singleBuffer;
        internal WSABuffer[] _wsaBuffers;
#endif

#if MONO
        private IntPtr Windows_GetSocketAddressPtr()
#else
        internal IntPtr GetSocketAddressPtr()
#endif
        {
            return Marshal.UnsafeAddrOfPinnedArrayElement(_socketAddress.Buffer, 0);
        }

#if MONO
        private IntPtr Windows_GetSocketAddressSizePtr()
#else
        internal IntPtr GetSocketAddressSizePtr()
#endif
        {
            return Marshal.UnsafeAddrOfPinnedArrayElement(_socketAddress.Buffer, _socketAddress.GetAddressSizeOffset());
        }

#if MONO
        private unsafe int Windows_GetSocketAddressSize()
#else
        internal unsafe int GetSocketAddressSize()
#endif
        {
            return *(int*)GetSocketAddressSizePtr();
        }

        // SetUnmanagedStructures
        //
        // Fills in overlapped structures used in an async overlapped Winsock call.
        // These calls are outside the runtime and are unmanaged code, so we need
        // to prepare specific structures and ints that lie in unmanaged memory
        // since the overlapped calls may complete asynchronously.
#if MONO
        private void Windows_SetUnmanagedStructures(byte[] buffer, int offset, int size, Internals.SocketAddress socketAddress, bool pinSocketAddress)
#else
        internal void SetUnmanagedStructures(byte[] buffer, int offset, int size, Internals.SocketAddress socketAddress, bool pinSocketAddress)
#endif
        {
            // Fill in Buffer Array structure that will be used for our send/recv Buffer
            _socketAddress = socketAddress;
            if (pinSocketAddress && _socketAddress != null)
            {
                object[] objectsToPin = null;
                objectsToPin = new object[2];
                objectsToPin[0] = buffer;

                _socketAddress.CopyAddressSizeIntoBuffer();
                objectsToPin[1] = _socketAddress.Buffer;

                base.SetUnmanagedStructures(objectsToPin);
            }
            else
            {
                base.SetUnmanagedStructures(buffer);
            }

#if MONO
            _Windows_singleBuffer.Length = size;
            _Windows_singleBuffer.Pointer = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, offset);
#else
            _singleBuffer.Length = size;
            _singleBuffer.Pointer = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, offset);
#endif
        }

#if MONO
        private void Windows_SetUnmanagedStructures(IList<ArraySegment<byte>> buffers)
#else
        internal void SetUnmanagedStructures(IList<ArraySegment<byte>> buffers)
#endif
        {
            // Fill in Buffer Array structure that will be used for our send/recv Buffer.
            // Make sure we don't let the app mess up the buffer array enough to cause
            // corruption.
            int count = buffers.Count;
            ArraySegment<byte>[] buffersCopy = new ArraySegment<byte>[count];

            for (int i = 0; i < count; i++)
            {
                buffersCopy[i] = buffers[i];
                RangeValidationHelpers.ValidateSegment(buffersCopy[i]);
            }

#if MONO
            _Windows_wsaBuffers = new WSABuffer[count];
#else
            _wsaBuffers = new WSABuffer[count];
#endif

            object[] objectsToPin = new object[count];
            for (int i = 0; i < count; i++)
            {
                objectsToPin[i] = buffersCopy[i].Array;
            }

            base.SetUnmanagedStructures(objectsToPin);

            for (int i = 0; i < count; i++)
            {
#if MONO
                _Windows_wsaBuffers[i].Length = buffersCopy[i].Count;
                _Windows_wsaBuffers[i].Pointer = Marshal.UnsafeAddrOfPinnedArrayElement(buffersCopy[i].Array, buffersCopy[i].Offset);
#else
                _wsaBuffers[i].Length = buffersCopy[i].Count;
                _wsaBuffers[i].Pointer = Marshal.UnsafeAddrOfPinnedArrayElement(buffersCopy[i].Array, buffersCopy[i].Offset);
#endif
            }
        }

        // This method is called after an asynchronous call is made for the user.
        // It checks and acts accordingly if the IO:
        // 1) completed synchronously.
        // 2) was pended.
        // 3) failed.
#if MONO
        private object Windows_PostCompletion(int numBytes)
#else
        internal override object PostCompletion(int numBytes)
#endif
        {
            if (ErrorCode == 0 && NetEventSource.IsEnabled)
            {
                LogBuffer(numBytes);
            }

            return base.PostCompletion(numBytes);
        }

        private void LogBuffer(int size)
        {
            if (!NetEventSource.IsEnabled)
            {
                return;
            }

            if (size > -1)
            {
#if MONO
                if (_Windows_wsaBuffers != null)
#else
                if (_wsaBuffers != null)
#endif
                {
#if MONO
                    foreach (WSABuffer wsaBuffer in _Windows_wsaBuffers)
#else
                    foreach (WSABuffer wsaBuffer in _wsaBuffers)
#endif
                    {
                        if (NetEventSource.IsEnabled) NetEventSource.DumpBuffer(this, wsaBuffer.Pointer, Math.Min(wsaBuffer.Length, size));
                        if ((size -= wsaBuffer.Length) <= 0)
                        {
                            break;
                        }
                    }
                }
                else
                {
#if MONO
                    if (NetEventSource.IsEnabled) NetEventSource.DumpBuffer(this, _Windows_singleBuffer.Pointer, Math.Min(_Windows_singleBuffer.Length, size));
#else
                    if (NetEventSource.IsEnabled) NetEventSource.DumpBuffer(this, _singleBuffer.Pointer, Math.Min(_singleBuffer.Length, size));
#endif
                }
            }
        }
    }
}
