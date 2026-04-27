using SimpleJSON;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestAPI : MonoBehaviour
{
    public string json;
    private API api;  

    private void Start()
    {
        api = GameManager.Instance.api;
        ParseData();
       
    }
    public void ParseData()
    {
        var spaceData = JSONNode.Parse(json);
      //  api.DummyTest(json);
        DebugUtil.Log("hi");
    }
}
