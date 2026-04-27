using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnbleErrorPanel : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject numpad;
    public GameObject enter;
    private void OnEnable()
    {
        StartCoroutine(EnablePanle());
    }
    IEnumerator EnablePanle()
    {
        numpad.SetActive(false);
        yield return new WaitForSeconds(3f);
        gameObject.SetActive(false);
        numpad.SetActive(true);
        enter.SetActive(true);
    }
}
