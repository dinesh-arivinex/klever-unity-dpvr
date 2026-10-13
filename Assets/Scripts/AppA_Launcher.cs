using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class AppA_Launcher : MonoBehaviour
{
    // Set this in the Inspector to the package name we give you
    public string appBPackageName = "com.melzo.Anubhav_Demo";

    public Button launchButton; // Assign in Inspector
// public Oculus.Interaction.InteractableUnityEventWrapper interactableEventWrapper;

    void Start()
    {
        if (launchButton == null)
            launchButton = GetComponentInChildren<Button>();
        if (launchButton != null)
            launchButton.onClick.AddListener(OnLaunchButtonPressed);

    }

    public void OnLaunchButtonPressed()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
string myPackage = Application.identifier;
AndroidAppLauncher.LaunchApp(appBPackageName, myPackage);
#endif
    }
}
