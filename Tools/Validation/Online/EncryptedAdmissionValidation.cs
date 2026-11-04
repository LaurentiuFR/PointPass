using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using PointPass.Online;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace PointPass.Validation
{
    /// <summary>Uses no invitation or real ticket. Proves TLS validation and rejected admission separately.</summary>
    public static class EncryptedAdmissionValidation
    {
        private static string Report => Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "Logs/OnlinePilotVerification/encrypted-admission-tests.txt");

        public static async Task<string> Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            if (NetworkManager.Singleton != null) throw new InvalidOperationException("Leave existing network sessions first.");
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, "Real Windows Unity/NGO encrypted transport to the deployed Linux worker. No live credentials used.\n");
            await Probe("62.238.115.207", true);
            await Probe("wrong-pointpass.invalid", false);
            await Probe("62.238.115.207", true);
            File.AppendAllText(Report, "COMPLETE Correct IP trust, wrong-name rejection and post-rejection recovery passed. Authenticated gameplay remains a separate test.\n");
            return File.ReadAllText(Report);
        }

        private static async Task Probe(string tlsName, bool expectAdmissionResponse)
        {
            var root = new GameObject("Encrypted admission validation");
            UnityTransport transport = root.AddComponent<UnityTransport>();
            transport.UseEncryption = true;
            transport.SetClientSecrets(tlsName);
            transport.SetConnectionData("62.238.115.207", 7777);
            NetworkManager manager = root.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false,
                ForceSamePrefabs = false, ConnectionApproval = true, TickRate = 30, ClientConnectionBufferTimeout = 8,
                ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new OnlineApiContracts.Approval { ticket = new string('A', 43) })) };
            bool connected = false, stopped = false;
            string reason = null;
            manager.OnClientConnectedCallback += _ => connected = true;
            manager.OnClientDisconnectCallback += _ => { reason = manager.DisconnectReason; stopped = true; };
            manager.OnTransportFailure += () => stopped = true;
            try
            {
                if (!manager.StartClient()) throw new InvalidOperationException("Probe could not start its client transport.");
                var watch = System.Diagnostics.Stopwatch.StartNew();
                while (!stopped && !connected && watch.Elapsed.TotalSeconds < 15) await Task.Delay(20);
                bool applicationRejected = !string.IsNullOrEmpty(reason) && reason.StartsWith("Ticket expired,", StringComparison.Ordinal);
                if (connected) throw new InvalidOperationException("The server accepted an invalid ticket.");
                if (expectAdmissionResponse && !applicationRejected)
                    throw new InvalidOperationException("The trusted encrypted connection did not reach the expected admission rejection.");
                if (!expectAdmissionResponse && applicationRejected)
                    throw new InvalidOperationException("A mismatched TLS server name reached the application.");
                File.AppendAllText(Report, expectAdmissionResponse
                    ? "PASS Trusted IP certificate reaches authenticated transport; invalid single-use ticket is rejected by the Linux API/worker.\n"
                    : "PASS Wrong TLS server name cannot reach application admission or connect; normal certificate validation remains enabled.\n");
            }
            finally
            {
                manager.Shutdown(true);
                var shutdown = System.Diagnostics.Stopwatch.StartNew();
                while ((manager.ShutdownInProgress || manager.IsListening) && shutdown.Elapsed.TotalSeconds < 5) await Task.Delay(20);
                UnityEngine.Object.Destroy(root);
                await Task.Delay(100);
            }
        }
    }
}
