using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SocketCommunication {

public class CommsTest : MonoBehaviour 
{
    [SerializeField]
    ControlAppServer server;

    [SerializeField]
    ControlAppClient client;

    void Start()
    {
        server.Init();

        client.Init();    
    }

    void OnDisable()
    {
        server.Stop();    
        client.Stop();
    }

    
}

}