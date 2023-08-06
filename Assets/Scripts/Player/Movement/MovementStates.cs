using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public static class MovementStates
{
    public struct InputPayLoad : INetworkSerializable
    {
        public int Tick;
        public Vector3 InputVector;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref InputVector);
        }
    }

    public struct PositionPayLoad : INetworkSerializable
    {
        public int Tick;
        public Vector3 Position;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref Position);
        }
    }
}
