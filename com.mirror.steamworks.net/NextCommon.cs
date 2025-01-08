#if !DISABLESTEAMWORKS
using System;
using System.Runtime.InteropServices;
using Steamworks;
using UnityEngine;

namespace Mirror.FizzySteam
{
    public abstract class NextCommon : IDisposable
    {
        private const int MAX_PACKET_SIZE = Constants.k_cbMaxSteamNetworkingSocketsMessageSizeSend;

        protected const int MAX_MESSAGES = 256;

        private readonly byte[] buffer;
        private readonly GCHandle pinnedBuffer;
        private readonly IntPtr bufferPtr;

        private bool disposed = false;

        public NextCommon()
        {
            buffer = new byte[MAX_PACKET_SIZE];
            pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            bufferPtr = pinnedBuffer.AddrOfPinnedObject();
        }

        protected EResult SendSocket(HSteamNetConnection conn, ArraySegment<byte> segment, int channelId)
        {
            int packetSize = segment.Count + 1;

            if (packetSize > MAX_PACKET_SIZE)
            {
                Debug.LogError($"Attempted to send oversize packet with {segment.Count} bytes on channel with ID {channelId}.");
                return EResult.k_EResultFail;
            }

            Array.Copy(segment.Array, segment.Offset, buffer, 0, segment.Count);
            buffer[segment.Count] = (byte)channelId;

            int sendFlag = channelId == Channels.Unreliable ? Constants.k_nSteamNetworkingSend_Unreliable : Constants.k_nSteamNetworkingSend_Reliable;

#if UNITY_SERVER
            EResult res = SteamGameServerNetworkingSockets.SendMessageToConnection(conn, pData, (uint)data.Length, sendFlag, out long _);
#else
            EResult res = SteamNetworkingSockets.SendMessageToConnection(conn, bufferPtr, (uint)packetSize, sendFlag, out long _);
#endif

            if (res != EResult.k_EResultOK)
            {
                Debug.LogWarning($"Send issue: {res}");
            }

            return res;
        }

        protected (byte[], int) ProcessMessage(IntPtr ptrs)
        {
            SteamNetworkingMessage_t data = Marshal.PtrToStructure<SteamNetworkingMessage_t>(ptrs);
            byte[] managedArray = new byte[data.m_cbSize];
            Marshal.Copy(data.m_pData, managedArray, 0, data.m_cbSize);

            SteamNetworkingMessage_t.Release(ptrs);

            int channel = managedArray[managedArray.Length - 1];
            Array.Resize(ref managedArray, managedArray.Length - 1);
            return (managedArray, channel);
        }

        public virtual void Dispose()
        {
            if (disposed)
            {
                return;
            }

            pinnedBuffer.Free();
            disposed = true;
        }
    }
}
#endif // !DISABLESTEAMWORKS