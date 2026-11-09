using System;
using System.IO;
using System.Reflection;
using PointPass.Online;

namespace PointPass.Validation
{
    /// <summary>Pure bounded-reader checks. No invitation is minted, logged or saved.</summary>
    public static class InvitationPipeValidation
    {
        public static string Run()
        {
            var read = typeof(OnlinePilotTestDriver).GetMethod("ReadPrivateInvitation", BindingFlags.Static | BindingFlags.NonPublic);
            if (read == null) throw new InvalidOperationException("Private invitation reader is unavailable in this development build.");
            int checks = 0;
            string Sample(string source)
            {
                using (var input = new StringReader(source)) return (string)read.Invoke(null, new object[] { input });
            }
            void Accept(string source, string expected)
            {
                if (Sample(source) != expected) throw new InvalidOperationException("Bounded private-input acceptance check failed.");
                checks++;
            }
            void Reject(string source)
            {
                try { Sample(source); }
                catch (TargetInvocationException failure) when (failure.InnerException is InvalidDataException) { checks++; return; }
                throw new InvalidOperationException("Bounded private-input rejection check failed.");
            }
            string synthetic = new string('A', 43);
            Accept(synthetic + "\n", synthetic);
            Accept(synthetic + "\r\n", synthetic);
            Accept(synthetic, synthetic);
            Accept("\n", "");
            Accept("", "");
            Accept(synthetic + "\nignored-second-line", synthetic);
            Reject(new string('A', 44) + "\n");
            Reject(new string('A', 42) + "\n");
            Reject(" " + synthetic + "\n");
            Reject(new string('A', 42) + "!\n");
            Reject(synthetic + "\rX");
            return "PASS " + checks + " bounded stdin checks; no real invitation or network access used.";
        }
    }
}
