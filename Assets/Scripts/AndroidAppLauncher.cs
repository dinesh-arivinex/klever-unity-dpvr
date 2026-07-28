using UnityEngine;
public class AndroidAppLauncher : MonoBehaviour
{
#if UNITY_ANDROID && !UNITY_EDITOR
public static void LaunchApp(string targetPackage,
string callerPackage = null)
{
try
{
using (AndroidJavaClass up = new AndroidJavaClass(
"com.unity3d.player.UnityPlayer"))
{
AndroidJavaObject activity =
up.GetStatic<AndroidJavaObject>("currentActivity");
AndroidJavaObject pm =
activity.Call<AndroidJavaObject>("getPackageManager");
AndroidJavaObject intent =
pm.Call<AndroidJavaObject>(
"getLaunchIntentForPackage", targetPackage);
if (intent != null)
{
intent.Call<AndroidJavaObject>(
"addFlags", 0x10000000 | 0x20000000);
if (!string.IsNullOrEmpty(callerPackage))
intent.Call<AndroidJavaObject>(
"putExtra", "caller_package", callerPackage);
activity.Call("startActivity", intent);
}
else { Debug.LogWarning("App not found: " + targetPackage); }
}
}
catch (System.Exception e) { Debug.LogException(e); }
}
#endif
}