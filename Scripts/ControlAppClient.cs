using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using BeaconLib;
using UnityEngine;

namespace SocketCommunication {

[CreateAssetMenu]
public class ControlAppClient : MessageEmitter
{
  [SerializeField] int maxMessagesInQueue = 20;

  Probe probe;

  Queue<Message> messages = new Queue<Message>();
  object queueLock = new object();

  Queue<Message> sendMessageQueue = new Queue<Message>();
  object sendQueueLock = new object();
  
  Thread clientThread;

  EventWaitHandle waitForServerAddress = new EventWaitHandle(false,EventResetMode.AutoReset);

  IPEndPoint serverAddress;

  byte[] tmpReadBytes = new byte[4096];

  volatile bool running;

  public HierarchicalLogger discoveryLogs;
  public HierarchicalLogger sendLogs;
  public HierarchicalLogger receiveLogs;


  public void Init()
  {
    running = true;
    
    // Event is raised on separate thread so need synchronization
    
    clientThread = new Thread(ClientLoop);
    clientThread.Start();

  }

  void StartProbe()
  {
    if(probe!=null){
      probe.BeaconsUpdated -= OnBeacons;
    }
    if(discoveryLogs)
      discoveryLogs.Log(HierarchicalLogger.Info, "Starting Probe");

    probe = new Probe("control-app");
    probe.Start();
    probe.BeaconsUpdated += OnBeacons;
  }

  public override bool HasQueuedMessages(){
    return messages.Count>0;
  }

  public override Message PopMessage()
  {
    if(HasQueuedMessages()){
      lock (queueLock){
        return messages.Dequeue();
      }
    }
    else {
      return null;
    }
  }

  public void Stop()
  {
    if(probe!=null){
      probe.BeaconsUpdated -= OnBeacons;
      probe.Stop();
    }
    running = false;    
  }

  public void Send(Message message){
    lock(sendQueueLock){
      sendMessageQueue.Enqueue(message);
      if(sendMessageQueue.Count>maxMessagesInQueue){
        sendMessageQueue.Dequeue();        
      }
    }
  }

  private void OnBeacons(IEnumerable<BeaconLocation> beacons)
  {
    IPEndPoint address = null; 
    foreach(var beacon in beacons){
      discoveryLogs.LogFormat(HierarchicalLogger.Info, "Found beacon @ {0}",beacon.Address);
      address = beacon.Address;
    }
    
    if(address==null)
      return;

    //probe.BeaconsUpdated -= OnBeacons;
    probe.Stop();
    
    lock(sendQueueLock){
      serverAddress = address; 
    }
    waitForServerAddress.Set();
  }

  private void ClientLoop(object obj)
  {
    StartProbe();
    TcpClient client = null;
    
    try{
      while(running){
      
      if( client==null || !client.Connected || !client.Client.Connected ){
        IPEndPoint address;
        lock(sendQueueLock){
          address = serverAddress;
        }
        if(address==null){
            if(discoveryLogs)
              discoveryLogs.LogFormat(HierarchicalLogger.Info,"Waiting for server discovery");
            waitForServerAddress.WaitOne();
            if(discoveryLogs)
              discoveryLogs.LogFormat(HierarchicalLogger.Info,"Conntect to {0}",serverAddress);

            //Thread.Sleep(1000);
            continue;
        }          

        if( client!=null )
          client.Close();

        client = new TcpClient();
        try {
          if(discoveryLogs)
            discoveryLogs.LogFormat(HierarchicalLogger.Info,"Attempting connect");
        
          client.Connect(address);
          client.NoDelay = true;
        }
        catch(SocketException e){
          //connect failed
          if(discoveryLogs){
            discoveryLogs.Log(HierarchicalLogger.Error,e.ToString());
          }
          client = null;
          lock(sendQueueLock){
            serverAddress = null;
          }
          
          try{ 
            discoveryLogs.Log(HierarchicalLogger.Info,"restarting probe");
            StartProbe();
          }
          catch(ThreadStateException e2){
            if(discoveryLogs){
              discoveryLogs.Log(HierarchicalLogger.Error,e2.ToString());
            }
            //probe thread was already running?
          }
          
        }
        Thread.Sleep(100);
        continue;
      }
      NetworkStream stream = client.GetStream();

      try{
        while( stream.CanWrite && sendMessageQueue.Count > 0 ){
          sendLogs.Log(HierarchicalLogger.Info,"sending message");
          lock(sendQueueLock){
            var data = sendMessageQueue.Dequeue().Data;
            stream.Write( data, 0, data.Length );            
          }
        }
        
      }
      catch(IOException e){
        discoveryLogs.Log(HierarchicalLogger.Error,e.ToString());        
        continue;
      }

      //Log("client checking for messages");
      int len=0;
      
      if( stream.CanRead && stream.DataAvailable && 0!=(len=stream.Read(tmpReadBytes,0,tmpReadBytes.Length)) ){
        if(receiveLogs)
          receiveLogs.Log(HierarchicalLogger.Info, "reading message");
        int index = 0;
        if(receiveLogs)
          receiveLogs.Log(HierarchicalLogger.Verbose, System.Text.Encoding.ASCII.GetString(tmpReadBytes,0,len));
        Message.FromStream(tmpReadBytes, ref index, len, messages);
      }
      //Log("client sleeping");
      Thread.Sleep(5);
    }
    }
    catch(ThreadAbortException e){
      return;
    }
    catch(Exception e){
      if(receiveLogs)
          receiveLogs.Log(HierarchicalLogger.Error, e.ToString());
    }

    if(client!=null && client.Connected){
      client.Close();
    }
  }
  
}

}