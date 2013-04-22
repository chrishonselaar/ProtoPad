using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ServiceDiscovery
{
    public class UdpDiscoveryServer: IDisposable
    {
        public const int UdpServerPort = 5353; // TODO: arbitrary value for now - update to use automatic free port finder?

        private readonly string _respondWithServerName;
        private readonly string _respondWithEndpointAddress;
        private readonly IPAddress _broadCastAddress;
        private readonly UdpClient _udpClient;

        public UdpDiscoveryServer(string respondWithServerName, string respondWithEndpointAddress, IPAddress broadCastAddress = null)
        {
            _respondWithServerName = respondWithServerName;
            _respondWithEndpointAddress = respondWithEndpointAddress;
            _broadCastAddress = broadCastAddress ?? IPAddress.Any;
            _udpClient = new UdpClient();
            ListenUdp();
        }

        private void ListenUdp()
        {
            var broadcastAddress = new IPEndPoint(_broadCastAddress, UdpServerPort);
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.ExclusiveAddressUse = false;
            _udpClient.Client.Bind(broadcastAddress);
            var udpState = new UdpState { EndPoint = broadcastAddress };
            _udpClient.BeginReceive(ReceiveCallback, udpState);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            var state = (UdpState)(ar.AsyncState);
            var receiveBytes = _udpClient.EndReceive(ar, ref state.EndPoint);
            var data = Encoding.UTF8.GetString(receiveBytes).Split(':');
            var clientIpAddress = IPAddress.Parse(data[0]);
            var clientPort = int.Parse(data[1]);
            SendServerPingBack(clientIpAddress, clientPort);
            _udpClient.BeginReceive(ReceiveCallback, state);
        }

        private void SendServerPingBack(IPAddress clientIpAddress, int clientPort)
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                var endPoint = new IPEndPoint(clientIpAddress, clientPort);
                var data = Encoding.UTF8.GetBytes(String.Format("{0}|{1}", _respondWithEndpointAddress, _respondWithServerName));
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                socket.SendTo(data, endPoint);
                socket.Close();

                Debug.WriteLine("Sent: {0} bytes, {1}, {2}", data.Length, _respondWithEndpointAddress, _respondWithServerName);
            }
        }

        class UdpState
        {
            public IPEndPoint EndPoint;
        }

        public void Dispose()
        {
            if (_udpClient == null) return;
            _udpClient.Close();
        }
    }
}