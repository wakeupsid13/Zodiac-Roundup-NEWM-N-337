using UnityEngine;
using System.Collections;

public class HideLoaderOnLobbyStart : MonoBehaviour
{
    IEnumerator Start()
    {
        // wait one frame so everything finishes loading
        yield return null;

        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.Hide();
        }
    }
}
