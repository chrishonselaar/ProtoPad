using System.Net.Sockets;
using System.Net;

namespace ServiceDiscovery
{
    public static class Helpers
    {
        public static IPAddress GetCurrentIPAddress()
        {
            IPAddress[] localIPs = null;

            // Try with GetHostName, add .local if that fails
            // We need to do this as there seems to be a bug / inconsistency
            // between the simulator and the device itself
            try
            {
                localIPs = Dns.GetHostAddresses(Dns.GetHostName());
            }
            catch (SocketException)
            {
            }

            if (localIPs == null)
            {
                try
                {
                    localIPs = Dns.GetHostAddresses(Dns.GetHostName() + ".local");
                }
                catch (SocketException)
                {
                    // fallback to loopback
                    return IPAddress.Loopback;
                }
            }

            foreach (var localIP in localIPs)
            {
                if ((localIP.AddressFamily == AddressFamily.InterNetwork) && (!IPAddress.IsLoopback(localIP)) && (localIP.ToString().StartsWith("192.168.1.")))
                    return localIP;
            }

            // Work through the IPs reported and return the first one
            // that is IPv4 and not a loopback
            foreach (var localIP in localIPs)
            {
                if ((localIP.AddressFamily == AddressFamily.InterNetwork) && (!IPAddress.IsLoopback(localIP)))
                    return localIP;
            }

            // Fallback to loopback if necessary
            return IPAddress.Loopback;
        }
    }
}