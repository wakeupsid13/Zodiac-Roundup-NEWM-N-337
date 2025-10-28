using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;

public class ToastBroadcaster : NetworkBehaviour
{
    public static ToastBroadcaster Instance;
    public TextMeshProUGUI toastText;
    public float showSeconds = 2.5f;

    void Awake()
    {
        Instance = this;
    }

    [ClientRpc]
    public void ShowToastClientRpc(string message)
    {
        if (!toastText) return;
        StopAllCoroutines();
        StartCoroutine(ShowRoutine(message));
    }

    IEnumerator ShowRoutine(string msg)
    {
        toastText.gameObject.SetActive(true);
        toastText.text = msg;
        yield return new WaitForSeconds(showSeconds);
        toastText.gameObject.SetActive(false);
    }
}
