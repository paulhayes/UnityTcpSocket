﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace BeaconLib
{
    /// <summary>
    /// Counterpart of the beacon, searches for beacons
    /// </summary>
    /// <remarks>
    /// The beacon list event will not be raised on your main thread!
    /// </remarks>
    public class Probe : IDisposable
    {
        /// <summary>
        /// Remove beacons older than this
        /// </summary>
        private static readonly TimeSpan BeaconTimeout = new TimeSpan(0, 0, 0, 5); // seconds

        public event Action<IEnumerable<BeaconLocation>> BeaconsUpdated;

        private Thread thread;
        private readonly EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
        private readonly UdpClient udp = new UdpClient();
        private IEnumerable<BeaconLocation> currentBeacons = Enumerable.Empty<BeaconLocation>();

        private bool running = true;

        public Probe(string beaconType)
        {
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            BeaconType = beaconType;

            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
#if false
            try 
            {
                udp.AllowNatTraversal(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error switching on NAT traversal: " + ex.Message);
            }
#endif
            udp.BeginReceive(ResponseReceived, null);
        }

        public void Start()
        {
          if(thread==null){
            thread = new Thread(BackgroundLoop) { IsBackground = true };
          }
          running = true;
          if(!thread.IsAlive)
            thread.Start();
            
        }

        private void ResponseReceived(IAsyncResult ar)
        {
            //UnityEngine.Debug.Log("ResponseReceived");
            var remote = new IPEndPoint(IPAddress.Any, 0);
            var bytes = udp.EndReceive(ar, ref remote);

            var typeBytes = Beacon.Encode(BeaconType).ToArray();
            
            if (Beacon.HasPrefix(bytes, typeBytes))
            {
                try
                {
                    var portBytes = bytes.Skip(typeBytes.Count()).Take(2).ToArray();
                    var port      = (ushort)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(portBytes, 0));
                    var payload   = Beacon.Decode(bytes.Skip(typeBytes.Count() + 2));
                    NewBeacon(new BeaconLocation(new IPEndPoint(remote.Address, port), payload, DateTime.Now));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }

            udp.BeginReceive(ResponseReceived, null);
        }

        public string BeaconType { get; private set; }

        private void BackgroundLoop()
        {
            while (running)
            {
                try
                {
                    BroadcastProbe();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }

                waitHandle.WaitOne(2000);
                PruneBeacons();
            }
        }

        private void BroadcastProbe()
        {
          //UnityEngine.Debug.Log("Sending BroadcastProbe");
            var probe = Beacon.Encode(BeaconType).ToArray();
            udp.Send(probe, probe.Length, new IPEndPoint(IPAddress.Broadcast, Beacon.DiscoveryPort));
        }

        private void PruneBeacons()
        {
            var cutOff = DateTime.Now - BeaconTimeout;
            var oldBeacons = currentBeacons.ToList();
            var newBeacons = oldBeacons.Where(_ => _.LastAdvertised >= cutOff).ToList();
            if (EnumsEqual(oldBeacons, newBeacons)) return;

            var u = BeaconsUpdated;
            if (u != null) u(newBeacons);
            currentBeacons = newBeacons;
        }

        private void NewBeacon(BeaconLocation newBeacon)
        {
            var newBeacons = currentBeacons
                .Where(_ => !_.Equals(newBeacon))
                .Concat(new [] { newBeacon })
                .OrderBy(_ => _.Data)
                .ThenBy(_ => _.Address, IPEndPointComparer.Instance)
                .ToList();
            var u = BeaconsUpdated;
            if (u != null) u(newBeacons);
            currentBeacons = newBeacons;
        }

        private static bool EnumsEqual<T>(IEnumerable<T> xs, IEnumerable<T> ys)
        {
            return xs.SequenceEqual(ys);
        }

        public void Stop()
        {
            running = false;
            waitHandle.Set();
            if(thread!=null){
              thread.Join();                    
              thread = null;
            }
            
        }

        public void Dispose()
        {
            try
            {
                Stop();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }
}
