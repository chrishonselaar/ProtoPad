using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;

namespace ServiceDiscovery
{
    public class UdpDiscoveryClient
    {
        public const int UdpClientPort = 5354; // TODO: arbitrary value for now - update to use automatic free port finder?        
        public const string UdpBroadcastMaskAddress = "192.168.1.255"; // TODO: this should work in most scenarios

        public delegate void FoundHandler(string foundServerName, string foundEndPointAddress);
        public delegate void ReadyHandler(bool ready);
        private readonly ReadyHandler _readyHandler;
        private readonly FoundHandler _foundHandler;

        public UdpDiscoveryClient(ReadyHandler readyHandler, FoundHandler foundHandler)
        {
            _readyHandler = readyHandler;
            _foundHandler = foundHandler;            
        }

        private void ReceiveServerPingCallback(IAsyncResult ar)
        {
            try
            {
                var state = (UdpState)(ar.AsyncState);
                var client = state.client;
                var endPoint = state.endPoint;
                var receiveBytes = client.EndReceive(ar, ref endPoint);
                var str = Encoding.UTF8.GetString(receiveBytes);
                var data = str.Split('|');
                var serviceName = data[1];
                var endPointAddress = data[0];
                Debug.WriteLine("Name: {0} Endpoint: {1}", serviceName, endPointAddress);
                _foundHandler(serviceName, endPointAddress);
                client.BeginReceive(ReceiveServerPingCallback, state);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error processing UDP ping callback: {0}", e.Message);
            }
        }

        /// <summary>
        /// Initiate listening for server pings and set a timeout
        /// </summary>
        private void StartListeningForServerPingBacks()
        {
            _readyHandler(false);            

            // Listen on our IP addresses on our port
            var endPoint = new IPEndPoint(Helpers.GetCurrentIPAddress(), UdpClientPort);
            
            // Make sure we don't grab exclusive "rights" to our address so we can use the same port for send and receive.
            var udpClient = new UdpClient();
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.ExclusiveAddressUse = false;
            udpClient.Client.Bind(endPoint);

            // Setup our "state" object so the callback has access to the client and endpoint
            var state = new UdpState(endPoint, udpClient);

            // Setup our async receive
            // Our callback will be called if and when data comes in
            udpClient.BeginReceive(ReceiveServerPingCallback, state);

            // Setup a timeout timer. When the timer elapses we invoke the 'ready' callback and close our udpclient.
            var enableTimer = new Timer(2000);
            enableTimer.Elapsed += (e, o) =>
                {
                    _readyHandler(true);
                    udpClient.Close();
                };
            enableTimer.AutoReset = false;
            enableTimer.Enabled = true;
        }

        public void SendServerPing()
        {
            StartListeningForServerPingBacks();

            // Create a socket udp ipv4 socket
            using (var sock = new Socket(AddressFamily.Unspecified, SocketType.Dgram, ProtocolType.Udp))
            {
                // Create our endpoint using the IP broadcast address and our port
                var endPoint = new IPEndPoint(IPAddress.Parse(UdpBroadcastMaskAddress), UdpDiscoveryServer.UdpServerPort);

                // Serialize our ping "payload"
                var data = Encoding.UTF8.GetBytes(String.Format("{0}:{1}", Helpers.GetCurrentIPAddress(), UdpClientPort));

                // Tell our socket to reuse the address so we can send and receive on the same port.
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                // Tell the socket we want to broadcast - if we don't do this it won't let us send.
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

                // Send the ping and close the socket.
                sock.SendTo(data, endPoint);
                sock.Close();
            }
        }

        private class UdpState
        {
            public readonly IPEndPoint endPoint;
            public readonly UdpClient client;
            public UdpState(IPEndPoint endPoint, UdpClient client)
            {
                this.endPoint = endPoint;
                this.client = client;
            }
        }
    }
}