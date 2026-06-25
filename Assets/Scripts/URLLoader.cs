using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class URLLoader : MonoBehaviour
{
    public string url;
    public void urlassign()
    {
        OpenURL(url);
    }
    public void OpenURL(string url)
    {
        //Application.OpenURL(url);
    }
}
