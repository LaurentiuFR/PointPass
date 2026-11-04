using System;
using System.IO;
using System.Linq;
using PointPass.Networking;
using PointPass.Online;
using PointPass.Players;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PointPass.Validation
{
    /// <summary>Executes against the actual Unity protocol and NGO buffer types, without a network connection.</summary>
    public static class ProtocolValidation
    {
        private static int checks;
        private static string Report => Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "Logs/OnlinePilotVerification/protocol-tests.txt");
        private static void Check(bool condition, string description)
        {
            checks++;
            File.AppendAllText(Report, (condition ? "PASS " : "FAIL ") + description + "\n");
            if (!condition) throw new InvalidOperationException(description);
        }
        private static byte[] Alter(byte[] source, int offset, params byte[] values)
        {
            byte[] copy = (byte[])source.Clone(); Array.Copy(values, 0, copy, offset, values.Length); return copy;
        }

        public static string Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, "Actual Unity/NGO online wire-codec checks\n"); checks = 0;
            var command = new NetworkInputCommand { Sequence = 0x11223344, Move = new Vector2(0.6f, -0.8f),
                Aim = Vector2.right, Curve = -0.75f, Sprint = true, Action = NetworkKickAction.ReleaseShot };
            byte[] input = OnlineProtocol.EncodeInput(command, 0x55667788);
            Check(input.Length == 31 && input.Length == OnlineProtocol.InputBytes, "Input wire length is exactly31bytes");
            Check(input[1] == 0x44 && input[2] == 0x33 && input[3] == 0x22 && input[4] == 0x11,
                "Integer fields use explicit little-endian bytes");
            Check(OnlineProtocol.TryInput(input, out NetworkInputCommand decoded, out uint acknowledgement)
                && decoded.Sequence == command.Sequence && decoded.Move == command.Move && decoded.Aim == command.Aim
                && decoded.Curve == command.Curve && decoded.Sprint && decoded.Action == command.Action && acknowledgement == 0x55667788,
                "Input round-trip retains all command fields and snapshot acknowledgement");
            Check(!OnlineProtocol.TryInput(null, out _, out _), "Null input is rejected");
            for (int length = 0; length < input.Length; length++)
                Check(!OnlineProtocol.TryInput(input.Take(length).ToArray(), out _, out _), "Truncated input length" + length + " rejected");
            Check(!OnlineProtocol.TryInput(input.Concat(new byte[] { 0 }).ToArray(), out _, out _), "Appended input bytes are rejected");
            Check(!OnlineProtocol.TryInput(Alter(input, 0, 2), out _, out _), "Unknown input version is rejected");
            Check(!OnlineProtocol.TryInput(Alter(input, 29, 2), out _, out _), "Invalid input boolean is rejected");
            Check(!OnlineProtocol.TryInput(Alter(input, 30, 255), out _, out _), "Unknown action is rejected");
            foreach (int offset in new[] { 9, 13, 17, 21, 25 })
                foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                    Check(!OnlineProtocol.TryInput(Alter(input, offset, BitConverter.GetBytes(invalid)), out _, out _),
                        "Nonfinite input field" + offset + " rejected");
            Check(!OnlineProtocol.TryInput(Alter(input, 25, BitConverter.GetBytes(1.01f)), out _, out _), "Excessive shot curve is rejected");
            Check(!OnlineProtocol.TryInput(Alter(input, 9, BitConverter.GetBytes(2f)), out _, out _), "Excessive movement magnitude is rejected");
            Check(!OnlineProtocol.TryInput(Alter(input, 17, BitConverter.GetBytes(2f)), out _, out _), "Excessive aim magnitude is rejected");

            var state = new NetworkMatchState { Sequence = 32, ServerTime = 123.5, ResetVersion = 4,
                BlueScore = 2, RedScore = 1, Half = 2, Clock = 17.25f, Playing = true, OwnerSlot = -1,
                BallPosition = new Vector2(2, 3), BallVelocity = new Vector2(-4, 5), BallAngle = 213.5f, BallCurve = 0.4f,
                Blue = Player(11, new Vector2(-3, -2)), Red = Player(22, new Vector2(3, 2)) };
            byte[] empty = OnlineProtocol.EncodeState(state);
            Check(empty.Length == 146, "Empty-message snapshot minimum is exactly146bytes (60-byte header and two43-byteplayers)");
            Check(OnlineProtocol.TryState(empty, out NetworkMatchState restored) && restored.Sequence == state.Sequence
                && restored.ServerTime == state.ServerTime && restored.Blue.ClientId == 11 && restored.Red.ClientId == 22
                && restored.OwnerSlot == -1 && restored.BallPosition == state.BallPosition && restored.Blue.Charge == state.Blue.Charge,
                "Minimum-length snapshot decodes successfully with actual Unity types");
            state.Message = "BLUE GOAL!";
            byte[] named = OnlineProtocol.EncodeState(state);
            Check(named.Length == 156 && OnlineProtocol.TryState(named, out restored) && restored.Message.ToString() == "BLUE GOAL!",
                "Snapshot message length and contents round-trip");
            state.Message = "GOAL ⚽";
            byte[] unicode = OnlineProtocol.EncodeState(state);
            Check(OnlineProtocol.TryState(unicode, out restored) && restored.Message.ToString() == "GOAL ⚽", "UTF8 snapshot messages round-trip");
            state.Message = new string('A', 125);
            byte[] maximum = OnlineProtocol.EncodeState(state);
            Check(maximum.Length == 271 && OnlineProtocol.TryState(maximum, out restored) && restored.Message.Length == 125,
                "Maximum FixedString128Bytes payload is125UTF8bytes and snapshot271bytes");
            Check(!OnlineProtocol.TryState(null, out _), "Null state is rejected");
            for (int length = 0; length < empty.Length; length++)
                Check(!OnlineProtocol.TryState(empty.Take(length).ToArray(), out _), "Truncated state length" + length + " rejected");
            Check(!OnlineProtocol.TryState(named.Take(named.Length - 1).ToArray(), out _), "Truncated variable-message snapshot is rejected without throwing");
            Check(!OnlineProtocol.TryState(empty.Concat(new byte[] { 0 }).ToArray(), out _), "Appended state bytes are rejected");
            Check(!OnlineProtocol.TryState(new byte[385], out _), "Oversized state is rejected");
            Check(!OnlineProtocol.TryState(Alter(empty, 0, 2), out _), "Unknown state version is rejected");
            Check(!OnlineProtocol.TryState(Alter(empty, 33, 8), out _), "Unknown state flags are rejected");
            Check(!OnlineProtocol.TryState(Alter(empty, 35, 2), out _), "Invalid owner slot is rejected");
            Check(!OnlineProtocol.TryState(Alter(empty, 17, BitConverter.GetBytes(-1)), out _), "Negative score is rejected");
            Check(!OnlineProtocol.TryState(Alter(empty, 25, BitConverter.GetBytes(3)), out _), "Invalid match half is rejected");
            Check(!OnlineProtocol.TryState(Alter(empty, 29, BitConverter.GetBytes(-1f)), out _), "Negative clock is rejected");
            Check(!OnlineProtocol.TryState(Alter(empty, 5, BitConverter.GetBytes(double.NaN)), out _), "Nonfinite server time is rejected");
            foreach (int offset in new[] { 29, 36, 40, 44, 48, 52, 56, 68, 72, 76, 80, 84, 88, 92, 111, 115, 119, 123, 127, 131, 135 })
                Check(!OnlineProtocol.TryState(Alter(empty, offset, BitConverter.GetBytes(float.NaN)), out _), "Nonfinite state field" + offset + " rejected");
            Check(!OnlineProtocol.TryState(Alter(empty, 96, 255), out _), "Invalid player charging enum is rejected");
            Check(!OnlineProtocol.TryState(Alter(empty, 101, 255), out _), "Invalid player last-kick enum is rejected");
            Check(!OnlineProtocol.TryState(Alter(empty, 102, 2), out _), "Invalid player celebration boolean is rejected");
            Check(!OnlineProtocol.TryState(Alter(empty, 88, BitConverter.GetBytes(1.01f)), out _), "Excessive charge is rejected");
            Check(!OnlineProtocol.TryState(Alter(empty, 92, BitConverter.GetBytes(-1.01f)), out _), "Excessive player curve is rejected");
            byte[] badUtf8 = (byte[])unicode.Clone(); badUtf8[35] = 255;
            Check(!OnlineProtocol.TryState(badUtf8, out _), "Invalid UTF8 message is rejected without throwing");

            using (var writer = new FastBufferWriter(input.Length, Allocator.Temp))
            {
                writer.WriteBytesSafe(input);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    byte[] actual = OnlineProtocol.Read(reader, OnlineProtocol.InputBytes);
                    Check(actual.SequenceEqual(input), "NGO FastBufferReader copies the explicit payload exactly");
                }
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                    Check(OnlineProtocol.Read(reader, OnlineProtocol.InputBytes - 1) == null, "NGO buffer rejects oversized message before allocation");
            }
            File.AppendAllText(Report, "COMPLETE " + checks + " codec checks passed. Worker lifecycle/security requires Linux integration tests.\n");
            return File.ReadAllText(Report);
        }

        private static NetworkPlayerState Player(ulong client, Vector2 position) => new NetworkPlayerState
        {
            ClientId = client, Position = position, Velocity = Vector2.one, Angle = 45, Charge = 0.5f, Curve = -0.2f,
            Charging = PlayerKicker.KickType.Shot, KickSerial = 7, LastKick = PlayerKicker.KickType.Pass, Celebrating = true
        };
    }
}
