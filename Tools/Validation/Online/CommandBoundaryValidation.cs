using System;
using System.IO;
using System.Threading.Tasks;
using PointPass.Gameplay;
using PointPass.Networking;
using PointPass.Players;
using PointPass.Server;
using UnityEngine;

namespace PointPass.Validation
{
    /// <summary>Engine-level command ownership and cancellation checks, using explicit local fixtures.
    /// This is not a network or two-client integration test.</summary>
    public static class CommandBoundaryValidation
    {
        private static string Report => Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "Logs/OnlinePilotVerification/command-boundary-tests.txt");
        private static int checks;

        private static void Check(bool condition, string description)
        {
            File.AppendAllText(Report, (condition ? "PASS " : "FAIL ") + description + "\n");
            if (!condition) throw new InvalidOperationException(description);
            checks++;
        }

        private static async Task Wait(Func<bool> condition, float seconds, string description)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && watch.Elapsed.TotalSeconds < seconds) await Task.Delay(20);
            Check(condition(), description);
        }

        public static async Task<string> Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, "Authoritative controller command boundaries (engine fixtures, not internet validation)\n");
            checks = 0;
            float savedScale = Time.timeScale;
            MatchManager[] previous = UnityEngine.Object.FindObjectsByType<MatchManager>(FindObjectsSortMode.None);
            foreach (MatchManager match in previous) match.gameObject.SetActive(false);
            ServerMatchWorld world = null;
            try
            {
                Time.timeScale = 1;
                world = new ServerMatchWorld(101, 202);
                await Wait(() => world.Simulation.IsMatchLive, 4, "Normal kickoff reaches authoritative play");
                PlayerController blue = world.Players[0], red = world.Players[1];
                blue.GetComponent<PlayerMovement>().ResetTo(new Vector2(-0.64f, 0), -90);
                red.GetComponent<PlayerMovement>().ResetTo(new Vector2(5, -4), 90);
                world.Ball.ResetTo(Vector2.zero);
                Physics2D.SyncTransforms();
                await Wait(() => world.Possession.Owner == blue, 1, "Fixture grants Blue the ball through physical pickup");

                Check(!red.BeginKick(PlayerKicker.KickType.Shot) && !red.BeginKick(PlayerKicker.KickType.Pass),
                    "Non-owner Red cannot begin either kick type");
                var unauthorized = new NetworkInputCommand { Sequence = 1, Aim = Vector2.left, Action = NetworkKickAction.BeginShot };
                unauthorized.Apply(red);
                Check(red.ChargingKick == PlayerKicker.KickType.None && world.Possession.Owner == blue,
                    "A routed non-owner command cannot start a charge or steal possession");

                Check(blue.BeginKick(PlayerKicker.KickType.Shot), "Ball owner begins a charged shot");
                await Task.Delay(180);
                float charge = blue.KickCharge;
                Check(charge > 0 && !blue.BeginKick(PlayerKicker.KickType.Pass) && blue.ChargingKick == PlayerKicker.KickType.Shot,
                    "A second kick action cannot replace the first charge");
                blue.ReleaseKick(PlayerKicker.KickType.Pass);
                Check(blue.ChargingKick == PlayerKicker.KickType.Shot && blue.KickCharge >= charge,
                    "Releasing the wrong kick button leaves the original charge intact");
                red.CancelActions(); red.ReleaseKick(PlayerKicker.KickType.Shot);
                Check(blue.ChargingKick == PlayerKicker.KickType.Shot && world.Possession.Owner == blue,
                    "Another player's cancel/release does not alter the owner's charge or possession");

                uint serial = world.Capture(1).Blue.KickSerial;
                blue.ReleaseKick(PlayerKicker.KickType.Shot);
                blue.CancelActions();
                Time.timeScale = 0;
                await Task.Delay(120);
                Time.timeScale = 1;
                await Task.Delay(180);
                Check(world.Capture(2).Blue.KickSerial == serial && world.Possession.Owner == blue,
                    "Cancelling a queued release before a disconnect pause prevents a delayed physical kick on resume");

                Check(blue.BeginKick(PlayerKicker.KickType.Shot), "Owner can charge again after cancellation");
                await Task.Delay(120);
                blue.Dribbler.AutomaticPickup = false;
                world.Possession.Release(blue);
                Check(blue.ChargingKick == PlayerKicker.KickType.None && blue.KickCharge == 0,
                    "Losing possession immediately clears charge and power");
                blue.ReleaseKick(PlayerKicker.KickType.Shot);
                await Task.Delay(150);
                Check(world.Capture(3).Blue.KickSerial == serial,
                    "A stale release after possession loss cannot execute a kick");

                blue.Dribbler.AutomaticPickup = true;
                world.Ball.ResetTo(Vector2.zero);
                blue.GetComponent<PlayerMovement>().ResetTo(new Vector2(-0.64f, 0), -90);
                Physics2D.SyncTransforms();
                await Wait(() => world.Possession.Owner == blue, 1, "Owner can recover the loose ball after the pickup cooldown");
                Check(blue.BeginKick(PlayerKicker.KickType.Pass), "Owner can begin a normal pass after stale release rejection");
                await Task.Delay(200);
                blue.ReleaseKick(PlayerKicker.KickType.Pass);
                blue.ReleaseKick(PlayerKicker.KickType.Pass);
                await Wait(() => world.Capture(4).Blue.KickSerial == serial + 1, 1,
                    "Duplicate release executes exactly one physical pass");
                Check(world.Ball.LastKickWasPass && world.Ball.Body.linearVelocity.x > 10,
                    "The accepted pass retains normal physical velocity and reception metadata");

                File.AppendAllText(Report, "COMPLETE " + checks + " command-boundary checks passed. Server transport and live clients remain separate checks.\n");
                return File.ReadAllText(Report);
            }
            finally
            {
                world?.Dispose();
                Time.timeScale = savedScale;
                foreach (MatchManager match in previous) if (match != null) match.gameObject.SetActive(true);
            }
        }
    }
}
