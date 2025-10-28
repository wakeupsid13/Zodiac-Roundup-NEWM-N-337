using System.Collections;
using UnityEngine;
using TMPro;

public class ScoreUI : MonoBehaviour
{
    public TextMeshProUGUI scoreText;

    Coroutine _bindRoutine;
    bool _isBound;

    void OnEnable()
    {
        // Start (re)binding when this UI is enabled
        _bindRoutine = StartCoroutine(BindWhenReady());
    }

    void OnDisable()
    {
        // Clean up
        Unbind();
        if (_bindRoutine != null) StopCoroutine(_bindRoutine);
        _bindRoutine = null;
    }

    IEnumerator BindWhenReady()
    {
        // Wait until the GameState singleton exists AND its NetworkObject is spawned
        while (GameState.Instance == null || !GameState.Instance.IsSpawned)
            yield return null;

        // Subscribe once
        if (!_isBound)
        {
            GameState.Instance.TeamScore.OnValueChanged += OnScoreChanged;
            _isBound = true;
        }

        // Force an initial refresh with the current value
        OnScoreChanged(0, GameState.Instance.TeamScore.Value);
    }

    void Unbind()
    {
        if (_isBound && GameState.Instance != null)
        {
            GameState.Instance.TeamScore.OnValueChanged -= OnScoreChanged;
        }
        _isBound = false;
    }

    void OnScoreChanged(int oldValue, int newValue)
    {
        if (scoreText != null)
            scoreText.text = $"Team Score: {newValue}";
    }
}
