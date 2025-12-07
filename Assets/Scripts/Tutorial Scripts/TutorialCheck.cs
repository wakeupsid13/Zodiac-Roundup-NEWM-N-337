using UnityEngine;

public class TutorialCheck : MonoBehaviour
{
    [Header("Tutorial Panel Set up")]
    public KeyCode[] keys;
    public GameObject yesImage;

    private bool _inRange;
    private bool _keyPressed;
    private bool[] keysPressed;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _inRange = false;
        _keyPressed = false;

        keysPressed = new bool[keys.Length];
        for(int i = 0; i < keysPressed.Length; i++)
        {
            keysPressed[i] = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!_inRange) return;
        for (int i = 0; i < keys.Length; i++)
        {
            if (Input.GetKeyDown(keys[i]) && keysPressed[i] == false)
            {
                keysPressed[i] = true;
            }
        }

        int count = 0;
        foreach(bool k in keysPressed)
        {
            if (k)
            {
                count += 1;
            }
        }
        if (count == keys.Length)
        {
            _keyPressed = true;
            yesImage.SetActive(true);
        }

    }

    private void OnTriggerEnter(Collider other) {
        if (other.gameObject.CompareTag("Player"))
        {
            _inRange = true;
        }
    }

    private void OnTriggerExit(Collider other) {
        if (other.gameObject.CompareTag("Player"))
        {
            _inRange = false;
        }
    }
}
