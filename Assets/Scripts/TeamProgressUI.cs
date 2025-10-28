using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using System.Collections;

public class TeamProgressUI : MonoBehaviour
{
    public Slider progress;        // assign in Inspector
    [Tooltip("Win points for 100% fill. Keep in sync with Game rules.")]
    public int winPoints = 100;

    Coroutine _bindRoutine;
    bool _bound;

    void OnEnable()
    {
        _bindRoutine = StartCoroutine(BindWhenReady());
    }
    void OnDisable()
    {
        if (_bindRoutine != null) StopCoroutine(_bindRoutine);
        _bindRoutine = null;
        Unbind();
    }

    IEnumerator BindWhenReady()
    {
        while (GameState.Instance == null || !GameState.Instance.IsSpawned)
            yield return null;

        if (!_bound)
        {
            GameState.Instance.TeamScore.OnValueChanged += OnScoreChanged;
            _bound = true;
        }

        OnScoreChanged(0, GameState.Instance.TeamScore.Value);
    }

    void Unbind()
    {
        if (_bound && GameState.Instance != null)
            GameState.Instance.TeamScore.OnValueChanged -= OnScoreChanged;
        _bound = false;
    }

    void OnScoreChanged(int oldVal, int newVal)
    {
        if (!progress) return;
        float pct = Mathf.Clamp01((winPoints <= 0 ? 0f : (float)newVal / winPoints));
        progress.value = pct * progress.maxValue; // maxValue should be 100 by default
    }
}
