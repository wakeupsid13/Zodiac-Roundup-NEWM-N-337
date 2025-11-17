using UnityEngine;
using TMPro;
using System.Collections;

public class TimerUI : MonoBehaviour
{
    public TextMeshProUGUI timerText;

    void Update()
    {
        if (!SingleSceneSessionManager.Instance) return;
        int s = Mathf.Max(0, Mathf.FloorToInt(SingleSceneSessionManager.Instance.SecondsRemaining.Value));
        int m = s / 60; int sec = s % 60;
        timerText.text = $"{m:00}:{sec:00}";
    }

}
