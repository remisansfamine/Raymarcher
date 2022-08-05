using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaycastMousePos : MonoBehaviour
{
    Camera cam;
    RaycastHit hit;
    Ray ray;
    Vector2 mousePos;
    Vector3 smootPoint;

    public float radius, softness, smoothSpeed, scaleFactor;


    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponent<Camera>();
    }
    
    // Update is called once per frame
    void Update()
    {
        radius += Input.GetAxis("Vertical") * scaleFactor;
        softness += Input.GetAxis("Horizontal") * scaleFactor;

        radius = Mathf.Clamp(radius, 0, 100);
        softness = Mathf.Clamp(softness, 0, 100);

        mousePos = Input.mousePosition;
        ray = GetComponent<Camera>().ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out hit))
        {
            smootPoint = Vector3.MoveTowards(smootPoint, hit.point, smoothSpeed * Time.deltaTime);
            Vector4 pos = new Vector4(smootPoint.x, smootPoint.y, smootPoint.z, 0);
            Shader.SetGlobalVector("GLOBALmask_Position", pos);
        }

        Shader.SetGlobalFloat("GLOBALmask_Radius", radius);
        Shader.SetGlobalFloat("GLOBALmask_Softness", softness);
    }
}
