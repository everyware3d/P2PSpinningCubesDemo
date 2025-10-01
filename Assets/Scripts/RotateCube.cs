using UnityEngine;

public class RotateCube : MonoBehaviour
{
    public float speed = 30f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // GetComponent<Renderer>().material.color = new Color(0, 255, 0);
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(speed * Time.deltaTime, 2 * speed * Time.deltaTime, -speed * Time.deltaTime);
    }
}
