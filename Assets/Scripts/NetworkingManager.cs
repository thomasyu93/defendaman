﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using System.Runtime.InteropServices;

/*Carson
Being used to denote what type of data we are sending/receiving for a given JSON object.
e.g. Player is valued at 1. If we receive a JSON object for type Player ID 1, that is "Player 1's" data.
     Projectile is defined at 2. If we receive a JSON object for type Projectile ID 3, that is "Projectile 3's" data
    
Enviroment does not have an ID associated with it, since it is one entity. The ID we use for it will always default to 0

Note: Does not start at value 0. Reason being, if JSON parser fails, it returns 0 for fail, so checking
for fail does not work 
*/
public enum DataType {
    Player = 1, Projectile = 2, Environment = 3
}

/*Carson
Class used for handling sending/receiving data. The class has 2 uses:
* To send/receive data from the Networking Team's clientside code, and
* Notifying subscribed objects when new data is updated

To subscribe for an objects updates from server, you would call the public Subscribe method.
This method takes in three things:
    Callback method, which is a void method that takes in a JSONClass as a parameter
    DataType you want to receive, e.g. DataType.Player for data of a player
    int ID of which of the DataType you want to receive info from, e.g. ID 1 on DataType.Player is Player 1's data

e.g. NetworkingManager.Subscribe((JSONClass json) => {Debug.Log("Got Player 1's Data");}, DataType.Player, 1);
*/
public class NetworkingManager : MonoBehaviour {
    // Update is called once per frame
    void Update() {
        update_data(receive_data());
        send_data();
    }

    ////Code for subscribing to updates from client-server system////
    #region SubscriberSystem

    //Holds the subscriber data
    private static Dictionary<Pair<DataType, int>, List<Action<JSONClass>>> _subscribedActions = new Dictionary<Pair<DataType, int>, List<Action<JSONClass>>>();
    /*
    To subscribe for an objects updates from server, you would call the public Subscribe method.
    This method takes in three things:
    Callback method, which is a void method that takes in a JSONClass as a parameter
    DataType you want to receive, e.g. DataType.Player for data of a player
    int ID of which of the DataType you want to receive info from, e.g. ID 1 on DataType.Player is Player 1's data

    e.g. NetworkingManager.Subscribe((JSONClass json) => {Debug.Log("Got Player 1's Data");}, DataType.Player, 1);
    */
    public static void Subscribe(Action<JSONClass> callback, DataType dataType, int id = 0) {
        Pair<DataType, int> pair = new Pair<DataType, int>(dataType, id);

        if (!(_subscribedActions.ContainsKey(pair))) {
            _subscribedActions.Add(pair, new List<Action<JSONClass>>());
        }
        List<Action<JSONClass>> val = null;
        _subscribedActions.TryGetValue(pair, out val);
        if (val != null)
        {
            //Add our callback to the list of entries under that pair of datatype and ID.
            _subscribedActions[pair].Add(callback);
        }
    }

    private void update_data(string JSONGameState) {
        var gameObjects = JSON.Parse(JSONGameState).AsArray;
        foreach (var node in gameObjects.Children) {
            var obj = node.AsObject;
            int dataType = obj["DataType"].AsInt;
            int id = obj["ID"].AsInt;

            if (id != 0 || dataType == (int)DataType.Environment) {
                Pair<DataType, int> pair = new Pair<DataType, int>((DataType)dataType, id);
                if (_subscribedActions.ContainsKey(pair)) {
                    foreach (Action<JSONClass> callback in _subscribedActions[pair]) {
                        callback(obj);
                    }
                }
            }
        }
    }

    #endregion

    ////Code for communicating with client-server system////
    #region CommunicationWithClientSystem
    // Game object to send data of
    public GameObject player;

    // Imported function from C++ library for receiving data
    [DllImport("NetworkingLibrary.so")]
    private static extern IntPtr receiveData();

    // Imported function from C++ library for sending data
    [DllImport("NetworkingLibrary.so")]
    private static extern void sendData(string data);

    //On Linux, send data to C++ file
    private void send_data() {
        if (Application.platform == RuntimePlatform.LinuxPlayer)
            sendData(create_sending_json());
    }

    //Receive a packet from C++ networking client code
    private string receive_data() {
        //On Linux, get a proper packet
        if (Application.platform == RuntimePlatform.LinuxPlayer)
            result = Marshal.PtrToStringAnsi(receiveData());
        else
            //On Windows, return whatever JSON data we want to generate/test for
            result = create_test_json();
        return result;
    }

    //Generate the JSON file to send to C++ networking client code
    private string create_sending_json() {
        //Open JSON array
        string sending = "[";

        //Add player data
        var memberItems = new List<Pair<string, string>>();
        memberItems.Add(new Pair<string, string>("x", player.transform.position.x.ToString()));
        memberItems.Add(new Pair<string, string>("y", player.transform.position.y.ToString()));
        send_next_packet(DataType.Player, 1, memberItems);

        //Add data that external sources want to send
        foreach (var item in jsonObjectsToSend)
            sending += item;
        jsonObjectsToSend.Clear();

        //Close json array
        sending = sending.Remove(sending.Length - 1, 1);
        sending += "]";
        return sending;
    }

    //Add data to be sent
    private static List<string> jsonObjectsToSend = new List<string>();
    public static void send_next_packet(DataType type, int id, List<Pair<string, string>> memersToSend) {
        string sending = "";
        sending = "{";

        foreach(var pair in memersToSend) {
            sending += " \"" + pair.first + "\" : " + pair.second + ",";
        }

        sending = sending.Remove(1, 1);
        sending = sending.Remove(sending.Length - 1, 1);
        sending += "},";
        jsonObjectsToSend.Add(sending);
    }

    #endregion

    ////Code for Carson's testing purposes////
    #region DummyTestingCode

    //Dummy data for the sake of testing.
    float _x = -15, _y = -10;
    string result = "receiving failed";

    void OnGUI() {
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), result);
        GUI.Label(new Rect(0, 20, Screen.width, Screen.height), create_sending_json());
    }

    string create_test_json() {
        return "[]";
    }

    #endregion
}
