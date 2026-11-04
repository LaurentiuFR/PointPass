using System;
using System.IO;
using System.Text;
using PointPass.Networking;
using PointPass.Players;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

namespace PointPass.Online
{
    /// <summary>Fixed, little-endian pilot wire format; never sends native struct padding.</summary>
    public static class OnlineProtocol
    {
        public const string GameVersion = "PointPass-Online-1";
        public const string InputMessage = "PP.Online.Input.1";
        public const string StateMessage = "PP.Online.State.1";
        public const string ProbeMessage = "PP.Online.Probe.1";
        public const byte WireVersion = 1;
        public const int InputBytes = 31;

        public static byte[] EncodeInput(NetworkInputCommand command, uint snapshot)
        {
            using (var stream = new MemoryStream(InputBytes))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(WireVersion); writer.Write(command.Sequence); writer.Write(snapshot);
                Vector(writer, command.Move); Vector(writer, command.Aim);
                writer.Write(command.Curve); writer.Write((byte)(command.Sprint ? 1 : 0));
                writer.Write((byte)command.Action);
                return stream.ToArray();
            }
        }

        public static bool TryInput(byte[] bytes, out NetworkInputCommand command, out uint snapshot)
        {
            command = default; snapshot = 0;
            if (bytes == null || bytes.Length != InputBytes) return false;
            using (var reader = new BinaryReader(new MemoryStream(bytes, false)))
            {
                if (reader.ReadByte() != WireVersion) return false;
                command.Sequence = reader.ReadUInt32(); snapshot = reader.ReadUInt32();
                command.Move = Vector(reader); command.Aim = Vector(reader);
                command.Curve = reader.ReadSingle(); byte sprint = reader.ReadByte();
                command.Sprint = sprint == 1; command.Action = (NetworkKickAction)reader.ReadByte();
                return sprint <= 1 && command.IsValid && command.Move.sqrMagnitude <= 1.002f
                    && command.Aim.sqrMagnitude <= 1.002f && Mathf.Abs(command.Curve) <= 1;
            }
        }

        public static byte[] EncodeState(NetworkMatchState state)
        {
            using (var stream = new MemoryStream(384))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(WireVersion); writer.Write(state.Sequence); writer.Write(state.ServerTime);
                writer.Write(state.ResetVersion); writer.Write(state.BlueScore); writer.Write(state.RedScore);
                writer.Write(state.Half); writer.Write(state.Clock);
                writer.Write((byte)((state.Overtime ? 1 : 0) | (state.Playing ? 2 : 0) | (state.FullTime ? 4 : 0)));
                byte[] message = Encoding.UTF8.GetBytes(state.Message.ToString());
                if (message.Length > 127) throw new InvalidDataException("Match message exceeds protocol limit.");
                writer.Write((byte)message.Length); writer.Write(message);
                writer.Write((sbyte)state.OwnerSlot); Vector(writer, state.BallPosition); Vector(writer, state.BallVelocity);
                writer.Write(state.BallAngle); writer.Write(state.BallCurve);
                Player(writer, state.Blue); Player(writer, state.Red);
                return stream.ToArray();
            }
        }

        public static bool TryState(byte[] bytes, out NetworkMatchState state)
        {
            state = default;
            if (bytes == null || bytes.Length < 146 || bytes.Length > 384) return false;
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(bytes, false), new UTF8Encoding(false, true)))
                {
                    if (reader.ReadByte() != WireVersion) return false;
                    state.Sequence = reader.ReadUInt32(); state.ServerTime = reader.ReadDouble();
                    state.ResetVersion = reader.ReadInt32(); state.BlueScore = reader.ReadInt32(); state.RedScore = reader.ReadInt32();
                    state.Half = reader.ReadInt32(); state.Clock = reader.ReadSingle(); byte flags = reader.ReadByte();
                    state.Overtime = (flags & 1) != 0; state.Playing = (flags & 2) != 0; state.FullTime = (flags & 4) != 0;
                    byte count = reader.ReadByte(); if (count > 127) return false;
                    byte[] message = reader.ReadBytes(count); if (message.Length != count) return false;
                    state.Message = new FixedString128Bytes(new UTF8Encoding(false, true).GetString(message));
                    state.OwnerSlot = reader.ReadSByte(); state.BallPosition = Vector(reader); state.BallVelocity = Vector(reader);
                    state.BallAngle = reader.ReadSingle(); state.BallCurve = reader.ReadSingle();
                    state.Blue = Player(reader); state.Red = Player(reader);
                    return reader.BaseStream.Position == reader.BaseStream.Length && flags <= 7
                        && state.OwnerSlot >= -1 && state.OwnerSlot <= 1 && state.BlueScore >= 0 && state.RedScore >= 0
                        && state.Half >= 1 && state.Half <= 2 && Finite(state.Clock) && state.Clock >= 0
                        && !double.IsNaN(state.ServerTime) && !double.IsInfinity(state.ServerTime)
                        && Finite(state.BallPosition) && Finite(state.BallVelocity) && Finite(state.BallAngle) && Finite(state.BallCurve)
                        && Valid(state.Blue) && Valid(state.Red);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException || exception is InvalidDataException) { return false; }
        }

        public static void Send(NetworkManager manager, string name, ulong recipient, byte[] bytes, NetworkDelivery delivery)
        {
            using (var writer = new FastBufferWriter(bytes.Length, Allocator.Temp))
            {
                writer.WriteBytesSafe(bytes);
                manager.CustomMessagingManager.SendNamedMessage(name, recipient, writer, delivery);
            }
        }

        public static byte[] Read(FastBufferReader reader, int maximum)
        {
            int remaining = reader.Length - reader.Position;
            if (remaining <= 0 || remaining > maximum) return null;
            var bytes = new byte[remaining]; reader.ReadBytesSafe(ref bytes, remaining); return bytes;
        }

        private static void Vector(BinaryWriter writer, Vector2 value) { writer.Write(value.x); writer.Write(value.y); }
        private static Vector2 Vector(BinaryReader reader) => new Vector2(reader.ReadSingle(), reader.ReadSingle());
        private static void Player(BinaryWriter writer, NetworkPlayerState value)
        {
            writer.Write(value.ClientId); Vector(writer, value.Position); Vector(writer, value.Velocity);
            writer.Write(value.Angle); writer.Write(value.Charge); writer.Write(value.Curve);
            writer.Write((byte)value.Charging); writer.Write(value.KickSerial); writer.Write((byte)value.LastKick);
            writer.Write((byte)(value.Celebrating ? 1 : 0));
        }
        private static NetworkPlayerState Player(BinaryReader reader)
        {
            var value = new NetworkPlayerState { ClientId = reader.ReadUInt64(), Position = Vector(reader), Velocity = Vector(reader),
                Angle = reader.ReadSingle(), Charge = reader.ReadSingle(), Curve = reader.ReadSingle(), Charging = (PlayerKicker.KickType)reader.ReadByte(),
                KickSerial = reader.ReadUInt32(), LastKick = (PlayerKicker.KickType)reader.ReadByte() };
            byte celebration = reader.ReadByte(); if (celebration > 1) throw new InvalidDataException();
            value.Celebrating = celebration == 1; return value;
        }
        private static bool Valid(NetworkPlayerState value) => Finite(value.Position) && Finite(value.Velocity) && Finite(value.Angle)
            && Finite(value.Charge) && value.Charge >= 0 && value.Charge <= 1 && Finite(value.Curve) && Mathf.Abs(value.Curve) <= 1
            && value.Charging >= PlayerKicker.KickType.None && value.Charging <= PlayerKicker.KickType.Shot
            && value.LastKick >= PlayerKicker.KickType.None && value.LastKick <= PlayerKicker.KickType.Shot;
        private static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
