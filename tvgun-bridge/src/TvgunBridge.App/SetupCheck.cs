using System.Diagnostics;

namespace TvgunBridge.App;

/// <summary>
/// First-run environment self-check: verifies the HttpListener URL ACL reservation
/// and the admin-created firewall rules that setup-admin-once.bat installs.
/// Read-only; returns machine-readable <c>SETUP_MISSING ...</c> log lines and never
/// throws. Elevation itself is the launcher script's job, not the bridge's.
/// </summary>
public static class SetupCheck
{
    /// <summary>Checks urlacl and firewall rules for the given service port (discovery is port + 1).</summary>
    public static IReadOnlyList<string> Check(int port)
    {
        var missing = new List<string>();
        var prefix = $"http://+:{port}/";

        var urlacl = RunNetsh("http show urlacl");
        if (urlacl is null)
        {
            missing.Add("SETUP_MISSING item=urlacl reason=check_failed");
        }
        else if (!urlacl.Contains(prefix, StringComparison.OrdinalIgnoreCase))
        {
            missing.Add($"SETUP_MISSING urlacl={prefix}");
        }

        var firewallFailed = false;
        foreach (var rule in new[] { $"tvgun-bridge TCP {port}", $"tvgun-bridge UDP {port}", $"tvgun-bridge UDP {port + 1}" })
        {
            var rules = RunNetsh($"advfirewall firewall show rule name=\"{rule}\"");
            if (rules is null)
            {
                firewallFailed = true;
                break;
            }

            if (!rules.Contains(rule, StringComparison.OrdinalIgnoreCase))
            {
                missing.Add($"SETUP_MISSING firewall_rule=\"{rule}\"");
            }
        }

        if (firewallFailed)
        {
            missing.Add("SETUP_MISSING item=firewall reason=check_failed");
        }

        return missing;
    }

    private static string? RunNetsh(string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo("netsh", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(10000))
            {
                try
                {
                    process.Kill();
                }
                catch (Exception)
                {
                    // Best effort.
                }

                return null;
            }

            process.WaitForExit(); // flush the async pipe readers
            _ = stderr;
            return stdout.GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
