using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using Klass.Net;

public class ContinuousInternetCheck : MonoBehaviour
{
    // Time interval in seconds to check the internet connection.
    private float checkInterval = 10f; // Adjust the interval as needed.
    public GameObject internteCanva;
    void Start()
    {
        // Start checking the internet connection at regular intervals.
        InvokeRepeating(nameof(CheckInternetConnection), 0f, checkInterval);
    }

    private void CheckInternetConnection()
    {
        StartCoroutine(CheckInternetConnectionCoroutine());
    }

    private IEnumerator CheckInternetConnectionCoroutine()
    {
        using UnityWebRequest request = UnityWebRequest.Head(KlassEndpoints.BackendBase);
        request.timeout = 5;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            DebugUtil.Log("Klever service is unreachable: " + request.error);
            if (internteCanva != null)
                internteCanva.SetActive(true);
        }
        else
        {
            DebugUtil.Log("Klever service is reachable.");
            if (internteCanva != null)
                internteCanva.SetActive(false);
        }
    }
}
