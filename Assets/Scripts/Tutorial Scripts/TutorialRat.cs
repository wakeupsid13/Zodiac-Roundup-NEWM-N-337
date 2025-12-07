using UnityEngine;

public class TutorialRat : MonoBehaviour
{

    private bool entered = false;
    private void OnTriggerEnter(Collider other) {
        if (other.gameObject.CompareTag("Player") && entered == false)
        {
            gameObject.transform.parent.GetComponent<Rigidbody>().AddForce(new Vector3(0, 0, 600f));
            entered = true;
        }
    }
}
